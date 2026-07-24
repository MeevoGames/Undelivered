using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// The stat-upgrade window shown on level-up. Drops in from the top with a bounce (like the shop /
    /// inventory), lists the player's stats on the right each with a "+" button, and a dice space on the
    /// left. Tapping a stat's "+" disables the others, rolls a classic d6, and adds the result to that
    /// stat. Then it bounces back up and the combat resumes (via the callback passed to <see cref="Show"/>).
    /// </summary>
    public class LevelUpPanel : MonoBehaviour
    {
        public static LevelUpPanel Instance { get; private set; }

        [SerializeField] private RectTransform panel;

        [Header("Stat rows (right): a + button and its value")]
        [SerializeField] private Button healthPlus;
        [SerializeField] private Button shieldPlus;
        [SerializeField] private Button speedPlus;
        [SerializeField] private Button luckPlus;
        [SerializeField] private TextMeshProUGUI healthValue;
        [SerializeField] private TextMeshProUGUI shieldValue;
        [SerializeField] private TextMeshProUGUI speedValue;
        [SerializeField] private TextMeshProUGUI luckValue;

        [Header("Dice space (left)")]
        [Tooltip("A classic d6 (1-2-3-4-5-6), e.g. DadoBasico.")]
        [SerializeField] private DiceData levelUpDie;
        [SerializeField] private Transform diceSlot;
        [SerializeField] private DieView dieViewPrefab;
        [SerializeField] private float flickerInterval = 0.05f;
        [Tooltip("Time the upgraded stat stays highlighted so the player sees the change, before closing.")]
        [SerializeField] private float afterRollPause = 1.1f;

        [Header("Die drop (falls in like a deck throw)")]
        [SerializeField] private float fallHeight = 500f;
        [SerializeField] private float fallDuration = 0.4f;
        [SerializeField] private float spinDegrees = 720f;
        [SerializeField] private int bounceCount = 2;
        [SerializeField] private float firstBounceHeight = 70f;
        [SerializeField] private float bounceDuration = 0.15f;
        [SerializeField, Range(0f, 1f)] private float bounceDamping = 0.4f;

        [Header("Upgraded-stat feedback")]
        [Tooltip("Colour the modified stat flashes (#58BE4E); all reset to normal on close.")]
        [SerializeField] private Color highlightColor = new Color(0.345f, 0.745f, 0.306f);
        [SerializeField] private Color normalColor = Color.white; // #FFFFFF
        [SerializeField] private float statShakeDuration = 0.35f;
        [SerializeField] private float statShakeMagnitude = 12f;

        [Header("Bounce (from the top, like the shop/inventory)")]
        [SerializeField] private float hiddenY = 1200f;
        [SerializeField] private float centerY = 0f;
        [SerializeField] private float undershootY = -25f;
        [SerializeField] private float overshootY = 15f;
        [SerializeField] private float speed = 3500f;
        [SerializeField] private float minSegment = 0.06f;

        private PlayerCombatant _player;
        private Action _onDone;
        private DieView _die;
        private Coroutine _anim;
        private bool _busy;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (panel != null) SetY(hiddenY);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Opens the upgrade window for a player; <paramref name="onDone"/> fires once a stat has been upgraded and it closes.</summary>
        public void Show(PlayerCombatant player, Action onDone)
        {
            _player = player;
            _onDone = onDone;
            _busy = false;

            RefreshValues();
            ResetValueColors();
            WireButton(healthPlus, StatKind.Health);
            WireButton(shieldPlus, StatKind.Shield);
            WireButton(speedPlus, StatKind.Speed);
            WireButton(luckPlus, StatKind.Luck);
            SetButtonsEnabled(true);

            BounceIn();
        }

        private void WireButton(Button button, StatKind stat)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnPick(stat));
        }

        private void OnPick(StatKind stat)
        {
            if (_busy) return;
            _busy = true;
            SetButtonsEnabled(false); // only one stat can be upgraded
            StartCoroutine(RollApplyClose(stat));
        }

        private IEnumerator RollApplyClose(StatKind stat)
        {
            int result = RollDie();      // classic d6 (not luck-biased)
            yield return AnimateDie(result);

            if (_player != null) _player.UpgradeStat(stat, result);
            RefreshValues();             // show the new value

            // Feedback so the change is noticed: the modified stat flashes green (#58BE4E) and shakes.
            TextMeshProUGUI changed = ValueTextFor(stat);
            if (changed != null)
            {
                changed.color = highlightColor;
                yield return ShakeText(changed);
            }
            yield return new WaitForSecondsRealtime(afterRollPause);

            yield return Close();

            // Once it's off-screen: reset all stat texts to white and remove the thrown die.
            ResetValueColors();
            if (_die != null) { Destroy(_die.gameObject); _die = null; }
            _onDone?.Invoke();
        }

        private int RollDie()
        {
            DiceFace face = levelUpDie != null ? levelUpDie.Roll() : null;
            return face != null ? Mathf.Max(1, face.Value) : 1;
        }

        // Drops a die into the slot — it falls from above, tumbling and flickering, then bounces and settles.
        private IEnumerator AnimateDie(int result)
        {
            if (diceSlot == null || dieViewPrefab == null || levelUpDie == null) yield break;

            for (int i = diceSlot.childCount - 1; i >= 0; i--) Destroy(diceSlot.GetChild(i).gameObject);
            _die = Instantiate(dieViewPrefab, diceSlot, false);
            _die.Setup(levelUpDie);
            _die.HideLuck(); // luck % only shows in the deck
            _die.HideDeckOnly();

            RectTransform rt = _die.transform as RectTransform;
            if (rt == null) { _die.ShowFace(FaceForValue(result)); yield break; }

            Vector2 land = Vector2.zero;                 // centred in the slot
            Vector2 start = land + Vector2.up * fallHeight;
            float flicker = 0f;

            // Fall: accelerate downward, spinning, faces flickering.
            for (float t = 0f; t < fallDuration; t += Time.unscaledDeltaTime)
            {
                float k = t / fallDuration;
                rt.anchoredPosition = Vector2.Lerp(start, land, k * k); // ease-in (gravity-ish)
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, spinDegrees, k));
                flicker = Flicker(flicker);
                yield return null;
            }
            rt.anchoredPosition = land;
            rt.localRotation = Quaternion.identity;

            // Damped bounces at the landing spot; show the rolled face after the first.
            float height = firstBounceHeight;
            for (int b = 0; b < Mathf.Max(1, bounceCount); b++)
            {
                for (float t = 0f; t < bounceDuration; t += Time.unscaledDeltaTime)
                {
                    float k = t / bounceDuration;
                    rt.anchoredPosition = new Vector2(land.x, land.y + height * 4f * k * (1f - k));
                    if (b == 0) flicker = Flicker(flicker);
                    yield return null;
                }
                rt.anchoredPosition = land;
                if (b == 0) _die.ShowFace(FaceForValue(result)); // stop flickering; show the result
                height *= bounceDamping;
            }

            _die.ShowFace(FaceForValue(result));
        }

        // Shows a random face when the flicker timer elapses; returns the updated timer.
        private float Flicker(float timer)
        {
            timer -= Time.unscaledDeltaTime;
            if (timer <= 0f) { _die.ShowFace(levelUpDie.Roll()); timer = flickerInterval; }
            return timer;
        }

        private IEnumerator ShakeText(TextMeshProUGUI text)
        {
            RectTransform rt = text.rectTransform;
            Vector2 home = rt.anchoredPosition;
            for (float t = 0f; t < statShakeDuration; t += Time.unscaledDeltaTime)
            {
                float damp = 1f - t / statShakeDuration;
                rt.anchoredPosition = home + UnityEngine.Random.insideUnitCircle * (statShakeMagnitude * damp);
                yield return null;
            }
            rt.anchoredPosition = home;
        }

        private TextMeshProUGUI ValueTextFor(StatKind stat)
        {
            switch (stat)
            {
                case StatKind.Health: return healthValue;
                case StatKind.Shield: return shieldValue;
                case StatKind.Speed: return speedValue;
                case StatKind.Luck: return luckValue;
                default: return null;
            }
        }

        private void ResetValueColors()
        {
            if (healthValue != null) healthValue.color = normalColor;
            if (shieldValue != null) shieldValue.color = normalColor;
            if (speedValue != null) speedValue.color = normalColor;
            if (luckValue != null) luckValue.color = normalColor;
        }

        private DiceFace FaceForValue(int value)
        {
            if (levelUpDie != null && levelUpDie.Faces != null)
                foreach (DiceFace f in levelUpDie.Faces)
                    if (f != null && f.Value == value) return f;
            return null;
        }

        private void RefreshValues()
        {
            if (_player == null) return;
            SetText(healthValue, _player.MaxHealth.ToString());
            SetText(shieldValue, _player.BaseShield.ToString());
            SetText(speedValue, _player.Speed.ToString());
            SetText(luckValue, _player.PermanentLuckPercent + "%");
        }

        private void SetButtonsEnabled(bool enabled)
        {
            if (healthPlus != null) healthPlus.interactable = enabled;
            if (shieldPlus != null) shieldPlus.interactable = enabled;
            if (speedPlus != null) speedPlus.interactable = enabled;
            if (luckPlus != null) luckPlus.interactable = enabled;
        }

        private void BounceIn()
        {
            if (panel == null) return;
            if (_anim != null) StopCoroutine(_anim);
            SetY(hiddenY);
            _anim = StartCoroutine(MoveThroughY(undershootY, overshootY, centerY));
        }

        private IEnumerator Close()
        {
            if (panel == null) yield break;
            if (_anim != null) StopCoroutine(_anim);
            yield return MoveThroughY(overshootY, undershootY, hiddenY);
        }

        private IEnumerator MoveThroughY(params float[] targets)
        {
            foreach (float target in targets)
            {
                Vector2 start = panel.anchoredPosition;
                float distance = Mathf.Abs(target - start.y);
                float duration = Mathf.Max(minSegment, distance / Mathf.Max(1f, speed));
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    float k = Mathf.SmoothStep(0f, 1f, t / duration);
                    panel.anchoredPosition = new Vector2(start.x, Mathf.Lerp(start.y, target, k));
                    yield return null;
                }
                panel.anchoredPosition = new Vector2(start.x, target);
            }
            _anim = null;
        }

        private void SetY(float y)
        {
            Vector2 p = panel.anchoredPosition;
            p.y = y;
            panel.anchoredPosition = p;
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null) text.text = value;
        }
    }
}
