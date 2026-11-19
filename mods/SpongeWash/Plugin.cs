using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Mirror;
using NomadDrive.Features.Hotbar;
using NomadDrive.Features.Interaction;
using NomadDrive.Features.LiquidTransferSystem;
using NomadDrive.Features.Player;
using NomadDrive.Features.Restoration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpongeWash
{
    // Pedido do cliente para o host: "lave esta esponja com a água da minha hotbar".
    // O host valida tudo e aplica; a condição da esponja e o nível das garrafas já
    // são SyncVars do jogo, então o resultado chega aos dois jogadores sozinho.
    public struct SpongeWashRequest : NetworkMessage
    {
        public uint SpongeNetId;
    }

    // Chamado pelo NomadModLoader quando a primeira cena carrega, ou pelo BepInExEntry.
    public static class ModEntry
    {
        private static bool _initialized;

        public static void Init()
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;
            GameObject host = new GameObject("SpongeWash");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<Plugin>();
        }
    }

    public class Plugin : MonoBehaviour
    {
        public const string Name = "Sponge Wash";
        public const string Version = "1.2.0";

        // Distância máxima entre o jogador e a esponja para o host aceitar o pedido.
        private const float MaxWashDistance = 5f;

        // De quanto em quanto tempo procura baldes novos para aplicar os multiplicadores.
        private const float BucketScanInterval = 2f;

        // De quanto em quanto tempo reavalia se o hint "Lavar esponja [K]" deve aparecer.
        private const float HintRefreshInterval = 0.1f;

        internal static Key WashKey = Key.K;
        internal static float WaterPerFullClean = 1f;
        internal static float BucketSpeedMultiplier = 3f;
        internal static float BucketWaterMultiplier = 0.5f;

        private static readonly FieldInfo BucketCleaningSpeed =
            typeof(Bucket).GetField("_cleaningSpeed", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo BucketWaterPerConditionUnit =
            typeof(Bucket).GetField("_waterPerConditionUnit", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly HashSet<Bucket> _tunedBuckets = new HashSet<Bucket>();
        private readonly ActionHint _hint = new ActionHint();
        private float _nextBucketScan;
        private float _nextHintRefresh;
        private bool _serverHandlerRegistered;
        private string _toastText;
        private float _toastUntil;
        private GUIStyle _toastStyle;

        private void Awake()
        {
            LoadConfig();

            // O weaver do Mirror não roda neste DLL, então o serializador da mensagem é registrado à mão.
            Writer<SpongeWashRequest>.write = (writer, msg) => writer.WriteUInt(msg.SpongeNetId);
            Reader<SpongeWashRequest>.read = reader => new SpongeWashRequest { SpongeNetId = reader.ReadUInt() };

            Debug.Log($"[{Name}] {Version} carregado. Tecla de lavar: {WashKey}");
        }

        // Mods\config\SpongeWash.cfg, no formato chave = valor. Criado com os padrões se não existir.
        private static void LoadConfig()
        {
            // NomadModLoader: Mods\plugins -> Mods\config. BepInEx: BepInEx\plugins -> BepInEx\config.
            string pluginsDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
            while (pluginsDir != null && !string.Equals(Path.GetFileName(pluginsDir), "plugins", StringComparison.OrdinalIgnoreCase))
            {
                pluginsDir = Path.GetDirectoryName(pluginsDir);
            }
            string baseDir = pluginsDir != null ? Path.GetDirectoryName(pluginsDir) : Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
            string configPath = Path.Combine(baseDir, "config", "SpongeWash.cfg");
            try
            {
                if (!File.Exists(configPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                    File.WriteAllText(configPath,
                        "# Sponge Wash - edit and restart the game / mude os valores e reabra o jogo.\n" +
                        "\n" +
                        "# Key to wash the sponge (in hand or aimed at) with water from your hotbar.\n" +
                        "# Tecla para lavar a esponja (na mão ou na mira) com a água da hotbar.\n" +
                        "# Unity Input System key names: K, L, G, F5, Numpad1...\n" +
                        "TeclaLavar = K\n" +
                        "\n" +
                        "# Water used to take the sponge from 0% to 100% (the game's bucket uses 1).\n" +
                        "# Água gasta para levar a esponja de 0% a 100% (o balde do jogo usa 1).\n" +
                        "AguaPorLimpezaCompleta = 1\n" +
                        "\n" +
                        "# Bucket, applies to the host. 1 = vanilla. / Balde, vale para quem hospeda. 1 = original.\n" +
                        "BaldeMultiplicadorVelocidade = 3\n" +
                        "BaldeMultiplicadorConsumoAgua = 0.5\n");
                }
                // File.ReadAllLines foi removido do mscorlib do jogo; ReadAllText existe.
                foreach (string rawLine in File.ReadAllText(configPath).Split('\n'))
                {
                    string line = rawLine.Trim();
                    int eq = line.IndexOf('=');
                    if (line.Length == 0 || line[0] == '#' || eq < 0)
                    {
                        continue;
                    }
                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();
                    switch (key)
                    {
                        case "TeclaLavar":
                            if (Enum.TryParse(value, true, out Key parsedKey)) WashKey = parsedKey;
                            break;
                        case "AguaPorLimpezaCompleta":
                            WaterPerFullClean = ParseFloat(value, WaterPerFullClean);
                            break;
                        case "BaldeMultiplicadorVelocidade":
                            BucketSpeedMultiplier = ParseFloat(value, BucketSpeedMultiplier);
                            break;
                        case "BaldeMultiplicadorConsumoAgua":
                            BucketWaterMultiplier = ParseFloat(value, BucketWaterMultiplier);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{Name}] Não consegui ler {configPath}, usando os padrões: {e.Message}");
            }
        }

        private static float ParseFloat(string value, float fallback)
        {
            return float.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;
        }

        private void Update()
        {
            if (NetworkServer.active && Time.unscaledTime >= _nextBucketScan)
            {
                _nextBucketScan = Time.unscaledTime + BucketScanInterval;
                TuneBuckets();
            }

            // O Mirror limpa os handlers quando o servidor desliga, então registra de novo a cada partida hospedada.
            if (NetworkServer.active && !_serverHandlerRegistered)
            {
                NetworkServer.ReplaceHandler<SpongeWashRequest>(OnServerWashRequest);
                _serverHandlerRegistered = true;
            }
            else if (!NetworkServer.active)
            {
                _serverHandlerRegistered = false;
            }

            if (Time.unscaledTime >= _nextHintRefresh)
            {
                _nextHintRefresh = Time.unscaledTime + HintRefreshInterval;
                _hint.SetVisible(CanWashNow(out _), WashKey.ToString(), T("Lavar esponja", "Wash sponge"));
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[WashKey].wasPressedThisFrame)
            {
                return;
            }
            TryRequestWash();
        }

        private void OnDestroy()
        {
            _hint.Hide();
        }

        // Texto em português se o sistema estiver em português; inglês nos demais casos.
        internal static string T(string pt, string en)
        {
            return Application.systemLanguage == SystemLanguage.Portuguese ? pt : en;
        }

        // Cursor solto = chat, console ou menu aberto; nesses casos o mod não reage.
        private static bool IsPlaying()
        {
            return Cursor.lockState == CursorLockMode.Locked && NetworkClient.active && NetworkClient.localPlayer != null;
        }

        // Vale a esponja na mão ou, se a mão estiver com outra coisa (ex.: a garrafa), a esponja na mira.
        private static CleanTool FindTargetSponge(NetworkIdentity player)
        {
            EquipmentManager equipment = player.GetComponentInChildren<EquipmentManager>();
            CleanTool sponge = equipment != null ? equipment.EquippedEntity as CleanTool : null;
            if (sponge != null)
            {
                return sponge;
            }
            InteractionManager interaction = player.GetComponentInChildren<InteractionManager>();
            Component hovered = interaction != null ? interaction.ActiveInteractable as Component : null;
            return hovered != null ? hovered.GetComponentInParent<CleanTool>() : null;
        }

        // Mesmo critério do hint e da tecla: esponja suja ao alcance e água na mão/hotbar.
        private static bool CanWashNow(out CleanTool sponge)
        {
            sponge = null;
            if (!IsPlaying())
            {
                return false;
            }
            NetworkIdentity player = NetworkClient.localPlayer;
            sponge = FindTargetSponge(player);
            return sponge != null && sponge.Condition < 1f && FindWaterContainers(player, NetworkClient.spawned).Count > 0;
        }

        private void TryRequestWash()
        {
            if (!IsPlaying())
            {
                return;
            }
            NetworkIdentity player = NetworkClient.localPlayer;
            CleanTool sponge = FindTargetSponge(player);
            if (sponge == null)
            {
                return;
            }
            if (sponge.Condition >= 1f)
            {
                ShowToast(T("A esponja já está limpa", "The sponge is already clean"));
                return;
            }
            if (FindWaterContainers(player, NetworkClient.spawned).Count == 0)
            {
                ShowToast(T("Sem água na hotbar", "No water in your hotbar"));
                return;
            }

            NetworkClient.Send(new SpongeWashRequest { SpongeNetId = sponge.netId });
            ShowToast(T("Esponja lavada", "Sponge washed"));
        }

        private static void OnServerWashRequest(NetworkConnectionToClient conn, SpongeWashRequest msg)
        {
            if (conn.identity == null || !NetworkServer.spawned.TryGetValue(msg.SpongeNetId, out NetworkIdentity spongeIdentity) || spongeIdentity == null)
            {
                return;
            }
            CleanTool sponge = spongeIdentity.GetComponent<CleanTool>();
            if (sponge == null || sponge.Condition >= 1f)
            {
                return;
            }
            if ((sponge.transform.position - conn.identity.transform.position).sqrMagnitude > MaxWashDistance * MaxWashDistance)
            {
                return;
            }

            float waterPerFullClean = Mathf.Max(0.0001f, WaterPerFullClean);
            float needed = (1f - sponge.Condition) * waterPerFullClean;
            float used = 0f;
            foreach (LiquidContainerComponent container in FindWaterContainers(conn.identity, NetworkServer.spawned))
            {
                float remaining = needed - used;
                if (remaining <= 0f)
                {
                    break;
                }
                float take = Mathf.Min(container.CurrentAmount, remaining);
                // No servidor, Drain devolve o que faltou drenar (0 quando tirou tudo).
                used += take - container.Drain(take);
            }

            if (used > 0f)
            {
                sponge.ServerRestoreCondition(used / waterPerFullClean);
            }
            Debug.Log($"[{Name}] washed sponge netId={msg.SpongeNetId}: water used={used:0.###}, condition now={sponge.Condition:0.00}");
        }

        // Item na mão primeiro, depois os slots da hotbar (a mochila só adiciona slots à hotbar).
        private static List<LiquidContainerComponent> FindWaterContainers(NetworkIdentity player, Dictionary<uint, NetworkIdentity> spawned)
        {
            List<LiquidContainerComponent> result = new List<LiquidContainerComponent>();
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

        private static void AddIfWater(LiquidContainerComponent container, List<LiquidContainerComponent> result)
        {
            if (container != null && container.CurrentLiquidType == LiquidType.Water && container.CurrentAmount > 0f && !result.Contains(container))
            {
                result.Add(container);
            }
        }

        private void ShowToast(string text)
        {
            _toastText = text;
            _toastUntil = Time.unscaledTime + 2f;
        }

        private void OnGUI()
        {
            if (_toastText == null || Time.unscaledTime > _toastUntil)
            {
                return;
            }
            if (_toastStyle == null)
            {
                _toastStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
                _toastStyle.normal.textColor = Color.white;
            }
            Rect rect = new Rect(0f, Screen.height * 0.72f, Screen.width, 40f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), _toastText, _toastStyle);
            GUI.color = Color.white;
            GUI.Label(rect, _toastText, _toastStyle);
        }

        // O balde só limpa no host, então esse ajuste vale para quem hospeda a partida.
        // Cada balde é ajustado uma vez; o HashSet evita multiplicar de novo a cada varredura.
        private void TuneBuckets()
        {
            if (BucketCleaningSpeed == null || BucketWaterPerConditionUnit == null)
            {
                return;
            }
            _tunedBuckets.RemoveWhere(bucket => bucket == null);
            foreach (Bucket bucket in FindObjectsByType<Bucket>(FindObjectsSortMode.None))
            {
                if (!_tunedBuckets.Add(bucket))
                {
                    continue;
                }
                float speed = (float)BucketCleaningSpeed.GetValue(bucket);
                float water = (float)BucketWaterPerConditionUnit.GetValue(bucket);
                BucketCleaningSpeed.SetValue(bucket, speed * Mathf.Max(0f, BucketSpeedMultiplier));
                BucketWaterPerConditionUnit.SetValue(bucket, water * Mathf.Max(0f, BucketWaterMultiplier));
            }
        }
    }
}
