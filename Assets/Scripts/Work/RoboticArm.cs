using System.Collections;
using System.Collections.Generic;
using Undelivered.UI;
using UnityEngine;

namespace Undelivered.Work
{
    /// <summary>
    /// An automated arm. The hand travels (empty) to a box, lifts it, carries it to a destination and
    /// lowers it, then travels on to the next box — it never teleports. Two modes:
    ///   • <see cref="Mode.Sorting"/> ("Brazo Clasificador") delivers boxes to their correct slot.
    ///   • <see cref="Mode.QualityControl"/> ("Control de Calidad") carries broken boxes to the
    ///     repackager, repairs them and brings them back to the table.
    /// If the player grabs the box the arm is after (starts a drag), the arm gives it up.
    ///
    /// Sorting is split across two upgrades: "Brazo Clasificador" sets how many arms work at once
    /// (<see cref="SortingArmCount"/>, up to 3) and "Velocidad de Clasificación" sets their shared
    /// speed (<see cref="SortingSpeed"/>). Place up to 3 Sorting arms in the scene (each with its own
    /// hand); the first <see cref="SortingArmCount"/> of them are the ones that work.
    /// </summary>
    public class RoboticArm : MonoBehaviour
    {
        public enum Mode { Sorting, QualityControl }

        // "Brazo Clasificador": number of sorting arms working at once. "Velocidad de Clasificación":
        // the speed they all share.
        public static int SortingArmCount;
        public static float SortingSpeed = 200f;

        // "Control de Calidad".
        public static bool QualityEnabled;
        public static float QualitySpeed = 400f;

        // Registries in scene order: Sorting arms use their index against SortingArmCount; the Quality
        // list lets sorting arms know a quality arm is around to handle broken boxes.
        private static readonly List<RoboticArm> SortingArms = new List<RoboticArm>();
        private static readonly List<RoboticArm> QualityArms = new List<RoboticArm>();

        // Boxes an arm is currently after, so two arms never target the same one.
        private static readonly HashSet<Box> Claimed = new HashSet<Box>();

        // True when a working quality arm is present, so broken boxes are its job (sorting arms skip them).
        private static bool BrokenHandledByQuality => QualityEnabled && QualityArms.Count > 0;

        [SerializeField] private Mode mode;

        [Tooltip("Hand sprite that travels to and carries the box. Should start hidden; the arm shows/hides it.")]
        [SerializeField] private RectTransform hand;

        [Tooltip("Pixels the box is lifted at pickup and lowered at the destination.")]
        [SerializeField] private float liftHeight = 60f;

        [Tooltip("Idle poll interval (seconds) while there's nothing to carry.")]
        [SerializeField] private float idlePoll = 0.25f;

        private bool ModeEnabled => mode == Mode.Sorting
            ? SortingArms.IndexOf(this) < SortingArmCount
            : QualityEnabled;

        private float Speed => Mathf.Max(1f, mode == Mode.Sorting ? SortingSpeed : QualitySpeed);

        private void Awake()
        {
            (mode == Mode.Sorting ? SortingArms : QualityArms).Add(this);
        }

        private void OnDestroy()
        {
            SortingArms.Remove(this);
            QualityArms.Remove(this);
        }

        private void Start()
        {
            HideHand();
            StartCoroutine(WorkLoop());
        }

        private IEnumerator WorkLoop()
        {
            WaitForSeconds wait = new WaitForSeconds(idlePoll);
            while (true)
            {
                if (!ModeEnabled)
                {
                    HideHand();
                    yield return wait;
                    continue;
                }

                Box target = FindTarget();
                if (target == null)
                {
                    HideHand(); // nothing to do: rest until a box shows up
                    yield return wait;
                    continue;
                }

                Claimed.Add(target);
                yield return Serve(target); // hand stays shown between boxes so it travels, not teleports
            }
        }

