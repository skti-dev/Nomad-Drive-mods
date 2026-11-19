using System;
using System.Collections.Generic;
using Mirror;
using NomadDrive.Features.Consumables;
using NomadDrive.Features.Cooking;
using NomadDrive.Features.Hotbar;
using NomadDrive.Features.Interaction;
using NomadDrive.Features.LiquidTransferSystem;
using NomadDrive.Features.Player;
using NomadDrive.Features.WorldGeneration.ObjectSpawning;
using UnityEngine;

namespace CozinhaExtra
{
    // Consultas ao jogo usadas tanto pelo host quanto pelos clientes.
    internal static class Game
    {
        // Mesmo id que o jogo usa para decidir onde um item encaixa ("Tomato", "CookingPot-01"...).
        public static string ItemId(GameObject go)
        {
            if (go == null)
            {
                return string.Empty;
            }
            if (go.TryGetComponent(out LootItemDefinition def) && !string.IsNullOrEmpty(def.DisplayName))
            {
                return def.DisplayName;
            }
            string name = go.name;
            return name.EndsWith("(Clone)") ? name.Substring(0, name.Length - 7).Trim() : name;
        }

        // A "panela" do jogo (CookingPot-01) é um Pan; a frigideira comum (Pan) fica de fora.
        public static Pan AsCookingPot(Component c)
        {
            Pan pan = c != null ? c.GetComponentInParent<Pan>() : null;
            if (pan == null)
            {
                return null;
            }
            return ItemId(pan.gameObject).StartsWith("CookingPot", StringComparison.OrdinalIgnoreCase) ? pan : null;
        }

        public static SoupType SoupFor(GameObject go)
        {
            if (go == null || go.GetComponent<Food>() == null)
            {
                return SoupType.None;
            }
            switch (ItemId(go))
            {
                case "Tomato":
                    return SoupType.Tomato;
                case "Potato":
                    return SoupType.Potato;
                default:
                    return SoupType.None;
            }
        }

        public static bool IsRotten(GameObject go)
        {
            Food food = go != null ? go.GetComponent<Food>() : null;
            return food != null && food.Network_foodDurabilityType == FoodDurabilityType.Rotten;
        }

        // O item que o cliente diz ter na mão, se for verdade: equipado, fora da hotbar e
        // pertencendo à conexão de quem pediu (itens na mão pertencem a quem segura).
        public static HeldItem ServerHeld(NetworkConnectionToClient conn, uint itemNetId)
        {
            if (conn == null || itemNetId == 0 || !NetworkServer.spawned.TryGetValue(itemNetId, out NetworkIdentity identity) || identity == null)
            {
                return null;
            }
            HeldItem item = identity.GetComponent<HeldItem>();
            if (item == null || !item.IsEquipped || item.IsStowed || item.IsBeingConsumedFromHand || item.ServerContainer != null)
            {
                return null;
            }
            return identity.connectionToClient == conn ? item : null;
        }

        public static bool Near(NetworkConnectionToClient conn, Transform target, float maxDistance)
        {
            return conn?.identity != null && target != null &&
                   (target.position - conn.identity.transform.position).sqrMagnitude <= maxDistance * maxDistance;
        }

        // Garrafas, cantis e baldes com água: primeiro o item da mão, depois a hotbar (inclui a mochila).
        public static List<LiquidContainerComponent> FindWaterContainers(NetworkIdentity player, Dictionary<uint, NetworkIdentity> spawned)
        {
            List<LiquidContainerComponent> result = new List<LiquidContainerComponent>();
            if (player == null)
            {
                return result;
            }
            EquipmentManager equipment = player.GetComponentInChildren<EquipmentManager>();
            if (equipment != null && equipment.EquippedEntity != null)
            {
                AddIfWater(equipment.EquippedEntity.GetComponent<LiquidContainerComponent>(), result);
            }
            HotbarManager hotbar = player.GetComponentInChildren<HotbarManager>();
            if (hotbar == null)
            {
                return result;
            }
            for (int i = 0; i < hotbar.SlotCount; i++)
            {
                uint netId = hotbar.GetSlotNetId(i);
                if (netId != 0 && spawned.TryGetValue(netId, out NetworkIdentity identity) && identity != null)
                {
                    AddIfWater(identity.GetComponent<LiquidContainerComponent>(), result);
                }
            }
            return result;
        }

        public static bool IsEmptyContainer(HeldItem item)
        {
            LiquidContainerComponent c = item != null ? item.GetComponent<LiquidContainerComponent>() : null;
            return c != null && (c.CurrentAmount <= 0.001f || c.CurrentLiquidType == LiquidType.Empty);
        }

        public static bool IsWaterContainer(HeldItem item)
        {
            LiquidContainerComponent c = item != null ? item.GetComponent<LiquidContainerComponent>() : null;
            return c != null && c.CurrentLiquidType == LiquidType.Water && c.CurrentAmount > 0f;
        }

        private static void AddIfWater(LiquidContainerComponent container, List<LiquidContainerComponent> result)
        {
            if (container != null && container.CurrentLiquidType == LiquidType.Water && container.CurrentAmount > 0f && !result.Contains(container))
            {
                result.Add(container);
            }
        }
    }
}
