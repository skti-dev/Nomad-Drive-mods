# Plano — [0002] Mais receitas na panela (fase 1 = 1.5.0: base de receitas, cogumelo, enlatados)

> A fase 2 (geleia, 1.5.1) ganha um adendo a este plano depois do teste da 1.5.0. Aqui só se
> **reserva** o que ela vai precisar (valor de enum, assetIds), sem implementar.

## Design técnico

Ideia central: trocar os ternários "tomate ou batata" por uma **tabela de receitas** (`Recipes.cs`) e
dar à panela um segundo tipo de conteúdo cru: **latas aquecendo** (`Canned`), que não pedem água e
esquentam mais rápido. Tudo o mais (servir, comer, devolver, save, canecas coloridas) já é genérico
por `SoupType`/`CePlate.Kind` e passa a funcionar para as receitas novas.

### 1. Tabela de receitas (`Recipes.cs`, novo)
```csharp
internal sealed class Recipe {
    public SoupType Type;
    public string NamePt, NameEn;          // "sopa de cogumelo" / "mushroom soup" (minúsculo, para frases)
    public string IngredientPt, IngredientEn; // ingrediente fresco ("cogumelo"); null se só vem de lata
    public bool Drink;                     // true = "Tomar" (sopa); false = "Comer" (feijão, atum)
    public Color Color;                    // líquido na panela, prato, caneca, ícone
    public uint PlateAssetId, MugAssetId;  // MugAssetId 0 = não vai em caneca
    public string PlateKey, MugKey;        // sufixo da chave de save/objeto ("MushroomSoupPlate")
    public string PlatePt, PlateEn, MugPt, MugEn;
    public ConfigEntry<float> Nutrition, Hydration, Energy; // Energy pode ser null (tomate/batata)
}
```
- `Recipes.All` (ordem fixa) e `Recipes.Get(SoupType)` (null para `None`/desconhecido).
- Fontes de ingrediente por **id do item do jogo** (`Game.ItemId`), com `Food` obrigatório:

| Item do jogo | Receita | Tipo |
|---|---|---|
| `Tomato` | Tomato | fresco (pede água) |
| `TomatoSoup` (lata) | Tomato | lata |
| `Potato` | Potato | fresco |
| `Mushroom` | Mushroom | fresco |
| `BakedBeans` | Beans | lata |
| `TunaCan` | Tuna | lata |

  `Recipes.TryIngredient(GameObject go, out SoupType soup, out bool canned)` substitui `Game.SoupFor`.
  Os itens do mod (pratos/canecas) têm id `CE_…` (`Items.BuildTemplate` troca o `_displayName`), então o
  prato de sopa de tomate (clone da lata `TomatoSoup`) **não** é confundido com a lata. ✅
- `SoupType` (Net.cs) ganha `Mushroom = 3, Beans = 4, Tuna = 5` (só acrescentados). Comentário reserva
  `Jam = 6` para a fase 2 (não declarado agora).
- assetIds: `MushroomPlate 0xCE5A0006`, `MushroomMug …07`, `BeansPlate …08`, `BeansMug …09`,
  `TunaPlate …0A`. Reservados para a fase 2: `…0B–…0F` (comentário em `Items`).
- Chaves de save novas: `CozinhaExtra:MushroomSoupPlate`, `…MushroomSoupMug`, `…HotBeansPlate`,
  `…HotBeansMug`, `…HotTunaPlate`. Tomate e batata mantêm `TomatoSoupPlate/PotatoSoupPlate/TomatoSoupMug/PotatoSoupMug`
  (nomes de objeto inalterados ⇒ chaves e assetIds iguais aos da 1.4.x).