        // Chooses the next box. Quality arms only take broken boxes. Sorting arms take normal boxes
        // first; broken ones are the quality arm's job — a sorting arm only takes a broken box when no
        // quality arm exists, and only once no normal boxes are left. Never targets a claimed box, one
        // being dragged, one carried by the forklift, or one still flying in (drag disabled).
        private Box FindTarget()
        {
            List<Box> normal = new List<Box>();
            List<Box> broken = new List<Box>();

            foreach (Box b in FindObjectsByType<Box>())
            {
                if (b == null) continue;
                if (Claimed.Contains(b)) continue;
                if (Forklift.IsCarried(b)) continue;

                UIDraggable drag = b.GetComponent<UIDraggable>();
                if (drag != null && (!drag.enabled || drag.IsDragging)) continue;

                if (b.IsBroken) broken.Add(b);
                else normal.Add(b);
            }

            if (mode == Mode.QualityControl)
            {
                return Pick(broken);
            }

            // Sorting mode.
            if (normal.Count > 0) return Pick(normal);
            if (!BrokenHandledByQuality) return Pick(broken); // no quality arm: handle broken ones last
            return null;
        }

        private static Box Pick(List<Box> boxes)
        {
            return boxes.Count == 0 ? null : boxes[Random.Range(0, boxes.Count)];
        }

        private IEnumerator Serve(Box box)
        {
            try
            {
                RectTransform boxRect = box.transform as RectTransform;
                if (boxRect == null) yield break;

                ShowHand();

                // 1) Empty travel: bring the hand over the box from wherever it currently is.
                yield return MoveHandTo(boxRect.position, box);
                if (box == null || IsDragging(box)) yield break;

                Vector3 origin = boxRect.position; // table spot to return to (QualityControl)

                if (mode == Mode.Sorting)
                {
                    Slot slot = FindSlot(box.Type);
                    if (slot == null) yield break;

                    yield return CarryBox(box, boxRect, slot.transform.position, true);
                    if (box != null && !IsDragging(box))
                    {
                        slot.Deliver(box);
                    }
                }
                else // QualityControl
                {
                    Repackager repackager = FindAnyObjectByType<Repackager>();
                    if (repackager == null) yield break;

                    yield return CarryBox(box, boxRect, repackager.transform.position, true);
                    if (box == null || IsDragging(box)) yield break;

                    Box fixedBox = repackager.Repair(box);
                    if (fixedBox == null) yield break;

                    RectTransform fixedRect = fixedBox.transform as RectTransform;
                    yield return CarryBox(fixedBox, fixedRect, origin, false); // bring it back to the table
                }
            }
            finally
            {
                Claimed.Remove(box);
            }
        }

        // Moves just the hand toward a point, stopping early if the target box is lost (destroyed/dragged).
        private IEnumerator MoveHandTo(Vector3 targetWorld, Box box)
        {
            if (hand == null) yield break;

            while ((hand.position - targetWorld).sqrMagnitude > 4f)
            {
                if (box == null || IsDragging(box)) yield break;
                hand.position = Vector3.MoveTowards(hand.position, targetWorld, Speed * Time.deltaTime);
                yield return null;
            }
        }

        // Carries a box (and the hand) through: lift up, over the target, lower onto it. Stops early if
        // the box is destroyed or (when cancelable) the player grabs it.
        private IEnumerator CarryBox(Box box, RectTransform boxRect, Vector3 targetWorld, bool cancelable)
        {
            if (boxRect == null) yield break;

            Vector3 lift = Vector3.up * liftHeight;
            Vector3[] waypoints = { boxRect.position + lift, targetWorld + lift, targetWorld };

            foreach (Vector3 wp in waypoints)
            {
                while (boxRect != null && (boxRect.position - wp).sqrMagnitude > 4f)
                {
                    if (box == null) yield break;
                    if (cancelable && IsDragging(box)) yield break; // player grabbed it: hand off

                    boxRect.position = Vector3.MoveTowards(boxRect.position, wp, Speed * Time.deltaTime);
                    if (hand != null) hand.position = boxRect.position;
                    yield return null;
                }
            }
        }

        private Slot FindSlot(BoxType type)
        {
            foreach (Slot s in FindObjectsByType<Slot>())
            {
                if (s != null && s.SlotType == type) return s;
            }
            return null;
        }

        private static bool IsDragging(Box box)
        {
            if (box == null) return false;
            UIDraggable drag = box.GetComponent<UIDraggable>();
            return drag != null && drag.IsDragging;
        }

        private void ShowHand()
        {
            if (hand != null && !hand.gameObject.activeSelf) hand.gameObject.SetActive(true);
        }

        private void HideHand()
        {
            if (hand != null && hand.gameObject.activeSelf) hand.gameObject.SetActive(false);
        }
    }
}
