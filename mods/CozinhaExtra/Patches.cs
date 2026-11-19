using System;
using System.Reflection;
using HarmonyLib;
using Mirror;
using Cysharp.Threading.Tasks;
using NomadDrive.Features.Consumables;
using NomadDrive.Features.SaveSystem;
using NomadDrive.Features.WorldGeneration.ObjectSpawning;
using UnityEngine;

namespace CozinhaExtra
{
    // Ganchos pequenos no jogo. Cada um é aplicado à parte: se um falhar numa
    // atualização do jogo, o resto do mod continua funcionando.
    internal static class Patches
    {
        public static void Apply(Harmony harmony)
        {
            // Host: um prato/caneca de sopa foi comido -> devolve o prato vazio ou a caneca.
            TryPatch(harmony,
                AccessTools.Method(typeof(Consumable), "UserCode_CmdConsumeFromHand__NetworkConnectionToClient"),
                nameof(ConsumePrefix), nameof(ConsumePostfix));

            // Todos: o tradutor do jogo não conhece os nomes do mod; mostra em PT-BR ou inglês conforme o idioma do jogo.
            Type localization = AccessTools.TypeByName("EvilCore.Localization.LocalizationService");
            TryPatch(harmony,
                localization != null ? AccessTools.Method(localization, "LocalizeName", new[] { typeof(string) }) : null,
                nameof(LocalizePrefix), null);

            // Clientes: o host avisa que tem o mod (ver Net.BeaconNetId).
            TryPatch(harmony,
                AccessTools.Method(typeof(NetworkClient), "OnObjectHide", new[] { typeof(ObjectHideMessage) }),
                nameof(ObjectHidePrefix), null);

            // Host: carregar do save os pratos e canecas do mod.
            TryPatch(harmony,
                AccessTools.Method(typeof(GameSaveService), "SpawnFromRecordAsync", new[] { typeof(ObjectRecord) }),
                nameof(SpawnFromRecordPrefix), null);

            // Host: loot de cozinha/comida às vezes ganha um prato do lado.
            TryPatch(harmony,
                AccessTools.Method(typeof(LootSpawningService), nameof(LootSpawningService.ApplyPostSpawnWorldIntegration)),
                null, nameof(LootSpawnedPostfix));
        }

        private static bool ObjectHidePrefix(ObjectHideMessage message)
        {
            if (message.netId != Net.BeaconNetId)
            {
                return true;
            }
            Client.OnModdedObjectSeen();
            return false;
        }

        private static bool SpawnFromRecordPrefix(ObjectRecord record, ref UniTask<PersistentObject> __result)
        {
            if (record == null || !SaveRestore.IsModKey(record.AddressableGuid))
            {
                return true;
            }
            __result = SaveRestore.Spawn(record);
            return false;
        }

        private static void LootSpawnedPostfix(GameObject instance)
        {
            try
            {
                LootPlates.OnLootSpawned(instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"LootSpawnedPostfix: {e.Message}");
            }
        }

        private static void TryPatch(Harmony harmony, MethodBase target, string prefix, string postfix)
        {
            if (target == null)
            {
                Plugin.Log.LogWarning($"Gancho não encontrado ({prefix}); essa parte do mod fica desligada.");
                return;
            }
            try
            {
                harmony.Patch(target,
                    prefix: prefix != null ? new HarmonyMethod(typeof(Patches), prefix) : null,
                    postfix: postfix != null ? new HarmonyMethod(typeof(Patches), postfix) : null);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Falha ao aplicar o gancho em {target.DeclaringType?.Name}.{target.Name}: {e.Message}");
            }
        }

        // O que foi comido, lido antes de o jogo destruir o item (sem guardar referência a objeto do Unity).
        internal struct Eaten
        {
            public bool Valid;
            public bool IsMug;
            public uint SourceAsset;
            public string SourceGuid;
        }

        // Mesmas condições que o jogo confere antes de destruir a comida da mão.
        private static void ConsumePrefix(Consumable __instance, NetworkConnectionToClient sender, out Eaten __state)
        {
            __state = default;
            try
            {
                CePlate plate = __instance != null ? __instance.GetComponent<CePlate>() : null;
                __state.Valid = plate != null && plate.Kind != 0 &&
                                __instance.IsEquipped && !__instance.IsStowed && __instance.ServerContainer == null &&
                                __instance.netIdentity.connectionToClient == sender;
                if (__state.Valid)
                {
                    __state.IsMug = plate.IsMug;
                    __state.SourceAsset = plate.SourceAssetId;
                    __state.SourceGuid = plate.SourceGuid;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"ConsumePrefix: {e.Message}");
            }
        }

        private static void ConsumePostfix(NetworkConnectionToClient sender, Eaten __state)
        {
            if (!__state.Valid)
            {
                return;
            }
            try
            {
                Server.GiveContainerBack(sender, __state.IsMug, __state.SourceAsset, __state.SourceGuid);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Não consegui devolver o prato/caneca: {e}");
            }
        }

        private static bool LocalizePrefix(string englishName, ref string __result)
        {
            if (Items.TryTranslate(englishName, out string text))
            {
                __result = text;
                return false;
            }
            return true;
        }
    }
}