### 2. Configuração (`Plugin.cs` → `Cfg`)
Seções novas (vale o do host para regras; fome/sede/energia valem para quem come, como hoje):
- `[SopaCogumelo]` Nutricao 16, Hidratacao 12.
- `[FeijaoQuente]` Nutricao 16, Hidratacao 0, Energia 5.
- `[AtumQuente]` Nutricao 16, Hidratacao 0, Energia 5.
- `[Panela]` `TempoAquecerLataSegundos` 20, `PorcoesPorLata` 2.
- `Cfg.Nutrition/Hydration(soup)` passam a ler da receita (`Recipes.Get(soup)`); tomate e batata continuam
  nas seções `[SopaTomate]`/`[SopaBatata]` (mesmas chaves ⇒ `.cfg` existente continua valendo).
- `Cfg.Energy(soup)` (0 se a receita não tem).

### 3. Itens (`Items.cs`)
- `Build()` passa a criar prato (e caneca, se `MugAssetId != 0`) **para cada receita** num laço, com
  material da cor da receita (`MakeMaterial(baseMat, recipe.Color, …)`, um por receita) e ícone
  `IconFactory.Plate(recipe.Color)`. Dicionários `PlatesBySoup`/`MugsBySoup`.
- `FilledPlate(soup)`/`FilledMug(soup)`/`SoupColor(soup)` leem os dicionários/tabela (null/cor padrão
  se não houver). `FilledMug(Tuna) == null` ⇒ "só prato".
- `Stats(soup)` inclui `PlayerStatType.Energy` quando > 0. O jogo aplica energia de comida normalmente
  (`PlayerStatsManager.ApplyConsumableEffects`, `.game-sources/Assembly-CSharp/NomadDrive.Features.Player/PlayerStatsManager.cs:376-401`).
- `EnsureRegistered()` registra todos os moldes da tabela.
- Visual do ingrediente cru na panela: carrega também `Mushroom` (Addressables, como `Tomato`/`Potato`) e
  `MakeIngredientVisual/IngredientRadius` aceitam qualquer receita com ingrediente fresco. O cogumelo **não
  tem filho `Model`** (malha em `Mushroom-01`, ver premissa 4) ⇒ busca `Model` e, se não houver, o primeiro
  `MeshFilter` com `MeshRenderer` fora de `Collider`/`HandGrip`. Falhou ⇒ sem visual (só a cor do líquido).
- `MugsReady` passa a exigir só `MugPrefab` + molde da caneca de tomate (as outras canecas são opcionais
  por receita; `FilledMug` null ⇒ "só prato").

### 4. Panela (`Server.cs`)
`PotData` ganha `public int Canned;` (latas aquecendo). Regras:
- **Despejar lata** (`AddIngredient` com lata na mão): mesma validação de hoje (`ServerHeld`, `TryGetPot`,
  estragado, `OtherSoup`, `Raw + Canned < MaxIngredientes`) ⇒ `ServerConsumeFromHand()` (lata some),
  `Canned++`, `Soup = receita`, resposta `CanPoured` (feedback novo).
- **Ingrediente fresco**: igual a hoje (`Raw++`), resposta `IngredientAdded`.
- **Mistura**: só a mesma `SoupType` ⇒ tomate fresco + lata de tomate pode; o resto dá `OtherSoup`.
- **Cozinhar** (`Cook`): `pending = Raw + Canned`; água necessária = `Raw × AguaPorIngrediente` (latas não
  contam); tempo = `TempoCozimentoSegundos` se `Raw > 0`, senão `TempoAquecerLataSegundos`. Pronto ⇒
  `Portions += Raw × PorcoesPorIngrediente + Canned × PorcoesPorLata`, `Water -= necessária`, `Raw = Canned = 0`.
- `TakePortion` só limpa `Soup` se `Raw == 0 && Canned == 0`; `IsEmpty` inclui `Canned`.
- **Modo de colocação** (`AbsorbPlacedIngredients`): só absorve ingredientes **frescos** (latas não são
  aceitas pela superfície, e se alguma superfície aceitar tudo a lata fica como item normal).
