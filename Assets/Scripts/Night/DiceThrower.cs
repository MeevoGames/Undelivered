using System.Collections;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// Throws a die onto the table: when a deck die is tapped, a die is launched into the throw zone,
    /// arcing up and tumbling (its face flickering), then settling with a couple of damped bounces on the
    /// rolled face. The effects activated in the effect deck are consumed on that throw.
    /// </summary>
    public class DiceThrower : MonoBehaviour
    {
        public static DiceThrower Instance { get; private set; }

        [Tooltip("The table area the die lands in. The thrown die is a centre-anchored child of this.")]
        [SerializeField] private RectTransform throwZone;
        [SerializeField] private DieView thrownDiePrefab;

        [Header("Launch (relative to the zone centre)")]
        [SerializeField] private float launchStartY = -500f;
        [SerializeField] private float launchSpreadX = 200f;
        [Tooltip("How much of the zone the die may land in (0..1 of half-size from the centre).")]
        [SerializeField, Range(0f, 1f)] private float landingArea = 0.5f;

        [Header("Flight")]
        [SerializeField] private float flightDuration = 0.5f;
        [SerializeField] private float arcHeight = 350f;
        [SerializeField] private float spinDegrees = 900f;
        [SerializeField] private float faceFlickerInterval = 0.05f;
        [Tooltip("Extra time the die tumbles in place after landing, to build anticipation.")]
        [SerializeField] private float rollDuration = 0.7f;
        [SerializeField] private float rollJitter = 12f;

        [Header("Settle")]
        [SerializeField] private int bounceCount = 2;
        [SerializeField] private float firstBounceHeight = 90f;
        [SerializeField] private float bounceDuration = 0.18f;
        [SerializeField, Range(0f, 1f)] private float bounceDamping = 0.45f;
        [SerializeField] private float restAngle = 12f;
        [Tooltip("Squash (x wide, y short) at the moment of impact; recovers over the first bounce.")]
        [SerializeField] private Vector2 impactSquash = new Vector2(1.25f, 0.7f);

        /// <summary>Fires when a thrown die settles on its final face (the combat controller resolves it).</summary>
        public event System.Action<DiceFace> DieLanded;

        private DieView _current;
        private Coroutine _routine;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Throws a die: rolls it and animates it onto the table. Fires <see cref="DieLanded"/> when it settles.</summary>
        public void Throw(DiceData die)
        {
            if (die == null || throwZone == null || thrownDiePrefab == null) return;

            DiceFace result = die.Roll();

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ThrowRoutine(die, result));
        }

        /// <summary>Clears the die currently on the table (e.g. at the end of a turn).</summary>
        public void ClearTable()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (_current != null) { Destroy(_current.gameObject); _current = null; }
        }

        private IEnumerator ThrowRoutine(DiceData die, DiceFace result)
        {
            if (_current != null) Destroy(_current.gameObject);
            _current = Instantiate(thrownDiePrefab, throwZone, false);
            _current.Setup(die);

            RectTransform rt = (RectTransform)_current.transform;
            Vector2 start = new Vector2(Random.Range(-launchSpreadX, launchSpreadX), launchStartY);
            Vector2 land = RandomLanding();
            float flicker = 0f;

            // Flight: parabolic arc, spinning, face flickering.
            for (float t = 0f; t < flightDuration; t += Time.unscaledDeltaTime)
            {
                float k = t / flightDuration;
                Vector2 pos = Vector2.Lerp(start, land, k);
                pos.y += arcHeight * 4f * k * (1f - k);
                rt.anchoredPosition = pos;
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, spinDegrees, k));
                flicker = Flicker(die, flicker);
                yield return null;
            }

            rt.anchoredPosition = land;

            // Roll in place: keep tumbling and flickering at the landing spot to build anticipation.
            float spin = spinDegrees;
            for (float t = 0f; t < rollDuration; t += Time.unscaledDeltaTime)
            {
                spin += spinDegrees * Time.unscaledDeltaTime;
                rt.anchoredPosition = land + Random.insideUnitCircle * rollJitter;
                rt.localRotation = Quaternion.Euler(0f, 0f, spin);
                flicker = Flicker(die, flicker);
                yield return null;
            }
            rt.anchoredPosition = land;

            // Settle: damped bounces at the landing spot, straightening out onto the final face.
            float landRotation = spin % 360f;
            float rest = Random.Range(-restAngle, restAngle);
            float height = firstBounceHeight;
            for (int b = 0; b < Mathf.Max(1, bounceCount); b++)
            {
                for (float t = 0f; t < bounceDuration; t += Time.unscaledDeltaTime)
                {
                    float k = t / bounceDuration;
                    rt.anchoredPosition = new Vector2(land.x, land.y + height * 4f * k * (1f - k));
                    if (b == 0)
                    {
                        rt.localScale = new Vector3(Mathf.Lerp(impactSquash.x, 1f, k), Mathf.Lerp(impactSquash.y, 1f, k), 1f);
                        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(landRotation, rest, k));
                        flicker = Flicker(die, flicker);
                    }
                    yield return null;
                }
                rt.anchoredPosition = land;
                if (b == 0) _current.ShowFace(result); // stop flickering; show the rolled face
                height *= bounceDamping;
            }

            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.Euler(0f, 0f, rest);
            rt.anchoredPosition = land;
            _current.ShowFace(result);
            _routine = null;

            DieLanded?.Invoke(result);
        }

        // Shows a random face when the flicker timer elapses; returns the updated timer.
        private float Flicker(DiceData die, float timer)
        {
            timer -= Time.unscaledDeltaTime;
            if (timer <= 0f)
            {
                _current.ShowFace(die.Roll());
                timer = faceFlickerInterval;
            }
            return timer;
        }

        private Vector2 RandomLanding()
        {
            Rect r = throwZone.rect;
            return new Vector2(
                Random.Range(-r.width * 0.5f * landingArea, r.width * 0.5f * landingArea),
                Random.Range(-r.height * 0.5f * landingArea, r.height * 0.5f * landingArea));
        }
    }
}
