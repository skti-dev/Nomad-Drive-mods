using BepInEx.Configuration;
using NomadDrive.Features.Consumables;
using UnityEngine;

namespace CozinhaExtra
{
    // Uma receita da panela: o que entra, como chama, de que cor é e o que rende.
    internal sealed class Recipe
    {
        public SoupType Type;
        // Nome em frases ("sopa de cogumelo" / "mushroom soup"), minúsculo.
        public string NamePt, NameEn;
        // Ingrediente fresco ("cogumelo"); null se a receita só vem de lata.
        public string IngredientPt, IngredientEn;
        // true = sopa ("Tomar"); false = comida ("Comer").
        public bool Drink;
        public Color Color;
        // Itens do mod. MugAssetId 0 = não vai em caneca. A chave vira "CozinhaExtra:<Key>" no save.
        public uint PlateAssetId, MugAssetId;
        public string PlateKey, MugKey;
        public string PlatePt, PlateEn, MugPt, MugEn;
        public ConfigEntry<float> Nutrition, Hydration, Energy;

        public bool HasMug => MugAssetId != 0;
    }

    internal static class Recipes
    {
        // Ordem fixa. Os valores de SoupType e os assetIds vão para a rede, para o save e para o
        // arquivo de panelas: nunca reaproveitar nem reordenar.
        // Reservado para a fase 2 (geleia, 1.5.1): SoupType 6 e assetIds 0xCE5A000B-0F.
        public static Recipe[] All = new Recipe[0];

        // Ids (Game.ItemId) dos ingredientes frescos: entram também pelo modo de colocação.
        public static readonly string[] FreshIds = { "Tomato", "Potato", "Mushroom" };

