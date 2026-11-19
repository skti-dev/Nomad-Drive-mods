using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CozinhaExtra
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "seabra.nomaddrive.cozinhaextra";
        public const string Name = "Cozinha Extra";
        public const string Version = "1.4.3";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            Cfg.Bind(Config);
            Net.RegisterSerializers();
            Patches.Apply(new Harmony(Guid));

            // Objeto próprio, fora do gerenciador do BepInEx, para o Update rodar a partida toda.
            GameObject host = new GameObject("CozinhaExtra");
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<Runner>();

            Log.LogInfo($"{Name} {Version} carregado. Tecla: {Cfg.ActionKey}");
        }
    }

    // Liga as partes do mod ao ciclo do Unity.
    internal sealed class Runner : MonoBehaviour
    {
        public static Runner Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            Items.Tick();
            MugSkins.Tick();
            Server.Tick();
            PotPatcher.Tick();
            Client.Tick();
            TestKit.Tick(this);
        }

        private void OnGUI()
        {
            Client.DrawGui();
        }

        private void OnDestroy()
        {
            Client.Shutdown();
        }
    }

    // BepInEx\config\seabra.nomaddrive.cozinhaextra.cfg
    internal static class Cfg
    {
        public static Key ActionKey = Key.K;
        public static Key TestKitKey = Key.F8;
        public static ConfigEntry<bool> TestKitEnabled;

        public static ConfigEntry<float> WaterPerPour;
        public static ConfigEntry<float> PotCapacity;
        public static ConfigEntry<float> WaterPerIngredient;
        public static ConfigEntry<int> MaxIngredients;
        public static ConfigEntry<float> CookSeconds;
        public static ConfigEntry<int> PortionsPerIngredient;
        public static ConfigEntry<float> EvaporationSeconds;

        public static ConfigEntry<float> TomatoNutrition;
        public static ConfigEntry<float> TomatoHydration;
        public static ConfigEntry<float> PotatoNutrition;
        public static ConfigEntry<float> PotatoHydration;

        public static ConfigEntry<bool> PlatesFromStove;
        public static ConfigEntry<bool> PlateBackAfterEating;
        public static ConfigEntry<bool> PlatesInWorld;
        public static ConfigEntry<float> PlateChanceKitchen;
        public static ConfigEntry<float> PlateChanceFood;

        public static ConfigEntry<float> LiquidMinY;
        public static ConfigEntry<float> LiquidMaxY;

        public static void Bind(ConfigFile c)
        {
            ConfigEntry<string> key = c.Bind("Geral", "TeclaAcao", "K",
                "Tecla das ações do mod (pôr água, colocar ingrediente, comer, servir no prato/caneca, pegar prato). " +
                "Nomes do Unity Input System: K, L, G, F5, Numpad1... / Action key.");
            if (Enum.TryParse(key.Value, true, out Key parsed) && parsed != Key.None)
            {
                ActionKey = parsed;
            }

            WaterPerPour = c.Bind("Panela", "AguaPorVez", 0.5f, "Água que cada aperto da tecla coloca na panela (litros do jogo). Vale o do host.");
            PotCapacity = c.Bind("Panela", "CapacidadeAgua", 1f, "Água máxima na panela. Vale o do host.");
            WaterPerIngredient = c.Bind("Panela", "AguaPorIngrediente", 0.5f, "Água que cada tomate/batata precisa para virar sopa. Vale o do host.");
            MaxIngredients = c.Bind("Panela", "MaxIngredientes", 2, "Quantos tomates/batatas cabem na panela de uma vez. Vale o do host.");
            CookSeconds = c.Bind("Panela", "TempoCozimentoSegundos", 60f, "Segundos no fogo aceso até a sopa ficar pronta. Vale o do host.");
            PortionsPerIngredient = c.Bind("Panela", "PorcoesPorIngrediente", 2, "Porções de sopa que cada tomate/batata rende. Vale o do host.");
            EvaporationSeconds = c.Bind("Panela", "EvaporacaoSegundos", 60f, "Sopa pronta que fica no fogo aceso perde 1 porção a cada tantos segundos (0 = não evapora). Vale o do host.");

            TomatoNutrition = c.Bind("SopaTomate", "Nutricao", 15f, "Fome recuperada por porção (a lata de sopa do jogo dá 20).");
            TomatoHydration = c.Bind("SopaTomate", "Hidratacao", 15f, "Sede recuperada por porção.");
            PotatoNutrition = c.Bind("SopaBatata", "Nutricao", 18f, "Fome recuperada por porção.");
            PotatoHydration = c.Bind("SopaBatata", "Hidratacao", 12f, "Sede recuperada por porção.");

            PlatesFromStove = c.Bind("Prato", "PegarPratoNoFogao", true, "Mirar num fogão com a mão vazia e apertar a tecla dá um prato vazio. Vale o do host.");
            PlatesInWorld = c.Bind("Prato", "PratosNoMapa", true, "Pratos aparecem pelo mapa junto do loot de cozinha e de comida. Vale o do host.");
            PlateChanceKitchen = c.Bind("Prato", "ChancePertoDeCozinha", 0.35f, "Chance (0 a 1) de um prato aparecer do lado de cada loot de cozinha (panela, caneca, frigideira...).");
            PlateChanceFood = c.Bind("Prato", "ChancePertoDeComida", 0.05f, "Chance (0 a 1) de um prato aparecer do lado de cada loot de comida.");
            PlateBackAfterEating = c.Bind("Prato", "PratoVoltaDepoisDeComer", true, "Depois de comer um prato de sopa, o prato vazio volta para a mão. Vale o do host.");

            TestKitEnabled = c.Bind("Teste", "KitDeTeste", false,
                "Liga o kit de teste: quem hospeda aperta a tecla abaixo e aparecem um fogão, uma panela, " +
                "2 tomates, 2 batatas, 2 garrafas de água cheias, uma caneca, um prato e uma mesa na frente (e até 2 canecas de outros mods, se instalados). Para testar o mod; deixe desligado no jogo normal.");
            ConfigEntry<string> kitKey = c.Bind("Teste", "TeclaKitDeTeste", "F8", "Tecla do kit de teste (só para quem hospeda).");
            if (Enum.TryParse(kitKey.Value, true, out Key parsedKit) && parsedKit != Key.None)
            {
                TestKitKey = parsedKit;
            }

            LiquidMinY = c.Bind("Visual", "AlturaLiquidoVazia", -0.03f, "Altura do líquido na panela quase vazia (ajuste fino do visual).");
            LiquidMaxY = c.Bind("Visual", "AlturaLiquidoCheia", 0.028f, "Altura do líquido na panela cheia (ajuste fino do visual).");
        }

        public static float Nutrition(SoupType soup) => soup == SoupType.Potato ? PotatoNutrition.Value : TomatoNutrition.Value;

        public static float Hydration(SoupType soup) => soup == SoupType.Potato ? PotatoHydration.Value : TomatoHydration.Value;

        // Volume que uma porção pronta ocupa na panela (para o nível não pular quando a sopa fica pronta).
        public static float PortionVolume => WaterPerIngredient.Value / Mathf.Max(1, PortionsPerIngredient.Value);
    }

    // Português (do Brasil) se o idioma escolhido nas opções do jogo for português; inglês nos
    // demais. Segue a troca de idioma no meio da partida.
    internal static class L
    {
        private static bool _pt;
        private static float _nextCheck = -1f;

        public static bool Pt
        {
            get
            {
                if (Time.unscaledTime >= _nextCheck)
                {
                    _nextCheck = Time.unscaledTime + 1f;
                    _pt = DetectPortuguese();
                }
                return _pt;
            }
        }

        private static bool DetectPortuguese()
        {
            try
            {
                string code = EvilCore.Localization.LocalizationService.Instance?.CurrentLocaleCode;
                if (!string.IsNullOrEmpty(code))
                {
                    return code.StartsWith("pt", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception)
            {
                // Sem o serviço de tradução do jogo: usa o idioma do Windows.
            }
            return Application.systemLanguage == SystemLanguage.Portuguese;
        }

        public static string T(string pt, string en) => Pt ? pt : en;

        public static string Soup(SoupType soup) => soup == SoupType.Potato ? T("sopa de batata", "potato soup") : T("sopa de tomate", "tomato soup");

        public static string Ingredient(SoupType soup) => soup == SoupType.Potato ? T("batata", "potato") : T("tomate", "tomato");
    }
}