- **Servir em caneca** (`Serve`/`ServeFromPot`): se `Items.FilledMug(pot.Soup) == null` ⇒ resposta
  `PlateOnly` (feedback novo) e nada é consumido.
- Evaporação: igual para todas as receitas da fase 1.

### 5. Rede (`Net.cs`)
- `CeFeedback` + `CanPoured = 14`, `PlateOnly = 15` (só acrescentados). Só vão por `Reply` a quem pediu
  (cliente que pediu já tem a 1.5.0) — `CanPoured` também por `BroadcastResult`? **Não**: lata só entra
  por K, então `Reply` basta.
- `CePotState` ganha `byte Canned` **no fim**. Compatibilidade:
  - cliente 1.4.x recebendo do host 1.5.0: cada mensagem tem tamanho próprio no lote
    (`.game-sources/Mirror/Mirror/Unbatcher.cs:62-68`) e o leitor não exige consumir tudo
    (`NetworkMessages.cs:79`) ⇒ o byte a mais é ignorado. ✅
  - cliente 1.5.0 com host 1.4.x: o leitor faz `Canned = r.Remaining > 0 ? r.ReadByte() : 0` — sem isso
    o cliente leria além do fim, daria exceção e o Mirror o desconectaria (`NetworkMessages.cs:83-86`).
- `Net.Protocol = 3` (1.5.0) no Hello, para o host saber no futuro. Nenhuma mensagem nova é condicionada a
  ele nesta fase (`CeMugSkin` continua com `ProtocolMugSkin = 2`).
- Cliente antigo vendo item novo (`0xCE5A0006+`): `FindOrSpawnObject` falha, loga erro e **não
  desconecta** (`.game-sources/Mirror/Mirror/NetworkClient.cs:880,1044-1046`) ⇒ o item só não aparece
  para ele. O LEIA-ME já pede a mesma versão em todos os PCs.

### 6. Cliente (`Client.cs`, `LiquidVisual`, `L`)
- `L.Soup(soup)`/`L.Ingredient(soup)` leem a tabela. `L.Eat(soup)` = "Tomar"/"Comer" conforme `Drink`.
- Painel de ações (`TryResolve`):
  - lata reconhecida na mão mirando a panela: **"Despejar na panela [K]"**;
  - cogumelo: "Colocar cogumelo na panela" (mesma linha do tomate);
  - comer da panela: "Tomar sopa de cogumelo" / "Comer feijão quente";
  - servir: "Servir {receita} no prato/na caneca"; caneca vazia mirando panela de atum ⇒ linha
    **"Atum quente só vai no prato"** com a ação de servir (o host responde `PlateOnly` ⇒ aviso), e o mesmo
    segurando a panela de atum mirando uma caneca vazia.
- Status embaixo da mira: latas aparecem como "2x lata: aquecendo 40%" / "ponha no fogo aceso"
  (nunca "falta água"); água necessária = `Raw × AguaPorIngrediente`.
- Avisos novos: `CanPoured` ⇒ "Lata despejada na panela"; `PlateOnly` ⇒ "Atum quente só vai no prato";
  `Ate` usa "Você tomou/comeu {receita}"; `Served` ⇒ "{Receita} servida/servido" → simplificado para
  "Servido!"/"Served!" em vez de "Sopa servida"; `PotEmpty` ⇒ "Não tem nada pronto na panela".
- Líquido: volume = `Water + (Portions + Canned × PorcoesPorLata) × PortionVolume` (lata sem água ainda
  mostra conteúdo); cor = receita (latas já vêm cozidas: cor cheia; fresco: como hoje).
- Ingredientes crus visíveis só para `Raw` (frescos).

### 7. Colocação, save, kit (`PotPatcher.cs`, `WorldItems.cs`, `TestKit.cs`)
- `PotPatcher.ExtraItems` = `Tomato`, `Potato`, `Mushroom` (lista vem dos ingredientes frescos da tabela).
- `PotStore`: grava 7º campo `canned`; lê com `f.Length >= 7 ? … : 0`. O leitor da 1.4.x exige `>= 6` e lê
  os 6 primeiros ⇒ arquivo novo continua legível se o Augusto voltar para a 1.4.3 (latas aquecendo somem). ✅
  `SoupType` 3–5 gravado e lido de volta pela 1.4.x vira "tomate" nas frases (sem quebrar).