        public static void Bind(ConfigFile c)
        {
            All = new[]
            {
                new Recipe
                {
                    Type = SoupType.Tomato, NamePt = "sopa de tomate", NameEn = "tomato soup",
                    IngredientPt = "tomate", IngredientEn = "tomato", Drink = true,
                    Color = new Color(0.72f, 0.14f, 0.07f),
                    PlateAssetId = 0xCE5A0002, MugAssetId = 0xCE5A0004,
                    PlateKey = "TomatoSoupPlate", MugKey = "TomatoSoupMug",
                    PlateEn = "Plate of tomato soup", PlatePt = "Prato de sopa de tomate",
                    MugEn = "Mug of tomato soup", MugPt = "Caneca de sopa de tomate",
                    Nutrition = c.Bind("SopaTomate", "Nutricao", 15f, "Fome recuperada por porção (a lata de sopa do jogo dá 20). A lata de sopa de tomate despejada na panela vira esta sopa."),
                    Hydration = c.Bind("SopaTomate", "Hidratacao", 15f, "Sede recuperada por porção.")
                },
                new Recipe
                {
                    Type = SoupType.Potato, NamePt = "sopa de batata", NameEn = "potato soup",
                    IngredientPt = "batata", IngredientEn = "potato", Drink = true,
                    Color = new Color(0.86f, 0.76f, 0.52f),
                    PlateAssetId = 0xCE5A0003, MugAssetId = 0xCE5A0005,
                    PlateKey = "PotatoSoupPlate", MugKey = "PotatoSoupMug",
                    PlateEn = "Plate of potato soup", PlatePt = "Prato de sopa de batata",
                    MugEn = "Mug of potato soup", MugPt = "Caneca de sopa de batata",
                    Nutrition = c.Bind("SopaBatata", "Nutricao", 18f, "Fome recuperada por porção."),
                    Hydration = c.Bind("SopaBatata", "Hidratacao", 12f, "Sede recuperada por porção.")
                },
                new Recipe
                {
                    Type = SoupType.Mushroom, NamePt = "sopa de cogumelo", NameEn = "mushroom soup",
                    IngredientPt = "cogumelo", IngredientEn = "mushroom", Drink = true,
                    Color = new Color(0.55f, 0.42f, 0.28f),
                    PlateAssetId = 0xCE5A0006, MugAssetId = 0xCE5A0007,
                    PlateKey = "MushroomSoupPlate", MugKey = "MushroomSoupMug",
                    PlateEn = "Plate of mushroom soup", PlatePt = "Prato de sopa de cogumelo",
                    MugEn = "Mug of mushroom soup", MugPt = "Caneca de sopa de cogumelo",
                    Nutrition = c.Bind("SopaCogumelo", "Nutricao", 16f, "Fome recuperada por porção (o cogumelo cru dá 20, mas envenena; a sopa não envenena)."),
                    Hydration = c.Bind("SopaCogumelo", "Hidratacao", 12f, "Sede recuperada por porção.")
                },
                new Recipe
                {
                    Type = SoupType.Beans, NamePt = "feijão quente", NameEn = "hot beans",
                    Drink = false,
                    Color = new Color(0.55f, 0.24f, 0.11f),
                    PlateAssetId = 0xCE5A0008, MugAssetId = 0xCE5A0009,
                    PlateKey = "HotBeansPlate", MugKey = "HotBeansMug",
                    PlateEn = "Plate of hot beans", PlatePt = "Prato de feijão quente",
                    MugEn = "Mug of hot beans", MugPt = "Caneca de feijão quente",
                    Nutrition = c.Bind("FeijaoQuente", "Nutricao", 16f, "Fome recuperada por porção (a lata de feijão fria dá 25; cada lata rende PorcoesPorLata porções)."),
                    Hydration = c.Bind("FeijaoQuente", "Hidratacao", 0f, "Sede recuperada por porção."),
                    Energy = c.Bind("FeijaoQuente", "Energia", 5f, "Energia recuperada por porção (máximo do jogo: 100).")
                },
                new Recipe
                {
                    Type = SoupType.Tuna, NamePt = "atum quente", NameEn = "hot tuna",
                    Drink = false,
                    Color = new Color(0.80f, 0.70f, 0.56f),
                    PlateAssetId = 0xCE5A000A, MugAssetId = 0,
                    PlateKey = "HotTunaPlate",
                    PlateEn = "Plate of hot tuna", PlatePt = "Prato de atum quente",
                    Nutrition = c.Bind("AtumQuente", "Nutricao", 16f, "Fome recuperada por porção (a lata de atum fria dá 25; cada lata rende PorcoesPorLata porções). Só vai em prato."),
                    Hydration = c.Bind("AtumQuente", "Hidratacao", 0f, "Sede recuperada por porção."),
                    Energy = c.Bind("AtumQuente", "Energia", 5f, "Energia recuperada por porção (máximo do jogo: 100).")
                }
            };
        }

        public static Recipe Get(SoupType soup)
        {
            foreach (Recipe r in All)
            {
                if (r.Type == soup)
                {
                    return r;
                }
            }
            return null;
        }

        // Item do jogo que vira receita: fresco (pede água) ou lata (despejada, sem água).
        public static bool TryIngredient(GameObject go, out SoupType soup, out bool canned)
        {
            soup = SoupType.None;
            canned = false;
            if (go == null || go.GetComponent<Food>() == null)
            {
                return false;
            }
            return TryIngredientId(Game.ItemId(go), out soup, out canned);
        }

        public static bool TryIngredientId(string id, out SoupType soup, out bool canned)
        {
            soup = SoupType.None;
            canned = false;
            switch (id)
            {
                case "Tomato":
                    soup = SoupType.Tomato;
                    break;
                case "Potato":
                    soup = SoupType.Potato;
                    break;
                case "Mushroom":
                    soup = SoupType.Mushroom;
                    break;
                case "TomatoSoup":
                    soup = SoupType.Tomato;
                    canned = true;
                    break;
                case "BakedBeans":
                    soup = SoupType.Beans;
                    canned = true;
                    break;
                case "TunaCan":
                    soup = SoupType.Tuna;
                    canned = true;
                    break;
                default:
                    return false;
            }
            return true;
        }

        public static bool IsFresh(GameObject go) => TryIngredient(go, out _, out bool canned) && !canned;
    }
}
