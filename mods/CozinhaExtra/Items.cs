using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using EvilCore;
using Mirror;
using NomadDrive.Features.Consumables;
using NomadDrive.Features.Interaction;
using NomadDrive.Features.Interaction.IK;
using NomadDrive.Features.ObjectPlacement;
using NomadDrive.Features.Player;
using NomadDrive.Features.WorldGeneration.ObjectSpawning;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace CozinhaExtra
{
    // Marca os itens do mod. Kind 0 = prato vazio; 1..9 = prato da receita (SoupType); 11..19 = caneca da receita.
    public sealed class CePlate : MonoBehaviour
    {
        public const byte MugOffset = 10;

        public byte Kind;

        // Caneca de sopa feita numa caneca de outro mod: de qual caneca veio (0 = caneca branca do jogo).
        public uint SourceAssetId;
        // GUID de save da caneca de origem (host; para devolvê-la e gravar no save).
        public string SourceGuid;

        public bool IsMug => Kind >= MugOffset;

        public bool IsEmptyPlate => Kind == 0;

        public SoupType Soup => (SoupType)(Kind % MugOffset);

        // Pratos/canecas do mod que existem na cena agora (os moldes nunca ficam ativos).
        public static readonly HashSet<CePlate> Live = new HashSet<CePlate>();

        private void Awake()
        {
            // Só existe na cena se o host tiver o mod: serve de sinal para o cliente se apresentar.
            Client.OnModdedObjectSeen();
        }

        // Aqui o netId já existe: reaplica a cara da caneca de origem (também quando o jogo recria o item).
        private void Start()
        {
            MugSkins.OnPlateStarted(this);
        }

        private void OnEnable()
        {
            Live.Add(this);
        }

        private void OnDisable()
        {
            Live.Remove(this);
        }
    }

    // Os itens novos (prato vazio, pratos e canecas de sopa) são clones de prefabs do jogo com
    // outra malha, outro nome e outro assetId do Mirror. Todos os PCs com o mod montam os
    // mesmos clones e registram os mesmos assetIds, então o host pode criá-los na rede normalmente.
    internal static partial class Items
    {
        // Ids fixos: precisam ser iguais em todos os PCs e não colidir com os do jogo.
        // Os pratos/canecas de cada receita ficam na tabela (Recipes): 0xCE5A0002-0A em uso,
        // 0xCE5A000B-0F reservados para a geleia (fase 2).
        public const uint EmptyPlateAssetId = 0xCE5A0001;

        public const string MugKey = "Mug";
        // Quadro do jogo: item simples, chato e deitado, com superfície de encaixe em cima (empilhar pratos).
        private const string PlateBaseKey = "PictureFrame";
        private const string SoupBaseKey = "TomatoSoup";    // Food: comer, fome e som já funcionam

        public const float MugHeight = 0.114f;
        private const float MugRadius = 0.058f;
        private const float MugFill = 0.85f;

        private static readonly FieldInfo AssetIdField =
            typeof(NetworkIdentity).GetField("_assetId", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo FootprintField =
            typeof(LootItemDefinition).GetField("footprint", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo CategoryField =
            typeof(LootItemDefinition).GetField("_category", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo PictureMaterialsField =
            typeof(NomadDrive.Features.Furnitures.Picture).GetField("_pictureMaterials", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo GripSelectionField =
            typeof(HeldItemIKSetup).GetField("gripHandSelection", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo SurfaceProfileField =
            typeof(SnappingPlane).GetField("surfaceProfile", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Vector3 PlateFootprint = new Vector3(0.226f, 0.025f, 0.226f);
        private static readonly Vector3 MugFootprint = new Vector3(0.116f, 0.114f, 0.116f);

        // Id que nenhum item usa: superfície que não aceita nada (não empilhar coisas em cima da sopa).
        private const string NoStackId = "CE_NoStack";

        private static readonly FieldInfo LootNameField =
            typeof(LootItemDefinition).GetField("_displayName", BindingFlags.Instance | BindingFlags.NonPublic);

        // Nome em inglês (o que fica gravado no item) -> nome em português.
        private static readonly Dictionary<string, string> ModNames = new Dictionary<string, string>();

        // Chave gravada no save ("CozinhaExtra:Plate"...) <-> molde.
        private static readonly Dictionary<string, GameObject> TemplatesByKey = new Dictionary<string, GameObject>();
        private static readonly Dictionary<GameObject, string> KeysByTemplate = new Dictionary<GameObject, string>();

        public static string SaveKey(GameObject template) =>
            template != null && KeysByTemplate.TryGetValue(template, out string key) ? key : null;

        public static bool TryGetTemplate(string key, out GameObject template)
        {
            template = null;
            if (key == null)
            {
                return false;
            }
            ParseSaveKey(key, out string baseKey, out _, out _);
            return TemplatesByKey.TryGetValue(baseKey, out template) && template != null;
        }

        // Caneca de sopa feita numa caneca de outro mod: "CozinhaExtra:TomatoSoupMug|<assetId hex>|<guid>".
        // Sem o mod, o jogo não acha o prefab e pula o registro, como com as outras chaves do mod.
        public static string SaveKeyWithSource(GameObject template, uint sourceAssetId, string sourceGuid)
        {
            string key = SaveKey(template);
            if (key == null || sourceAssetId == 0)
            {
                return key;
            }
            return key + "|" + sourceAssetId.ToString("X8") + "|" + (sourceGuid ?? string.Empty);
        }

        public static void ParseSaveKey(string key, out string baseKey, out uint sourceAssetId, out string sourceGuid)
        {
            baseKey = key;
            sourceAssetId = 0;
            sourceGuid = null;
            if (key == null)
            {
                return;
            }
            string[] parts = key.Split(new[] { '|' }, 3);
            baseKey = parts[0];
            if (parts.Length >= 2 && uint.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint id))
            {
                sourceAssetId = id;
            }
            if (parts.Length >= 3 && parts[2].Length > 0)
            {
                sourceGuid = parts[2];
            }
        }

        private static GameObject _holder;
        private static GameObject _plateBase;
        private static GameObject _soupBase;
        // Prefab do jogo de cada ingrediente fresco (só para o visual dentro da panela).
        private static readonly Dictionary<SoupType, GameObject> IngredientBases = new Dictionary<SoupType, GameObject>();
        private static readonly Dictionary<SoupType, float> IngredientRadii = new Dictionary<SoupType, float>();
        private static readonly Dictionary<SoupType, GameObject> Plates = new Dictionary<SoupType, GameObject>();
        private static readonly Dictionary<SoupType, GameObject> Mugs = new Dictionary<SoupType, GameObject>();
        private static int _pendingLoads;
        private static bool _loadStarted;
        private static bool _failed;

        public static GameObject EmptyPlate { get; private set; }

        // Caneca do jogo (a que volta para a mão depois de tomar a sopa).
        public static GameObject MugPrefab { get; private set; }

        public static bool Ready { get; private set; }

        // Dá para servir em caneca (alguma receita tem caneca montada). Receita sem caneca: FilledMug == null.
        public static bool MugsReady => Ready && MugPrefab != null && Mugs.Count > 0;

        // Material de referência (HDRP Lit) para o líquido da panela.
        public static Material LitTemplate { get; private set; }

        private static readonly Color FallbackColor = new Color(0.72f, 0.14f, 0.07f);
        public static readonly Color WaterColor = new Color(0.42f, 0.55f, 0.62f);

        public static Color SoupColor(SoupType soup) => Recipes.Get(soup)?.Color ?? FallbackColor;

        // Usado pelo gancho do tradutor do jogo: devolve o nome no idioma escolhido no jogo.
        public static bool TryTranslate(string english, out string text)
        {
            text = null;
            if (english == null || !ModNames.TryGetValue(english, out string pt))
            {
                return false;
            }
            text = L.Pt ? pt : english;
            return true;
        }

        // Malha visível de um ingrediente do jogo: o filho "Model" (tomate, batata) ou, se não houver
        // (o cogumelo usa "Mushroom-01"), o primeiro MeshFilter com MeshRenderer.
        private static bool TryIngredientMesh(SoupType soup, out Transform model, out MeshFilter mf, out MeshRenderer mr)
        {
            model = null;
            mf = null;
            mr = null;
            if (!IngredientBases.TryGetValue(soup, out GameObject prefab) || prefab == null)
            {
                return false;
            }
            model = prefab.transform.Find("Model");
            if (model == null)
            {
                foreach (MeshFilter candidate in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (candidate.sharedMesh != null && candidate.GetComponent<MeshRenderer>() != null)
                    {
                        model = candidate.transform;
                        break;
                    }
                }
            }
            mf = model != null ? model.GetComponent<MeshFilter>() : null;
            mr = model != null ? model.GetComponent<MeshRenderer>() : null;
            return mf != null && mr != null && mf.sharedMesh != null;
        }

        // Cópia só visual (sem rede, sem física) do ingrediente do jogo, para mostrar dentro da panela.
        public static GameObject MakeIngredientVisual(SoupType soup, Transform parent)
        {
            if (!TryIngredientMesh(soup, out Transform model, out MeshFilter mf, out MeshRenderer mr))
            {
                return null;
            }
            GameObject root = new GameObject("CozinhaExtra_Ingredient");
            root.transform.SetParent(parent, false);
            root.layer = parent.gameObject.layer;
            GameObject vis = new GameObject("Mesh");
            vis.transform.SetParent(root.transform, false);
            vis.layer = root.layer;
            vis.transform.localRotation = model.localRotation;
            vis.transform.localScale = model.localScale;
            // Centraliza a malha no objeto raiz (o pivô do modelo pode não estar no meio).
            vis.transform.localPosition = -(model.localRotation * Vector3.Scale(mf.sharedMesh.bounds.center, model.localScale));
            vis.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            MeshRenderer r = vis.AddComponent<MeshRenderer>();
            r.sharedMaterials = mr.sharedMaterials;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        public static float IngredientRadius(SoupType soup)
        {
            if (IngredientRadii.TryGetValue(soup, out float r))
            {
                return r;
            }
            r = 0.05f;
            if (TryIngredientMesh(soup, out Transform model, out MeshFilter mf, out _))
            {
                Vector3 e = Vector3.Scale(mf.sharedMesh.bounds.extents, model.localScale);
                r = Mathf.Max(e.x, Mathf.Max(e.y, e.z));
            }
            IngredientRadii[soup] = r;
            return r;
        }

        public static GameObject FilledPlate(SoupType soup) => Plates.TryGetValue(soup, out GameObject t) ? t : null;

        // null = esta receita não vai em caneca (atum) ou a caneca dela não pôde ser montada.
        public static GameObject FilledMug(SoupType soup) => Mugs.TryGetValue(soup, out GameObject t) ? t : null;

        public static Food FilledPlateFood(SoupType soup)
        {
            GameObject t = FilledPlate(soup);
            return t != null ? t.GetComponent<Food>() : null;
        }

        public static void Tick()
        {
            if (_failed)
            {
                return;
            }
            // Espera o jogo inicializar o Addressables antes de pedir os prefabs.
            if (!_loadStarted && Time.realtimeSinceStartup > 5f)
            {
                StartLoad();
            }
            if (Ready)
            {
                EnsureRegistered();
            }
        }

        private static void StartLoad()
        {
            _loadStarted = true;
            _pendingLoads = 3 + Recipes.FreshIds.Length;
            Load(PlateBaseKey, go => _plateBase = go);
            Load(SoupBaseKey, go => _soupBase = go);
            Load(MugKey, go => MugPrefab = go);
            foreach (string id in Recipes.FreshIds)
            {
                Recipes.TryIngredientId(id, out SoupType soup, out _);
                Load(id, go => IngredientBases[soup] = go);
            }
        }

        private static void Load(string key, Action<GameObject> store)
        {
            try
            {
                AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(key);
                handle.Completed += h =>
                {
                    if (h.Status == AsyncOperationStatus.Succeeded && h.Result != null)
                    {
                        store(h.Result);
                    }
                    else
                    {
                        Plugin.Log.LogError($"Não consegui carregar o prefab '{key}' do jogo: {h.OperationException?.Message}");
                    }
                    if (--_pendingLoads == 0)
                    {
                        Build();
                    }
                };
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Falha ao pedir o prefab '{key}': {e}");
                _failed = true;
            }
        }

        private static void Build()
        {
            if (_plateBase == null || _soupBase == null)
            {
                _failed = true;
                Plugin.Log.LogError("Pratos desativados: prefabs base não encontrados. A panela continua funcionando.");
                return;
            }
            try
            {
                _holder = new GameObject("CozinhaExtra_Templates");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
                _holder.hideFlags = HideFlags.HideAndDontSave;

                MeshRenderer soupRenderer = _soupBase.GetComponentInChildren<MeshRenderer>(true);
                Material baseMat = soupRenderer != null ? soupRenderer.sharedMaterial : null;
                LitTemplate = baseMat;

                Material ceramic = MakeMaterial(baseMat, new Color(0.93f, 0.92f, 0.88f), 0.75f, "CozinhaExtra_Ceramic");
                Mesh collider = MeshFactory.Cylinder(MeshFactory.PlateRadius, MeshFactory.PlateHeight, 0f);
                Mesh colliderCentered = MeshFactory.Cylinder(MeshFactory.PlateRadius, MeshFactory.PlateHeight, -MeshFactory.PlateHeight / 2f);

                // Segurar e colocar como a lata de sopa (uma mão, pose de item pequeno).
                HeldItemConfig plateConfig = _soupBase.GetComponent<HeldItem>().heldItemConfig;

                // Todos os pratos têm o pivô no centro (o quadro e a lata também).
                EmptyPlate = BuildTemplate(_plateBase, EmptyPlateAssetId, 0, "CozinhaExtra_Plate", "CE_Plate",
                    "Plate", "Prato",
                    MeshFactory.Plate(-MeshFactory.PlateHeight / 2f, false), new[] { ceramic }, colliderCentered,
                    IconFactory.Plate(Color.clear), MeshFactory.PlateHeight / 2f, plateConfig, PlateFootprint);
                ConfigureEmptyPlate(EmptyPlate, ceramic);

                // Um prato (e uma caneca, se a receita levar) por receita; materiais criados uma vez cada.
                Dictionary<SoupType, Material> soupMaterials = new Dictionary<SoupType, Material>();
                foreach (Recipe recipe in Recipes.All)
                {
                    Material soupMat = MakeMaterial(baseMat, recipe.Color, Smoothness(recipe.Type), "CozinhaExtra_" + recipe.Type + "Soup");
                    soupMaterials[recipe.Type] = soupMat;
                    GameObject plate = BuildTemplate(_soupBase, recipe.PlateAssetId, (byte)recipe.Type, "CozinhaExtra_" + recipe.PlateKey, "CE_" + recipe.PlateKey,
                        recipe.PlateEn, recipe.PlatePt,
                        MeshFactory.Plate(-MeshFactory.PlateHeight / 2f, true), new[] { ceramic, soupMat }, colliderCentered,
                        IconFactory.Plate(recipe.Color), MeshFactory.PlateHeight / 2f, plateConfig, PlateFootprint);
                    SetupSoupFood(plate, recipe.Type);
                    Plates[recipe.Type] = plate;
                }

                Ready = true;
                BuildMugs(soupMaterials);
                EnsureRegistered();
                Plugin.Log.LogInfo($"Pratos prontos: {string.Join(", ", NamesOf(Plates))}. " +
                    (MugsReady ? $"Canecas: {string.Join(", ", NamesOf(Mugs))}." : "Canecas de sopa desativadas."));
            }
            catch (Exception e)
            {
                _failed = true;
                Ready = false;
                Plugin.Log.LogError($"Pratos desativados por erro ao montar os itens: {e}");
            }
        }

        // Brilho do líquido: tomate e batata como na 1.4.x; as outras no meio-termo.
        private static float Smoothness(SoupType soup) => soup == SoupType.Tomato ? 0.55f : soup == SoupType.Potato ? 0.45f : 0.5f;

        private static List<string> NamesOf(Dictionary<SoupType, GameObject> templates)
        {
            List<string> names = new List<string>();
            foreach (GameObject t in templates.Values)
            {
                names.Add(t.name.Replace("CozinhaExtra_", ""));
            }
            return names;
        }

        // Caneca de sopa = lata de sopa (comida) com a malha e os materiais da caneca do jogo e a
        // malha de líquido dela pintada com a cor da sopa. Se algo faltar, só as canecas ficam de fora.
        private static void BuildMugs(Dictionary<SoupType, Material> soupMaterials)
        {
            try
            {
                if (MugPrefab == null)
                {
                    return;
                }
                Transform mugModel = MugPrefab.transform.Find("Model");
                MeshFilter mugFilter = mugModel != null ? mugModel.GetComponent<MeshFilter>() : null;
                MeshRenderer mugRenderer = mugModel != null ? mugModel.GetComponent<MeshRenderer>() : null;
                Transform liquid = MugPrefab.transform.Find("Mug-01-LiquidMesh");
                MeshFilter liquidFilter = liquid != null ? liquid.GetComponent<MeshFilter>() : null;
                if (mugFilter == null || mugRenderer == null || liquidFilter == null)
                {
                    Plugin.Log.LogWarning("Caneca do jogo diferente do esperado; canecas de sopa desativadas.");
                    return;
                }
                HeldItem mugItem = MugPrefab.GetComponent<HeldItem>();
                Sprite icon = mugItem != null && mugItem.heldItemConfig != null ? mugItem.heldItemConfig.inventoryIcon : null;
                Mesh collider = MeshFactory.Cylinder(MugRadius, MugHeight, -MugHeight / 2f);

                foreach (Recipe recipe in Recipes.All)
                {
                    if (!recipe.HasMug)
                    {
                        continue;
                    }
                    Mugs[recipe.Type] = BuildMug(recipe, mugModel, mugFilter, mugRenderer, liquid, liquidFilter,
                        soupMaterials[recipe.Type], collider, icon != null ? icon : IconFactory.Plate(recipe.Color));
                }
            }
            catch (Exception e)
            {
                Mugs.Clear();
                Plugin.Log.LogError($"Canecas de sopa desativadas por erro: {e}");
            }
        }

        private static GameObject BuildMug(Recipe recipe,
            Transform mugModel, MeshFilter mugFilter, MeshRenderer mugRenderer, Transform liquid, MeshFilter liquidFilter,
            Material soupMaterial, Mesh collider, Sprite icon)
        {
            SoupType soup = recipe.Type;
            GameObject t = BuildTemplate(_soupBase, recipe.MugAssetId, (byte)(CePlate.MugOffset + (byte)soup),
                "CozinhaExtra_" + recipe.MugKey, "CE_" + recipe.MugKey, recipe.MugEn, recipe.MugPt,
                mugFilter.sharedMesh, mugRenderer.sharedMaterials, collider, icon, MugHeight / 2f,
                MugPrefab.GetComponent<HeldItem>().heldItemConfig, MugFootprint);

            // A caneca tem o pivô na base; a lata, no centro.
            Transform model = t.transform.Find("Model");
            model.localRotation = mugModel.localRotation;
            model.localPosition = new Vector3(0f, -MugHeight / 2f, 0f);

            // Na caneca do jogo o líquido é irmão do Model, com a mesma rotação; aqui vira filho dele.
            GameObject fill = new GameObject("CozinhaExtra_SoupFill");
            fill.transform.SetParent(model, false);
            fill.transform.localPosition = Quaternion.Inverse(mugModel.localRotation) * (liquid.localPosition - mugModel.localPosition);
            fill.transform.localRotation = Quaternion.Inverse(mugModel.localRotation) * liquid.localRotation;
            Vector3 s = liquid.localScale;
            fill.transform.localScale = new Vector3(s.x, s.y * MugFill, s.z);
            fill.AddComponent<MeshFilter>().sharedMesh = liquidFilter.sharedMesh;
            MeshRenderer fillRenderer = fill.AddComponent<MeshRenderer>();
            fillRenderer.sharedMaterial = soupMaterial;
            fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fill.layer = model.gameObject.layer;

            // O colisor é filho do Model, que acabou de descer meia caneca: refaz o cilindro para
            // ficar centrado no pivô do item (senão metade da colisão fica abaixo da caneca).
            foreach (MeshCollider mc in t.GetComponentsInChildren<MeshCollider>(true))
            {
                float pivotY = mc.transform.InverseTransformPoint(t.transform.position).y;
                mc.sharedMesh = MeshFactory.Cylinder(MugRadius, MugHeight, pivotY - MugHeight / 2f);
            }

            // Mão como na caneca do jogo: mesma pegada e mesma pose de dedos.
            Transform sourceGrip = mugModel.Find("HandGripR");
            Transform targetGrip = model.Find("HandGripR");
            if (sourceGrip != null && targetGrip != null)
            {
                CopyPose(sourceGrip, targetGrip);
            }

            SetupSoupFood(t, soup);
            return t;
        }

        // Copia posição/rotação local de uma hierarquia para outra com os mesmos nomes (ossos da mão).
        private static void CopyPose(Transform source, Transform target)
        {
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            foreach (Transform child in source)
            {
                Transform match = target.Find(child.name);
                if (match != null)
                {
                    CopyPose(child, match);
                }
            }
        }

        private static GameObject BuildTemplate(GameObject basePrefab, uint assetId, byte kind, string objectName, string itemId,
            string english, string portuguese, Mesh visual, Material[] materials, Mesh collider, Sprite icon, float snapY,
            HeldItemConfig configSource, Vector3 footprintSize)
        {
            // Filho de um objeto inativo: o Awake do jogo não roda no molde, só nas cópias.
            GameObject t = Object.Instantiate(basePrefab, _holder.transform);
            t.name = objectName;

            NetworkIdentity identity = t.GetComponent<NetworkIdentity>();
            AssetIdField.SetValue(identity, assetId);

            // O item guarda o nome em inglês; o gancho do tradutor mostra em português quando o jogo está em português.
            HeldItem held = t.GetComponent<HeldItem>();
            held.interactableName = english;
            ModNames[english] = portuguese;
            // A pose de segurar/colocar vem de um item do jogo de formato parecido (lata ou caneca).
            HeldItemConfig source = configSource != null ? configSource : held.heldItemConfig;
            if (source != null)
            {
                HeldItemConfig config = Object.Instantiate(source);
                config.name = objectName + "_HIC";
                config.isInventoryItem = true;
                config.inventoryIcon = icon;
                config.itemMaterial = ItemMaterial.Glass;
                config.hideFlags = HideFlags.DontUnloadUnusedAsset;
                held.heldItemConfig = config;
            }

            LootItemDefinition def = t.GetComponent<LootItemDefinition>();
            if (def != null && LootNameField != null)
            {
                LootNameField.SetValue(def, itemId);
            }
            // O jogo decide onde o item cabe pelo tamanho registrado; sem isso valeria o da lata/quadro.
            if (def != null && FootprintField != null)
            {
                LootFootprint footprint = (LootFootprint)FootprintField.GetValue(def);
                footprint.isBaked = true;
                footprint.fromRenderers = false;
                footprint.size = footprintSize;
                footprint.centerOffset = Vector3.zero;
                FootprintField.SetValue(def, footprint);
            }

            Transform model = t.transform.Find("Model");
            MeshFilter filter = model != null ? model.GetComponent<MeshFilter>() : null;
            MeshRenderer renderer = model != null ? model.GetComponent<MeshRenderer>() : null;
            if (filter == null || renderer == null)
            {
                throw new InvalidOperationException($"'{basePrefab.name}' não tem Model com MeshFilter/MeshRenderer");
            }
            filter.sharedMesh = visual;
            renderer.sharedMaterials = materials;

            foreach (MeshCollider mc in t.GetComponentsInChildren<MeshCollider>(true))
            {
                mc.sharedMesh = collider;
                mc.convex = true;
            }
            // Alguns itens base (o quadro) usam caixa em vez de malha: a caixa sólida vira do tamanho
            // do item, centrada no pivô (todos os itens do mod têm o pivô no centro).
            foreach (BoxCollider box in t.GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.isTrigger)
                {
                    continue;
                }
                box.center = box.transform.InverseTransformPoint(t.transform.position);
                box.size = footprintSize;
            }

            // A lata tem uma superfície de encaixe no topo (para empilhar latas): acompanha o novo
            // topo e não aceita nada (nada empilha em cima de sopa).
            Transform snap = t.transform.Find("SnappingPlane");
            if (snap != null)
            {
                Vector3 p = snap.localPosition;
                snap.localPosition = new Vector3(p.x, snapY, p.z);
                SnappingPlane plane = snap.GetComponent<SnappingPlane>();
                if (plane != null && plane.AllowedItemIDs != null)
                {
                    plane.AllowedItemIDs.Clear();
                    plane.AllowedItemIDs.Add(NoStackId);
                }
            }

            t.AddComponent<CePlate>().Kind = kind;
            string saveKey = SaveRestore.KeyPrefix + objectName.Replace("CozinhaExtra_", "");
            TemplatesByKey[saveKey] = t;
            KeysByTemplate[t] = saveKey;
            return t;
        }

        // Ajustes do prato vazio feito do quadro: deitado do jeito certo, segurado com uma mão,
        // cor de louça e uma superfície em cima que só aceita outro prato (empilhar).
        private static void ConfigureEmptyPlate(GameObject t, Material ceramic)
        {
            Transform model = t.transform.Find("Model");
            if (model != null)
            {
                // O quadro tem o modelo virado de cabeça para baixo.
                model.localRotation = Quaternion.identity;
                model.localPosition = Vector3.zero;
                Transform gripR = model.Find("HandGripR");
                if (gripR != null)
                {
                    gripR.localPosition = new Vector3(MeshFactory.PlateRadius - 0.01f, 0f, 0f);
                }
                Transform gripL = model.Find("HandGripL");
                if (gripL != null)
                {
                    gripL.localPosition = new Vector3(-(MeshFactory.PlateRadius - 0.01f), 0f, 0f);
                }
            }

            // O quadro sorteia uma pintura e troca o material do modelo; aqui só existe "louça".
            NomadDrive.Features.Furnitures.Picture picture = t.GetComponent<NomadDrive.Features.Furnitures.Picture>();
            if (picture != null && PictureMaterialsField != null)
            {
                PictureMaterialsField.SetValue(picture, new[] { ceramic });
            }

            // Uma mão, como a lata.
            HeldItemIKSetup ik = t.GetComponent<HeldItemIKSetup>();
            HeldItemIKSetup canIk = _soupBase.GetComponent<HeldItemIKSetup>();
            if (ik != null && canIk != null && GripSelectionField != null)
            {
                GripSelectionField.SetValue(ik, GripSelectionField.GetValue(canIk));
            }

            LootItemDefinition def = t.GetComponent<LootItemDefinition>();
            if (def != null && CategoryField != null)
            {
                CategoryField.SetValue(def, LootCategory.Kitchen);
            }

            Transform top = t.transform.Find("SnapSurface_Top");
            if (top != null)
            {
                top.localPosition = new Vector3(0f, MeshFactory.PlateHeight / 2f, 0f);
                BoxCollider box = top.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.center = new Vector3(0f, box.center.y, 0f);
                    box.size = new Vector3(0.2f, box.size.y, 0.2f);
                }
                SnappingPlane plane = top.GetComponent<SnappingPlane>();
                if (plane != null)
                {
                    if (plane.AllowedItemIDs != null)
                    {
                        plane.AllowedItemIDs.Clear();
                        plane.AllowedItemIDs.Add("CE_Plate");
                    }
                    // O perfil do quadro limita tamanhos; para prato em cima de prato não precisa.
                    SurfaceProfileField?.SetValue(plane, null);
                }
            }
        }

        private static void SetupSoupFood(GameObject template, SoupType soup)
        {
            Food food = template.GetComponent<Food>();
            ConsumableConfig config = ScriptableObject.CreateInstance<ConsumableConfig>();
            config.name = "CozinhaExtra_" + soup + "SoupConsumable";
            config.hideFlags = HideFlags.DontUnloadUnusedAsset;
            config.playerStatCollection = Stats(soup);
            food.consumableConfig = config;
            // Cópias pegam isto do config no Start; no molde fica preenchido para "comer da panela".
            food.currentPlayerStatCollection = Stats(soup);
        }

        // Fome, sede e energia da receita (só as que dão algo). O dicionário do jogo se reconstrói a
        // partir das listas keys/values quando o Unity o recarrega, então as duas formas ficam preenchidas.
        private static SerializableDictionary<PlayerStatType, float> Stats(SoupType soup)
        {
            SerializableDictionary<PlayerStatType, float> d = new SerializableDictionary<PlayerStatType, float>();
            AddStat(d, PlayerStatType.Nutrition, Cfg.Nutrition(soup));
            AddStat(d, PlayerStatType.Hydration, Cfg.Hydration(soup));
            AddStat(d, PlayerStatType.Energy, Cfg.Energy(soup));
            d.OnBeforeSerialize();
            return d;
        }

        private static void AddStat(SerializableDictionary<PlayerStatType, float> d, PlayerStatType type, float value)
        {
            if (value != 0f)
            {
                d[type] = value;
            }
        }

        // O Mirror limpa a lista de prefabs ao trocar de partida, então confere sempre.
        private static void EnsureRegistered()
        {
            Register(EmptyPlate);
            foreach (GameObject t in Plates.Values)
            {
                Register(t);
            }
            foreach (GameObject t in Mugs.Values)
            {
                Register(t);
            }
        }

        private static void Register(GameObject template)
        {
            if (template == null)
            {
                return;
            }
            uint id = template.GetComponent<NetworkIdentity>().assetId;
            if (NetworkClient.prefabs.TryGetValue(id, out GameObject existing))
            {
                if (existing != template)
                {
                    Plugin.Log.LogError($"assetId {id:X} já usado por '{existing.name}'; pratos podem não aparecer.");
                    _failed = true;
                }
                return;
            }
            NetworkClient.RegisterPrefab(template);
        }
    }
}
