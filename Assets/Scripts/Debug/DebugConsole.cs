using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Undelivered.Game;
using Undelivered.Night;
using Undelivered.Player;
using Undelivered.Progression;
using Undelivered.Tutorial;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Undelivered.DebugTools
{
    /// <summary>
    /// A developer cheat console. The "-" key shows a text input that is hidden the rest of the time;
    /// "-" again or ESCAPE hides it. Type a command and press Enter to run it. Commands:
    /// <list type="bullet">
    /// <item>give random effect|dice|box [N] — N copies (default 1), e.g. "give random dice 3"</item>
    /// <item>give golden [N] — N random golden effects</item>
    /// <item>upgrade level — opens the level-up stat picker</item>
    /// <item>heal xN, shield xN, rich xN (gold), gems xN — add N of that resource</item>
    /// <item>maxheal — refill the player's health and shield</item>
    /// <item>jumptuto — skip the current mode's tutorial (day or night, not both)</item>
    /// <item>clean — empty the inventory and both decks</item>
    /// <item>allunlock — reveal every glossary entry; alllock — forget the whole glossary</item>
    /// <item>uulock — buy every day-mode upgrade at every level</item>
    /// <item>switch — toggle day/night mode</item>
    /// </list>
    /// The pools for "give random" / "allunlock" come from the <see cref="GlossaryPanel"/> lists and the
    /// <see cref="Night.Shop"/> box pool.
    /// </summary>
    public class DebugConsole : MonoBehaviour
    {
        [Tooltip("The console root (input field + backdrop). Hidden until SHIFT+TAB.")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_InputField input;
        [Tooltip("Optional: shows the result of the last command.")]
        [SerializeField] private TMP_Text feedback;

        /// <summary>True while the console is open — the glossary ignores TAB then.</summary>
        public static bool IsOpen { get; private set; }

        private void Awake()
        {
            if (root != null) root.SetActive(false);
            IsOpen = false;
            if (input != null) input.onSubmit.AddListener(OnSubmit);
        }

        private void OnDestroy()
        {
            if (input != null) input.onSubmit.RemoveListener(OnSubmit);
            IsOpen = false;
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null) return;

            if (k.minusKey.wasPressedThisFrame || k.numpadMinusKey.wasPressedThisFrame) Toggle();
            else if (IsOpen && k.escapeKey.wasPressedThisFrame) Close();
        }

        private void Toggle() { if (IsOpen) Close(); else Open(); }

        private void Open()
        {
            IsOpen = true;
            if (root != null) root.SetActive(true);
            if (input != null)
            {
                input.text = string.Empty;
                input.ActivateInputField();
                input.Select();
            }
        }

        private void Close()
        {
            IsOpen = false;
            if (input != null) { input.text = string.Empty; input.DeactivateInputField(); }
            if (root != null) root.SetActive(false);
        }

        // Enter in the input field.
        private void OnSubmit(string text)
        {
            if (!IsOpen) return;
            Run(text);
            if (input != null)
            {
                input.text = string.Empty;
                input.ActivateInputField(); // keep the field focused for the next command
            }
        }

        // ----- command parsing -----

        private void Run(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            string[] t = raw.Trim().ToLowerInvariant().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length == 0) return;

            switch (t[0])
            {
                case "give":
                    if (t.Length >= 3 && t[1] == "random")
                    {
                        int count = CountAt(t, 3); // optional trailing amount: "give random dice 3"
                        switch (t[2])
                        {
                            case "effect": GiveRandomEffect(count); return;
                            case "dice": GiveRandomDice(count); return;
                            case "box": GiveRandomBox(count); return;
                        }
                    }
                    else if (t.Length >= 2 && t[1] == "golden")
                    {
                        GiveGoldenEffect(CountAt(t, 2));
                        return;
                    }
                    break;

                case "upgrade":
                    if (t.Length >= 2 && t[1] == "level") { UpgradeLevel(); return; }
                    break;

                case "heal": if (Amount(t, out int h)) Heal(h); return;
                case "shield": if (Amount(t, out int s)) Shield(s); return;
                case "rich": if (Amount(t, out int g)) Rich(g); return;
                case "gems": if (Amount(t, out int gm)) Gems(gm); return;

                case "maxheal": MaxHeal(); return;
                case "jumptuto": JumpTuto(); return;
                case "clean": Clean(); return;
                case "allunlock": AllUnlock(); return;
                case "alllock": AllLock(); return;
                case "uulock": UULock(); return;
                case "switch": Switch(); return;
            }

            Report($"Comando no reconocido: {raw}");
        }

        // Reads "xN" (or plain "N") from the second token; reports usage if missing.
        private bool Amount(string[] t, out int n)
        {
            n = 0;
            if (t.Length >= 2 && TryAmount(t[1], out n)) return true;
            Report($"Uso: {t[0]} xN (ej. {t[0]} x10)");
            return false;
        }

        private static bool TryAmount(string s, out int n)
        {
            n = 0;
            if (string.IsNullOrEmpty(s)) return false;
            if (s[0] == 'x' || s[0] == 'X') s = s.Substring(1);
            return int.TryParse(s, out n);
        }

        // Optional repeat count at token 'index' (default 1, clamped to 1..99).
        private static int CountAt(string[] t, int index)
        {
            if (index < t.Length && TryAmount(t[index], out int n)) return Mathf.Clamp(n, 1, 99);
            return 1;
        }

        // ----- commands -----

        private void GiveRandomEffect(int count)
        {
            int given = 0; string last = null;
            for (int i = 0; i < count; i++)
            {
                EffectData e = RandomOf(Effects());
                if (e == null || Inventory.Instance == null) break;
                Inventory.Instance.AddEffect(e);
                given++; last = e.EffectName;
            }
            ReportGiven(given, last, "efecto", "efectos", "No hay efectos en el pool.");
        }

        private void GiveRandomDice(int count)
        {
            int given = 0; string last = null;
            for (int i = 0; i < count; i++)
            {
                DiceData d = RandomOf(Dice());
                if (d == null || Inventory.Instance == null) break;
                Inventory.Instance.AddDie(d);
                given++; last = d.name;
            }
            ReportGiven(given, last, "dado", "dados", "No hay dados en el pool.");
        }

        private void GiveRandomBox(int count)
        {
            int given = 0; string last = null;
            for (int i = 0; i < count; i++)
            {
                BoxData b = RandomOf(Boxes());
                if (b == null || Inventory.Instance == null) break;
                Inventory.Instance.AddBox(b);
                given++; last = b.name;
            }
            ReportGiven(given, last, "caja", "cajas", "No hay cajas en el pool.");
        }

        private void GiveGoldenEffect(int count)
        {
            IReadOnlyList<EffectData> pool = Effects();
            List<EffectData> golden = pool == null ? null : pool.Where(x => x != null && x.IsGolden).ToList();

            int given = 0; string last = null;
            for (int i = 0; i < count; i++)
            {
                EffectData e = RandomOf(golden);
                if (e == null || Inventory.Instance == null) break;
                Inventory.Instance.AddEffect(e);
                given++; last = e.EffectName;
            }
            ReportGiven(given, last, "efecto dorado", "efectos dorados", "No hay efectos dorados en el pool.");
        }

        // Summarises a repeated give: none, one (name), or a count with the last item.
        private void ReportGiven(int given, string last, string singular, string plural, string emptyMessage)
        {
            if (given <= 0) Report(emptyMessage);
            else if (given == 1) Report($"{char.ToUpper(singular[0])}{singular.Substring(1)} otorgado: {last}");
            else Report($"{given} {plural} otorgados (último: {last}).");
        }

        private void UpgradeLevel()
        {
            PlayerCombatant p = PlayerCombatant.Instance;
            if (p == null || LevelUpPanel.Instance == null) { Report("No hay panel de subida de nivel."); return; }
            LevelUpPanel.Instance.Show(p, () => { });
            Report("Subida de nivel abierta.");
        }

        private void Heal(int n)
        {
            PlayerCombatant p = PlayerCombatant.Instance;
            if (p == null) { Report("No hay jugador."); return; }
            p.Heal(n);
            p.ShowNumber(n, FloatingNumbers.Kind.Heal);
            Report($"Curado +{n}.");
        }

        private void Shield(int n)
        {
            PlayerCombatant p = PlayerCombatant.Instance;
            if (p == null) { Report("No hay jugador."); return; }
            p.AddShield(n);
            Report($"Escudo +{n}.");
        }

        private void Rich(int n)
        {
            if (StatsManager.Instance == null) { Report("No hay StatsManager."); return; }
            StatsManager.Instance.AddGold(n);
            Report($"Oro +{n} (total {StatsManager.Instance.Gold}).");
        }

        private void Gems(int n)
        {
            if (NightWallet.Instance == null) { Report("No hay NightWallet."); return; }
            NightWallet.Instance.AddGems(n);
            Report($"Gemas +{n} (total {NightWallet.Instance.Gems}).");
        }

        private void MaxHeal()
        {
            PlayerCombatant p = PlayerCombatant.Instance;
            if (p == null) { Report("No hay jugador."); return; }
            p.SetHealth(p.MaxHealth);
            p.SetShield(p.BaseShield);
            Report("Vida y escudo al máximo.");
        }

        private void Clean()
        {
            if (Inventory.Instance != null) Inventory.Instance.Clear();
            if (Deck.Instance != null) Deck.Instance.SetDice(Array.Empty<DiceData>());
            if (EffectDeck.Instance != null) EffectDeck.Instance.SetEffects(Array.Empty<EffectData>());
            Report("Inventario y decks vaciados.");
        }

        private void AllUnlock()
        {
            GlossaryPanel g = GlossaryPanel.Instance;
            if (Knowledge.Instance == null || g == null) { Report("No hay glosario/conocimiento."); return; }

            foreach (DiceData d in g.AllDice) Knowledge.Instance.LearnDie(d);
            foreach (EffectData e in g.AllEffects) Knowledge.Instance.LearnEffect(e);
            foreach (EnemyData en in g.AllEnemies) Knowledge.Instance.LearnEnemy(en);
            g.RefreshIfOpen();
            Report("Glosario completamente desbloqueado.");
        }

        private void AllLock()
        {
            if (Knowledge.Instance == null) { Report("No hay conocimiento."); return; }
            Knowledge.Instance.Clear();
            if (GlossaryPanel.Instance != null) GlossaryPanel.Instance.RefreshIfOpen();
            Report("Glosario olvidado por completo.");
        }

        private void UULock()
        {
            if (ProgressionManager.Instance == null) { Report("No hay ProgressionManager."); return; }
            int count = ProgressionManager.Instance.DebugUnlockAllUpgrades();
            Report($"{count} mejoras compradas al máximo.");
        }

        private void Switch()
        {
            ModeSwitcher switcher = UnityEngine.Object.FindAnyObjectByType<ModeSwitcher>();
            if (switcher == null) { Report("No hay ModeSwitcher."); return; }
            switcher.SwitchMode();
            Report("Modo cambiado.");
        }

        // Skips only the current mode's tutorial (day vs night, per the mode switcher).
        private void JumpTuto()
        {
            ModeSwitcher switcher = UnityEngine.Object.FindAnyObjectByType<ModeSwitcher>();
            bool night = switcher != null && switcher.IsNight;
            if (night)
            {
                NightTutorial nt = UnityEngine.Object.FindAnyObjectByType<NightTutorial>();
                if (nt != null) { nt.Skip(); Report("Tutorial de noche saltado."); }
                else Report("No hay tutorial de noche en escena.");
            }
            else
            {
                WorkTutorial wt = UnityEngine.Object.FindAnyObjectByType<WorkTutorial>();
                if (wt != null) { wt.Skip(); Report("Tutorial de dia saltado."); }
                else Report("No hay tutorial de dia en escena.");
            }
        }

        // ----- helpers -----

        private static IReadOnlyList<EffectData> Effects() => GlossaryPanel.Instance != null ? GlossaryPanel.Instance.AllEffects : null;
        private static IReadOnlyList<DiceData> Dice() => GlossaryPanel.Instance != null ? GlossaryPanel.Instance.AllDice : null;
        private static IReadOnlyList<BoxData> Boxes() => Night.Shop.Instance != null ? Night.Shop.Instance.BoxPool : null;

        private static T RandomOf<T>(IReadOnlyList<T> list) where T : class
            => list != null && list.Count > 0 ? list[UnityEngine.Random.Range(0, list.Count)] : null;

        private void Report(string message)
        {
            Debug.Log($"[Consola] {message}");
            if (feedback != null) feedback.text = message;
        }
    }
}