- `SaveRestore`: genérico por chave (`Items.TryGetTemplate`) — nada a mudar além de registrar os moldes
  novos com as chaves novas.
- Kit de teste (F8): acrescenta `Mushroom ×2`, `TomatoSoup`, `BakedBeans`, `TunaCan`.

### 8. Versão e docs
`Plugin.Version = "1.5.0"`; `LEIA-ME.txt` (PT/EN): sopa de cogumelo (não envenena), latas, atum só prato,
novas chaves do `.cfg`; `CHANGELOG.md`; `nexus/descricao-nexus.txt`; `specs/system/arquitetura.md` (tabela de
itens/assetIds, `Recipes.cs`, `Canned`, protocolo 3); `specs/system/jogo.md` (fatos novos: cogumelo venenoso,
valores das comidas, nenhuma é `OrganicFood`, sem lata vazia, Mushroom sem `Model`).

### 9. Ajustes da crítica (incorporados; ver `critique.md`)
- **Lotação** `Raw + Canned` também em `AbsorbPlacedIngredients` (hoje `Server.cs:602`).
- **Panela só com lata (sem água) não é "vazia"**: `Canned` entra na guarda do `BuildStatus` (`Client.cs:419`),
  no `continue` antecipado do `Cook` (`Server.cs:185`) e na cópia do `PotStore.TryRestore` (`Server.cs:986-994`).
- **Progresso compartilhado**: ao entrar o primeiro fresco numa panela com `Canned > 0 && Raw == 0`,
  `Progress *= TempoAquecerLata / TempoCozimento` (o tempo restante passa a ser o da sopa, sem atalho).
- **Receita desconhecida** (rollback da 1.5.1, arquivo editado): `PotStore.Load` descarta linha com tipo sem
  receita; `L.Soup`/`SoupColor` têm reserva ("comida"/cor de tomate); `Serve`/`ServeFromPot` sem receita ⇒ `PotEmpty`.
- **Chaves**: `BuildTemplate`/`BuildMug` recebem `recipe.PlateKey`/`MugKey` (nome do objeto `CozinhaExtra_<Key>`);
  tomate/batata seguem gerando `TomatoSoupPlate/PotatoSoupPlate/TomatoSoupMug/PotatoSoupMug`. Comentário: `Kind`
  comporta até a receita 9.
- **`PlateOnly` genérico**: "{receita} só vai no prato" (o `CeResult` já leva `Soup`).
- **Cliente 1.4.x com caneca mirando panela de atum**: host responde `PotEmpty` a quem tem protocolo < 3 (o aviso
  antigo existe nele), `PlateOnly` aos demais.
- **`EatPortionLocally`** reserva também aplica `Cfg.Energy`; `Stats()` omite chaves com valor 0.
- **Textos**: descrições do `.cfg` deixam de dizer "tomate/batata"; `L.Ingredient` tem reserva ("lata"/"can");
  evaporação de receita não-`Drink` diz "secou no fogo"/"dried out on the fire".
- **Carregamento**: `_pendingLoads` conta o `Mushroom`; cabeçalho do `panelas.txt` ganha `canned`.
- **Energia**: máximo 100 (`PlayerStatsManager.cs:199`); 5 por porção = 10 por lata. Ajustável no `.cfg`.

