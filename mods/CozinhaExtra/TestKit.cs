using System.Collections;
using System.Linq;
using Mirror;
using NomadDrive.Features.Consumables;
using NomadDrive.Features.LiquidTransferSystem;
using NomadDrive.Features.SaveSystem;
using NomadDrive.Features.WorldGeneration.ObjectSpawning;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace CozinhaExtra
{
    // Kit para testar o mod: o host aperta a tecla e aparecem, na frente dele, um fogão,
    // uma panela, tomates, batatas, garrafas de água cheias, uma caneca, um prato e uma mesa. Desligado por padrão
    // (BepInEx\config\seabra.nomaddrive.cozinhaextra.cfg, seção [Teste]).
    // Usa o mesmo caminho do comando "world.spawnLoot" do console do jogo.
    internal static class TestKit
    {
        private static readonly string[] Kit =
        {
            "Stove01", "CookingPot-01",
            "Tomato", "Tomato", "Potato", "Potato",
            "BottleWater", "BottleWater", "Mug"
        };

        private static float _nextAllowed;

        public static void Tick(MonoBehaviour runner)
        {
            if (!Cfg.TestKitEnabled.Value || !NetworkServer.active)
            {
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[Cfg.TestKitKey].wasPressedThisFrame || Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }
            if (Time.unscaledTime < _nextAllowed)
            {
                return;
            }
            _nextAllowed = Time.unscaledTime + 3f;
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }
            runner.StartCoroutine(SpawnKit(cam.transform));
        }

        private static IEnumerator SpawnKit(Transform cam)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 origin = cam.position + forward * 1.6f;
            // Acha o chão na frente do jogador.
            if (Physics.Raycast(origin + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
            {
                origin = hit.point;
            }

            LootRegistry registry = Resources.FindObjectsOfTypeAll<LootRegistry>().FirstOrDefault();
            LootPlates.Suppressed = true;

            // Mesa um pouco mais à frente, para testar colocar e empilhar.
            AsyncOperationHandle<GameObject> tableHandle = Addressables.LoadAssetAsync<GameObject>("Table-01");
            yield return tableHandle;
            if (tableHandle.Status == AsyncOperationStatus.Succeeded && tableHandle.Result != null && NetworkServer.active)
            {
                GameObject table = Object.Instantiate(tableHandle.Result, origin + forward * 1.3f + Vector3.up * 0.05f, Quaternion.LookRotation(-forward));
                NetworkServer.Spawn(table, NetworkServer.localConnection);
                LootSpawningService.ApplyPostSpawnWorldIntegration(table, registry != null ? registry.GetByAddress("Table-01")?.assetGuid : null);
            }
            for (int i = 0; i < Kit.Length; i++)
            {
                string key = Kit[i];
                // Fogão no meio, o resto em fileira dos lados, um pouco acima do chão.
                float side = i == 0 ? 0f : (i % 2 == 1 ? 1f : -1f) * (0.45f + 0.3f * ((i - 1) / 2));
                Vector3 position = origin + right * side + Vector3.up * (i == 0 ? 0.15f : 0.35f);

                AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(key);
                yield return handle;
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null || !NetworkServer.active)
                {
                    Plugin.Log.LogWarning($"Kit de teste: não consegui carregar '{key}'.");
                    continue;
                }
                GameObject go = Object.Instantiate(handle.Result, position, Quaternion.LookRotation(-forward));
                NetworkServer.Spawn(go, NetworkServer.localConnection);
                string guid = registry != null ? registry.GetByAddress(key)?.assetGuid : null;
                // Registra no save como loot normal (se o jogo conhecer o item) e na raiz do mundo.
                LootSpawningService.ApplyPostSpawnWorldIntegration(go, guid);
                if (go.TryGetComponent(out Food food))
                {
                    food.ServerSetFreshMax();
                }
                yield return null;
                // Só as garrafas vêm cheias (a caneca precisa estar vazia para receber sopa).
                if (go != null && key == "BottleWater" && go.TryGetComponent(out LiquidContainerComponent liquid))
                {
                    liquid.SetLiquidType(LiquidType.Water);
                    liquid.SetAmount(liquid.Capacity);
                }
            }
            // O prato do mod não é loot do jogo: vem do molde e é salvo com a chave do mod.
            if (Items.Ready && NetworkServer.active)
            {
                GameObject plate = Object.Instantiate(Items.EmptyPlate, origin - right * 1.4f + Vector3.up * 0.35f, Quaternion.identity);
                plate.name = Items.EmptyPlate.name;
                NetworkServer.Spawn(plate, NetworkServer.localConnection);
                LootSpawningService.ApplyPostSpawnWorldIntegration(plate, Items.SaveKey(Items.EmptyPlate));
            }
            LootPlates.Suppressed = false;
            Plugin.Log.LogInfo("Kit de teste criado.");
        }
    }
}
