using System.Collections.Generic;
using Mirror;
using NomadDrive.Features.CoffeeBrewing;
using NomadDrive.Features.Consumables;
using NomadDrive.Features.Cooking;
using NomadDrive.Features.Interaction;
using NomadDrive.Features.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace CozinhaExtra
{
    // Roda em todo PC com o mod (inclusive no do host): lê a tecla, mostra as dicas e o
    // líquido nas panelas. Nada aqui muda o mundo; tudo vira pedido para o host.
    internal static class Client
    {
        private struct Context
        {
            public CeAction Action;
            public uint Target;
            public uint Held;
            public string Label;
            public Pan Pot;
        }

        private const float HintRefreshInterval = 0.1f;

        private static readonly Dictionary<uint, CePotState> States = new Dictionary<uint, CePotState>();
        private static readonly Dictionary<uint, LiquidVisual> Visuals = new Dictionary<uint, LiquidVisual>();
        private static readonly List<uint> Scratch = new List<uint>();
        private static readonly ActionHint Hint = new ActionHint();

        private static bool _handlersRegistered;
        private static bool _helloSent;
        private static bool _seenModdedObject;
        private static float _nextHintRefresh;
        private static uint _pendingEquip;
        private static float _pendingEquipSince;
        private static string _toast;
        private static float _toastUntil;
        private static string _status;
        private static GUIStyle _toastStyle;
        private static GUIStyle _statusStyle;

        public static void OnModdedObjectSeen()
        {
            _seenModdedObject = true;
        }

        public static void Tick()
        {
            if (!NetworkClient.active)
            {
                if (_handlersRegistered)
                {
                    Reset();
                }
                return;
            }
            if (!_handlersRegistered)
            {
                NetworkClient.ReplaceHandler<CePotState>(OnPotState);
                NetworkClient.ReplaceHandler<CeEquip>(OnEquip);
                NetworkClient.ReplaceHandler<CeResult>(OnResult);
                NetworkClient.ReplaceHandler<CeMugSkin>(OnMugSkin);
                _handlersRegistered = true;
            }

            // Um prato do mod apareceu: o host tem o mod, então dá para se apresentar sem risco.
            if (_seenModdedObject && !_helloSent)
            {
                SendHello();
            }

            ProcessPendingEquip();
            UpdateVisuals();

            float now = Time.unscaledTime;
            if (now >= _nextHintRefresh)
            {
                _nextHintRefresh = now + HintRefreshInterval;
                bool has = TryResolve(out Context ctx);
                Hint.SetVisible(has, Cfg.ActionKey.ToString(), has ? ctx.Label : null);
                _status = BuildStatus();
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[Cfg.ActionKey].wasPressedThisFrame && TryResolve(out Context action))
            {
                SendHello();
                NetworkClient.Send(new CeRequest { Action = (byte)action.Action, Target = action.Target, Held = action.Held });
            }
        }

        public static void Shutdown()
        {
            Hint.Hide();
        }

        private static void Reset()
        {
            _handlersRegistered = false;
            _helloSent = false;
            _seenModdedObject = false;
            _pendingEquip = 0;
            States.Clear();
            foreach (LiquidVisual visual in Visuals.Values)
            {
                if (visual != null)
                {
                    Object.Destroy(visual);
                }
            }
            Visuals.Clear();
            MugSkins.Reset();
            Hint.Hide();
        }

        // O host não precisa se apresentar (a conexão local já conta como "tem o mod").
        // Clientes só se apresentam ao usar o mod ou ao ver um item do mod, para não serem
        // desconectados por um host sem o mod.
        private static void SendHello()
        {
            if (_helloSent || NetworkServer.active || !NetworkClient.isConnected)
            {
                return;
            }
            _helloSent = true;
            // Target do Hello = versão das mensagens que este cliente entende.
            NetworkClient.Send(new CeRequest { Action = (byte)CeAction.Hello, Target = Net.Protocol });
        }

        private static void OnMugSkin(CeMugSkin msg)
        {
            MugSkins.Remember(msg.Item, msg.Source);
        }

        private static void OnPotState(CePotState msg)
        {
            States[msg.Pot] = msg;
        }

        private static void OnEquip(CeEquip msg)
        {
            _pendingEquip = msg.Item;
            _pendingEquipSince = Time.unscaledTime;
        }

        private static void OnResult(CeResult msg)
        {
            SoupType soup = (SoupType)msg.Soup;
            switch ((CeFeedback)msg.Code)
            {
                case CeFeedback.Ate:
                    EatPortionLocally(soup);
                    ShowToast(L.T("Você tomou ", "You had some ") + L.Soup(soup));
                    break;
                case CeFeedback.WaterAdded:
                    ShowToast(L.T("Água na panela", "Water added to the pot"));
                    break;
                case CeFeedback.IngredientAdded:
                    if (soup == SoupType.None)
                    {
                        break;
                    }
                    ShowToast(L.Pt ? $"{Capitalize(L.Ingredient(soup))} na panela" : $"{Capitalize(L.Ingredient(soup))} added to the pot");
                    break;
                case CeFeedback.NoWater:
                    ShowToast(L.T("Sem água na mão ou na hotbar", "No water in your hand or hotbar"));
                    break;
                case CeFeedback.PotFull:
                    ShowToast(L.T("A panela está cheia", "The pot is full"));
                    break;
                case CeFeedback.OtherSoup:
                    ShowToast(L.Pt ? $"A panela já tem {L.Soup(soup)}" : $"The pot already has {L.Soup(soup)}");
                    break;
                case CeFeedback.Served:
                    ShowToast(L.T("Sopa servida", "Soup served"));
                    break;
                case CeFeedback.MugNotEmpty:
                    ShowToast(L.T("Esvazie a caneca primeiro", "Empty the mug first"));
                    break;
                case CeFeedback.Evaporated:
                    ShowToast(L.Pt ? $"Uma porção de {L.Soup(soup)} evaporou no fogo" : $"A portion of {L.Soup(soup)} boiled away");
                    break;
                case CeFeedback.PlateTaken:
                    ShowToast(L.T("Você pegou um prato", "You took a plate"));
                    break;
                case CeFeedback.PotEmpty:
                    ShowToast(L.T("A panela está sem sopa", "There is no soup in the pot"));
                    break;
                case CeFeedback.Rotten:
                    ShowToast(L.T("Isso está estragado", "That is rotten"));
                    break;
                case CeFeedback.HandBusy:
                    ShowToast(L.T("Esvazie a mão primeiro", "Empty your hand first"));
                    break;
                case CeFeedback.TooFar:
                    ShowToast(L.T("Chegue mais perto", "Get closer"));
                    break;
            }
        }

        private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // Fome e sede são do lado de quem come, igual a comida do jogo.
        private static void EatPortionLocally(SoupType soup)
        {
            NetworkIdentity player = NetworkClient.localPlayer;
            PlayerStatsManager stats = player != null ? player.GetComponentInChildren<PlayerStatsManager>(true) : null;
            if (stats == null)
            {
                return;
            }
            Food template = Items.FilledPlateFood(soup);
            if (template != null)
            {
                stats.ApplyConsumableEffects(template);
                return;
            }
            stats.Nutrition?.IncreaseValue(Cfg.Nutrition(soup));
            stats.Hydration?.IncreaseValue(Cfg.Hydration(soup));
        }

        // O host criou um prato para nós: pega assim que ele aparecer (pegar é sempre do cliente).
        private static void ProcessPendingEquip()
        {
            if (_pendingEquip == 0)
            {
                return;
            }
            float waited = Time.unscaledTime - _pendingEquipSince;
            if (waited > 5f)
            {
                _pendingEquip = 0;
                return;
            }
            if (waited < 0.3f || !NetworkClient.spawned.TryGetValue(_pendingEquip, out NetworkIdentity identity) || identity == null)
            {
                return;
            }
            NetworkIdentity player = NetworkClient.localPlayer;
            EquipmentManager equipment = player != null ? player.GetComponentInChildren<EquipmentManager>() : null;
            HeldItem item = identity.GetComponent<HeldItem>();
            if (equipment == null || item == null)
            {
                _pendingEquip = 0;
                return;
            }
            // Ainda segurando o item que foi gasto? Espera o jogo soltar.
            if (equipment.EquippedEntity != null && equipment.EquippedEntity != item)
            {
                return;
            }
            _pendingEquip = 0;
            if (equipment.EquippedEntity == null)
            {
                equipment.Equip(item);
            }
        }

        // Cursor solto = chat, console ou menu aberto; nesses casos o mod não reage.
        private static bool IsPlaying()
        {
            return Cursor.lockState == CursorLockMode.Locked && NetworkClient.active && NetworkClient.localPlayer != null;
        }

        private static bool TryGetState(Pan pot, out CePotState state)
        {
            state = default;
            return pot != null && States.TryGetValue(pot.netId, out state);
        }

        // Decide o que a tecla faz agora. A mesma regra serve para a dica e para o aperto.
        private static bool TryResolve(out Context ctx)
        {
            ctx = default;
            if (!IsPlaying())
            {
                return false;
            }
            NetworkIdentity player = NetworkClient.localPlayer;
            EquipmentManager equipment = player.GetComponentInChildren<EquipmentManager>();
            InteractionManager interaction = player.GetComponentInChildren<InteractionManager>();
            HeldItem held = equipment != null ? equipment.EquippedEntity : null;
            Component hovered = interaction != null ? interaction.ActiveInteractable as Component : null;

            Pan heldPot = held != null ? Game.AsCookingPot(held) : null;
            Pan hoverPot = hovered != null ? Game.AsCookingPot(hovered) : null;
            if (hoverPot != null && hoverPot == heldPot)
            {
                hoverPot = null;
            }
            Pan pot = hoverPot != null ? hoverPot : heldPot;
            TryGetState(pot, out CePotState state);
            CePlate heldPlate = held != null ? held.GetComponent<CePlate>() : null;

            // Panela com sopa na mão, mirando um prato ou caneca vazio (em cima de algo ou no chão).
            if (heldPot != null && hovered != null && hoverPot == null && state.Portions > 0)
            {
                HeldItem container = hovered.GetComponentInParent<HeldItem>();
                CePlate plate = container != null ? container.GetComponent<CePlate>() : null;
                bool isPlate = plate != null && plate.IsEmptyPlate;
                bool isMug = container != null && Items.MugsReady && container.GetComponent<Mug>() != null && Game.IsEmptyContainer(container);
                if (isPlate || isMug)
                {
                    ctx = new Context
                    {
                        Action = CeAction.ServeFromPot,
                        Target = heldPot.netId,
                        Held = container.netId,
                        Pot = heldPot,
                        Label = isMug ? L.T("Servir sopa na caneca", "Pour soup into the mug") : L.T("Servir sopa no prato", "Serve soup on the plate")
                    };
                    return true;
                }
            }

            // Tomate ou batata na mão, mirando a panela.
            if (hoverPot != null && held != null)
            {
                SoupType soup = Game.SoupFor(held.gameObject);
                if (soup != SoupType.None)
                {
                    ctx = new Context
                    {
                        Action = CeAction.AddIngredient,
                        Target = hoverPot.netId,
                        Held = held.netId,
                        Pot = hoverPot,
                        Label = L.Pt ? $"Colocar {L.Ingredient(soup)} na panela" : $"Put the {L.Ingredient(soup)} in the pot"
                    };
                    return true;
                }
            }

            // Prato vazio na mão, mirando a panela com sopa.
            if (hoverPot != null && heldPlate != null && heldPlate.IsEmptyPlate)
            {
                if (state.Portions > 0)
                {
                    ctx = new Context { Action = CeAction.ServeToPlate, Target = hoverPot.netId, Held = held.netId, Pot = hoverPot, Label = L.T("Servir sopa no prato", "Serve soup on the plate") };
                    return true;
                }
                return false;
            }

            // Caneca vazia do jogo na mão, mirando a panela com sopa.
            if (hoverPot != null && held != null && state.Portions > 0 && Items.MugsReady &&
                held.GetComponent<Mug>() != null && Game.IsEmptyContainer(held))
            {
                ctx = new Context { Action = CeAction.ServeToPlate, Target = hoverPot.netId, Held = held.netId, Pot = hoverPot, Label = L.T("Servir sopa na caneca", "Pour soup into the mug") };
                return true;
            }

            bool hasWater = Game.FindWaterContainers(player, NetworkClient.spawned).Count > 0;
            bool roomForWater = state.Water < Cfg.PotCapacity.Value - 0.01f;
            Context water = pot != null
                ? new Context { Action = CeAction.AddWater, Target = pot.netId, Pot = pot, Label = L.T("Pôr água na panela", "Pour water into the pot") }
                : default;

            // Garrafa/cantil/balde na mão, mirando a panela.
            if (hoverPot != null && Game.IsWaterContainer(held) && roomForWater)
            {
                ctx = water;
                return true;
            }

            // Comer direto da panela (mirando com a mão vazia, ou segurando a panela).
            if (pot != null && state.Portions > 0 && (held == null || heldPot != null))
            {
                ctx = new Context
                {
                    Action = CeAction.EatFromPot,
                    Target = pot.netId,
                    Pot = pot,
                    Label = L.Pt ? $"Tomar {L.Soup((SoupType)state.Soup)}" : $"Have some {L.Soup((SoupType)state.Soup)}"
                };
                return true;
            }

            // Água da hotbar para a panela (mão vazia mirando, ou segurando a panela).
            if (pot != null && hasWater && roomForWater && (held == null || heldPot != null))
            {
                ctx = water;
                return true;
            }

            // Pegar prato num fogão com a mão vazia.
            if (held == null && hovered != null && Cfg.PlatesFromStove.Value && Items.Ready)
            {
                Stove stove = hovered.GetComponentInParent<Stove>();
                if (stove != null)
                {
                    ctx = new Context { Action = CeAction.TakePlate, Target = stove.netId, Label = L.T("Pegar prato", "Take a plate") };
                    return true;
                }
            }
            return false;
        }

        // Linha de status embaixo da mira quando olhando/segurando uma panela com algo dentro.
        private static string BuildStatus()
        {
            if (!IsPlaying())
            {
                return null;
            }
            NetworkIdentity player = NetworkClient.localPlayer;
            EquipmentManager equipment = player.GetComponentInChildren<EquipmentManager>();
            InteractionManager interaction = player.GetComponentInChildren<InteractionManager>();
            Component hovered = interaction != null ? interaction.ActiveInteractable as Component : null;
            Pan pot = hovered != null ? Game.AsCookingPot(hovered) : null;
            if (pot == null && equipment != null && equipment.EquippedEntity != null)
            {
                pot = Game.AsCookingPot(equipment.EquippedEntity);
            }
            if (!TryGetState(pot, out CePotState s) || (s.Water <= 0.001f && s.Raw == 0 && s.Portions == 0))
            {
                return null;
            }

            List<string> parts = new List<string>();
            if (s.Water > 0.001f)
            {
                int pct = Mathf.RoundToInt(s.Water / Mathf.Max(0.01f, Cfg.PotCapacity.Value) * 100f);
                parts.Add(L.T("Água ", "Water ") + pct + "%");
            }
            SoupType soup = (SoupType)s.Soup;
            if (s.Raw > 0)
            {
                string what = (s.Raw > 1 ? s.Raw + "x " : "") + L.Ingredient(soup);
                float needed = s.Raw * Cfg.WaterPerIngredient.Value;
                if (s.Water + 0.0001f < needed)
                {
                    parts.Add(what + L.T(": falta água", ": needs more water"));
                }
                else if (!pot.ReadyForCooking)
                {
                    parts.Add(what + L.T(": ponha no fogo aceso", ": put it on a lit burner"));
                }
                else
                {
                    parts.Add(what + L.T(" cozinhando ", " cooking ") + Mathf.RoundToInt(s.Progress * 100f) + "%");
                }
            }
            if (s.Portions > 0)
            {
                string soupName = Capitalize(L.Soup(soup));
                parts.Add(L.Pt
                    ? $"{soupName}: {s.Portions} {(s.Portions == 1 ? "porção" : "porções")}"
                    : $"{soupName}: {s.Portions} {(s.Portions == 1 ? "portion" : "portions")}");
                float evap = Cfg.EvaporationSeconds.Value;
                if (evap > 0f && pot.ReadyForCooking)
                {
                    int left = Mathf.Max(0, Mathf.CeilToInt(evap - s.Evap));
                    parts.Add(L.Pt ? $"no fogo: perde 1 porção em {left}s" : $"on the fire: loses 1 portion in {left}s");
                }
            }
            return string.Join("  ·  ", parts.ToArray());
        }

        private static void UpdateVisuals()
        {
            foreach (KeyValuePair<uint, CePotState> pair in States)
            {
                if (!NetworkClient.spawned.TryGetValue(pair.Key, out NetworkIdentity identity) || identity == null)
                {
                    continue;
                }
                if (!Visuals.TryGetValue(pair.Key, out LiquidVisual visual) || visual == null)
                {
                    visual = identity.gameObject.GetComponent<LiquidVisual>();
                    if (visual == null)
                    {
                        visual = identity.gameObject.AddComponent<LiquidVisual>();
                    }
                    Visuals[pair.Key] = visual;
                }
                visual.Apply(pair.Value);
            }

            Scratch.Clear();
            foreach (KeyValuePair<uint, LiquidVisual> pair in Visuals)
            {
                if (pair.Value == null || !States.ContainsKey(pair.Key))
                {
                    Scratch.Add(pair.Key);
                }
            }
            foreach (uint key in Scratch)
            {
                if (Visuals.TryGetValue(key, out LiquidVisual v) && v != null)
                {
                    Object.Destroy(v);
                }
                Visuals.Remove(key);
            }
        }

        private static void ShowToast(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 2.2f;
        }

        public static void DrawGui()
        {
            if (_toastStyle == null)
            {
                _toastStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
                _toastStyle.normal.textColor = Color.white;
                _statusStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 17 };
                _statusStyle.normal.textColor = new Color(1f, 0.95f, 0.85f);
            }
            if (_status != null && IsPlaying())
            {
                Shadowed(new Rect(0f, Screen.height * 0.5f + 34f, Screen.width, 28f), _status, _statusStyle);
            }
            if (_toast != null && Time.unscaledTime <= _toastUntil)
            {
                Shadowed(new Rect(0f, Screen.height * 0.72f, Screen.width, 40f), _toast, _toastStyle);
            }
        }

        private static void Shadowed(Rect rect, string text, GUIStyle style)
        {
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            GUI.color = old;
            GUI.Label(rect, text, style);
        }
    }

    // Superfície de água/sopa e legumes crus dentro da panela, locais em cada PC (o estado vem do host).
    internal sealed class LiquidVisual : MonoBehaviour
    {
        private const int MaxShownIngredients = 3;

        // Onde cada legume fica na panela (x, z), para não ficarem empilhados.
        private static readonly Vector2[] IngredientSpots =
        {
            new Vector2(0.035f, 0.02f), new Vector2(-0.04f, -0.015f), new Vector2(0.005f, -0.05f)
        };

        private static Mesh _disc;

        private GameObject _surface;
        private Material _material;
        private GameObject _sparks;
        private bool _sparksWereActive;
        private readonly GameObject[] _ingredients = new GameObject[MaxShownIngredients];
        private Renderer _potRenderer;
        private SoupType _ingredientType = SoupType.None;

        public void Apply(CePotState s)
        {
            float capacity = Mathf.Max(0.01f, Cfg.PotCapacity.Value);
            float volume = s.Water + s.Portions * Cfg.PortionVolume;
            float level = Mathf.Clamp01(volume / capacity);
            bool hasLiquid = volume > 0.005f;
            float y = Mathf.Lerp(Cfg.LiquidMinY.Value, Cfg.LiquidMaxY.Value, level);

            UpdateIngredients(s, hasLiquid, y);
            SyncLayers();

            if (!hasLiquid)
            {
                if (_surface != null)
                {
                    _surface.SetActive(false);
                }
                SetSparks(true);
                return;
            }
            bool created = _surface == null;
            EnsureSurface();
            if (_surface == null)
            {
                return;
            }
            _surface.SetActive(true);
            SetSparks(false);
            if (created)
            {
                SyncLayers();
            }

            // A panela abre um pouco para cima, então o disco cresce junto com o nível.
            float radius = Mathf.Lerp(0.118f, 0.142f, level);
            _surface.transform.localPosition = new Vector3(0f, y, 0f);
            _surface.transform.localRotation = Quaternion.identity;
            _surface.transform.localScale = new Vector3(radius, 1f, radius);

            SoupType soup = (SoupType)s.Soup;
            Color color;
            if (s.Portions > 0)
            {
                color = Items.SoupColor(soup);
            }
            else if (s.Raw > 0)
            {
                color = Color.Lerp(Items.WaterColor, Items.SoupColor(soup), 0.15f + s.Progress * 0.85f);
            }
            else
            {
                color = Items.WaterColor;
            }
            Items.SetColor(_material, color);
        }

        // Os legumes crus aparecem boiando (ou no fundo, se ainda não tem água) e vão
        // encolhendo enquanto cozinham, até virar sopa.
        private void UpdateIngredients(CePotState s, bool hasLiquid, float liquidY)
        {
            SoupType soup = (SoupType)s.Soup;
            int count = soup == SoupType.None ? 0 : Mathf.Min(s.Raw, MaxShownIngredients);
            if (soup != _ingredientType)
            {
                ClearIngredients();
                _ingredientType = soup;
            }
            for (int i = 0; i < MaxShownIngredients; i++)
            {
                bool show = i < count;
                if (!show)
                {
                    if (_ingredients[i] != null)
                    {
                        _ingredients[i].SetActive(false);
                    }
                    continue;
                }
                if (_ingredients[i] == null)
                {
                    _ingredients[i] = Items.MakeIngredientVisual(soup, transform);
                    if (_ingredients[i] == null)
                    {
                        return;
                    }
                    _ingredients[i].transform.localRotation = Quaternion.Euler(0f, i * 117f, i * 23f);
                }
                GameObject go = _ingredients[i];
                go.SetActive(true);
                float shrink = Mathf.Lerp(1f, 0.55f, s.Progress);
                float size = Items.IngredientRadius(soup) * shrink;
                Vector2 spot = IngredientSpots[i];
                // Na água: meio afundado e balançando de leve. Sem água: apoiado no fundo.
                float bob = hasLiquid ? Mathf.Sin(Time.time * 1.7f + i * 2.1f) * 0.002f : 0f;
                float y = hasLiquid ? liquidY - size * 0.25f + bob : Cfg.LiquidMinY.Value + size * 0.8f;
                go.transform.localPosition = new Vector3(spot.x, y, spot.y);
                go.transform.localScale = Vector3.one * shrink;
            }
        }

        // O jogo destaca um item trocando a layer de cada parte visível (e só das ativas). As partes
        // do mod copiam a layer do modelo da panela, então acendem e apagam junto com ela.
        private void SyncLayers()
        {
            if (_potRenderer == null)
            {
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                {
                    if (r is MeshRenderer && !r.gameObject.name.StartsWith("CozinhaExtra_") && r.transform.parent != null &&
                        !r.transform.parent.name.StartsWith("CozinhaExtra_"))
                    {
                        _potRenderer = r;
                        break;
                    }
                }
                if (_potRenderer == null)
                {
                    return;
                }
            }
            int layer = _potRenderer.gameObject.layer;
            if (_surface != null)
            {
                SetLayer(_surface, layer);
            }
            foreach (GameObject go in _ingredients)
            {
                if (go != null)
                {
                    SetLayer(go, layer);
                }
            }
        }

        private static void SetLayer(GameObject root, int layer)
        {
            if (root.layer != layer)
            {
                root.layer = layer;
            }
            foreach (Transform child in root.transform)
            {
                if (child.gameObject.layer != layer)
                {
                    child.gameObject.layer = layer;
                }
            }
        }

        private void ClearIngredients()
        {
            for (int i = 0; i < _ingredients.Length; i++)
            {
                if (_ingredients[i] != null)
                {
                    Destroy(_ingredients[i]);
                }
                _ingredients[i] = null;
            }
        }

        private void EnsureSurface()
        {
            if (_surface != null)
            {
                return;
            }
            if (_disc == null)
            {
                _disc = MeshFactory.UnitDisc();
            }
            Material source = Items.LitTemplate;
            if (source == null)
            {
                MeshRenderer own = GetComponentInChildren<MeshRenderer>(true);
                source = own != null ? own.sharedMaterial : null;
            }
            _material = Items.MakeMaterial(source, Items.WaterColor, 0.9f, "CozinhaExtra_PotLiquid");

            _surface = new GameObject("CozinhaExtra_Liquid");
            _surface.transform.SetParent(transform, false);
            _surface.AddComponent<MeshFilter>().sharedMesh = _disc;
            MeshRenderer renderer = _surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _surface.layer = gameObject.layer;

            // As faíscas de fritura da panela não combinam com sopa.
            Transform sparks = FindChild(transform, "Sparks");
            _sparks = sparks != null ? sparks.gameObject : null;
            _sparksWereActive = _sparks != null && _sparks.activeSelf;
        }

        private void SetSparks(bool active)
        {
            if (_sparks != null && _sparksWereActive && _sparks.activeSelf != active)
            {
                _sparks.SetActive(active);
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }
            return null;
        }

        private void OnDestroy()
        {
            SetSparks(true);
            ClearIngredients();
            if (_surface != null)
            {
                Destroy(_surface);
            }
            if (_material != null)
            {
                Destroy(_material);
            }
        }
    }
}
