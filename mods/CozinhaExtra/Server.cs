using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using Mirror;
using System.Collections;
using System.Linq;
using NomadDrive.Features.CoffeeBrewing;
using NomadDrive.Features.Consumables;
using NomadDrive.Features.Cooking;
using NomadDrive.Features.Interaction;
using NomadDrive.Features.LiquidTransferSystem;
using NomadDrive.Features.ObjectPlacement;
using NomadDrive.Features.SaveSystem;
using NomadDrive.Features.WorldGeneration.ObjectSpawning;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CozinhaExtra
{
    internal sealed class PotData
    {
        public float Water;
        public SoupType Soup;
        public int Raw;         // ingredientes frescos (pedem água)
        public int Canned;      // latas despejadas esquentando (não pedem água)
        public int Portions;
        public float Progress;
        public float EvapTimer;
        public string Guid;
        public bool Dirty = true;
        public float NextProgressSync;

        public bool IsEmpty => Water <= 0.0001f && Raw == 0 && Canned == 0 && Portions == 0;

        public int Pending => Raw + Canned;

        public CePotState ToMessage(uint netId) => new CePotState
        {
            Pot = netId,
            Water = Water,
            Soup = (byte)Soup,
            Raw = (byte)Raw,
            Portions = (byte)Mathf.Clamp(Portions, 0, 255),
            Progress = Progress,
            Evap = EvapTimer,
            Canned = (byte)Mathf.Clamp(Canned, 0, 255)
        };
    }

    // Tudo o que muda o mundo roda aqui, no host. Os clientes só pedem (CeRequest) e recebem o
    // estado das panelas (CePotState). Itens novos usam a criação de objetos normal do Mirror.
    internal static class Server
    {
        private const float MaxReach = 5f;
        private const float RequestCooldown = 0.2f;
        private const float SpawnCooldown = 0.8f;

        private static readonly Dictionary<uint, PotData> Pots = new Dictionary<uint, PotData>();
        private static readonly HashSet<int> ModdedConnections = new HashSet<int>();
        // Versão das mensagens que cada cliente entende (vem no Hello; 1.4.2 manda 0).
        private static readonly Dictionary<int, uint> ProtocolOf = new Dictionary<int, uint>();
        private static readonly List<int> GoneScratch = new List<int>();
        private static readonly Dictionary<int, float> LastRequest = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> LastSpawn = new Dictionary<int, float>();
        private static readonly List<uint> Scratch = new List<uint>();

        private static bool _registered;
        private static float _nextScan;
        private static float _nextFullSync;
        private static float _nextAbsorb;
        private static float _nextBeacon;
        private static readonly HashSet<int> Beaconed = new HashSet<int>();
        private static readonly List<int> BeaconScratch = new List<int>();
        private static readonly Dictionary<uint, float> PlacedSince = new Dictionary<uint, float>();
        private static readonly HashSet<uint> PlacedWarned = new HashSet<uint>();
        private static readonly List<uint> PlacedScratch = new List<uint>();

        public static void Tick()
        {
            if (!NetworkServer.active)
            {
                if (_registered)
                {
                    PotStore.Flush(Pots, force: true);
                    _registered = false;
                    Pots.Clear();
                    ModdedConnections.Clear();
                    ProtocolOf.Clear();
                    PlacedSince.Clear();
                    Beaconed.Clear();
                    PlacedWarned.Clear();
                    LastRequest.Clear();
                    LastSpawn.Clear();
                }
                return;
            }
            // O Mirror limpa os handlers quando o servidor desliga, então registra de novo a cada partida.
            if (!_registered)
            {
                NetworkServer.ReplaceHandler<CeRequest>(OnRequest);
                _registered = true;
                PotStore.Load();
            }

            float now = Time.unscaledTime;
            if (now >= _nextScan)
            {
                _nextScan = now + 2f;
                Scan();
            }

            Cook(Time.deltaTime, now);
            if (now >= _nextBeacon)
            {
                _nextBeacon = now + 1f;
                SendBeacons();
            }
            if (now >= _nextAbsorb)
            {
                _nextAbsorb = now + 0.5f;
                AbsorbPlacedIngredients(now);
            }

            foreach (KeyValuePair<uint, PotData> pair in Pots)
            {
                if (pair.Value.Dirty)
                {
                    pair.Value.Dirty = false;
                    Broadcast(pair.Value.ToMessage(pair.Key));
                    PotStore.Remember(pair.Value);
                }
            }

            if (now >= _nextFullSync)
            {
                _nextFullSync = now + 3f;
                foreach (KeyValuePair<uint, PotData> pair in Pots)
                {
                    Broadcast(pair.Value.ToMessage(pair.Key));
                }
            }

            PotStore.Flush(Pots, force: false);
        }

        // Tira panelas que sumiram e restaura as que voltaram de um save (pelo GUID do jogo).
        private static void Scan()
        {
            Scratch.Clear();
            foreach (uint netId in Pots.Keys)
            {
                if (!NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity identity) || identity == null)
                {
                    Scratch.Add(netId);
                }
            }
            foreach (uint netId in Scratch)
            {
                Pots.Remove(netId);
            }

            foreach (Pan pan in Object.FindObjectsByType<Pan>(FindObjectsSortMode.None))
            {
                if (pan == null || pan.netId == 0 || Pots.ContainsKey(pan.netId) || Game.AsCookingPot(pan) == null)
                {
                    continue;
                }
                string guid = GuidOf(pan);
                if (guid != null && PotStore.TryRestore(guid, out PotData restored))
                {
                    Pots[pan.netId] = restored;
                }
            }
        }

        private static void Cook(float dt, float now)
        {
            if (dt <= 0f)
            {
                return;
            }
            float evapSeconds = Cfg.EvaporationSeconds.Value;
            foreach (KeyValuePair<uint, PotData> pair in Pots)
            {
                PotData pot = pair.Value;
                if (pot.Pending <= 0 && pot.Portions <= 0)
                {
                    continue;
                }
                if (!NetworkServer.spawned.TryGetValue(pair.Key, out NetworkIdentity identity) || identity == null)
                {
                    continue;
                }
                Pan pan = identity.GetComponent<Pan>();
                // ReadyForCooking = o próprio jogo diz que a panela está sobre fogo aceso.
                bool heated = pan != null && pan.ReadyForCooking;

                // Sopa pronta esquecida no fogo vai evaporando: perde 1 porção a cada EvaporacaoSegundos.
                if (heated && pot.Portions > 0 && evapSeconds > 0f)
                {
                    pot.EvapTimer += dt;
                    if (pot.EvapTimer >= evapSeconds)
                    {
                        SoupType evaporated = pot.Soup;
                        pot.EvapTimer = 0f;
                        TakePortion(pot);
                        BroadcastResult(CeFeedback.Evaporated, evaporated, identity.transform);
                    }
                    else if (now >= pot.NextProgressSync)
                    {
                        pot.NextProgressSync = now + 1f;
                        pot.Dirty = true;
                    }
                }
                if (pot.Portions <= 0 && pot.EvapTimer > 0f)
                {
                    pot.EvapTimer = 0f;
                    pot.Dirty = true;
                }

                // Só os frescos pedem água; latas só esquentam (mais rápido, se não houver fresco junto).
                float needed = pot.Raw * Cfg.WaterPerIngredient.Value;
                if (pot.Pending <= 0 || !heated || pot.Water + 0.0001f < needed)
                {
                    continue;
                }
                pot.Progress += dt / CookSecondsFor(pot);
                if (pot.Progress >= 1f)
                {
                    pot.Portions += pot.Raw * Mathf.Max(1, Cfg.PortionsPerIngredient.Value) +
                                    pot.Canned * Mathf.Max(1, Cfg.PortionsPerCan.Value);
                    pot.Water = Mathf.Max(0f, pot.Water - needed);
                    pot.Raw = 0;
                    pot.Canned = 0;
                    pot.Progress = 0f;
                    pot.Dirty = true;
                    Plugin.Log.LogInfo($"Sopa pronta na panela {pair.Key}: {pot.Soup}, {pot.Portions} porções");
                }
                else if (now >= pot.NextProgressSync)
                {
                    pot.NextProgressSync = now + 1f;
                    pot.Dirty = true;
                }
            }
        }

        private static float CookSecondsFor(PotData pot) =>
            Mathf.Max(1f, pot.Raw > 0 ? Cfg.CookSeconds.Value : Cfg.CanHeatSeconds.Value);

        // Põe um ingrediente na panela (já validado). O primeiro fresco depois de latas passa a valer
        // o tempo da sopa: o progresso é reescalado para não virar sopa em segundos.
        private static void AddToPot(PotData pot, SoupType soup, bool canned)
        {
            if (!canned && pot.Raw == 0 && pot.Canned > 0 && pot.Progress > 0f)
            {
                pot.Progress *= Mathf.Max(1f, Cfg.CanHeatSeconds.Value) / Mathf.Max(1f, Cfg.CookSeconds.Value);
            }
            if (canned)
            {
                pot.Canned++;
            }
            else
            {
                pot.Raw++;
            }
            pot.Soup = soup;
            pot.Dirty = true;
        }

        private static void OnRequest(NetworkConnectionToClient conn, CeRequest msg)
        {
            if (conn == null || conn.identity == null)
            {
                return;
            }
            // Antes do SendAll, para ele já saber se pode mandar as canecas coloridas.
            if (msg.Action == (byte)CeAction.Hello)
            {
                ProtocolOf[conn.connectionId] = msg.Target;
            }
            bool firstContact = ModdedConnections.Add(conn.connectionId);
            if (firstContact || msg.Action == (byte)CeAction.Hello)
            {
                SendAll(conn);
            }
            if (msg.Action == (byte)CeAction.Hello)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (LastRequest.TryGetValue(conn.connectionId, out float last) && now - last < RequestCooldown)
            {
                return;
            }
            LastRequest[conn.connectionId] = now;

            try
            {
                switch ((CeAction)msg.Action)
                {
                    case CeAction.AddWater:
                        AddWater(conn, msg.Target);
                        break;
                    case CeAction.AddIngredient:
                        AddIngredient(conn, msg.Target, msg.Held);
                        break;
                    case CeAction.EatFromPot:
                        EatFromPot(conn, msg.Target);
                        break;
                    case CeAction.ServeToPlate:
                        Serve(conn, msg.Target, msg.Held);
                        break;
                    case CeAction.TakePlate:
                        TakePlate(conn, msg.Target);
                        break;
                    case CeAction.ServeFromPot:
                        ServeFromPot(conn, msg.Target, msg.Held);
                        break;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Erro ao tratar pedido {msg.Action} de {conn}: {e}");
            }
        }

        private static bool TryGetPot(NetworkConnectionToClient conn, uint netId, out Pan pan, out PotData data)
        {
            pan = null;
            data = null;
            if (!NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity identity) || identity == null)
            {
                return false;
            }
            pan = Game.AsCookingPot(identity);
            if (pan == null)
            {
                return false;
            }
            if (!Game.Near(conn, pan.transform, MaxReach))
            {
                Reply(conn, CeFeedback.TooFar);
                return false;
            }
            if (!Pots.TryGetValue(netId, out data))
            {
                data = new PotData { Guid = GuidOf(pan) };
                Pots[netId] = data;
            }
            return true;
        }

        private static void AddWater(NetworkConnectionToClient conn, uint potId)
        {
            if (!TryGetPot(conn, potId, out _, out PotData pot))
            {
                return;
            }
            float room = Cfg.PotCapacity.Value - pot.Water;
            if (room <= 0.001f)
            {
                Reply(conn, CeFeedback.PotFull);
                return;
            }
            List<LiquidContainerComponent> containers = Game.FindWaterContainers(conn.identity, NetworkServer.spawned);
            if (containers.Count == 0)
            {
                Reply(conn, CeFeedback.NoWater);
                return;
            }
            float wanted = Mathf.Min(room, Cfg.WaterPerPour.Value);
            float used = 0f;
            foreach (LiquidContainerComponent container in containers)
            {
                float remaining = wanted - used;
                if (remaining <= 0f)
                {
                    break;
                }
                float take = Mathf.Min(container.CurrentAmount, remaining);
                // No servidor, Drain devolve o que faltou drenar (0 quando tirou tudo).
                used += take - container.Drain(take);
            }
            if (used <= 0f)
            {
                Reply(conn, CeFeedback.NoWater);
                return;
            }
            pot.Water += used;
            pot.Dirty = true;
            Reply(conn, CeFeedback.WaterAdded);
        }

        private static void AddIngredient(NetworkConnectionToClient conn, uint potId, uint heldId)
        {
            HeldItem held = Game.ServerHeld(conn, heldId);
            if (held == null || !Recipes.TryIngredient(held.gameObject, out SoupType soup, out bool canned) ||
                !TryGetPot(conn, potId, out _, out PotData pot))
            {
                return;
            }
            if (Game.IsRotten(held.gameObject))
            {
                Reply(conn, CeFeedback.Rotten, soup);
                return;
            }
            if (pot.Soup != SoupType.None && pot.Soup != soup)
            {
                Reply(conn, CeFeedback.OtherSoup, pot.Soup);
                return;
            }
            if (pot.Pending >= Mathf.Max(1, Cfg.MaxIngredients.Value))
            {
                Reply(conn, CeFeedback.PotFull);
                return;
            }
            // Mesmo caminho que o jogo usa para gastar uma chave no pé de cabra: tira da mão e destrói.
            // A lata some junto (o jogo não tem lata vazia).
            held.ServerConsumeFromHand();
            AddToPot(pot, soup, canned);
            Reply(conn, canned ? CeFeedback.CanPoured : CeFeedback.IngredientAdded, soup);
        }

        private static void EatFromPot(NetworkConnectionToClient conn, uint potId)
        {
            if (!TryGetPot(conn, potId, out _, out PotData pot))
            {
                return;
            }
            if (pot.Portions <= 0)
            {
                Reply(conn, CeFeedback.PotEmpty);
                return;
            }
            SoupType soup = pot.Soup;
            TakePortion(pot);
            Reply(conn, CeFeedback.Ate, soup);
        }

        private static void Serve(NetworkConnectionToClient conn, uint potId, uint heldId)
        {
            HeldItem held = Game.ServerHeld(conn, heldId);
            CePlate plate = held != null ? held.GetComponent<CePlate>() : null;
            bool isPlate = plate != null && plate.IsEmptyPlate;
            bool isMug = held != null && held.GetComponent<Mug>() != null && Items.MugsReady;
            if ((!isPlate && !isMug) || !Items.Ready || !TryGetPot(conn, potId, out _, out PotData pot))
            {
                return;
            }
            if (isMug && !Game.IsEmptyContainer(held))
            {
                Reply(conn, CeFeedback.MugNotEmpty);
                return;
            }
            if (pot.Portions <= 0 || Recipes.Get(pot.Soup) == null)
            {
                Reply(conn, CeFeedback.PotEmpty);
                return;
            }
            GameObject template = isMug ? Items.FilledMug(pot.Soup) : Items.FilledPlate(pot.Soup);
            if (template == null)
            {
                ReplyPlateOnly(conn, pot.Soup, isMug);
                return;
            }
            SoupType soup = pot.Soup;
            // Caneca de outro mod (ex.: colorida): lida antes de a caneca sumir da mão.
            uint sourceAsset = 0;
            string sourceGuid = null;
            if (isMug)
            {
                MugSkins.TryReadSource(held, out sourceAsset, out sourceGuid);
            }
            held.ServerConsumeFromHand();
            TakePortion(pot);
            SpawnIntoHand(template, conn, null, sourceAsset, sourceGuid);
            Reply(conn, CeFeedback.Served, soup);
        }

        // Segurando a panela, servir num prato/caneca vazio que está em algum lugar (mesa, chão, prateleira).
        private static void ServeFromPot(NetworkConnectionToClient conn, uint potId, uint containerId)
        {
            HeldItem potItem = Game.ServerHeld(conn, potId);
            Pan pan = potItem != null ? Game.AsCookingPot(potItem) : null;
            if (pan == null || !Items.Ready)
            {
                return;
            }
            if (!Pots.TryGetValue(pan.netId, out PotData pot) || pot.Portions <= 0 || Recipes.Get(pot.Soup) == null)
            {
                Reply(conn, CeFeedback.PotEmpty);
                return;
            }
            if (!NetworkServer.spawned.TryGetValue(containerId, out NetworkIdentity identity) || identity == null)
            {
                return;
            }
            HeldItem container = identity.GetComponent<HeldItem>();
            if (container == null || container.IsEquipped || container.IsStowed || container.ServerContainer != null || container.IsBeingConsumedFromHand)
            {
                return;
            }
            if (!Game.Near(conn, container.transform, MaxReach))
            {
                Reply(conn, CeFeedback.TooFar);
                return;
            }
            CePlate plate = container.GetComponent<CePlate>();
            bool isPlate = plate != null && plate.IsEmptyPlate;
            bool isMug = container.GetComponent<Mug>() != null && Items.MugsReady;
            if (!isPlate && !isMug)
            {
                return;
            }
            if (isMug && !Game.IsEmptyContainer(container))
            {
                Reply(conn, CeFeedback.MugNotEmpty);
                return;
            }
            GameObject template = isMug ? Items.FilledMug(pot.Soup) : Items.FilledPlate(pot.Soup);
            if (template == null)
            {
                ReplyPlateOnly(conn, pot.Soup, isMug);
                return;
            }
            SoupType soup = pot.Soup;
            // O recipiente vazio tem o pivô na base; o cheio (feito da lata), no centro.
            float lift = isMug ? Items.MugHeight / 2f : MeshFactory.PlateHeight / 2f;
            Vector3 position = container.transform.position + Vector3.up * (lift + 0.004f);
            Quaternion rotation = Quaternion.Euler(0f, container.transform.eulerAngles.y, 0f);
            // Se o recipiente estava encaixado (prateleira, mesa do veículo...), o novo vai para o mesmo lugar.
            PlacementNetworkData placement = container.PlacementNetworkData;
            SnappingPlane plane = null;
            if (placement.ParentNetworkID != 0)
            {
                SnappingPlanesManager.TryResolvePlane(placement.ParentNetworkID, placement.PlaneId, out plane);
            }
            // Caneca de outro mod (ex.: colorida): lida antes de a caneca ser destruída.
            uint sourceAsset = 0;
            string sourceGuid = null;
            if (isMug)
            {
                MugSkins.TryReadSource(container, out sourceAsset, out sourceGuid);
            }
            Unplace(container.netId);
            NetworkServer.Destroy(container.gameObject);
            TakePortion(pot);

            GameObject go = Object.Instantiate(template, position, rotation);
            go.name = template.name;
            MugSkins.SetSource(go, sourceAsset, sourceGuid);
            NetworkServer.Spawn(go, NetworkServer.localConnection);
            LootSpawningService.ApplyPostSpawnWorldIntegration(go, Items.SaveKeyWithSource(template, sourceAsset, sourceGuid));
            SendMugSkin(go);
            if (go.TryGetComponent(out Food food))
            {
                food.ServerSetFreshMax();
            }
            Runner.Instance?.StartCoroutine(SettleSoon(go.GetComponent<HeldItem>(), plane, position, rotation, 0f));
            Reply(conn, CeFeedback.Served, soup);
        }

        // Receita que não vai em caneca. Cliente 1.4.x não conhece o aviso novo: recebe "sem sopa".
        private static void ReplyPlateOnly(NetworkConnectionToClient conn, SoupType soup, bool isMug)
        {
            if (!isMug)
            {
                return;
            }
            bool knows = conn == NetworkServer.localConnection ||
                         (ProtocolOf.TryGetValue(conn.connectionId, out uint protocol) && protocol >= Net.ProtocolRecipes);
            Reply(conn, knows ? CeFeedback.PlateOnly : CeFeedback.PotEmpty, soup);
        }

        // Tira o item de qualquer superfície de encaixe antes de destruí-lo, para ela não ficar "ocupada".
        private static void Unplace(uint itemNetId)
        {
            foreach (SnappingPlanesManager manager in Object.FindObjectsByType<SnappingPlanesManager>(FindObjectsSortMode.None))
            {
                if (manager != null)
                {
                    manager.ServerRemovePlaced(itemNetId);
                }
            }
        }

        // Ingrediente fresco (tomate, batata, cogumelo) colocado na panela pelo modo de colocação vira ingrediente.
        private static void AbsorbPlacedIngredients(float now)
        {
            foreach (Pan pan in PotPatcher.Pots)
            {
                if (pan == null || pan.netId == 0)
                {
                    continue;
                }
                SnappingPlanesManager manager = pan.GetComponent<SnappingPlanesManager>();
                if (manager == null || manager.PlacedItems.Count == 0)
                {
                    continue;
                }
                PlacedScratch.Clear();
                foreach (uint id in manager.PlacedItems.Keys)
                {
                    PlacedScratch.Add(id);
                }
                foreach (uint id in PlacedScratch)
                {
                    if (!NetworkServer.spawned.TryGetValue(id, out NetworkIdentity identity) || identity == null)
                    {
                        continue;
                    }
                    // Latas só entram pela tecla (despejar); se alguma superfície aceitar, ficam como item normal.
                    if (!Recipes.TryIngredient(identity.gameObject, out SoupType soup, out bool canned) || canned)
                    {
                        continue;
                    }
                    // Espera o jogo terminar de encaixar (os comandos de encaixe chegam em sequência).
                    if (!PlacedSince.TryGetValue(id, out float since))
                    {
                        PlacedSince[id] = now;
                        continue;
                    }
                    if (now - since < 0.6f)
                    {
                        continue;
                    }
                    if (!Pots.TryGetValue(pan.netId, out PotData pot))
                    {
                        pot = new PotData { Guid = GuidOf(pan) };
                        Pots[pan.netId] = pot;
                    }
                    CeFeedback? problem = null;
                    if (Game.IsRotten(identity.gameObject))
                    {
                        problem = CeFeedback.Rotten;
                    }
                    else if (pot.Soup != SoupType.None && pot.Soup != soup)
                    {
                        problem = CeFeedback.OtherSoup;
                    }
                    else if (pot.Pending >= Mathf.Max(1, Cfg.MaxIngredients.Value))
                    {
                        problem = CeFeedback.PotFull;
                    }
                    if (problem != null)
                    {
                        // Fica encaixado como item normal; avisa uma vez quem está perto.
                        if (PlacedWarned.Add(id))
                        {
                            BroadcastResult(problem.Value, problem == CeFeedback.OtherSoup ? pot.Soup : soup, pan.transform);
                        }
                        continue;
                    }
                    manager.ServerRemovePlaced(id);
                    NetworkServer.Destroy(identity.gameObject);
                    PlacedSince.Remove(id);
                    PlacedWarned.Remove(id);
                    AddToPot(pot, soup, false);
                    BroadcastResult(CeFeedback.IngredientAdded, soup, pan.transform);
                }
            }
        }

        private static void TakePlate(NetworkConnectionToClient conn, uint stoveId)
        {
            if (!Cfg.PlatesFromStove.Value || !Items.Ready)
            {
                return;
            }
            if (!NetworkServer.spawned.TryGetValue(stoveId, out NetworkIdentity identity) || identity == null)
            {
                return;
            }
            Stove stove = identity.GetComponentInParent<Stove>();
            if (stove == null)
            {
                return;
            }
            if (!Game.Near(conn, stove.transform, MaxReach))
            {
                Reply(conn, CeFeedback.TooFar);
                return;
            }
            float now = Time.unscaledTime;
            if (LastSpawn.TryGetValue(conn.connectionId, out float last) && now - last < SpawnCooldown)
            {
                return;
            }
            LastSpawn[conn.connectionId] = now;
            SpawnIntoHand(Items.EmptyPlate, conn);
            Reply(conn, CeFeedback.PlateTaken);
        }

        // Chamado pelo patch do "comer": devolve o prato vazio ou a caneca (a mesma de antes, se veio de outro mod).
        public static void GiveContainerBack(NetworkConnectionToClient conn, bool mug, uint sourceAsset, string sourceGuid)
        {
            if (!NetworkServer.active || !Items.Ready || conn?.identity == null)
            {
                return;
            }
            if (mug)
            {
                // A caneca devolvida passa pelo gancho de loot; não sorteia prato do lado dela.
                bool wasSuppressed = LootPlates.Suppressed;
                LootPlates.Suppressed = true;
                try
                {
                    // Caneca de outro mod: o mesmo prefab, com o GUID de save dela (o mod dela restaura).
                    if (sourceAsset != 0 && MugSkins.TryGetPrefab(sourceAsset, out GameObject sourcePrefab))
                    {
                        string sourceSave = !string.IsNullOrEmpty(sourceGuid) ? sourceGuid : MugSkins.PersistentGuidOf(sourcePrefab);
                        SpawnIntoHand(sourcePrefab, conn, sourceSave);
                        return;
                    }
                    // A caneca é um item do jogo: volta sempre e entra no save como loot normal.
                    LootRegistry registry = Resources.FindObjectsOfTypeAll<LootRegistry>().FirstOrDefault();
                    string guid = registry != null ? registry.GetByAddress(Items.MugKey)?.assetGuid : null;
                    SpawnIntoHand(Items.MugPrefab, conn, guid);
                }
                finally
                {
                    LootPlates.Suppressed = wasSuppressed;
                }
                return;
            }
            if (Cfg.PlateBackAfterEating.Value)
            {
                SpawnIntoHand(Items.EmptyPlate, conn);
            }
        }

        private static void TakePortion(PotData pot)
        {
            pot.Portions--;
            if (pot.Portions <= 0)
            {
                pot.Portions = 0;
                if (pot.Pending == 0)
                {
                    pot.Soup = SoupType.None;
                }
            }
            pot.Dirty = true;
        }

        // Cria o item na frente do jogador e pede para o cliente dele pegar (o "pegar" é sempre do cliente).
        private static GameObject SpawnIntoHand(GameObject template, NetworkConnectionToClient conn, string persistentGuid = null,
            uint sourceAsset = 0, string sourceGuid = null)
        {
            if (template == null || conn?.identity == null)
            {
                return null;
            }
            Transform player = conn.identity.transform;
            Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }
            Vector3 position = player.position + forward * 0.45f + Vector3.up * 1.1f;
            GameObject go = Object.Instantiate(template, position, Quaternion.LookRotation(forward));
            go.name = template.name;
            // Prefab de outro mod pode estar guardado desativado (o Nomad Items faz o mesmo ao criar os dele).
            go.SetActive(true);
            MugSkins.SetSource(go, sourceAsset, sourceGuid);
            NetworkServer.Spawn(go, NetworkServer.localConnection);
            // Mesmo ajuste que o jogo faz no loot (raiz do mundo; registro no save se tiver guid).
            LootSpawningService.ApplyPostSpawnWorldIntegration(go, persistentGuid ?? Items.SaveKeyWithSource(template, sourceAsset, sourceGuid));
            if (go.TryGetComponent(out Food food))
            {
                food.ServerSetFreshMax();
            }
            SendMugSkin(go);
            conn.Send(new CeEquip { Item = go.GetComponent<NetworkIdentity>().netId });
            // Se ninguém pegar (mão ocupada, por exemplo), assenta onde caiu, preso ao veículo se for o caso.
            Runner.Instance?.StartCoroutine(SettleSoon(go.GetComponent<HeldItem>(), null, Vector3.zero, Quaternion.identity, 2.5f));
            return go;
        }

        // Itens que o mod cria já no lugar precisam "assentar" como o jogo faz ao soltar um item:
        // é isso que prende o item no veículo (senão ele fica para trás quando o veículo anda).
        private static IEnumerator SettleSoon(HeldItem item, SnappingPlane plane, Vector3 position, Quaternion rotation, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
            else
            {
                yield return null;
            }
            if (item == null || !NetworkServer.active || item.IsEquipped || item.IsStowed || item.ServerContainer != null)
            {
                yield break;
            }
            if (delay > 0f)
            {
                // Item que caiu sozinho: usa onde ele parou, se ainda não estiver preso em nada.
                if (item.PlacementNetworkData.ParentNetworkID != 0)
                {
                    yield break;
                }
                position = item.transform.position;
                rotation = item.transform.rotation;
            }
            try
            {
                if (plane != null)
                {
                    plane.ServerRestorePlacementAtWorldPose(item, position, rotation);
                    yield break;
                }
                Transform surface = SurfaceBelow(item, position);
                if (surface != null)
                {
                    item.ServerRestRefusedPlaneDrop(surface, position, rotation);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Não consegui assentar '{item.name}': {e.Message}");
            }
        }

        private static Transform SurfaceBelow(HeldItem item, Vector3 position)
        {
            RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 0.05f, Vector3.down, 0.6f, ~0, QueryTriggerInteraction.Ignore);
            Transform best = null;
            float bestDistance = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(item.transform) || hit.distance >= bestDistance)
                {
                    continue;
                }
                best = hit.collider.transform;
                bestDistance = hit.distance;
            }
            return best;
        }

        // Avisa cada jogador que entrou que este host tem o mod (inofensivo para quem não tem).
        private static void SendBeacons()
        {
            BeaconScratch.Clear();
            foreach (int id in Beaconed)
            {
                if (!NetworkServer.connections.ContainsKey(id))
                {
                    BeaconScratch.Add(id);
                }
            }
            foreach (int id in BeaconScratch)
            {
                Beaconed.Remove(id);
            }
            // Quem saiu deixa de contar como "tem o mod": o connectionId pode ser reaproveitado por outra pessoa.
            GoneScratch.Clear();
            foreach (int id in ModdedConnections)
            {
                if (!NetworkServer.connections.ContainsKey(id))
                {
                    GoneScratch.Add(id);
                }
            }
            foreach (int id in GoneScratch)
            {
                ModdedConnections.Remove(id);
                ProtocolOf.Remove(id);
                LastRequest.Remove(id);
                LastSpawn.Remove(id);
            }
            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                if (conn != null && conn != NetworkServer.localConnection && conn.isReady && Beaconed.Add(conn.connectionId))
                {
                    conn.Send(new ObjectHideMessage { netId = Net.BeaconNetId });
                }
            }
        }

        private static void Reply(NetworkConnectionToClient conn, CeFeedback code, SoupType soup = SoupType.None)
        {
            conn?.Send(new CeResult { Code = (byte)code, Soup = (byte)soup });
        }

        private static void BroadcastResult(CeFeedback code, SoupType soup, Transform near)
        {
            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                if (conn != null && (conn == NetworkServer.localConnection || ModdedConnections.Contains(conn.connectionId)) &&
                    Game.Near(conn, near, 8f))
                {
                    Reply(conn, code, soup);
                }
            }
        }

        private static void SendAll(NetworkConnectionToClient conn)
        {
            foreach (KeyValuePair<uint, PotData> pair in Pots)
            {
                conn.Send(pair.Value.ToMessage(pair.Key));
            }
            // Todas as canecas de sopa coloridas, inclusive as que esta pessoa ainda não está vendo.
            if (!KnowsMugSkins(conn))
            {
                return;
            }
            foreach (CePlate plate in CePlate.Live)
            {
                if (plate == null || !plate.IsMug || plate.SourceAssetId == 0)
                {
                    continue;
                }
                NetworkIdentity identity = plate.GetComponent<NetworkIdentity>();
                if (identity != null && identity.netId != 0)
                {
                    conn.Send(new CeMugSkin { Item = identity.netId, Source = plate.SourceAssetId });
                }
            }
        }

        private static bool KnowsMugSkins(NetworkConnectionToClient conn)
        {
            return conn != null && conn != NetworkServer.localConnection &&
                   ProtocolOf.TryGetValue(conn.connectionId, out uint protocol) && protocol >= Net.ProtocolMugSkin;
        }

        // Conta a todos que entendem de qual caneca veio esta caneca de sopa (o host já aplicou no dele).
        internal static void SendMugSkin(GameObject soupMug)
        {
            CePlate plate = soupMug != null ? soupMug.GetComponent<CePlate>() : null;
            NetworkIdentity identity = soupMug != null ? soupMug.GetComponent<NetworkIdentity>() : null;
            if (plate == null || plate.SourceAssetId == 0 || identity == null || identity.netId == 0)
            {
                return;
            }
            CeMugSkin msg = new CeMugSkin { Item = identity.netId, Source = plate.SourceAssetId };
            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                if (KnowsMugSkins(conn))
                {
                    conn.Send(msg);
                }
            }
        }

        // Só manda para quem tem o mod (mensagem desconhecida faz o Mirror desconectar a pessoa).
        private static void Broadcast(CePotState msg)
        {
            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                if (conn != null && (conn == NetworkServer.localConnection || ModdedConnections.Contains(conn.connectionId)))
                {
                    conn.Send(msg);
                }
            }
        }

        private static string GuidOf(Component c)
        {
            PersistentId id = c != null ? c.GetComponent<PersistentId>() : null;
            return id != null && id.HasGuid ? id.Guid : null;
        }
    }

    // Guarda o conteúdo das panelas entre sessões, pelo GUID que o save do jogo dá a cada panela.
    // Arquivo: BepInEx\config\CozinhaExtra.panelas.txt (só no PC de quem hospeda).
    internal static class PotStore
    {
        private static readonly Dictionary<string, PotData> Saved = new Dictionary<string, PotData>();
        private static bool _loaded;
        private static bool _changed;
        private static float _nextWrite;

        private static string FilePath => Path.Combine(Paths.ConfigPath, "CozinhaExtra.panelas.txt");

        public static void Load()
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;
            try
            {
                if (!File.Exists(FilePath))
                {
                    return;
                }
                foreach (string raw in File.ReadAllText(FilePath).Split('\n'))
                {
                    string[] f = raw.Trim().Split('|');
                    if (f.Length < 6 || f[0].StartsWith("#"))
                    {
                        continue;
                    }
                    PotData stored = new PotData
                    {
                        Guid = f[0],
                        Water = float.Parse(f[1], CultureInfo.InvariantCulture),
                        Soup = (SoupType)byte.Parse(f[2], CultureInfo.InvariantCulture),
                        Raw = int.Parse(f[3], CultureInfo.InvariantCulture),
                        Portions = int.Parse(f[4], CultureInfo.InvariantCulture),
                        Progress = float.Parse(f[5], CultureInfo.InvariantCulture),
                        // 1.5.0: 7º campo opcional (a 1.4.x grava 6 e lê só os 6 primeiros).
                        Canned = f.Length >= 7 ? int.Parse(f[6], CultureInfo.InvariantCulture) : 0
                    };
                    // Receita que esta versão não conhece (ex.: voltou de uma versão mais nova): descarta.
                    if (stored.Soup != SoupType.None && Recipes.Get(stored.Soup) == null)
                    {
                        Plugin.Log.LogWarning($"Panela {f[0]}: receita {(byte)stored.Soup} desconhecida nesta versão; conteúdo descartado.");
                        continue;
                    }
                    Saved[f[0]] = stored;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Não consegui ler {FilePath}: {e.Message}");
            }
        }

        public static bool TryRestore(string guid, out PotData data)
        {
            data = null;
            if (!Saved.TryGetValue(guid, out PotData stored) || stored.IsEmpty)
            {
                return false;
            }
            data = new PotData
            {
                Guid = guid,
                Water = stored.Water,
                Soup = stored.Soup,
                Raw = stored.Raw,
                Canned = stored.Canned,
                Portions = stored.Portions,
                Progress = stored.Progress
            };
            return true;
        }

        public static void Remember(PotData pot)
        {
            if (string.IsNullOrEmpty(pot.Guid))
            {
                return;
            }
            if (pot.IsEmpty)
            {
                _changed |= Saved.Remove(pot.Guid);
                return;
            }
            Saved[pot.Guid] = pot;
            _changed = true;
        }

        public static void Flush(Dictionary<uint, PotData> live, bool force)
        {
            float now = Time.unscaledTime;
            if (!_changed || (!force && now < _nextWrite))
            {
                return;
            }
            _nextWrite = now + 5f;
            _changed = false;
            try
            {
                StringBuilder sb = new StringBuilder("# Cozinha Extra: conteúdo das panelas (guid|agua|sopa|cruas|porcoes|progresso|latas)\n");
                foreach (PotData pot in Saved.Values)
                {
                    sb.Append(pot.Guid).Append('|')
                        .Append(pot.Water.ToString("0.###", CultureInfo.InvariantCulture)).Append('|')
                        .Append(((byte)pot.Soup).ToString(CultureInfo.InvariantCulture)).Append('|')
                        .Append(pot.Raw.ToString(CultureInfo.InvariantCulture)).Append('|')
                        .Append(pot.Portions.ToString(CultureInfo.InvariantCulture)).Append('|')
                        .Append(pot.Progress.ToString("0.###", CultureInfo.InvariantCulture)).Append('|')
                        .Append(pot.Canned.ToString(CultureInfo.InvariantCulture)).Append('\n');
                }
                File.WriteAllText(FilePath, sb.ToString());
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Não consegui gravar {FilePath}: {e.Message}");
            }
        }
    }
}
