using System.Collections;
using System.Collections.Generic;
using TMPro;
using Undelivered.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// The gems tab: shows the player's gold and gems, and trades between them. Three plans buy gems
    /// with gold, and one trade buys gold with gems. Gold lives in <see cref="StatsManager"/> (the day
    /// currency), gems in <see cref="NightWallet"/>.
    ///
    /// Wire each plan button's onClick to <see cref="BuyGems"/> with its index (0-2), and the gold
    /// trade button to <see cref="BuyGold"/>.
    /// </summary>
    public class GemTradePanel : MonoBehaviour
    {
        [System.Serializable]
        public class GemPlan
        {
            public int goldCost = 100;
            public int gems = 10;
        }

        [Header("Balances")]
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI gemsText;

        [Header("Buy gems with gold")]
        [SerializeField]
        private GemPlan[] plans =
        {
            new GemPlan { goldCost = 100, gems = 10 },
            new GemPlan { goldCost = 150, gems = 20 },
            new GemPlan { goldCost = 300, gems = 50 },
        };

        [Header("Buy gold with gems")]
        [SerializeField] private int goldTradeGemsCost = 100;
        [SerializeField] private int goldTradeGoldGained = 500;

        [Header("Purchase effects — slot shake")]
        [Tooltip("The 3 plan slots (RectTransforms) to shake when bought.")]
        [SerializeField] private RectTransform[] planSlots = new RectTransform[3];
        [SerializeField] private float shakeDuration = 0.25f;
        [SerializeField] private float shakeMagnitude = 12f;

        [Header("Purchase effects — gem confetti")]
        [SerializeField] private Sprite gemSprite;
        [Tooltip("Full-screen, centred container the gems spawn under (defaults to this panel). Its width/height set the spread.")]
        [SerializeField] private RectTransform confettiParent;
        [SerializeField] private int confettiCount = 60;
        [Tooltip("Random gem size range (varied sizes).")]
        [SerializeField] private float gemSizeMin = 32f;
        [SerializeField] private float gemSizeMax = 90f;
        [Tooltip("Seconds over which the gems are spawned, so they rain in staggered — not all at once.")]
        [SerializeField] private float spawnWindow = 0.9f;
        [Tooltip("Initial downward speed range (varied).")]
        [SerializeField] private float fallSpeedMin = 200f;
        [SerializeField] private float fallSpeedMax = 500f;
        [Tooltip("Horizontal drift range.")]
        [SerializeField] private float drift = 150f;
        [Tooltip("Downward acceleration.")]
        [SerializeField] private float gravity = 1200f;
        [Tooltip("Max spin (deg/s).")]
        [SerializeField] private float spin = 220f;
        [Tooltip("Extra distance below the bottom edge before a gem is removed (should exceed the gem size).")]
        [SerializeField] private float offscreenMargin = 120f;

        private bool _subscribed;
        private readonly Dictionary<RectTransform, Vector3> _shakeHome = new Dictionary<RectTransform, Vector3>();
        private readonly Dictionary<RectTransform, Coroutine> _shakeCo = new Dictionary<RectTransform, Coroutine>();

        private void OnEnable()
        {
            TrySubscribe();
            Refresh();
        }

        private void Start()
        {
            TrySubscribe();
            Refresh();
        }

        private void OnDisable()
        {
            if (StatsManager.Instance != null) StatsManager.Instance.GoldChanged -= OnBalanceChanged;
            if (NightWallet.Instance != null) NightWallet.Instance.GemsChanged -= OnBalanceChanged;
            _subscribed = false;
        }

        private void TrySubscribe()
        {
            if (_subscribed || StatsManager.Instance == null || NightWallet.Instance == null) return;
            StatsManager.Instance.GoldChanged += OnBalanceChanged;
            NightWallet.Instance.GemsChanged += OnBalanceChanged;
            _subscribed = true;
        }

        private void OnBalanceChanged(int _) => Refresh();

        /// <summary>Buys a gem plan (0-2) with gold, if there's enough gold.</summary>
        public void BuyGems(int index)
        {
            if (index < 0 || index >= plans.Length) return;

            GemPlan plan = plans[index];
            StatsManager stats = StatsManager.Instance;
            if (stats == null) return;
            if (stats.Gold < plan.goldCost)
            {
                Debug.LogWarning("No alcanza el oro para comprar gemas.");
                return;
            }

            stats.AddGold(-plan.goldCost);
            NightWallet.Instance?.AddGems(plan.gems);
            Refresh();

            if (planSlots != null && index >= 0 && index < planSlots.Length && planSlots[index] != null)
            {
                ShakeSlot(planSlots[index]);
            }
            StartCoroutine(Confetti());
        }

        /// <summary>Buys gold with gems (the reverse trade), if there are enough gems.</summary>
        public void BuyGold()
        {
            if (NightWallet.Instance == null || !NightWallet.Instance.TrySpend(goldTradeGemsCost))
            {
                Debug.LogWarning("No alcanzan las gemas para comprar oro.");
                return;
            }

            StatsManager.Instance?.AddGold(goldTradeGoldGained);
            Refresh();
        }

        private void Refresh()
        {
            if (goldText != null) goldText.text = (StatsManager.Instance != null ? StatsManager.Instance.Gold : 0).ToString();
            if (gemsText != null) gemsText.text = (NightWallet.Instance != null ? NightWallet.Instance.Gems : 0).ToString();
        }

        // Shakes a slot, always returning it to its true resting position — even if bought again mid-shake.
        private void ShakeSlot(RectTransform rect)
        {
            // Remember the resting position the first time; reset to it before every shake.
            if (!_shakeHome.ContainsKey(rect)) _shakeHome[rect] = rect.localPosition;
            if (_shakeCo.TryGetValue(rect, out Coroutine running) && running != null) StopCoroutine(running);
            rect.localPosition = _shakeHome[rect];

            _shakeCo[rect] = StartCoroutine(ShakeRoutine(rect, _shakeHome[rect]));
        }

        private IEnumerator ShakeRoutine(RectTransform rect, Vector3 home)
        {
            for (float e = 0f; e < shakeDuration; e += Time.unscaledDeltaTime)
            {
                float damper = 1f - (e / shakeDuration);
                rect.localPosition = home + (Vector3)(Random.insideUnitCircle * (shakeMagnitude * damper));
                yield return null;
            }
            rect.localPosition = home;
            _shakeCo[rect] = null;
        }

        // A burst of gem icons that rise to fill the screen a little, then fall and fade out.
        private IEnumerator Confetti()
        {
            if (gemSprite == null) yield break;

            RectTransform parent = confettiParent != null ? confettiParent : (RectTransform)transform;
            float halfWidth = parent.rect.width * 0.5f;
            float top = parent.rect.height * 0.5f;
            float killY = -top - offscreenMargin; // once below this, the gem is out of view

            var pieces = new List<Piece>();
            int spawned = 0;
            float interval = confettiCount > 0 ? spawnWindow / confettiCount : 0f;
            float timer = 0f;

            while (spawned < confettiCount || pieces.Count > 0)
            {
                // Spawn staggered over the spawn window (a steady rain, not all at once).
                timer += Time.unscaledDeltaTime;
                while (spawned < confettiCount && timer >= interval)
                {
                    timer -= interval;
                    pieces.Add(SpawnGem(parent, halfWidth, top));
                    spawned++;
                }

                // Fall; remove each gem only once it has dropped out of view.
                for (int i = pieces.Count - 1; i >= 0; i--)
                {
                    Piece p = pieces[i];
                    if (p.rect == null) { pieces.RemoveAt(i); continue; }

                    p.velocity += Vector2.down * (gravity * Time.unscaledDeltaTime);
                    p.rect.anchoredPosition += p.velocity * Time.unscaledDeltaTime;
                    p.rect.Rotate(0f, 0f, p.spin * Time.unscaledDeltaTime);

                    if (p.rect.anchoredPosition.y < killY)
                    {
                        Destroy(p.rect.gameObject);
                        pieces.RemoveAt(i);
                    }
                }
                yield return null;
            }
        }

        private Piece SpawnGem(RectTransform parent, float halfWidth, float top)
        {
            var go = new GameObject("GemConfetti", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            float size = Random.Range(gemSizeMin, gemSizeMax); // varied sizes
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(Random.Range(-halfWidth, halfWidth), top + Random.Range(0f, 80f));

            var img = go.GetComponent<Image>();
            img.sprite = gemSprite;
            img.raycastTarget = false;

            Vector2 vel = new Vector2(Random.Range(-drift, drift), -Random.Range(fallSpeedMin, fallSpeedMax));
            return new Piece { rect = rt, velocity = vel, spin = Random.Range(-spin, spin), image = img };
        }

        private class Piece
        {
            public RectTransform rect;
            public Vector2 velocity;
            public float spin;
            public Image image;
        }
    }
}
