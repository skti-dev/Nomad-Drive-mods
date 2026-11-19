using System.Collections.Generic;
using Mirror;
using NomadDrive.Features.Interaction;
using NomadDrive.Features.SaveSystem;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CozinhaExtra
{
    // Canecas de outros mods (ex.: as coloridas do Nomad Items) são a caneca do jogo com outro
    // material e outro prefab. A caneca de sopa guarda de qual caneca veio (assetId do Mirror e
    // GUID de save) para ficar com a mesma cara e devolver a mesma caneca depois de comer.
    // Nada aqui conhece o Nomad Items: vale para qualquer caneca registrada no Mirror.
    internal static class MugSkins
    {
        // Cliente: de qual caneca veio cada caneca de sopa (por netId), durante a partida. Não expira:
        // o jogo destrói e recria loot longe do jogador, e a cópia recriada sai do molde branco.
        private static readonly Dictionary<uint, uint> Known = new Dictionary<uint, uint>();

        // Canecas de sopa cujo prefab de origem ainda não existe neste PC (ex.: o outro mod ainda montando).
        private static readonly List<CePlate> Unresolved = new List<CePlate>();
        private static readonly List<CePlate> RetryScratch = new List<CePlate>();
        private static float _nextRetry;

        // Configs de item com o ícone da caneca de origem: um por (tipo de sopa, caneca de origem).
        private static readonly Dictionary<ulong, HeldItemConfig> IconConfigs = new Dictionary<ulong, HeldItemConfig>();

        // Host: lê a origem de uma caneca vazia antes de ela virar caneca de sopa.
        // A caneca branca do jogo não conta como origem (fica tudo como era antes).
        public static bool TryReadSource(HeldItem mug, out uint assetId, out string guid)
        {
            assetId = 0;
            guid = null;
            if (mug == null || Items.MugPrefab == null)
            {
                return false;
            }
            NetworkIdentity identity = mug.GetComponent<NetworkIdentity>();
            NetworkIdentity white = Items.MugPrefab.GetComponent<NetworkIdentity>();
            uint id = identity != null ? identity.assetId : 0u;
            if (id == 0 || white == null || id == white.assetId)
            {
                return false;
            }
            assetId = id;
            PersistentObject persistent = mug.GetComponent<PersistentObject>();
            guid = persistent != null ? persistent.AddressableGuid : null;
            return true;
        }

        public static bool TryGetPrefab(uint assetId, out GameObject prefab)
        {
            prefab = null;
            return assetId != 0 && NetworkClient.prefabs.TryGetValue(assetId, out prefab) && prefab != null;
        }

        // GUID de save que o prefab de origem usa (fallback quando a caneca não tinha um).
        public static string PersistentGuidOf(GameObject prefab)
        {
            PersistentObject persistent = prefab != null ? prefab.GetComponent<PersistentObject>() : null;
            return persistent != null ? persistent.AddressableGuid : null;
        }

        // Host: marca a origem na caneca de sopa e aplica a aparência.
        public static void SetSource(GameObject soupMug, uint assetId, string guid)
        {
            CePlate plate = soupMug != null ? soupMug.GetComponent<CePlate>() : null;
            if (plate == null || assetId == 0)
            {
                return;
            }
            plate.SourceAssetId = assetId;
            plate.SourceGuid = guid;
            Apply(plate);
        }

        // Cliente: o host contou de qual caneca veio uma caneca de sopa.
        public static void Remember(uint netId, uint assetId)
        {
            if (netId == 0 || assetId == 0)
            {
                return;
            }
            Known[netId] = assetId;
            if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity) && identity != null)
            {
                CePlate plate = identity.GetComponent<CePlate>();
                if (plate != null)
                {
                    plate.SourceAssetId = assetId;
                    Apply(plate);
                }
            }
        }

        // Toda vez que uma caneca de sopa aparece (criada, carregada ou recriada por distância).
        public static void OnPlateStarted(CePlate plate)
        {
            if (plate == null || !plate.IsMug)
            {
                return;
            }
            if (plate.SourceAssetId == 0)
            {
                NetworkIdentity identity = plate.GetComponent<NetworkIdentity>();
                if (identity == null || identity.netId == 0 || !Known.TryGetValue(identity.netId, out uint assetId))
                {
                    return;
                }
                plate.SourceAssetId = assetId;
            }
            Apply(plate);
        }

        // Copia os materiais do corpo da caneca de origem e o ícone dela. Idempotente.
        public static void Apply(CePlate plate)
        {
            if (plate == null || !plate.IsMug || plate.SourceAssetId == 0)
            {
                return;
            }
            if (!TryGetPrefab(plate.SourceAssetId, out GameObject prefab))
            {
                if (!Unresolved.Contains(plate))
                {
                    Unresolved.Add(plate);
                }
                return;
            }
            // Sopa estragada: o jogo troca o material para o de "estragado"; não briga com isso.
            if (Game.IsRotten(plate.gameObject))
            {
                return;
            }
            Transform sourceModel = prefab.transform.Find("Model");
            Transform model = plate.transform.Find("Model");
            MeshRenderer sourceRenderer = sourceModel != null ? sourceModel.GetComponent<MeshRenderer>() : null;
            MeshFilter sourceFilter = sourceModel != null ? sourceModel.GetComponent<MeshFilter>() : null;
            MeshRenderer renderer = model != null ? model.GetComponent<MeshRenderer>() : null;
            MeshFilter filter = model != null ? model.GetComponent<MeshFilter>() : null;
            if (sourceRenderer == null || sourceFilter == null || renderer == null || filter == null ||
                sourceFilter.sharedMesh == null || filter.sharedMesh == null ||
                sourceFilter.sharedMesh.subMeshCount != filter.sharedMesh.subMeshCount)
            {
                // Caneca de outro formato: fica com a cara da caneca do jogo (a devolução continua certa).
                return;
            }
            // Materiais compartilhados com o prefab de origem: nada é criado por caneca.
            renderer.sharedMaterials = sourceRenderer.sharedMaterials;
            ApplyIcon(plate, prefab);
        }

        private static void ApplyIcon(CePlate plate, GameObject prefab)
        {
            HeldItem held = plate.GetComponent<HeldItem>();
            HeldItem sourceHeld = prefab.GetComponent<HeldItem>();
            GameObject template = Items.FilledMug(plate.Soup);
            HeldItem templateHeld = template != null ? template.GetComponent<HeldItem>() : null;
            if (held == null || sourceHeld == null || sourceHeld.heldItemConfig == null ||
                templateHeld == null || templateHeld.heldItemConfig == null)
            {
                return;
            }
            Sprite icon = sourceHeld.heldItemConfig.inventoryIcon;
            if (icon == null)
            {
                return;
            }
            ulong key = ((ulong)plate.Kind << 32) | plate.SourceAssetId;
            if (!IconConfigs.TryGetValue(key, out HeldItemConfig config) || config == null)
            {
                // Igual ao config da caneca de sopa, só com o ícone da caneca de origem.
                config = Object.Instantiate(templateHeld.heldItemConfig);
                config.name = templateHeld.heldItemConfig.name + "_" + plate.SourceAssetId.ToString("X8");
                config.inventoryIcon = icon;
                config.hideFlags = HideFlags.DontUnloadUnusedAsset;
                IconConfigs[key] = config;
            }
            held.heldItemConfig = config;
        }

        public static void Tick()
        {
            if (Unresolved.Count == 0)
            {
                return;
            }
            float now = Time.unscaledTime;
            if (now < _nextRetry)
            {
                return;
            }
            _nextRetry = now + 1f;
            RetryScratch.Clear();
            RetryScratch.AddRange(Unresolved);
            Unresolved.Clear();
            foreach (CePlate plate in RetryScratch)
            {
                if (plate != null)
                {
                    Apply(plate);
                }
            }
        }

        // Fim da partida (os netIds recomeçam na próxima).
        public static void Reset()
        {
            Known.Clear();
            Unresolved.Clear();
        }
    }
}
