using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Undelivered.UI;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// Drives the combat turn loop and resolves each throw. Turn order is by speed (the player plus
    /// every enemy), the same order the turn bar shows.
    ///
    /// On the player's turn they tap one die; on an enemy's turn that enemy throws its own die. Either
    /// resolves in order: the die falls → (player only) the activated effects are applied → its value
    /// damages the target — the closest enemy on the player's turn, the player on an enemy's turn —
    /// shield first then health, with a camera shake → the die lingers and is removed → the turn passes.
    ///
    /// Enemy abilities/synergies and win/lose are still to come.
    /// </summary>
    public class CombatController : MonoBehaviour
    {
        public static CombatController Instance { get; private set; }

        [Header("Combat")]
        [SerializeField] private EncounterData encounter;

        [Header("Player")]
        [SerializeField] private PlayerCombatant player;
        [Tooltip("Fallback speed/sprite used if no PlayerCombatant is assigned.")]
        [SerializeField] private int playerSpeed = 5;
        [SerializeField] private Sprite playerSprite;

        [Header("References")]
        [SerializeField] private TurnOrderBar turnBar;
        [SerializeField] private CombatEnemies enemies;
        [SerializeField] private Deck deck;
        [SerializeField] private EffectDeck effectDeck;
        [SerializeField] private DiceThrower thrower;

        [Header("Timing")]
        [Tooltip("How long a thrown die stays on the table before it is removed and the turn passes.")]
        [SerializeField] private float dieLingerSeconds = 1.5f;
        [Tooltip("Pause before an enemy throws, so its turn reads clearly.")]
        [SerializeField] private float enemyThinkSeconds = 0.6f;

        private struct Turn { public bool IsPlayer; public int EnemyIndex; }

        private readonly List<Turn> _order = new List<Turn>();
        private int _current;

        // The player to damage: the assigned one, else whatever PlayerCombatant is in the scene.
        private PlayerCombatant ResolvedPlayer => player != null ? player : PlayerCombatant.Instance;
        private int PlayerSpeed => ResolvedPlayer != null ? ResolvedPlayer.Speed : playerSpeed;
        private Sprite PlayerSprite => ResolvedPlayer != null ? ResolvedPlayer.Sprite : playerSprite;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            if (thrower != null) thrower.DieLanded += OnDieLanded;
        }

        private void OnDisable()
        {
            if (thrower != null) thrower.DieLanded -= OnDieLanded;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (encounter != null) StartCombat(encounter);
        }

        /// <summary>Sets up a combat: builds the enemies and turn bar, computes turn order, starts turn 1.</summary>
        public void StartCombat(EncounterData combat)
        {
            encounter = combat;
            if (enemies != null) enemies.Build(combat);
            if (turnBar != null) turnBar.Build(combat, PlayerSpeed, PlayerSprite);

            BuildTurnOrder();
            BeginTurnAt(0);
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
                        participants.Add((false, i, es[i].enemy.Speed));
            }

            foreach (var p in participants.OrderByDescending(p => p.speed))
                _order.Add(new Turn { IsPlayer = p.isPlayer, EnemyIndex = p.enemyIndex });
        }

        private void BeginTurnAt(int index)
        {
            if (_order.Count == 0) return;
            _current = ((index % _order.Count) + _order.Count) % _order.Count;

            if (turnBar != null) turnBar.SetCurrentTurn(_current);

            bool playerTurn = _order[_current].IsPlayer;
            SetPlayerInput(playerTurn); // dice + effects tappable only on the player's turn

            if (playerTurn)
            {
                if (deck != null) deck.BeginTurn(); // refresh the deck if the player is out of dice
                // now wait for the player to throw a die (resolved in OnDieLanded)
            }
            else
            {
                StartCoroutine(EnemyTurn());
            }
        }

        private IEnumerator EnemyTurn()
        {
            yield return new WaitForSecondsRealtime(enemyThinkSeconds);

            EnemyData enemy = EnemyAt(_order[_current].EnemyIndex);
            DiceData die = enemy != null ? enemy.Die : null;

            if (die != null && thrower != null)
            {
                thrower.Throw(die); // the enemy rolls its own die; resolved in OnDieLanded
            }
            else
            {
                BeginTurnAt(_current + 1); // no die to throw: just pass
            }
        }

        private void OnDieLanded(DiceFace result)
        {
            if (_order.Count == 0) return;

            SetPlayerInput(false); // no input while a throw resolves
            if (_order[_current].IsPlayer) StartCoroutine(ResolvePlayerThrow(result));
            else StartCoroutine(ResolveEnemyThrow(result));
        }

        private IEnumerator ResolvePlayerThrow(DiceFace result)
        {
            // the die has fallen (this fires on landing); apply the activated effects
            if (effectDeck != null) effectDeck.ConsumeSelected();

            // the die's value damages the closest enemy (shield first, then health) + shake
            if (result != null && result.Value > 0 && enemies != null)
            {
                EnemySlot target = enemies.ClosestEnemy();
                if (target != null)
                {
                    target.ApplyDamage(result.Value);
                    CameraShake.Trigger();
                }
            }

            yield return new WaitForSecondsRealtime(dieLingerSeconds);
            if (thrower != null) thrower.ClearTable();
            BeginTurnAt(_current + 1);
        }

        private IEnumerator ResolveEnemyThrow(DiceFace result)
        {
            // the enemy die's value damages the player (shield first, then health) + shake
            PlayerCombatant target = ResolvedPlayer;
            if (result != null && result.Value > 0 && target != null)
            {
                target.ApplyDamage(result.Value);
                CameraShake.Trigger();
            }

            yield return new WaitForSecondsRealtime(dieLingerSeconds);
            if (thrower != null) thrower.ClearTable();
            BeginTurnAt(_current + 1);
        }

        private EnemyData EnemyAt(int enemyIndex)
        {
            if (encounter == null || enemyIndex < 0) return null;
            IReadOnlyList<EncounterData.Enemy> es = encounter.Enemies;
            if (enemyIndex >= es.Count || es[enemyIndex] == null) return null;
            return es[enemyIndex].enemy;
        }

        private void SetPlayerInput(bool enabled)
        {
            if (deck != null) deck.InputEnabled = enabled;
            if (effectDeck != null) effectDeck.InputEnabled = enabled;
        }
    }
}
