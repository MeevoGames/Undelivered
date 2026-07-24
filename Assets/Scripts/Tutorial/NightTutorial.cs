using System.Collections;
using TMPro;
using Undelivered.Game;
using Undelivered.Night;
using Undelivered.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Tutorial
{
    /// <summary>
    /// Scripted tutorial for the night (Dicegeon) mode, run once the first time the player enters it.
    /// On the menu a welcome window bounces in ("¡Bienvenido nuevo jugador!") with a RECLAMAR button; claiming
    /// grants a starter kit (3 distinct dice, 3 effects of different rarities, 10 gems) shown on a reward
    /// screen (hover for detail). CONTINUAR returns to the menu, the friend calls to send the player into a
    /// tournament, and the "Comenzar torneo" button blinks — tapping it forces the first (tutorial) tournament
    /// straight into combat. Reuses <see cref="PhoneCall"/>, <see cref="TutorialHighlight"/>, <see cref="BouncePanel"/>.
    /// </summary>
    public class NightTutorial : MonoBehaviour
    {
        [SerializeField] private bool runTutorial = true;

        [Tooltip("The mode switcher whose EnteredNight fires the tutorial (auto-found if left empty).")]
        [SerializeField] private ModeSwitcher modeSwitcher;
        [Tooltip("Beat after entering night (letting the menu settle) before the welcome window.")]
        [SerializeField] private float startDelaySeconds = 0.8f;

        [Header("Welcome window")]
        [SerializeField] private BouncePanel welcomePanel;
        [Tooltip("The RECLAMAR button on the welcome window.")]
        [SerializeField] private Button reclaimButton;

        [Header("Starter kit")]
        [Tooltip("The 3 dice to grant — assign three DISTINCT dice.")]
        [SerializeField] private DiceData[] starterDice;
        [Tooltip("The 3 effects to grant — assign three of DIFFERENT rarities.")]
        [SerializeField] private EffectData[] starterEffects;
        [SerializeField] private int starterGems = 10;

        [Header("Reward screen")]
        [SerializeField] private BouncePanel rewardPanel;
        [Tooltip("Cell prefab for a granted die (an ItemView with a Dice TooltipTrigger).")]
        [SerializeField] private ItemView dieCellPrefab;
        [SerializeField] private Transform diceContainer;
        [Tooltip("Cell prefab for a granted effect (an EffectView).")]
        [SerializeField] private EffectView effectCellPrefab;
        [SerializeField] private Transform effectContainer;
        [SerializeField] private TMP_Text gemsText;
        [Tooltip("The CONTINUAR button on the reward screen.")]
        [SerializeField] private Button continueButton;

        [Header("Friend call")]
        [SerializeField] private PhoneCall phone;
        [SerializeField, TextArea] private string[] friendLines =
        {
            "Ahora unite a un torneo a ver si ganas algo.",
            "Suerte bro, yo me voy que ya estoy en una."
        };

        [Header("Start tournament")]
        [Tooltip("The menu screen that holds the 'Comenzar torneo' button — it bounces out when tapped (put a BouncePanel on it, startHidden off).")]
        [SerializeField] private BouncePanel menuPanel;
        [Tooltip("The 'Comenzar torneo' button (blinks, then forces the tutorial tournament).")]
        [SerializeField] private Button startTournamentButton;
        [Tooltip("The forced first tournament (the tutorial one).")]
        [SerializeField] private TournamentData tutorialTournament;

        [Header("Combat intro (shown between the menu leaving and the combat)")]
        [Tooltip("Window that announces the combat; drops in with a bounce and leaves upward.")]
        [SerializeField] private BouncePanel combatIntroPanel;
        [SerializeField] private TMP_Text combatIntroText;
        [SerializeField] private string combatIntroMessage = "Primer combate";
        [Tooltip("How long the announcement stays before it leaves and the combat begins.")]
        [SerializeField] private float combatIntroSeconds = 1.5f;

        private bool _ran;
        private bool _reclaimed;
        private bool _continued;
        private bool _startPressed;
        private bool _granted;

        private void Start()
        {
            if (!runTutorial) return;
            if (modeSwitcher == null) modeSwitcher = FindAnyObjectByType<ModeSwitcher>();
            if (modeSwitcher != null) modeSwitcher.EnteredNight += OnEnteredNight;
        }

        private void OnDestroy()
        {
            if (modeSwitcher != null) modeSwitcher.EnteredNight -= OnEnteredNight;
        }

        private void OnEnteredNight()
        {
            if (_ran) return; // only the first night
            _ran = true;
            StartCoroutine(RunTutorial());
        }

        /// <summary>Debug ("jumptuto"): abort the night tutorial, still granting the starter kit so the player can play.</summary>
        public void Skip()
        {
            StopAllCoroutines();
            TutorialHighlight.StopAll();
            if (phone != null) phone.Cancel();
            if (welcomePanel != null) welcomePanel.SnapHidden();
            if (rewardPanel != null) rewardPanel.SnapHidden();
            GrantStarterKit(); // no-op if already claimed
        }

        private IEnumerator RunTutorial()
        {
            yield return new WaitForSeconds(startDelaySeconds); // let the mode transition settle

            // 1. The menu bounces into the centre of the screen and stays there for the whole tutorial.
            if (menuPanel != null) yield return menuPanel.Show();

            // 2. The welcome window bounces in on top of it; wait for RECLAMAR.
            if (welcomePanel != null) yield return welcomePanel.Show();
            _reclaimed = false;
            if (reclaimButton != null) reclaimButton.onClick.AddListener(OnReclaim);
            yield return new WaitUntil(() => _reclaimed);
            if (reclaimButton != null) reclaimButton.onClick.RemoveListener(OnReclaim);

            // 3. Grant the starter kit and fill the reward screen.
            GrantStarterKit();
            PopulateReward();

            // 4. Welcome closes, the reward screen opens; wait for CONTINUAR, then it closes (back to menu).
            if (welcomePanel != null) yield return welcomePanel.Hide();
            if (rewardPanel != null) yield return rewardPanel.Show();
            _continued = false;
            if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
            yield return new WaitUntil(() => _continued);
            if (continueButton != null) continueButton.onClick.RemoveListener(OnContinue);
            if (rewardPanel != null) yield return rewardPanel.Hide();

            // 5. The friend calls: join a tournament, good luck.
            if (phone != null) yield return phone.PlayCall(friendLines);

            // 6. Blink "Comenzar torneo"; tapping it forces the tutorial tournament straight into combat.
            _startPressed = false;
            if (startTournamentButton != null)
            {
                TutorialHighlight.Blink(startTournamentButton.gameObject);
                startTournamentButton.onClick.AddListener(OnStartTournament);
            }
            yield return new WaitUntil(() => _startPressed);
            if (startTournamentButton != null)
            {
                startTournamentButton.onClick.RemoveListener(OnStartTournament);
                TutorialHighlight.StopBlink(startTournamentButton.gameObject);
            }

            if (menuPanel != null) StartCoroutine(menuPanel.Hide()); // 6.1 the menu screen leaves with a bounce

            // The hub goes with it, and the arena (background, characters, turn bar) comes in right away,
            // so the announcement plays over the real scene instead of an empty screen.
            CombatController combat = CombatController.Instance;
            if (combat != null)
            {
                combat.HideHub();
                combat.PrepareTournament(tutorialTournament);
            }

            // 6.2-6.4 The combat announcement drops in, holds, then bounces back up out of the way.
            if (combatIntroPanel != null)
            {
                if (combatIntroText != null) combatIntroText.text = combatIntroMessage;
                yield return combatIntroPanel.Show();
                yield return new WaitForSeconds(combatIntroSeconds);
                yield return combatIntroPanel.Hide();
            }

            // 6.5 Only now do the turns start.
            if (combat != null) combat.BeginPreparedCombat();
        }

        // ----- grants + reward screen -----

        private void GrantStarterKit()
        {
            if (_granted) return;
            _granted = true;
            if (Inventory.Instance != null)
            {
                if (starterDice != null)
                    foreach (DiceData die in starterDice)
                        if (die != null) Inventory.Instance.AddDie(die);
                if (starterEffects != null)
                    foreach (EffectData effect in starterEffects)
                        if (effect != null) Inventory.Instance.AddEffect(effect);
            }
            if (NightWallet.Instance != null && starterGems != 0) NightWallet.Instance.AddGems(starterGems);
        }

        private void PopulateReward()
        {
            // Clear FIRST (both may be the same container — clearing after filling would wipe the dice).
            Clear(diceContainer);
            if (effectContainer != diceContainer) Clear(effectContainer);

            // Nothing is being bought here — it's a gift, so the cells show no price.
            if (starterDice != null && dieCellPrefab != null && diceContainer != null)
                foreach (DiceData die in starterDice)
                    if (die != null) Instantiate(dieCellPrefab, diceContainer, false).Setup(die, false);

            if (starterEffects != null && effectCellPrefab != null && effectContainer != null)
                foreach (EffectData effect in starterEffects)
                    if (effect != null) Instantiate(effectCellPrefab, effectContainer, false).Setup(effect, false);

            if (gemsText != null) gemsText.text = $"+{starterGems}";
        }

        private static void Clear(Transform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--) Destroy(container.GetChild(i).gameObject);
        }

        // ----- button handlers -----

        private void OnReclaim() => _reclaimed = true;
        private void OnContinue() => _continued = true;

        private void OnStartTournament() => _startPressed = true;
    }
}
