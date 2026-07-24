using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// A consumable effect. Has a name, tooltip, price, rarity and an image. The rarity is shown as the
    /// colour of the item's border (a background image) wherever the effect is listed. What each effect
    /// does is resolved later. Implements <see cref="IItem"/> so the shop can list it.
    /// </summary>
    [CreateAssetMenu(fileName = "Effect", menuName = "Undelivered/Night/Effect")]
    public class EffectData : ScriptableObject, IItem
    {
        /// <summary>What the effect does. Types 1-13 are built; the rest come later.</summary>
        public enum EffectType { None, RollModifier, StatConverter, NumberTransform, GrantTurn, NumberBlock, TurnManip, Renewal, MiniDice, DegradeEnemyDie, ShieldManip, AddState, Counterattack, AddLuck }

        /// <summary>Degrade-enemy-die scope: only the closest enemy, or all of them.</summary>
        public enum EnemyScope { Closest, All }

        /// <summary>Shield manipulation: deal the roll straight to HP (skip the shield), or destroy all shield with no damage.</summary>
        public enum ShieldKind { SkipShield, BreakShield }

        /// <summary>Counterattack: reflect the next enemy hit back to that attacker, or deal it to every enemy.</summary>
        public enum CounterKind { ToAttacker, ToAll }

        /// <summary>Turn manipulation: move the player first, send the fastest last, or +speed for the combat.</summary>
        public enum TurnManipKind { GoFirst, FastestLast, SpeedBoost }

        /// <summary>Renewal: refresh the dice deck, the effect deck, the die just used, or the last effect used.</summary>
        public enum RenewalKind { DiceDeck, EffectDeck, UsedDie, LastEffect }

        /// <summary>How a roll-modifier effect changes the value.</summary>
        public enum RollOperation { Add, Multiply }

        /// <summary>Where a StatConverter sends the roll value. Default is enemy damage.</summary>
        public enum TargetStat { Damage, Heal, Shield, Speed, Luck }

        /// <summary>How a NumberTransform / NumberBlock compares the roll (Even/Odd ignore the threshold).</summary>
        public enum Comparison { Less, LessOrEqual, Equal, GreaterOrEqual, Greater, Even, Odd }

        /// <summary>
        /// What a GrantTurn effect gives: another full turn; a second (random) die dropped next to the
        /// first; or a second die that copies the first's number. The two "die" variants sum both results.
        /// </summary>
        public enum GrantKind { ExtraTurn, SecondDie, CopyDie }

        [SerializeField] private string effectName;
        [SerializeField, TextArea] private string descriptionForTooltip;
        [SerializeField] private Sprite icon;
        [Tooltip("Price in gems.")]
        [SerializeField] private int price = 10;
        [Tooltip("A golden effect: does what another one does but at a much bigger scale (Quema → Quema Total). " +
                 "They are scarce and much more expensive, and show a golden border.")]
        [SerializeField] private bool golden;

        [Header("Behaviour")]
        [SerializeField] private EffectType type = EffectType.None;
        [Tooltip("Roll modifier: add the amount to the roll, or multiply the roll by it.")]
        [SerializeField] private RollOperation rollOperation = RollOperation.Multiply;
        [SerializeField] private int amount = 2;
        [Tooltip("StatConverter: the roll value applies to this stat instead of dealing enemy damage.")]
        [SerializeField] private TargetStat stat = TargetStat.Damage;

        [Header("NumberTransform (your roll) / NumberBlock (the opponent's roll)")]
        [Tooltip("If the roll compares this way against the threshold, it becomes 'transformTo'.")]
        [SerializeField] private Comparison comparison = Comparison.Less;
        [SerializeField] private int threshold = 3;
        [SerializeField] private int transformTo = 9;

        [Header("GrantTurn")]
        [Tooltip("ExtraTurn: play again. SecondDie: a second random die next to the first. CopyDie: a second die on the first's number.")]
        [SerializeField] private GrantKind grant = GrantKind.ExtraTurn;

        [Header("TurnManip")]
        [Tooltip("GoFirst: move yourself first. FastestLast: send the fastest last. SpeedBoost: +amount speed for the combat.")]
        [SerializeField] private TurnManipKind turnManip = TurnManipKind.GoFirst;

        [Header("Renewal")]
        [Tooltip("DiceDeck / EffectDeck: refresh all. UsedDie: the die just thrown. LastEffect: the last effect used (not this one).")]
        [SerializeField] private RenewalKind renewal = RenewalKind.DiceDeck;

        [Header("MiniDice")]
        [Tooltip("Create-dice (type 8): after your throw, spawn N of this die (N = your roll) and throw them for damage; your roll itself deals none. The mini die should only roll 1/2/3.")]
        [SerializeField] private DiceData miniDie;

        [Header("DegradeEnemyDie")]
        [Tooltip("Degrade-enemy-die (type 9): for one turn the target enemy's die becomes this (basic) die.")]
        [SerializeField] private DiceData degradeDie;
        [Tooltip("Whether the degrade hits only the closest enemy or every enemy.")]
        [SerializeField] private EnemyScope enemyScope = EnemyScope.Closest;

        [Header("ShieldManip")]
        [Tooltip("SkipShield: your roll damages HP directly. BreakShield: no damage, but destroy the closest enemy's shield.")]
        [SerializeField] private ShieldKind shieldKind = ShieldKind.SkipShield;

        [Header("AddState")]
        [Tooltip("Add-state (type 11): on attack, apply the rolled number of marks of this state. Uses 'enemyScope' for closest vs all.")]
        [SerializeField] private StatusType statusType = StatusType.Burn;

        [Header("Counterattack")]
        [Tooltip("ToAttacker: your roll deals no damage; the next hit you take is returned to its attacker. ToAll: same, but the next hit is dealt to ALL enemies and this die can't be renewed this combat.")]
        [SerializeField] private CounterKind counterKind = CounterKind.ToAttacker;

        [Header("AddLuck")]
        [Tooltip("Add-luck (type 13): adds (roll x this) percent luck to the die you threw.")]
        [SerializeField] private int luckRollMultiplier = 1;
        [Tooltip("How many of the player's turns the luck bonus lasts. 0 = the whole combat.")]
        [SerializeField] private int luckTurns = 1;

        public string EffectName => effectName;

        /// <summary>True for a golden effect — a bigger-scale version of another one.</summary>
        public bool IsGolden => golden;

        /// <summary>Extra tooltip line: marks the golden ones, blank otherwise.</summary>
        public string GoldenText => golden ? "Efecto dorado" : string.Empty;

        public Sprite Icon => icon;
        public int Price => price;
        public string DescriptionForTooltip => descriptionForTooltip;
        public EffectType Type => type;
        public TargetStat Stat => stat;
        public GrantKind Grant => grant;
        public TurnManipKind Manip => turnManip;
        public RenewalKind Renewal => renewal;
        public int Amount => amount;
        public DiceData MiniDie => miniDie;
        public DiceData DegradeDie => degradeDie;
        public EnemyScope Scope => enemyScope;
        public ShieldKind Shield => shieldKind;
        public StatusType State => statusType;
        public CounterKind Counter => counterKind;
        public int LuckRollMultiplier => luckRollMultiplier;
        public int LuckTurns => luckTurns;

        // IItem
        public string ItemName => effectName;

        /// <summary>Applies this effect to a roll value: type 1 (add/multiply) or type 4 (conditional transform). Others pass through.</summary>
        public int ModifyRoll(int value)
        {
            switch (type)
            {
                case EffectType.RollModifier:
                    return rollOperation == RollOperation.Multiply ? value * amount : value + amount;
                case EffectType.NumberTransform:
                    return Transform(value);
                default:
                    return value;
            }
        }

        /// <summary>The comparison→value rule (NumberTransform on your roll, NumberBlock on the opponent's).</summary>
        public int Transform(int value) => Meets(value) ? transformTo : value;

        private bool Meets(int value)
        {
            switch (comparison)
            {
                case Comparison.Less: return value < threshold;
                case Comparison.LessOrEqual: return value <= threshold;
                case Comparison.Equal: return value == threshold;
                case Comparison.GreaterOrEqual: return value >= threshold;
                case Comparison.Greater: return value > threshold;
                case Comparison.Even: return value % 2 == 0;
                case Comparison.Odd: return value % 2 != 0;
                default: return false;
            }
        }
    }
}
