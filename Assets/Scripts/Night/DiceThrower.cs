using System.Collections;
using System.Collections.Generic;
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

        [Header("Number-transform (result changed by an effect)")]
        [Tooltip("Face sprites per value, used when a NumberTransform swaps the shown number.")]
        [SerializeField] private NumberSprite[] numberSprites;
        [SerializeField] private float transformShakeDuration = 0.3f;
        [SerializeField] private float transformShakeMagnitude = 14f;

        [Header("Extra die (GrantTurn: SecondDie / CopyDie)")]
        [Tooltip("How far to the side the second die lands from the first.")]
        [SerializeField] private float extraDieOffset = 200f;

        [Header("Mini dice (Create-dice, type 8)")]
        [Tooltip("Size of a spawned mini die as a fraction of a normal die.")]
        [SerializeField, Range(0.1f, 1f)] private float miniDieScale = 0.6f;
        [Tooltip("Where the first mini die lands, relative to the main die.")]
        [SerializeField] private float miniDieFirstOffset = 160f;
        [Tooltip("Gap between mini dice (and the row height when they wrap).")]
        [SerializeField] private float miniDieSpacing = 95f;
        [Tooltip("How many mini dice per row before wrapping to the next.")]
        [SerializeField] private int miniDiePerRow = 4;
        [Tooltip("Delay between each mini die being thrown.")]
        [SerializeField] private float miniDieStagger = 0.06f;

        [System.Serializable]
        public struct NumberSprite { public int value; public Sprite sprite; }

        /// <summary>Fires when a throw starts (the thrower plays its throw wind-up).</summary>
        public event System.Action ThrowStarted;

        /// <summary>Fires when a thrown die settles on its final face (the combat controller resolves it).</summary>
        public event System.Action<DiceFace> DieLanded;

        /// <summary>The last die thrown (so a RethrowDie effect can throw it again).</summary>
        public DiceData LastDie { get; private set; }

        /// <summary>Optional luck source (type 13): maps a die to its 0..1 chance of the highest face. Null = natural rolls.</summary>
        public static System.Func<DiceData, float> LuckProvider;

        private DieView _current;
        private readonly List<DieView> _extra = new List<DieView>();
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

            LastDie = die;
            ThrowStarted?.Invoke();
            float luck = LuckProvider != null ? LuckProvider(die) : -1f;
            DiceFace result = luck >= 0f ? die.RollBiased(luck) : die.Roll(); // type 13: luck biases toward the max face

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ThrowRoutine(die, result));
        }

        /// <summary>Clears every die on the table (e.g. at the end of a turn).</summary>
        public void ClearTable()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (_current != null) { Destroy(_current.gameObject); _current = null; }
            foreach (DieView extra in _extra) if (extra != null) Destroy(extra.gameObject);
            _extra.Clear();
        }

        /// <summary>
        /// Drops a second die next to the first (the first stays put). If <paramref name="copyFace"/> is
        /// given (Doble Tirada) it spins little and lands on that exact face; otherwise (Repetir Dado) it
        /// tumbles to a random result. Reports the settled value.
        /// </summary>
        public IEnumerator ThrowExtraRoutine(DiceData die, DiceFace copyFace, System.Action<int> onResult)
        {
            if (die == null || thrownDiePrefab == null || throwZone == null) { onResult?.Invoke(0); yield break; }

            bool copy = copyFace != null;
            DiceFace result = copy ? copyFace : die.Roll();

            DieView view = Instantiate(thrownDiePrefab, throwZone, false);
            _extra.Add(view);
            view.Setup(die);
            view.HideLuck();
            view.HideDeckOnly();
            if (copy) view.ShowFace(result); // already showing the target number

            RectTransform rt = (RectTransform)view.transform;
            Vector2 anchor = _current != null ? ((RectTransform)_current.transform).anchoredPosition : Vector2.zero;
            Vector2 land = anchor + new Vector2(extraDieOffset, 0f);
            Vector2 start = new Vector2(land.x, land.y + launchStartY);
            rt.anchoredPosition = start;

            float duration = copy ? flightDuration * 0.6f : flightDuration;
            float spin = copy ? spinDegrees * 0.15f : spinDegrees; // copy spins very little
            float flicker = 0f;

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                Vector2 pos = Vector2.Lerp(start, land, k);
                pos.y += arcHeight * 4f * k * (1f - k);
                rt.anchoredPosition = pos;
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, spin, k));
                if (!copy)
                {
                    flicker -= Time.unscaledDeltaTime;
                    if (flicker <= 0f) { view.ShowFace(die.Roll()); flicker = faceFlickerInterval; }
                }
                yield return null;
            }

            rt.anchoredPosition = land;
            rt.localRotation = Quaternion.Euler(0f, 0f, copy ? 0f : Random.Range(-restAngle, restAngle));
            view.ShowFace(result);

            onResult?.Invoke(result != null ? result.Value : 0);
        }

        /// <summary>
        /// Create-dice (type 8): drops <paramref name="count"/> smaller dice (<see cref="miniDieScale"/>) next
        /// to the main die, throwing each in turn (they stay on the table). Reports the sum of their values.
        /// </summary>
        public IEnumerator ThrowMiniDiceRoutine(DiceData mini, int count, System.Action<int> onTotal)
        {
            if (mini == null || thrownDiePrefab == null || throwZone == null || count <= 0) { onTotal?.Invoke(0); yield break; }

            Vector2 origin = _current != null ? ((RectTransform)_current.transform).anchoredPosition : Vector2.zero;
            int total = 0;

            for (int i = 0; i < count; i++)
            {
                DiceFace result = mini.Roll();
                total += result != null ? result.Value : 0;

                DieView view = Instantiate(thrownDiePrefab, throwZone, false);
                _extra.Add(view);
                view.Setup(mini);
                view.HideLuck();
                view.HideDeckOnly();

                RectTransform rt = (RectTransform)view.transform;
                rt.localScale = Vector3.one * miniDieScale;

                int col = i % Mathf.Max(1, miniDiePerRow);
                int row = i / Mathf.Max(1, miniDiePerRow);
                Vector2 land = origin + new Vector2(miniDieFirstOffset + col * miniDieSpacing, row * miniDieSpacing);
                Vector2 start = new Vector2(land.x, land.y + launchStartY);
                rt.anchoredPosition = start;

                float duration = flightDuration * 0.5f; // mini dice snap onto the table quicker
                float flicker = 0f;
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    float k = t / duration;
                    Vector2 pos = Vector2.Lerp(start, land, k);
                    pos.y += arcHeight * 0.6f * 4f * k * (1f - k);
                    rt.anchoredPosition = pos;
                    rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, spinDegrees, k));
                    flicker -= Time.unscaledDeltaTime;
                    if (flicker <= 0f) { view.ShowFace(mini.Roll()); flicker = faceFlickerInterval; }
                    yield return null;
                }

                rt.anchoredPosition = land;
                rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-restAngle, restAngle));
                view.ShowFace(result);

                if (miniDieStagger > 0f) yield return new WaitForSecondsRealtime(miniDieStagger);
            }

            onTotal?.Invoke(total);
        }

        /// <summary>Swaps the thrown die's face to the sprite for a value and shakes it (a NumberTransform result).</summary>
        public void ShowResultValue(int value)
        {
            if (_current == null) return;

            Sprite sprite = SpriteFor(value);
            if (sprite != null) _current.SetFaceSprite(sprite);
            StartCoroutine(ShakeCurrent());
        }

        private Sprite SpriteFor(int value)
        {
            if (numberSprites != null)
                foreach (NumberSprite entry in numberSprites)
                    if (entry.value == value) return entry.sprite;
            return null;
        }

        private IEnumerator ShakeCurrent()
        {
            RectTransform rt = (RectTransform)_current.transform;
            Vector2 home = rt.anchoredPosition;
            for (float t = 0f; t < transformShakeDuration; t += Time.unscaledDeltaTime)
            {
                float damper = 1f - (t / transformShakeDuration);
                rt.anchoredPosition = home + Random.insideUnitCircle * (transformShakeMagnitude * damper);
                yield return null;
            }
            rt.anchoredPosition = home;
        }

        private IEnumerator ThrowRoutine(DiceData die, DiceFace result)
        {
            if (_current != null) Destroy(_current.gameObject);
            _current = Instantiate(thrownDiePrefab, throwZone, false);
            _current.Setup(die);
            _current.HideLuck(); // the luck % only shows in the deck
            _current.HideDeckOnly();

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
