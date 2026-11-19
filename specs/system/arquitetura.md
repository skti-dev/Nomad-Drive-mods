# Arquitetura

Os dois mods são plugins **BepInEx 5** (`net472`) carregados pelo *BepInExPack_NomadDrive*, com
**Harmony** para ganchos e a rede do próprio jogo (**Mirror**). Compilam contra as DLLs do jogo
instalado (`$(GameDir)\Nomad Drive_Data\Managed`, ver `.csproj`).

## Cozinha Extra — `mods/CozinhaExtra/`

```
Plugin.cs      entrada BepInEx; Cfg (config .cfg); L (PT/EN pelo idioma do jogo); Runner (Update único)
Recipes.cs     tabela de receitas (SoupType -> nomes, cor, assetIds, chaves, .cfg) e ingredientes (fresco/lata)
Net.cs         enums e mensagens de rede (SoupType, CeAction, CeFeedback, CeRequest, CePotState, CeEquip...)
Client.cs      em todo PC: lê a tecla, decide a ação (Context), mostra ActionHint/toast/estado da panela,
               desenha o líquido; manda CeRequest. Nunca muda o mundo.
Server.cs      só no host: PotData por panela (água, frescos, latas, porções, progresso), cozimento/evaporação no
               fogão aceso, valida e executa pedidos, cria itens (SpawnIntoHand), devolve recipiente.
Items.cs       moldes dos itens novos (clones de prefabs do jogo) + CePlate (marca o item do mod)
ItemsMaterials.cs  materiais HDRP Lit de cor lisa (louça, sopa)
Factories.cs   malhas por código (prato, cilindro de colisão, disco de líquido) e ícones
Game.cs        consultas ao jogo (id do item, é panela?, item da mão validado, recipientes com água)
Patches.cs     ganchos Harmony (ver abaixo), cada um com TryPatch
PotPatcher.cs  panela aceita os ingredientes frescos (tomate, batata, cogumelo) no modo de colocação; lista de panelas
WorldItems.cs  SaveRestore (itens do mod no save) e LootPlates (pratos pelo mapa)
MugSkins.cs    caneca de sopa feita em caneca de outro mod: origem (assetId+GUID), cor, ícone, devolução
TestKit.cs     F8 no host cria fogão, panela, ingredientes, água, caneca, prato e mesa
ActionHint.cs  linha própria no painel de ações do jogo
```

### Itens do mod (moldes)
Clones **inativos** de prefabs do jogo (carregados por Addressables), com malha/material/nome/assetId
próprios, registrados com `NetworkClient.RegisterPrefab` em todo PC (`Items.EnsureRegistered`).

| Item | Base (prefab do jogo) | assetId | `CePlate.Kind` | Chave no save |
|---|---|---|---|---|
| Prato vazio | `PictureFrame` | `0xCE5A0001` | 0 | `CozinhaExtra:Plate` |
| Prato de sopa de tomate / batata | `TomatoSoup` (lata, é `Food`) | `…02` / `…03` | 1 / 2 | `CozinhaExtra:TomatoSoupPlate` / `…PotatoSoupPlate` |
| Caneca de sopa de tomate / batata | `TomatoSoup` + malha/materiais da `Mug` | `…04` / `…05` | 11 / 12 | `CozinhaExtra:TomatoSoupMug` / `…PotatoSoupMug` |
| Prato / caneca de sopa de cogumelo (1.5.0) | idem | `…06` / `…07` | 3 / 13 | `CozinhaExtra:MushroomSoupPlate` / `…MushroomSoupMug` |
| Prato / caneca de feijão quente (1.5.0) | idem | `…08` / `…09` | 4 / 14 | `CozinhaExtra:HotBeansPlate` / `…HotBeansMug` |
| Prato de atum quente (1.5.0; sem caneca) | idem | `…0A` | 5 | `CozinhaExtra:HotTunaPlate` |

`Kind` = `SoupType` (+10 se caneca; cabem receitas até 9). Caneca **vazia** é a `Mug` do jogo (não é item do mod).
Reservados para a geleia (fase 2 da 0002): `SoupType` 6 e assetIds `0xCE5A000B–0F`.

### Receitas e panela (1.5.0)
Tudo que varia por receita está em `Recipes.All` (ordem e valores fixos). Ingrediente é reconhecido pelo id do
item do jogo (`Recipes.TryIngredient`): frescos `Tomato`, `Potato`, `Mushroom` (pedem água, entram por K ou modo de
colocação) e latas `TomatoSoup` (→ sopa de tomate), `BakedBeans` (→ feijão quente), `TunaCan` (→ atum quente), que
entram só por K ("Despejar na panela"), somem, não pedem água e contam em `PotData.Canned`. Tempo de cozimento =
`TempoCozimentoSegundos` se há fresco, senão `TempoAquecerLataSegundos`; o primeiro fresco depois de latas reescala o
progresso. Receita sem caneca (`MugAssetId = 0`, atum) ⇒ `CeFeedback.PlateOnly`.