## Verificação das invariantes (evidências)
| Invariante | Como é garantida | Evidência |
|---|---|---|
| Jogador guiado | linhas novas no painel (despejar, colocar cogumelo, comer/tomar, "atum só no prato"); avisos `CanPoured`/`PlateOnly`; status cobre latas; LEIA-ME/Nexus | §6; `Client.TryResolve`/`BuildStatus`/`OnResult` |
| Host autoridade | lata validada com `Game.ServerHeld` + `TryGetPot` (distância) + estragado + receita + lotação; caneca de atum recusada no host | `Server.AddIngredient/Serve/ServeFromPot` (§4) |
| Convive bem | enums/feedback/assetIds/chaves só acrescentados; `CePotState` com byte no fim lido com `Remaining`; `panelas.txt` com 7º campo opcional; chaves de tomate/batata intactas; cliente antigo não cai | §5, §7; `Unbatcher.cs:62-68`, `NetworkMessages.cs:79-86`, `NetworkClient.cs:880,1044-1046` |
| Jogo enxuto | só APIs já usadas + `NetworkReader.Remaining`, `ConfigEntry`; RefCheck na DoD | — |
| Performance | moldes/materiais criados uma vez por receita no `Build`; nada novo por frame (cozimento já itera as panelas); `Recipes.Get` é array/switch sem alocação | §3, §4 |
| Bilíngue/configurável | todos os textos novos via `L.T`/tabela; nomes de item via `ModNames`; números novos no `.cfg` com descrição | §2, §6 |

## Verificação de premissas
1. **Cogumelo, frutas e latas não são `OrganicFood`** (só `Food`): dump dos prefabs (`loots_assets_all_*.bundle`,
   componentes `<Food>` em Mushroom, Apple, Banana, Orange, Watermelon, Tomato, Potato, TomatoSoup, BakedBeans,
   TunaCan). Só `OrganicFood` é cozido pelo jogo (`CookingHeatZone.cs` filtra `GetComponentInParent<OrganicFood>()`;
   `Pan.cs:158-190` só inicia cozimento de `OrganicFood`). ✅ Sem conflito.
2. **Valores** (`ConsumableConfig.playerStatCollection`, chaves 0 = Nutrition, 1 = Hydration): Mushroom 20;
   TomatoSoup 20; BakedBeans 25; TunaCan 25; Tomato 6/3; Potato 5. Mushroom `FoodConfig.isPoisonous = 1`. ✅
3. **Sopa de cogumelo não envenena**: os pratos/canecas do mod são clones da lata `TomatoSoup`, cujo
   `FoodConfig` tem `isPoisonous = 0`; o cogumelo é consumido com `ServerConsumeFromHand` (não é comido). ✅
4. **Prefab do cogumelo** não tem filho `Model`: hierarquia `Mushroom` → `Mushroom-01` (MeshFilter/MeshRenderer)
   → `Collider`, `HandGripR`. ⇒ busca alternativa da malha (§3). ✅
5. **Energia de comida**: `PlayerStatsManager.ApplyConsumableEffects` trata `PlayerStatType.Energy`
   (`PlayerStatsManager.cs:393-395`). Escala: nenhuma comida do jogo dá energia; 5 por porção é palpite
   conservador, ajustável. ⚠️ Conferir no jogo que a barra de energia sobe visivelmente.
6. **Mirror tolera bytes a mais** (premissa da compatibilidade do `CePotState`): ver §5. ✅
7. **Item desconhecido não derruba cliente**: ver §5. ✅
8. **Lata vazia não existe** no jogo (nenhum prefab de lata vazia/sucata no bundle de loot). ✅ ⇒ a lata some.
9. **A superfície da panela não aceita latas** no jogo puro (aceita maçã, banana, coxa, bife — `jogo.md`) e o
   mod só acrescenta ingredientes frescos ⇒ latas só entram por K. ✅

