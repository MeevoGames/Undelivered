using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Undelivered.Player;
using Undelivered.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// Drives the combat turn loop and resolves each throw. Turn order is by speed (the player plus every
    /// living enemy); dead enemies are skipped and drop out of the turn bar. Throwing order: throw die →
    /// actor leans → die result → actor attacks → target takes damage.
    ///
    /// If the player dies, the Game Over window opens; its "Continuar" applies the tournament penalty and
    /// returns to tournament selection. Win handling and tournament sequencing are still to come.
    /// </summary>
    public class CombatController : MonoBehaviour
    {
        public static CombatController Instance { get; private set; }

        [Header("Combat")]
        [SerializeField] private EncounterData encounter;
        [Tooltip("The tournament this combat belongs to — its penalty is applied if the player loses.")]
        [SerializeField] private TournamentData currentTournament;
        [Tooltip("Auto-start the serialized encounter on scene load (testing only; the real flow uses the Comenzar button).")]
        [SerializeField] private bool autoStartForTesting;

        [Header("Player")]
        [SerializeField] private PlayerCombatant player;
        [Tooltip("Fallback speed/sprite used if no PlayerCombatant is assigned.")]
        [SerializeField] private int playerSpeed = 5;
        [SerializeField] private Sprite playerSprite;

        [Header("References")]
        [SerializeField] private TurnOrderBar turnBar;
        [SerializeField] private CombatEnemies enemies;
        [Tooltip("Evaluates enemy synergies (same-type/rarity/identical bonuses) and lists their icons.")]
        [SerializeField] private SynergySystem synergy;
        [SerializeField] private Deck deck;
        [SerializeField] private EffectDeck effectDeck;
        [SerializeField] private PlayerLevel level;         // XP / level system
        [SerializeField] private LevelUpPanel levelUpPanel; // stat-upgrade UI
        [Tooltip("How long the heal-room fountain stays before it sends its heal icons.")]
        [SerializeField] private float healRoomHoldSeconds = 0.8f;

        [Header("Combat-prep enemy preview")]
        [Tooltip("Where the first combat's enemy icons are listed in the prep view.")]
        [SerializeField] private Transform combatPreviewContainer;
        [SerializeField] private EnemyPreviewIcon enemyPreviewIconPrefab;
        [Tooltip("Bonus XP for clearing every enemy in a combat (awarded on victory).")]
        [SerializeField] private int clearAllBonusXp = 10;
        [Tooltip("Dead time after the next combat's enemies arrive before it starts (room for a future intro animation).")]
        [SerializeField] private float nextCombatDelay = 1f;
        [SerializeField] private DiceThrower thrower;
        [Tooltip("Shaker on a UI content root of the combat (NOT the camera — overlay UI ignores it).")]
        [SerializeField] private CameraShake cameraShake;
        [Tooltip("The COMENZAR COMBATE button — disabled while the dice deck is empty.")]
        [SerializeField] private Button beginCombatButton;

        [Header("Defeat")]
        [SerializeField] private GameOverPanel gameOverPanel;
        [SerializeField] private NightScreens screens;
        [Tooltip("The 'Cerrar por hoy' button on the tournament UI — hidden until a defeat, then revealed.")]
        [SerializeField] private GameObject closeForTodayButton;

        [Header("Timing")]
        [Tooltip("How long a thrown die stays on the table before it is removed and the turn passes.")]
        [SerializeField] private float dieLingerSeconds = 1.5f;
        [Tooltip("Pause before an enemy throws, so its turn reads clearly.")]
        [SerializeField] private float enemyThinkSeconds = 0.6f;
        [Tooltip("Delay from the attack animation to the moment damage lands (the strike impact).")]
        [SerializeField] private float attackImpactDelay = 0.25f;
        [Tooltip("How long the transformed die face (sprite swap + shake) shows before the attack.")]
        [SerializeField] private float transformShowSeconds = 0.5f;
        [Tooltip("How long the turn-bar 'extra turn' loop plays before the player acts again.")]
        [SerializeField] private float extraTurnLoopSeconds = 0.9f;
        [Tooltip("How long the start-of-turn state damage (Quema/Veneno) shows before the actor acts.")]
        [SerializeField] private float stateTickSeconds = 0.55f;

        private struct Turn { public bool IsPlayer; public int EnemyIndex; }

        private readonly List<Turn> _order = new List<Turn>();
        private readonly List<EffectData> _pendingBlocks = new List<EffectData>(); // type 5: block enemy rolls
        private bool _previousWasEnemy;
        private readonly List<EffectData> _pendingManips = new List<EffectData>(); // type 6: applied at the next round start
        private bool _orderManipulated; // a one-round GoFirst/FastestLast is active this round (restore next round)
        private EffectData _lastUsedEffect; // type 7 LastEffect: the last non-renewal effect used
        private readonly Dictionary<int, DiceData> _enemyDieOverride = new Dictionary<int, DiceData>(); // type 9: one-turn degraded enemy dice, keyed by enemy index
        private readonly StatusTracker _states = new StatusTracker(); // type 11: Quema/Veneno/Congelamiento marks on enemies
        private enum CounterMode { None, ToAttacker, ToAll } // type 12: a pending reflect of the next enemy hit
        private CounterMode _pendingCounter = CounterMode.None;
        private List<EffectData> _pendingCounterMods; // type 12: add/×mods from the counter throw, applied to the reflected hit
        private readonly LuckSystem _luck = new LuckSystem(); // type 13: per-die luck bonuses
        private int _playerTurn; // counts the player's turns (luck bonuses expire by this)
        private int _round; // current round (1-based); states expire 2 rounds after they're applied
        private int _current;
        private bool _combatEnded;
        private int _combatIndex; // which combat of the current tournament we're in (0-based)
        private TournamentEntry _currentEntry;

        private PlayerCombatant ResolvedPlayer => player != null ? player : PlayerCombatant.Instance;
        private int PlayerSpeed => ResolvedPlayer != null ? ResolvedPlayer.Speed : playerSpeed;
        private Sprite PlayerSprite => ResolvedPlayer != null ? ResolvedPlayer.Sprite : playerSprite;
        private PlayerLevel ResolvedLevel => level != null ? level : PlayerLevel.Instance;
        private LevelUpPanel ResolvedLevelPanel => levelUpPanel != null ? levelUpPanel : LevelUpPanel.Instance;

        // Damage to an enemy is XP: gain it and fly XP icons from the enemy to the bar.
        private void OnEnemyDamaged(EnemySlot slot, int amount)
        {
            PlayerLevel lvl = ResolvedLevel;
            if (lvl == null || amount <= 0) return;
            lvl.GainXp(amount);
            lvl.SpawnXpIcons(slot != null ? slot.transform as RectTransform : null, amount);
        }

        // Pauses the combat while any queued level-up is resolved through the stat-upgrade UI.
        private IEnumerator ProcessLevelUps()
        {
            PlayerLevel lvl = ResolvedLevel;
            LevelUpPanel ui = ResolvedLevelPanel;
            if (lvl == null || ui == null || !lvl.HasPendingLevelUp) yield break;

            SetPlayerInput(false);
            while (lvl.HasPendingLevelUp)
            {
                yield return lvl.PlayLevelUp(); // bar fills → holds → empties, then the UI opens
                bool done = false;
                ui.Show(ResolvedPlayer, () => done = true);
                while (!done) yield return null;
                RefreshLuck(); // a luck upgrade changes every die's %
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DiceThrower.LuckProvider = LuckFractionFor; // type 13: player rolls are biased by each die's luck
        }

        // A die's luck as a 0..1 chance of its highest face (natural + active bonuses).
        private float LuckFractionFor(DiceData die) => _luck.LuckFraction(die);

        private void OnEnable()
        {
            if (thrower != null)
            {
                thrower.ThrowStarted += OnThrowStarted;
                thrower.DieLanded += OnDieLanded;
            }
            if (gameOverPanel != null) gameOverPanel.Continued += OnGameOverContinue;
            if (deck != null) deck.Changed += UpdateBeginButton;
            UpdateBeginButton();
        }

        private void OnDisable()
        {
            if (thrower != null)
            {
                thrower.ThrowStarted -= OnThrowStarted;
                thrower.DieLanded -= OnDieLanded;
            }
            if (gameOverPanel != null) gameOverPanel.Continued -= OnGameOverContinue;
            if (deck != null) deck.Changed -= UpdateBeginButton;
        }

        // The combat can only start with at least one die in the deck.
        private void UpdateBeginButton()
        {
            if (beginCombatButton != null) beginCombatButton.interactable = deck != null && deck.HasDice;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (DiceThrower.LuckProvider == LuckFractionFor) DiceThrower.LuckProvider = null;
        }

        private void Start()
        {
            if (closeForTodayButton != null) closeForTodayButton.SetActive(false); // hidden on entering the mode
            if (autoStartForTesting && encounter != null) StartCombat(encounter);
        }

        /// <summary>Sets the tournament whose penalty applies on defeat (set by the tournament flow).</summary>
        public void SetTournament(TournamentData tournament) => currentTournament = tournament;

        /// <summary>
        /// Full reset when the night mode is (re)entered: snap the panels back, clear the arena, hide
        /// "Cerrar por hoy", reset the player + decks (dropping penalty losses), and re-open every tournament.
        /// </summary>
        public void ResetForNewNight()
        {
            _combatEnded = false;
            _currentEntry = null;

            if (screens != null) screens.ResetLayout();
            if (closeForTodayButton != null) closeForTodayButton.SetActive(false);
            ResetArena();

            foreach (TournamentEntry entry in FindObjectsByType<TournamentEntry>(FindObjectsInactive.Exclude))
                entry.ResetEntry();

            SetPlayerInput(false);
            UpdateBeginButton();
        }

        // Clears the arena and returns the player/decks to a clean out-of-combat state (shared by the
        // night-entry reset and the end-of-tournament return to selection).
        private void ResetArena()
        {
            if (thrower != null) thrower.ClearTable();
            SynergyEndCombat();                                           // remove every synergy icon
            if (enemies != null) enemies.Build(null);                     // no enemies on the board
            if (turnBar != null) turnBar.Build(new List<TurnOrderBar.Participant>());

            if (ResolvedPlayer != null) ResolvedPlayer.ResetForCombat();  // full health, placed, revived
            if (deck != null) deck.ResetForNewNight();                    // renew the dice deck (unlocked, from inventory)
            if (effectDeck != null) effectDeck.ResetForNewNight();        // renew the effect deck

            _states.Clear();
            _enemyDieOverride.Clear();
            _pendingBlocks.Clear();
            _pendingManips.Clear();
            _pendingCounter = CounterMode.None;
            _pendingCounterMods = null;
            _luck.Clear();
            RefreshLuck(); // dice show their base luck again
        }

        /// <summary>Entering a tournament from the selection UI: remembers it and shows the combat-prep view.</summary>
        public void EnterTournament(TournamentEntry entry)
        {
            _currentEntry = entry;
            currentTournament = entry != null ? entry.Tournament : null;
            _combatIndex = 0;
            if (currentTournament != null && currentTournament.Combats.Count > 0)
                encounter = currentTournament.Combats[0];

            BuildCombatPreview(); // the first combat's enemies, in the prep view

            if (closeForTodayButton != null) closeForTodayButton.SetActive(false);
            if (screens != null) screens.OpenTournament();
            UpdateBeginButton();
        }

        /// <summary>"Salir del torneo": back out of the prep view to the tournament list (nothing is played).</summary>
        public void ExitTournament()
        {
            _currentEntry = null;
            currentTournament = null;
            ClearCombatPreview();
            if (screens != null) screens.ReturnToTournament();
            if (TournamentPanel.Instance != null) TournamentPanel.Instance.Refresh();
        }

        // Lists the first combat's enemies as clickable icons (tap → EnemyDetailPanel).
        private void BuildCombatPreview()
        {
            if (combatPreviewContainer == null || enemyPreviewIconPrefab == null) return;
            ClearCombatPreview();
            if (encounter == null) return;

            foreach (EncounterData.Enemy e in encounter.Enemies)
            {
                if (e == null || e.enemy == null) continue;
                EnemyPreviewIcon icon = Instantiate(enemyPreviewIconPrefab, combatPreviewContainer, false);
                icon.Setup(e.enemy, e.rarity, null); // synergy is derived only at combat time
            }
        }

        private void ClearCombatPreview()
        {
            if (combatPreviewContainer == null) return;
            for (int i = combatPreviewContainer.childCount - 1; i >= 0; i--)
                Destroy(combatPreviewContainer.GetChild(i).gameObject);
        }

        /// <summary>"COMENZAR COMBATE": leaves the prep view and starts the combat (needs dice in the deck).</summary>
        public void BeginCombat()
        {
            if (encounter == null || deck == null || !deck.HasDice) return;
            if (closeForTodayButton != null) closeForTodayButton.SetActive(false); // hidden during combat
            if (screens != null) screens.StartCombat();
            StartCombat(encounter);
        }

        /// <summary>Starts the first combat of a tournament: fresh deck (unlocked), fresh enemies, turn 1.</summary>
        public void StartCombat(EncounterData combat)
        {
            _combatIndex = 0;
            ResetCombatState(combat);

            if (ResolvedPlayer != null) ResolvedPlayer.ResetForCombat(); // full health, revived
            if (enemies != null) { enemies.Build(combat); SubscribeEnemyDamage(); }
            if (deck != null) deck.StartCombat();                         // full deck: unlock + un-spend every die
            if (effectDeck != null) effectDeck.StartCombat();             // Épica effects available again

            SynergyStartCombat();  // list this line-up's synergies and apply their opening bonuses (before turn order)
            BuildTurnOrder();
            RebuildTurnBar();
            RefreshLuck(); // dice show their base luck %
            BeginTurnAt(0);
        }

        // Clears all per-combat state (keeps level/XP, permanent stats, and deck locks).
        private void ResetCombatState(EncounterData combat)
        {
            encounter = combat;

            if (combat != null && Knowledge.Instance != null) // glossary memory: you fought these enemies (win or lose)
                foreach (EncounterData.Enemy e in combat.Enemies)
                    if (e != null) Knowledge.Instance.LearnEnemy(e.enemy);

            _combatEnded = false;
            _pendingBlocks.Clear();
            _previousWasEnemy = false;
            _pendingManips.Clear();
            _orderManipulated = false;
            _lastUsedEffect = null;
            _enemyDieOverride.Clear();
            _states.Clear();
            _pendingCounter = CounterMode.None;
            _pendingCounterMods = null;
            _luck.Clear();
            _playerTurn = 0;
            _round = 1;
        }

        // XP + healing drops: listen for damage and death on each freshly-built enemy slot.
        private void SubscribeEnemyDamage()
        {
            for (int i = 0; i < EnemyCount; i++)
            {
                EnemySlot s = enemies.Slot(i);
                if (s == null) continue;
                s.Damaged += amount => OnEnemyDamaged(s, amount);
                s.Died += () => OnEnemyDied(s);
            }
        }

        // A dead enemy may drop a healing item (per its chance): a pickup falls, rests, then flies to the player.
        private void OnEnemyDied(EnemySlot slot)
        {
            SynergyEvaluate(); // one fewer enemy: a "N of a kind" may drop → revert its bonuses, break its icon

            if (slot == null || slot.Enemy == null) return;
            EnemyData e = slot.Enemy;
            if (e.HealDropChance <= 0f || e.HealDropAmount <= 0) return;
            if (UnityEngine.Random.value > e.HealDropChance) return; // rolled no drop

            PlayerCombatant p = ResolvedPlayer;
            if (p == null) return;

            if (HealVisuals.Instance != null)
            {
                HealVisuals.Instance.DropFromEnemy(slot.transform as RectTransform, e.HealDropAmount, p);
            }
            else // no visuals wired: heal straight away
            {
                p.Heal(e.HealDropAmount);
                p.ShowNumber(e.HealDropAmount, FloatingNumbers.Kind.Heal);
            }
        }

        // Victory on a combat that isn't the last: bonus XP, renew the (unlocked) decks, then slide the next
        // combat's enemies in and, after a beat, begin — no UI in between.
        private IEnumerator AdvanceToNextCombat()
        {
            _combatIndex++;
            EncounterData combat = currentTournament.Combats[_combatIndex];
            ResetCombatState(combat);
            _combatEnded = true; // stays "ended" through the transition so nothing acts until turn 1
            SynergyEndCombat();  // the won combat is over → clear its icons before the next line-up arrives

            if (ResolvedPlayer != null) ResolvedPlayer.ClearCombatBoosts(); // health/shield carry over; drop the per-combat boosts
            if (deck != null) deck.RefreshDeck();       // un-spend the dice that aren't locked
            if (effectDeck != null) effectDeck.RefreshEffects(); // renew the effect deck

            if (enemies != null)
            {
                enemies.Build(combat);
                SubscribeEnemyDamage();
                yield return enemies.BounceIn();                          // arrive from off-screen right
            }

            SynergyStartCombat();  // fresh synergies for the new line-up (clears the previous combat's icons)
            BuildTurnOrder();
            RebuildTurnBar();
            RefreshLuck();

            yield return new WaitForSecondsRealtime(nextCombatDelay);      // dead time (room for a future intro)

            _combatEnded = false;
            BeginTurnAt(0);
        }

        // Between combats: if the tournament defines a heal room after this combat, a fountain bounces into
        // slot 3, waits, then sends +1 heal icons to the player.
        private IEnumerator ShowHealRoomIfAny(int combatIndex)
        {
            int maxHeal = currentTournament != null ? currentTournament.HealRoomAfter(combatIndex) : 0;
            if (maxHeal <= 0 || enemies == null) yield break;

            SetPlayerInput(false);
            SynergyEndCombat();    // the beaten enemies leave → clear their synergy icons
            enemies.Build(null);   // clear the beaten enemies so the fountain stands alone
            RebuildTurnBar();

            yield return enemies.ShowFountain();                             // bounces into slot 3
            yield return new WaitForSecondsRealtime(healRoomHoldSeconds);    // stays a beat

            HealVisuals hv = HealVisuals.Instance;
            if (hv != null && enemies.Fountain != null)
                yield return hv.FountainHeal(enemies.Fountain, maxHeal, ResolvedPlayer); // icons fly + heal +1 each
            else if (ResolvedPlayer != null)                                 // no visuals: heal straight away
                ResolvedPlayer.Heal(maxHeal);

            enemies.ClearFountain();
        }

        // Player + enemies, sorted by speed (fastest first). Same order the turn bar draws.
        private void BuildTurnOrder()
        {
            _order.Clear();

            var participants = new List<(bool isPlayer, int enemyIndex, int speed)>
            {
                (true, -1, PlayerSpeed)
            };
            if (encounter != null)
            {
                IReadOnlyList<EncounterData.Enemy> es = encounter.Enemies;
                for (int i = 0; i < es.Count; i++)
                    if (es[i] != null && es[i].enemy != null)
                        participants.Add((false, i, EnemySpeed(i))); // includes any synergy speed bonus
            }

            foreach (var p in participants.OrderByDescending(p => p.speed))
                _order.Add(new Turn { IsPlayer = p.isPlayer, EnemyIndex = p.enemyIndex });
        }

        private void BeginTurnAt(int index)
        {
            if (_combatEnded || _order.Count == 0) return;

            // Skip dead participants (dead enemies don't act).
            int found = -1;
            for (int step = 0; step < _order.Count; step++)
            {
                int i = ((index + step) % _order.Count + _order.Count) % _order.Count;
                if (IsAlive(_order[i])) { found = i; break; }
            }
            if (found < 0) return;
            _current = found;

            if (turnBar != null) turnBar.SetCurrentTurn(AliveIndexOf(_current));

            bool playerTurn = _order[_current].IsPlayer;
            if (playerTurn && _previousWasEnemy) _pendingBlocks.Clear(); // a round of enemy attacks ended → Épica blocks expire
            _previousWasEnemy = !playerTurn;

            SetPlayerInput(false);        // no input while the start-of-turn states resolve
            StartCoroutine(TakeTurn());
        }

        // Every turn starts by ticking states (poison on all enemies, burn on the current enemy), then the actor acts.
        private IEnumerator TakeTurn()
        {
            yield return TickStatesAtTurnStart();
            if (_combatEnded) yield break;
            if (AllEnemiesDead()) { yield return WinCombat(); yield break; } // a state tick cleared the board

            yield return ProcessLevelUps(); // state-tick damage may have leveled the player up
            if (_combatEnded) yield break;

            // A tick may have killed the current enemy → skip straight to the next participant.
            if (!IsAlive(_order[_current])) { AdvanceTurn(); yield break; }

            if (turnBar != null) turnBar.SetCurrentTurn(AliveIndexOf(_current)); // ticks may have re-slotted the bar

            if (_order[_current].IsPlayer)
            {
                _playerTurn++;
                _luck.Expire(_playerTurn); // luck bonuses whose turns have passed fall off
                if (effectDeck != null) effectDeck.TickCooldowns(); // Épica renews in 2 turns, Rara in 4
                RefreshLuck();
                SetPlayerInput(true);
                if (deck != null) deck.BeginTurn(); // refresh the deck if the player is out of dice
                // now wait for the player to throw a die (resolved in OnDieLanded)
            }
            else
            {
                StartCoroutine(EnemyTurn());
            }
        }

        // Poison hits every living enemy on any turn; burn hits the current enemy on its own turn. Both bypass shield.
        private IEnumerator TickStatesAtTurnStart()
        {
            int currentEnemy = _order[_current].IsPlayer ? -1 : _order[_current].EnemyIndex;
            bool anyTick = false;

            for (int i = 0; i < EnemyCount; i++)
            {
                EnemySlot slot = enemies != null ? enemies.Slot(i) : null;
                if (slot == null || !slot.IsAlive) continue;

                int poison = slot.PoisonImmune ? 0 : _states.PoisonDamage(i); // veneno: any turn (immune enemies take none)
                int burn = i == currentEnemy ? _states.BurnDamage(i) : 0;    // quema: only its own turn
                int dmg = poison + burn;
                if (dmg <= 0) continue;

                slot.ApplyHealthDamage(dmg); // states bypass shield
                if (poison > 0) slot.ShowNumber(poison, FloatingNumbers.Kind.Poison); // each state in its own colour
                if (burn > 0) slot.ShowNumber(burn, FloatingNumbers.Kind.Burn);
                anyTick = true;
            }

            if (anyTick)
            {
                Shake();
                yield return new WaitForSecondsRealtime(stateTickSeconds);
                RebuildTurnBar(); // some enemies may have died (TakeTurn checks for the win)
            }
        }

        // Advances to the next living participant; detects the round wrap to run OnRoundStart.
        private void AdvanceTurn()
        {
            if (_order.Count == 0) return;
            int next = NextAlive(_current + 1);
            if (next < 0) return;

            if (next <= _current) // wrapped around → a new round begins
            {
                OnRoundStart();
                next = NextAlive(0);
                if (next < 0) return;
            }
            BeginTurnAt(next);
        }

        private int NextAlive(int from)
        {
            for (int step = 0; step < _order.Count; step++)
            {
                int i = ((from + step) % _order.Count + _order.Count) % _order.Count;
                if (IsAlive(_order[i])) return i;
            }
            return -1;
        }

        // At each round start: bump the round, expire old state marks, then handle type-6 manips.
        private void OnRoundStart()
        {
            _round++;
            _states.Expire(_round); // marks applied 2+ rounds ago fall off
            RefreshAllStatuses();   // markers reflect the marks that just dropped

            SynergyRoundStart();    // per-round synergy boons (heal / +max health) + re-check the icons

            if (!_orderManipulated && _pendingManips.Count == 0) return; // no turn-order change to apply

            ResortBySpeed();          // base order by current speed (restores one-round manips + reflects boosts)
            _orderManipulated = false;

            // Permanent speed boosts first, then re-sort so the new speed takes effect.
            bool boosted = false;
            foreach (EffectData m in _pendingManips)
                if (m.Manip == EffectData.TurnManipKind.SpeedBoost)
                {
                    if (ResolvedPlayer != null) ResolvedPlayer.AddSpeed(m.Amount);
                    boosted = true;
                }
            if (boosted) ResortBySpeed();

            // One-round reorders on top (this round only): move you first / send the fastest last.
            foreach (EffectData m in _pendingManips)
            {
                if (m.Manip == EffectData.TurnManipKind.GoFirst) { MovePlayerToFront(); _orderManipulated = true; }
                else if (m.Manip == EffectData.TurnManipKind.FastestLast) { MoveFastestToLast(); _orderManipulated = true; }
            }

            _pendingManips.Clear();

            int first = NextAlive(0);
            if (first >= 0) _current = first; // the round restarts at the new first, so the indicator lands there
            RebuildTurnBar();
        }

        private IEnumerator EnemyTurn()
        {
            yield return new WaitForSecondsRealtime(enemyThinkSeconds);

            int enemyIndex = _order[_current].EnemyIndex;
            EnemyData enemy = EnemyAt(enemyIndex);
            DiceData die = enemy != null ? enemy.Die : null;

            // Type 9: if this enemy's die was degraded, throw the degraded die this turn, then it reverts.
            if (_enemyDieOverride.TryGetValue(enemyIndex, out DiceData degraded) && degraded != null)
            {
                die = degraded;
                _enemyDieOverride.Remove(enemyIndex);
            }

            if (die != null && thrower != null)
            {
                thrower.Throw(die); // ThrowStarted → the enemy's throw lean; result resolved in OnDieLanded
            }
            else
            {
                AdvanceTurn(); // no die to throw: just pass
            }
        }

        // The current actor leans forward as it throws its die.
        private void OnThrowStarted()
        {
            ActorPlayThrow();
        }

        private void OnDieLanded(DiceFace result)
        {
            if (_combatEnded || _order.Count == 0) return;

            SetPlayerInput(false); // no input while a throw resolves
            if (_order[_current].IsPlayer) StartCoroutine(ResolvePlayerThrow(result));
            else StartCoroutine(ResolveEnemyThrow(result));
        }

        private IEnumerator ResolvePlayerThrow(DiceFace result)
        {
            // the die has fallen; apply the activated effects to the roll BEFORE consuming them
            int baseValue = result != null ? result.Value : 0;
            int finalValue = baseValue;
            EffectData.TargetStat targetStat = EffectData.TargetStat.Damage;
            bool transformed = false;
            EffectData.GrantKind? grant = null;
            List<EffectData> renewals = null;
            EffectData miniDiceEffect = null;
            int miniDiceCount = 0; // how many Create-dice effects were used (they stack)
            bool skipShield = false;  // type 10 SkipShield: your roll damages HP directly
            bool breakShield = false; // type 10 BreakShield: no damage, destroy the closest enemy's shield
            List<EffectData> stateEffects = null; // type 11: Quema/Veneno/Congelamiento to apply on the attack
            bool counterattack = false; // type 12: your roll deals no damage; arm a reflect for the next enemy hit
            bool lockThrownDie = false; // type 12 (ToAll): the thrown die can't be renewed this combat
            List<EffectData> rollMods = null; // type 1 add/×mods used this throw (redirected to the reflect if it's a counter)
            List<EffectData> luckEffects = null; // type 13: luck bonuses to add to the thrown die
            if (effectDeck != null)
            {
                foreach (EffectData effect in effectDeck.SelectedEffects)
                {
                    if (effect == null) continue;
                    int before = finalValue;
                    finalValue = effect.ModifyRoll(finalValue);                                  // type 1: add/multiply, type 4: transform
                    if (effect.Type == EffectData.EffectType.RollModifier) (rollMods ??= new List<EffectData>()).Add(effect); // may be redirected to a counter
                    if (effect.Type == EffectData.EffectType.StatConverter) targetStat = effect.Stat; // type 2: last converter wins
                    if (effect.Type == EffectData.EffectType.NumberTransform && finalValue != before) transformed = true;
                    if (effect.Type == EffectData.EffectType.GrantTurn) grant = effect.Grant;    // type 3
                    if (effect.Type == EffectData.EffectType.NumberBlock) _pendingBlocks.Add(effect); // type 5: hits the enemy's next roll
                    if (effect.Type == EffectData.EffectType.TurnManip) _pendingManips.Add(effect); // type 6: applies next round
                    if (effect.Type == EffectData.EffectType.MiniDice) { miniDiceEffect = effect; miniDiceCount++; } // type 8 (stacks)
                    if (effect.Type == EffectData.EffectType.DegradeEnemyDie) ApplyDegrade(effect); // type 9
                    if (effect.Type == EffectData.EffectType.ShieldManip) // type 10
                    {
                        if (effect.Shield == EffectData.ShieldKind.SkipShield) skipShield = true;
                        else breakShield = true;
                    }
                    if (effect.Type == EffectData.EffectType.AddState) (stateEffects ??= new List<EffectData>()).Add(effect); // type 11
                    if (effect.Type == EffectData.EffectType.Counterattack) // type 12
                    {
                        counterattack = true;
                        if (effect.Counter == EffectData.CounterKind.ToAll) { _pendingCounter = CounterMode.ToAll; lockThrownDie = true; }
                        else _pendingCounter = CounterMode.ToAttacker;
                    }
                    if (effect.Type == EffectData.EffectType.AddLuck) (luckEffects ??= new List<EffectData>()).Add(effect); // type 13
                    if (effect.Type == EffectData.EffectType.Renewal) (renewals ??= new List<EffectData>()).Add(effect); // type 7
                    else _lastUsedEffect = effect; // remember the last non-renewal effect used (for LastEffect renewal)
                }
                effectDeck.ConsumeSelected();
            }

            // Type 12: keep this throw's add/×mods so they scale the reflected hit; lock the die (ToAll) before renewals run.
            if (counterattack) _pendingCounterMods = rollMods;
            if (lockThrownDie && deck != null && thrower != null) deck.LockDie(thrower.LastDie);

            // Type 7: renewals — refresh decks / the used die / the last effect.
            if (renewals != null)
                foreach (EffectData r in renewals) ApplyRenewal(r);

            // Type 4: a transform changed the die's number → show it on the face (sprite swap) + shake first.
            if (transformed && thrower != null)
            {
                thrower.ShowResultValue(finalValue);
                yield return new WaitForSecondsRealtime(transformShowSeconds);
            }

            // Type 3 (SecondDie / CopyDie): drop a second die next to the first; its value adds to the total.
            int extra = 0;
            if ((grant == EffectData.GrantKind.SecondDie || grant == EffectData.GrantKind.CopyDie) && thrower != null && thrower.LastDie != null)
            {
                DiceFace copyFace = grant == EffectData.GrantKind.CopyDie ? result : null;
                yield return thrower.ThrowExtraRoutine(thrower.LastDie, copyFace, r => extra = r);
            }

            // Type 8 (MiniDice): your roll becomes the COUNT of mini dice (it deals no damage itself);
            // spawn that many, throw them next to the main die, and their sum is the damage.
            int total;
            int shownBase; // the plain "damage" floating number
            if (miniDiceEffect != null)
            {
                int count = Mathf.Max(0, finalValue) * miniDiceCount; // each Create-dice effect multiplies the spawn count
                int miniTotal = 0;
                if (count > 0 && thrower != null)
                    yield return thrower.ThrowMiniDiceRoutine(miniDiceEffect.MiniDie, count, s => miniTotal = s);
                total = miniTotal;
                shownBase = miniTotal; // the mini-dice sum is the whole hit
            }
            else
            {
                total = finalValue + extra;
                shownBase = baseValue;
            }
            int bonus = total - shownBase;

            // ORDER: the results are in → play the attack, THEN the target reacts.
            ActorPlayAttack();
            yield return new WaitForSecondsRealtime(attackImpactDelay);

            if (counterattack)
            {
                // Type 12: your roll deals no damage; the reflect is armed for the next enemy hit.
            }
            else if (breakShield)
            {
                // Type 10 (BreakShield): no damage — destroy the closest enemy's shield entirely.
                EnemySlot target = enemies != null ? enemies.ClosestEnemy() : null;
                if (target != null && target.CurrentShield > 0)
                {
                    int removed = target.CurrentShield;
                    target.BreakShield();
                    target.ShowNumber(removed, FloatingNumbers.Kind.Effect); // feedback: shield destroyed
                    Shake();
                }
            }
            else if (targetStat == EffectData.TargetStat.Damage)
            {
                if (total > 0 && enemies != null)
                {
                    EnemySlot target = enemies.ClosestEnemy();
                    if (target != null)
                    {
                        if (skipShield) target.ApplyHealthDamage(total); // type 10: straight to HP, ignoring shield
                        else target.ApplyDamage(total);                  // shield absorbs first
                        if (shownBase > 0) target.ShowNumber(shownBase, FloatingNumbers.Kind.Damage); // normal damage
                        if (bonus > 0) target.ShowNumber(bonus, FloatingNumbers.Kind.Bonus);           // "+X" (effect / second die)
                        Shake();
                        if (!target.IsAlive) RebuildTurnBar(); // it just died: drop it from the turn bar
                    }
                }
            }
            else
            {
                ApplyToPlayerStat(targetStat, total); // convert the roll into heal / shield / speed / luck
            }

            // Type 11 (AddState): stamp the rolled number of Quema/Veneno/Congelamiento marks on the target(s).
            if (stateEffects != null) ApplyStates(stateEffects, finalValue);

            // Type 13 (AddLuck): add (roll × mult)% luck to the player (every die), for the effect's duration.
            if (luckEffects != null)
            {
                foreach (EffectData e in luckEffects)
                {
                    if (e == null) continue;
                    int turns = e.LuckTurns <= 0 ? LuckSystem.WholeCombat : e.LuckTurns;
                    _luck.AddLuck(baseValue * Mathf.Max(1, e.LuckRollMultiplier), _playerTurn, turns);
                }
                RefreshLuck();
            }

            yield return new WaitForSecondsRealtime(dieLingerSeconds);
            if (thrower != null) thrower.ClearTable();

            if (AllEnemiesDead()) { yield return WinCombat(); yield break; }

            yield return ProcessLevelUps(); // XP from this attack may have leveled the player up
            if (_combatEnded) yield break;

            // Type 3 (ExtraTurn): play again.
            if (grant == EffectData.GrantKind.ExtraTurn)
            {
                if (turnBar != null) turnBar.PlayExtraTurnLoop(AliveIndexOf(_current));
                yield return new WaitForSecondsRealtime(extraTurnLoopSeconds);
                BeginTurnAt(_current); // same player again (used dice/effects stay spent)
                yield break;
            }

            AdvanceTurn();
        }

        // Effect type 2: the roll value becomes a player stat instead of enemy damage.
        private void ApplyToPlayerStat(EffectData.TargetStat stat, int value)
        {
            PlayerCombatant p = ResolvedPlayer;
            if (p == null || value <= 0) return;

            switch (stat)
            {
                case EffectData.TargetStat.Heal: p.Heal(value); break;
                case EffectData.TargetStat.Shield: p.AddShield(value); break;
                case EffectData.TargetStat.Speed: p.AddSpeed(value); break;
                case EffectData.TargetStat.Luck: p.AddLuck(value); break;
            }
            p.ShowNumber(value, FloatingNumbers.Kind.Heal); // "+N" beneficial gain on the player
        }

        // Effect type 7: renew a whole deck, the die just used, or the last effect used.
        private void ApplyRenewal(EffectData renewal)
        {
            switch (renewal.Renewal)
            {
                case EffectData.RenewalKind.DiceDeck:
                    if (deck != null) deck.RefreshDeck();
                    break;
                case EffectData.RenewalKind.EffectDeck:
                    if (effectDeck != null) effectDeck.RefreshEffects();
                    break;
                case EffectData.RenewalKind.UsedDie:
                    if (deck != null && thrower != null) deck.RenewDie(thrower.LastDie);
                    break;
                case EffectData.RenewalKind.LastEffect:
                    if (_lastUsedEffect != null && effectDeck != null)
                    {
                        effectDeck.RenewEffect(_lastUsedEffect);
                        _lastUsedEffect = null; // consumed the renewal target
                    }
                    break;
            }
        }

        // Type 9: for one turn, swap the target enemy's die for a basic/degraded one (consumed on its next throw).
        private void ApplyDegrade(EffectData e)
        {
            if (e == null || e.DegradeDie == null || enemies == null) return;

            if (e.Scope == EffectData.EnemyScope.All)
            {
                int count = encounter != null ? encounter.Enemies.Count : 0;
                for (int i = 0; i < count; i++)
                {
                    EnemySlot slot = enemies.Slot(i);
                    if (slot != null && slot.IsAlive) _enemyDieOverride[i] = e.DegradeDie;
                }
            }
            else
            {
                int idx = enemies.ClosestEnemyIndex();
                if (idx >= 0) _enemyDieOverride[idx] = e.DegradeDie;
            }
        }

        // Type 11: apply 'marks' marks of each state effect to the closest enemy or all enemies (this round's stamp).
        private void ApplyStates(List<EffectData> stateEffects, int marks)
        {
            if (stateEffects == null || marks <= 0 || enemies == null) return;

            foreach (EffectData e in stateEffects)
            {
                if (e == null) continue;
                if (e.Scope == EffectData.EnemyScope.All)
                {
                    for (int i = 0; i < EnemyCount; i++)
                    {
                        EnemySlot slot = enemies.Slot(i);
                        if (slot != null && slot.IsAlive && !IsPoisonImmune(slot, e.State)) _states.AddMarks(i, e.State, marks, _round);
                    }
                }
                else
                {
                    int idx = enemies.ClosestEnemyIndex();
                    if (idx >= 0 && !IsPoisonImmune(enemies.Slot(idx), e.State)) _states.AddMarks(idx, e.State, marks, _round);
                }
            }

            RefreshAllStatuses(); // update the markers above the enemies
        }

        // A poison-immune enemy (synergy) never receives Veneno marks; other states apply as normal.
        private static bool IsPoisonImmune(EnemySlot slot, StatusType state)
            => slot != null && state == StatusType.Poison && slot.PoisonImmune;

        // Redraws the state markers above one enemy (or all of them) from its current marks.
        private void RefreshEnemyStatus(int i)
        {
            EnemySlot slot = enemies != null ? enemies.Slot(i) : null;
            if (slot == null) return;
            if (!slot.IsAlive) { slot.SetStatuses(0, 0, 0); return; } // dead enemies show no markers
            slot.SetStatuses(
                _states.Count(i, StatusType.Burn),
                _states.Count(i, StatusType.Poison),
                _states.Count(i, StatusType.Freeze));
        }

        private void RefreshAllStatuses()
        {
            for (int i = 0; i < EnemyCount; i++) RefreshEnemyStatus(i);
        }

        // Type 13: refresh the luck % on every die and the player's luck marker.
        private void RefreshLuck()
        {
            if (ResolvedPlayer != null) _luck.Permanent = ResolvedPlayer.PermanentLuckPercent; // level-up luck
            bool boosted = _luck.TotalBonus() > 0; // temporary effect bonus tints the text (permanent doesn't)
            if (deck != null) deck.RefreshLuck(_luck.LuckPercent, boosted);
            if (ResolvedPlayer != null) ResolvedPlayer.SetLuckMark(_luck.TotalBonus());
        }

        private IEnumerator ResolveEnemyThrow(DiceFace result)
        {
            int damage = result != null ? result.Value : 0;
            int original = damage;

            // Congelamiento (state, type 11): each freeze mark on this enemy reduces its roll by 1.
            int enemyIndex = _order[_current].EnemyIndex;
            int freeze = _states.FreezeReduction(enemyIndex);
            if (freeze > 0) damage = Mathf.Max(0, damage - freeze);

            // Type 5: the player's pending number-blocks hit the enemy's roll.
            if (_pendingBlocks.Count > 0)
            {
                foreach (EffectData block in _pendingBlocks) if (block != null) damage = block.Transform(damage);
                // Común blocks only hit the first enemy; Épica (non-Común) stay for the rest of the round.
                _pendingBlocks.RemoveAll(b => b == null || b.EffectRarity == EffectData.Rarity.Comun);
            }

            // If the roll changed (frozen and/or blocked), show the new number on the enemy die + shake.
            if (damage != original && thrower != null)
            {
                thrower.ShowResultValue(damage);
                yield return new WaitForSecondsRealtime(transformShowSeconds);
            }

            // ORDER: the result is in → play the attack, THEN the player takes damage (recoils).
            ActorPlayAttack();
            yield return new WaitForSecondsRealtime(attackImpactDelay);

            PlayerCombatant target = ResolvedPlayer;
            bool reflected = false;
            if (_pendingCounter != CounterMode.None)
            {
                // Type 12: the counter is spent on the NEXT enemy attack — even a 0 (bad luck / misread),
                // which still shows a "0" on the target(s). The player takes none; real damage is scaled
                // by this throw's add/×mods and reflected.
                int dmg = damage;
                if (dmg > 0 && _pendingCounterMods != null)
                    foreach (EffectData m in _pendingCounterMods) if (m != null) dmg = m.ModifyRoll(dmg);
                ApplyCounterattack(dmg, enemyIndex); // shows the number (even 0); deals damage only if > 0
                reflected = dmg > 0;
                _pendingCounter = CounterMode.None;
                _pendingCounterMods = null;
            }
            else if (damage > 0 && target != null)
            {
                // Skip-shield synergy: this enemy's hit lands straight on the player's health.
                EnemySlot attacker = enemies != null ? enemies.Slot(enemyIndex) : null;
                if (attacker != null && attacker.SkipsShield) target.ApplyHealthDamage(damage);
                else target.ApplyDamage(damage); // triggers the player's hurt/death animation
                target.ShowNumber(damage, FloatingNumbers.Kind.Damage);
                Shake();
            }

            yield return new WaitForSecondsRealtime(dieLingerSeconds);
            if (thrower != null) thrower.ClearTable();

            if (target != null && !target.IsAlive) { GameOver(); yield break; }
            if (reflected) // a reflect landed → it may have killed enemies
            {
                if (AllEnemiesDead()) { yield return WinCombat(); yield break; }
                RebuildTurnBar();
                yield return ProcessLevelUps(); // counterattack XP may have leveled up
                if (_combatEnded) yield break;
            }
            AdvanceTurn();
        }

        // Type 12: the reflected enemy hit — returned to its attacker, or dealt to every enemy. A 0 still shows
        // a "0" floating text on the target(s) to signal the counter was spent (even though it did nothing).
        private void ApplyCounterattack(int damage, int attackerIndex)
        {
            if (enemies == null) return;

            if (_pendingCounter == CounterMode.ToAll)
            {
                for (int i = 0; i < EnemyCount; i++) HitCounter(enemies.Slot(i), damage);
            }
            else
            {
                HitCounter(enemies.Slot(attackerIndex), damage);
            }
            if (damage > 0) Shake();
        }

        private void HitCounter(EnemySlot slot, int damage)
        {
            if (slot == null || !slot.IsAlive) return;
            if (damage > 0) slot.ApplyDamage(damage);
            slot.ShowNumber(damage, FloatingNumbers.Kind.Damage); // even "0" — the counter was spent
        }

        // ----- defeat -----

        private void GameOver()
        {
            _combatEnded = true;
            SetPlayerInput(false);
            if (gameOverPanel != null) gameOverPanel.Show();
        }

        // "Continuar": apply the tournament penalty and return to tournament selection (marked as lost).
        private void OnGameOverContinue()
        {
            ApplyPenalty();
            if (_currentEntry != null) _currentEntry.MarkLost();
            EndTournamentToSelection();
        }

        // The player cleared the combat: bonus XP, then either advance to the next combat (no UI) or, if this
        // was the tournament's last combat, mark it won and return to selection.
        private IEnumerator WinCombat()
        {
            _combatEnded = true;
            SetPlayerInput(false);
            if (thrower != null) thrower.ClearTable();

            PlayerLevel lvl = ResolvedLevel;
            if (lvl != null && clearAllBonusXp > 0) lvl.GainXp(clearAllBonusXp); // reward for clearing every enemy
            yield return ProcessLevelUps();

            if (currentTournament != null && _combatIndex + 1 < currentTournament.Combats.Count)
            {
                yield return ShowHealRoomIfAny(_combatIndex); // optional heal room after the combat just won
                yield return AdvanceToNextCombat();           // then into the next combat
            }
            else
            {
                if (_currentEntry != null) _currentEntry.MarkWon();
                EndTournamentToSelection(); // whole tournament cleared → show the result
            }
        }

        // End of a tournament (won or lost): reset the arena/player/decks and snap the UI back to the
        // tournament-selection layout (like re-entering the night), then reveal "Cerrar por hoy".
        private void EndTournamentToSelection()
        {
            _combatEnded = true;
            _currentEntry = null;
            SetPlayerInput(false);

            ResetArena();
            if (screens != null) screens.ResetLayout(); // UI back to its common (tournament-selection) layout
            if (closeForTodayButton != null) closeForTodayButton.SetActive(true);
            if (TournamentPanel.Instance != null) TournamentPanel.Instance.Refresh(); // entry requirements may have changed
        }

        private void ApplyPenalty()
        {
            if (currentTournament == null) return;
            TournamentData.Penalty penalty = currentTournament.TournamentPenalty;
            if (penalty == null) return;

            if (penalty.gold != 0 && StatsManager.Instance != null) StatsManager.Instance.AddGold(-penalty.gold);
            if (penalty.gems != 0 && NightWallet.Instance != null) NightWallet.Instance.AddGems(-penalty.gems);

            RemoveRandomDice(penalty.diceLost);
            RemoveRandomEffects(penalty.effectsLost);

            if (effectDeck != null) effectDeck.EndTournament(); // destroy the spent Rara effects too
        }

        private void RemoveRandomDice(int count)
        {
            if (count <= 0 || Inventory.Instance == null) return;
            for (int i = 0; i < count && Inventory.Instance.Dice.Count > 0; i++)
            {
                IReadOnlyList<DiceData> dice = Inventory.Instance.Dice;
                Inventory.Instance.RemoveDie(dice[Random.Range(0, dice.Count)]);
            }
        }

        private void RemoveRandomEffects(int count)
        {
            if (count <= 0 || Inventory.Instance == null) return;
            for (int i = 0; i < count && Inventory.Instance.Effects.Count > 0; i++)
            {
                IReadOnlyList<EffectData> effects = Inventory.Instance.Effects;
                Inventory.Instance.RemoveEffect(effects[Random.Range(0, effects.Count)]);
            }
        }

        // ----- helpers -----

        private bool IsAlive(Turn turn)
        {
            if (turn.IsPlayer) return ResolvedPlayer == null || ResolvedPlayer.IsAlive;
            EnemySlot slot = enemies != null ? enemies.Slot(turn.EnemyIndex) : null;
            return slot != null && slot.IsAlive;
        }

        private int EnemyCount => encounter != null ? encounter.Enemies.Count : 0;

        private bool AllEnemiesDead()
        {
            bool hasEnemy = false;
            foreach (Turn t in _order)
            {
                if (t.IsPlayer) continue;
                hasEnemy = true;
                if (IsAlive(t)) return false;
            }
            return hasEnemy;
        }

        // The turn-bar slot index of an order entry = how many living participants come before it.
        // ----- turn manipulation (type 6) -----

        private void MovePlayerToFront()
        {
            int i = PlayerOrderIndex();
            if (i <= 0) return;
            Turn t = _order[i];
            _order.RemoveAt(i);
            _order.Insert(0, t);
        }

        private void MoveFastestToLast()
        {
            if (_order.Count < 2) return;
            int fastest = 0, maxSpeed = int.MinValue;
            for (int i = 0; i < _order.Count; i++)
            {
                int s = SpeedOf(_order[i]);
                if (s > maxSpeed) { maxSpeed = s; fastest = i; }
            }
            Turn t = _order[fastest];
            _order.RemoveAt(fastest);
            _order.Add(t);
        }

        private void ResortBySpeed()
        {
            List<Turn> sorted = _order.OrderByDescending(SpeedOf).ToList();
            _order.Clear();
            _order.AddRange(sorted);
        }

        private int PlayerOrderIndex()
        {
            for (int i = 0; i < _order.Count; i++) if (_order[i].IsPlayer) return i;
            return 0;
        }

        private int SpeedOf(Turn t)
        {
            if (t.IsPlayer) return PlayerSpeed;
            return EnemySpeed(t.EnemyIndex); // base + synergy speed bonus
        }

        private int AliveIndexOf(int orderIndex)
        {
            int alive = 0;
            for (int i = 0; i < orderIndex && i < _order.Count; i++)
                if (IsAlive(_order[i])) alive++;
            return alive;
        }

        // Rebuilds the turn bar from the living participants only, keeping the current turn highlighted.
        private void RebuildTurnBar()
        {
            if (turnBar == null) return;

            var participants = new List<TurnOrderBar.Participant>();
            foreach (Turn t in _order)
            {
                if (!IsAlive(t)) continue;
                if (t.IsPlayer)
                {
                    participants.Add(new TurnOrderBar.Participant { isPlayer = true, speed = PlayerSpeed, sprite = PlayerSprite });
                }
                else
                {
                    EnemyData e = EnemyAt(t.EnemyIndex);
                    participants.Add(new TurnOrderBar.Participant { isPlayer = false, speed = EnemySpeed(t.EnemyIndex), sprite = e != null ? e.Sprite : null });
                }
            }
            turnBar.Build(participants);
            turnBar.SetCurrentTurn(AliveIndexOf(_current), true);

            RefreshAllStatuses(); // keep the state markers in sync (dead enemies clear theirs)
        }

        private EnemyData EnemyAt(int enemyIndex)
        {
            if (encounter == null || enemyIndex < 0) return null;
            IReadOnlyList<EncounterData.Enemy> es = encounter.Enemies;
            if (enemyIndex >= es.Count || es[enemyIndex] == null) return null;
            return es[enemyIndex].enemy;
        }

        // An enemy's speed for turn order = its base speed plus any active synergy speed bonus.
        private int EnemySpeed(int enemyIndex)
        {
            EnemyData e = EnemyAt(enemyIndex);
            if (e == null) return 0;
            EnemySlot slot = enemies != null ? enemies.Slot(enemyIndex) : null;
            return e.Speed + (slot != null ? slot.SynergySpeed : 0);
        }

        // ----- enemy synergies -----

        // Snapshot of every enemy slot (alive or dead) for the synergy system to read.
        private EnemySlot[] SynergySlots()
        {
            int n = EnemyCount;
            var slots = new EnemySlot[n];
            for (int i = 0; i < n; i++) slots[i] = enemies != null ? enemies.Slot(i) : null;
            return slots;
        }

        private void SynergyStartCombat()
        {
            if (synergy == null) return;
            synergy.StartCombat(SynergySlots());
            if (enemies != null) enemies.SetSynergy(synergy.PrimaryActive); // detail panel shows the first active one
        }

        private void SynergyEvaluate()
        {
            if (synergy == null) return;
            synergy.Evaluate(SynergySlots());
            if (enemies != null) enemies.SetSynergy(synergy.PrimaryActive);
        }

        private void SynergyRoundStart()
        {
            if (synergy == null) return;
            synergy.RoundStart(SynergySlots());
            if (enemies != null) enemies.SetSynergy(synergy.PrimaryActive);
        }

        private void SynergyEndCombat()
        {
            if (synergy != null) synergy.EndCombat();
            if (enemies != null) enemies.SetSynergy(null);
        }

        private void ActorPlayThrow()
        {
            if (_order.Count == 0) return;
            if (_order[_current].IsPlayer) { if (ResolvedPlayer != null) ResolvedPlayer.PlayThrow(); }
            else { EnemySlot slot = enemies != null ? enemies.Slot(_order[_current].EnemyIndex) : null; if (slot != null) slot.PlayThrow(); }
        }

        private void ActorPlayAttack()
        {
            if (_order.Count == 0) return;
            if (_order[_current].IsPlayer) { if (ResolvedPlayer != null) ResolvedPlayer.PlayAttack(); }
            else { EnemySlot slot = enemies != null ? enemies.Slot(_order[_current].EnemyIndex) : null; if (slot != null) slot.PlayAttack(); }
        }

        private void SetPlayerInput(bool enabled)
        {
            if (deck != null) deck.InputEnabled = enabled;
            if (effectDeck != null) effectDeck.InputEnabled = enabled;
        }

        private void Shake()
        {
            if (cameraShake != null) cameraShake.Shake();
            else CameraShake.Trigger();
        }
    }
}