**Canecas de outros mods (1.4.3):** qualquer caneca com o componente `Mug` serve. Se o assetId dela
não é o da caneca branca, a caneca de sopa guarda `CePlate.SourceAssetId/SourceGuid`, copia os materiais
do `Model` do prefab de origem e usa o ícone dele (config clonado e cacheado). A chave de save vira
`CozinhaExtra:TomatoSoupMug|<assetId hex>|<guid>` e, ao comer, volta o prefab de origem com aquele GUID.
Clientes recebem a origem por `CeMugSkin` (só protocolo ≥ 2), guardam por `netId` (`MugSkins.Known`) e
reaplicam em `CePlate.Start`, porque o jogo recria loot que sai e volta do alcance.
Fome/sede da sopa: `ConsumableConfig` criado pelo mod (`SetupSoupFood`), valores do `.cfg`.

### Fluxos principais
- **Pedido:** tecla K → `Client` monta `Context` (ação, alvo, item da mão) → `CeRequest` → `Server`
  valida → muda `PotData` / cria item → `CeFeedback` para quem pediu + `CePotState` para todos.
- **Servir na mão** (`Server.Serve`): recipiente vazio na mão + mira na panela → consome o recipiente
  (`ServerConsumeFromHand`) → cria prato/caneca cheia e manda o cliente equipar (`CeEquip`).
- **Servir da panela** (`Server.ServeFromPot`): panela na mão + mira num recipiente vazio no mundo →
  destrói o recipiente e cria o cheio no mesmo lugar/encaixe.
- **Comer** (gancho em `Consumable.UserCode_CmdConsumeFromHand`): o jogo come o item do mod →
  `Server.GiveContainerBack` cria prato vazio ou a `Mug` do jogo na mão.
- **Save:** itens do mod gravam a chave `CozinhaExtra:…` no lugar do GUID; gancho em
  `GameSaveService.SpawnFromRecordAsync` → `SaveRestore.Spawn`. Conteúdo das panelas fica em
  `BepInEx\config\CozinhaExtra.panelas.txt` (pelo GUID persistente da panela).
- **Handshake:** host tem o mod se um item do mod aparece (`CePlate.Awake`) ou pelo beacon
  (`Net.BeaconNetId` via `ObjectHideMessage`); só então o cliente manda `Hello`, com
  `Target = Net.Protocol` (1.4.2 = 0; 1.4.3 = 2; 1.5.0 = 3). O host guarda `ProtocolOf[conexão]` e só manda
  mensagem nova a quem tem protocolo suficiente (mensagem desconhecida derruba o cliente no Mirror).
  Campo novo em mensagem existente vai **no fim** e é lido com `reader.Remaining > 0` (ex.: `CePotState.Canned`,
  1.5.0): o Mirror dá tamanho a cada mensagem do lote, então quem não conhece o campo ignora o byte a mais.

### Ganchos (Patches.cs)
| Alvo | Lado | Para quê |
|---|---|---|
| `Consumable.UserCode_CmdConsumeFromHand__NetworkConnectionToClient` | host | devolver recipiente depois de comer |
| `EvilCore.Localization.LocalizationService.LocalizeName(string)` | todos | nome dos itens do mod em PT/EN |
| `NetworkClient.OnObjectHide(ObjectHideMessage)` | cliente | beacon "host tem o mod" |
| `GameSaveService.SpawnFromRecordAsync(ObjectRecord)` | host | restaurar itens do mod do save |
| `LootSpawningService.ApplyPostSpawnWorldIntegration` | host | pratos ao lado de loot |

## Sponge Wash — `mods/SpongeWash/`
`BepInExEntry` (entrada BepInEx) → `ModEntry`/`Plugin` (MonoBehaviour). Cliente manda
`SpongeWashRequest`; host valida e altera só valores que o jogo já sincroniza (condição da esponja,
água das garrafas). Gancho no balde para limpar mais rápido com menos água. `ActionHint` igual ao da
Cozinha Extra (código duplicado de propósito: cada mod é uma DLL independente).

## Ferramentas
- `tools/RefCheck` — lista tipos/métodos que a DLL usa e não existem no jogo enxuto.
- `tools/unity-dump` — UnityPy: `list_prefabs.py` (prefabs de um bundle), `dump_prefab.py`
  (hierarquia + componentes), `read_ids.py` (objetos por pathId), `find_bundles.py`.
- `tools/decompilar.sh` — `ilspycmd` nas DLLs do jogo → `.game-sources/`.
- `tools/NomadModLoader-obsoleto` — carregador próprio da época antes do BepInExPack; não usar.
