# Conhecimento do jogo (Nomad Drive)

Referência do que já foi descoberto. **Confirme no código descompilado antes de usar** — o jogo atualiza.

## Onde está
- Jogo: `C:\Program Files (x86)\Steam\steamapps\common\Nomad Drive` (Steam app 2223980).
- DLLs: `Nomad Drive_Data\Managed` (`Assembly-CSharp.dll`, `EvilCore*.dll`, `Mirror.dll`...).
- Bundles (Addressables): `Nomad Drive_Data\StreamingAssets\aa\StandaloneWindows64\`
  (`loots_assets_all_*.bundle` = itens; `shared_*`, `places_*`, `localization-string-tables-*`).
- Log: `BepInEx\LogOutput.log`. Configs dos mods: `BepInEx\config\`.
- Saves: `%USERPROFILE%\AppData\LocalLow\Indie Devil\Nomad Drive`.

## Como investigar
1. **Código:** `bash tools/decompilar.sh` → `.game-sources/<dll>/<namespace>/<Classe>.cs`.
   `VERSAO-DO-JOGO.txt` diz de qual build veio; jogo atualizou ⇒ rode de novo.
2. **Prefabs:** `tools/unity-dump/.venv/Scripts/python.exe -I tools/unity-dump/list_prefabs.py <bundle> [filtro]`
   e `dump_prefab.py "<bundle>" Nome.prefab`. Componentes `MonoBehaviour` podem sair como
   "File cab-… not found" (o script fica em outro bundle); a hierarquia e os componentes Unity saem.
   A `.venv` foi copiada de uma sessão antiga; se quebrar: `python -m venv tools/unity-dump/.venv`
   e `pip install -r tools/unity-dump/requirements.txt`.
3. **Em jogo:** kit de teste (F8) e o console do jogo (`world.spawnLoot <id>`).

## Fatos úteis
- Rede: **Mirror**. Host = servidor + cliente local. `NetworkServer.spawned[netId]`.
- Itens seguráveis: `HeldItem` (`IsEquipped`, `IsStowed`, `ServerContainer`, `ServerConsumeFromHand()`).
  Comida: `Food : Consumable` com `ConsumableConfig.playerStatCollection` (`PlayerStatType`:
  `Nutrition`, `Hydration`, `Energy`, `Health`). Comida estraga: `FoodDurabilityType.Fresh/Rotten`.
- Id do item: `LootItemDefinition.DisplayName` (`"Tomato"`, `"Mug"`, `"CookingPot-01"`).
  Categoria: `LootItemDefinition.Category` (`Kitchen`, `Food`...).
- Panela do jogo = `Pan` cujo id começa com `CookingPot`; `ReadyForCooking` = sobre fogo aceso.
  A superfície da panela, no jogo puro, aceita maçã, banana, coxa (`Drumstick`) e bife (`Steak`).
- Caneca: prefab `Mug` com componente `Mug`, filho `Model` (MeshFilter/MeshRenderer, `HandGripR`)
  e líquido em `Mug-01-LiquidMesh` (irmão do `Model`), `LiquidContainerComponent`.
- Save: `GameSaveService`, `ObjectRecord` (`AddressableGuid`, `Guid`, posição/rotação),
  `PersistentObject` (`addressableGuid`). Loot criado em runtime: `LootSpawningService.ApplyPostSpawnWorldIntegration(go, guid)`.

## Comidas do jogo (`Assets/_Project/Features/Consumables/` e `Cooking/`)
Apple, Banana, Orange, Watermelon, Tomato, Potato, **Mushroom**, BakedBeans (lata), **TomatoSoup** (lata),
**TunaCan** (lata), PeanutButter, Cereal, Chips-01, ChocolateBar-01..03, Soda, Beer, Drumstick, Steak.
(Não há frutas vermelhas, açúcar nem lata vazia.)

Valores lidos dos bundles (jogo de 2026-11-04; `ConsumableConfig.playerStatCollection`, 0 = Nutrition, 1 = Hydration):
| Item | Fome | Sede | Observação |
|---|---|---|---|
| Mushroom | 20 | – | **venenoso cru** (`FoodConfig.isPoisonous = 1`); malha em `Mushroom-01`, **sem filho `Model`** |
| Apple / Banana | 8 | – | |
| Orange | 5 | 5 | |
| Watermelon | 15 | 25 | |
| Tomato / Potato | 6 / 5 | 3 / – | |
| TomatoSoup / BakedBeans / TunaCan (latas) | 20 / 25 / 25 | – | |
| PeanutButter | 20 | – | |

Todas são `Food` comum; só `OrganicFood` (coxa, bife) é cozido pelo jogo (`CookingHeatZone`, `Pan`). Nenhuma comida dá
energia; energia máxima = 100 (`PlayerStatsManager`), e `ApplyConsumableEffects` aplica `Energy` de comida normalmente.

## Armadilhas já pagas
- **Stripping:** `Mathf.PI` e outros não existem; rode o RefCheck.
- **Mirror desconecta** quem recebe mensagem desconhecida: só mande mensagem do mod a quem disse olá.
- **Mirror limpa** handlers e a lista de prefabs ao trocar de partida: registre de novo a cada partida.
- **Item clonado:** ajuste TODOS os colisores (caixa também), o `footprint` do `LootItemDefinition`
  (o encaixe usa ele) e lembre que colisores e pegadas da mão são filhos do `Model`.
- **Loot longe some no cliente:** `LootInterestManagement` destrói loot fora do alcance (~80 m) no cliente e
  recria do prefab quando volta (exceto preso a veículo não cortável). Estado visual mandado por mensagem
  precisa ser guardado por `netId` e reaplicado quando o objeto reaparece.
- Mensagem nova do mod ⇒ versione pelo Hello (`CeRequest.Target` = protocolo) para não derrubar cliente antigo.
- Campo novo numa mensagem existente: só no fim e lido com `reader.Remaining > 0` (o Mirror separa as mensagens do
  lote por tamanho, `Unbatcher`; ler além do fim dá exceção e o `WrapHandler` desconecta).
- Item com assetId desconhecido no cliente: o Mirror só loga erro e não cria o objeto (não desconecta).
- **Moldes** ficam sob um objeto **inativo** (o `Awake` do jogo não roda no molde).
- `ilspycmd` precisa de `DOTNET_ROLL_FORWARD=Major` (o script já faz).
- PowerShell `Get-Content`/`Set-Content` estraga UTF-8 (acentos).

## Mods de terceiros conhecidos
- **Nomad Items** (`.third-party/NomadItems-0.2.80/`, fonte descompilada em `src/`): canecas
  coloridas = clone da `Mug` do jogo com materiais tingidos (`Mugs.Assemble`), nome "Red Mug"...,
  assetId próprio registrado com `NetworkClient.RegisterPrefab(prefab, assetId)` e
  `PersistentObject.addressableGuid` próprio (restaura pelo próprio gancho em `SpawnFromRecordAsync`).
