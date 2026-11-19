using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Mirror;
using NomadDrive.Features.FloatingOrigin;
using NomadDrive.Features.SaveSystem;
using NomadDrive.Features.WorldGeneration.ObjectSpawning;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace CozinhaExtra
{
    // Itens do mod no save do jogo. Cada item do mod é gravado com uma chave própria
    // ("CozinhaExtra:Plate"...) no lugar do GUID de prefab do jogo. Ao carregar, o gancho em
    // GameSaveService.SpawnFromRecordAsync manda essas chaves para cá. Sem o mod, o jogo só
    // não acha o prefab e pula o registro (o save continua abrindo).
    internal static class SaveRestore
    {
        public const string KeyPrefix = "CozinhaExtra:";

        public static bool IsModKey(string key) => key != null && key.StartsWith(KeyPrefix, StringComparison.Ordinal);

        public static async UniTask<PersistentObject> Spawn(ObjectRecord record)
        {
            // O mundo pode começar a carregar antes de os moldes ficarem prontos.
            float giveUp = Time.realtimeSinceStartup + 20f;
            while (!Items.Ready && Time.realtimeSinceStartup < giveUp)
            {
                await UniTask.Yield();
            }
            if (!NetworkServer.active || !Items.TryGetTemplate(record.AddressableGuid, out GameObject template))
            {
                Plugin.Log.LogWarning($"Item do save não restaurado: {record.AddressableGuid}");
                return null;
            }
            FloatingOriginManager origin = FloatingOriginManager.Instance;
            Vector3 position = origin != null ? origin.WorldToRender(record.Position) : record.Position;
            GameObject go = Object.Instantiate(template, position, record.Rotation);
            go.name = template.name;
            PersistentObject result = PersistentObject.ServerEnsure(go, record.AddressableGuid, record.Guid);
            NetworkServer.Spawn(go, NetworkServer.localConnection);
            // Raiz do mundo, igual ao jogo faz com o que restaura (guid nulo: o carimbo já foi feito acima).
            LootSpawningService.ApplyPostSpawnWorldIntegration(go, null);
            return result;
        }
    }

    // Pratos espalhados pelo mapa: quando o jogo coloca loot de cozinha ou de comida no mundo,
    // às vezes aparece um prato do lado, na mesma superfície. Só no host.
    internal static class LootPlates
    {
        private static readonly Vector3[] Directions =
        {
            Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
            new Vector3(0.7071f, 0f, 0.7071f), new Vector3(-0.7071f, 0f, 0.7071f),
            new Vector3(0.7071f, 0f, -0.7071f), new Vector3(-0.7071f, 0f, -0.7071f)
        };

        private static readonly Vector3 PlateHalfExtents = new Vector3(MeshFactory.PlateRadius, MeshFactory.PlateHeight / 2f, MeshFactory.PlateRadius);

        // Enquanto o próprio mod cria coisas (kit de teste), não sorteia prato extra.
        public static bool Suppressed;

        public static void OnLootSpawned(GameObject loot)
        {
            if (Suppressed || loot == null || !NetworkServer.active || !Cfg.PlatesInWorld.Value || !Items.Ready)
            {
                return;
            }
            if (loot.GetComponent<CePlate>() != null || !loot.TryGetComponent(out LootItemDefinition def))
            {
                return;
            }
            float chance = def.Category == LootCategory.Kitchen ? Cfg.PlateChanceKitchen.Value
                : def.Category == LootCategory.Food ? Cfg.PlateChanceFood.Value
                : 0f;
            if (chance <= 0f || Random.value > chance)
            {
                return;
            }
            try
            {
                TrySpawnBeside(loot);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Prato do mapa não criado perto de '{loot.name}': {e.Message}");
            }
        }

        private static void TrySpawnBeside(GameObject loot)
        {
            if (!TryGetSolidBounds(loot, out Bounds bounds))
            {
                return;
            }
            // O loot pode sumir e voltar quando a área recarrega; não empilha pratos no mesmo lugar.
            foreach (CePlate plate in CePlate.Live)
            {
                if (plate != null && (plate.transform.position - bounds.center).sqrMagnitude < 1f)
                {
                    return;
                }
            }

            float surfaceY = bounds.min.y;
            float reach = Mathf.Max(bounds.extents.x, bounds.extents.z) + MeshFactory.PlateRadius + 0.02f;
            int start = Random.Range(0, Directions.Length);
            for (int i = 0; i < Directions.Length; i++)
            {
                Vector3 dir = Directions[(start + i) % Directions.Length];
                Vector3 probe = new Vector3(bounds.center.x, surfaceY, bounds.center.z) + dir * reach;
                // Precisa ter chão (a mesma prateleira/mesa) logo embaixo...
                if (!Physics.Raycast(probe + Vector3.up * 0.25f, Vector3.down, out RaycastHit hit, 0.5f, ~0, QueryTriggerInteraction.Ignore) ||
                    Mathf.Abs(hit.point.y - surfaceY) > 0.05f || hit.normal.y < 0.9f)
                {
                    continue;
                }
                // ...e espaço livre para o prato.
                Vector3 center = hit.point + Vector3.up * (MeshFactory.PlateHeight / 2f + 0.004f);
                if (!IsFree(center))
                {
                    continue;
                }
                GameObject go = Object.Instantiate(Items.EmptyPlate, hit.point + Vector3.up * 0.003f,
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                go.name = Items.EmptyPlate.name;
                NetworkServer.Spawn(go, NetworkServer.localConnection);
                LootSpawningService.ApplyPostSpawnWorldIntegration(go, Items.SaveKey(Items.EmptyPlate));
                return;
            }
        }

        // O Physics.CheckBox não existe no Unity enxugado do jogo: o espaço do prato é
        // conferido com esferas pequenas no centro e na borda (mais uma acima, para o vão).
        private static bool IsFree(Vector3 center)
        {
            const float r = 0.011f;
            float rim = MeshFactory.PlateRadius - r;
            Vector3[] points =
            {
                center,
                center + new Vector3(rim, 0f, 0f), center + new Vector3(-rim, 0f, 0f),
                center + new Vector3(0f, 0f, rim), center + new Vector3(0f, 0f, -rim),
                center + new Vector3(rim * 0.7071f, 0f, rim * 0.7071f), center + new Vector3(-rim * 0.7071f, 0f, -rim * 0.7071f),
                center + new Vector3(rim * 0.7071f, 0f, -rim * 0.7071f), center + new Vector3(-rim * 0.7071f, 0f, rim * 0.7071f)
            };
            foreach (Vector3 p in points)
            {
                if (Physics.OverlapSphere(p, r, ~0, QueryTriggerInteraction.Ignore).Length > 0)
                {
                    return false;
                }
            }
            return Physics.OverlapSphere(center + Vector3.up * 0.06f, 0.04f, ~0, QueryTriggerInteraction.Ignore).Length == 0;
        }

        private static bool TryGetSolidBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Collider c in go.GetComponentsInChildren<Collider>())
            {
                if (c == null || c.isTrigger || !c.enabled)
                {
                    continue;
                }
                if (!any)
                {
                    bounds = c.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(c.bounds);
                }
            }
            if (any)
            {
                return true;
            }
            // Loot recém-criado às vezes está com os colisores em modo gatilho; usa o visual.
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled)
                {
                    continue;
                }
                if (!any)
                {
                    bounds = r.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
            return any;
        }
    }
}