## Arquivos (criar/alterar)
- `mods/CozinhaExtra/Recipes.cs` (novo): `Recipe`, `Recipes.All/Get/TryIngredient/FreshIds`.
- `Net.cs`: `SoupType` 3–5, `CeFeedback` 14–15, `CePotState.Canned`, leitor tolerante, `Protocol = 3`.
- `Plugin.cs`: `Cfg` (seções novas, `Nutrition/Hydration/Energy` pela tabela), `L.Soup/Ingredient/Eat`, versão.
- `Items.cs`: `Build` em laço, dicionários, `Stats` com energia, cogumelo no carregamento e visual.
- `Game.cs`: `SoupFor` sai (substituído por `Recipes.TryIngredient`).
- `Server.cs`: `PotData.Canned`, `AddIngredient`, `Cook`, `TakePortion`, `AbsorbPlacedIngredients`, `Serve`,
  `ServeFromPot`, `PotStore`.
- `Client.cs`: `TryResolve`, `BuildStatus`, `OnResult`, `LiquidVisual.Apply`.
- `PotPatcher.cs`: lista de frescos da tabela.
- `TestKit.cs`: itens novos.
- Docs: `LEIA-ME.txt`, `CHANGELOG.md`, `nexus/descricao-nexus.txt`, `specs/system/arquitetura.md`, `specs/system/jogo.md`.

## Ordem de execução
`Recipes` + enums → `Cfg`/`L` → `Items` (moldes por receita) → `Server` (panela/latas/caneca de atum) →
`Net` (feedback, `Canned`, protocolo) → `Client` (painel, status, avisos, líquido) → `PotPatcher`/`PotStore`/kit →
versão e docs → build + RefCheck → roteiro de teste.

## Como testar no jogo
Kit F8 (host) traz fogão, panela, tomate, batata, cogumelo ×2, latas (sopa de tomate, feijão, atum), água, caneca.
1. **Cogumelo**: água + 2 cogumelos (um por K, outro pelo modo de colocação) → fogo → ~60 s → sopa marrom;
   comer da panela ("Tomar sopa de cogumelo"), servir em prato e em caneca, comer: prato/caneca voltam; nada de
   veneno (barra de saúde/ícone de veneno).
2. **Lata de feijão** sem água: "Despejar na panela" → lata some → fogo → ~20 s → "Feijão quente: 2 porções";
   energia sobe ao comer; servir em caneca funciona.
3. **Atum**: despejar → servir com caneca dá "Atum quente só vai no prato" (painel e aviso); prato funciona;
   segurando a panela e mirando caneca vazia: mesma recusa.
4. **Tomate fresco + lata de tomate** na mesma panela (com água para o tomate) → sopa de tomate com 4 porções.
5. **Mistura proibida**: cogumelo numa panela com feijão → "A panela já tem feijão quente".
6. **Regressão**: sopa de tomate e de batata como na 1.4.3; caneca colorida do Nomad Items com sopa de cogumelo
   e feijão mantém a cor e volta colorida.
7. **Save**: salvar com panela com lata aquecendo, prato de cogumelo, caneca de feijão; sair e carregar: tudo volta.
8. **Co-op** (host + cliente 1.5.0): cliente despeja lata, vê líquido/status; cliente que entra no meio vê a
   panela certa e os pratos novos.
9. Log `BepInEx\LogOutput.log` sem erros do mod.

## Riscos / rollback
- **Escala de energia** desconhecida (premissa 5): ajustável no `.cfg`; se 5 for imperceptível, sobe o padrão.
- **Visual do cogumelo na panela** pode ficar grande/torto (outra hierarquia): risco só estético; fallback é não
  mostrar o ingrediente.
- **Cliente 1.4.x com host 1.5.0**: não cai, mas não vê itens novos e lê sopas novas como "tomate" nas frases —
  aceitável e documentado ("todos na mesma versão").
- **Cliente 1.5.0 com host 1.4.x**: despejar lata/colocar cogumelo fica sem resposta (o host antigo não conhece);
  aceito — "mesma versão em todos os PCs" no LEIA-ME.
- **Rollback**: voltar para a 1.4.3 mantém saves (itens novos somem como itens de mod desconhecidos; panelas com
  receitas novas aparecem como tomate até esvaziar).
