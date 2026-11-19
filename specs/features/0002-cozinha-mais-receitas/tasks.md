# Tarefas — [0002] Mais receitas na panela (fase 1 = 1.5.0)

> Primeira tarefa: criar a branch `feat/0002-cozinha-mais-receitas` a partir de `staging`.

## Base
- [x] Branch `feat/0002-cozinha-mais-receitas` a partir de `staging`; spec com status `em implementação`.
- [x] `Recipes.cs` com a tabela (tomate, batata, cogumelo, feijão, atum), `Get`, `TryIngredient` (fresco/lata)
      e lista de ids frescos — critério: `TryIngredient` reconhece os 6 itens da tabela do plano e recusa os
      itens do mod (`CE_…`) — invariante: convive
- [x] `SoupType` 3–5 e `CeFeedback` 14–15 só acrescentados; comentário reservando `Jam = 6` e assetIds
      `0xCE5A000B–0F` — critério: valores 0–2 e 0–13 inalterados — invariante: convive
- [x] `Cfg`: seções `[SopaCogumelo]`, `[FeijaoQuente]`, `[AtumQuente]`, `TempoAquecerLataSegundos`,
      `PorcoesPorLata`; `Nutrition/Hydration/Energy(soup)` pela tabela — critério: `.cfg` da 1.4.3 continua
      válido (chaves antigas iguais) — invariante: bilíngue/configurável

## Host / lógica
- [x] `Items.Build` em laço por receita (prato sempre; caneca se `MugAssetId != 0`), material e ícone por cor,
      `Stats` com energia, `EnsureRegistered` de todos — critério: log "pratos e canecas prontos" lista as
      receitas; assetIds/chaves de tomate e batata iguais aos da 1.4.3 — invariante: convive, performance
- [x] Cogumelo carregado para o visual cru; `MakeIngredientVisual`/`IngredientRadius` com busca de malha
      sem `Model` — critério: cogumelo aparece na panela; se falhar, só não aparece — invariante: convive
- [x] `PotData.Canned`; `AddIngredient` aceita lata (consome da mão, `Canned++`, `CanPoured`) e fresco
      (como hoje); mistura só da mesma receita; lotação `Raw + Canned` — invariante: host
- [x] `Cook`: água só para `Raw`; tempo de lata quando não há fresco; porções por fresco e por lata;
      `TakePortion`/`IsEmpty` contam `Canned` — critério: lata sem água aquece; tomate + lata dá 4 porções
- [x] `AbsorbPlacedIngredients` só absorve frescos — critério: cogumelo pelo modo de colocação entra
- [x] `Serve`/`ServeFromPot`: receita sem caneca ⇒ `PlateOnly`, nada consumido — invariante: host, guiado

- [x] Ajustes da crítica (plan §9): lotação `Raw + Canned` no modo de colocação; `Canned` no `continue` do `Cook`,
      no `TryRestore` e na guarda do status; reescala do `Progress` ao entrar fresco depois de lata; receita
      desconhecida descartada/reserva/`PotEmpty`; `PotEmpty` para protocolo < 3 em vez de `PlateOnly`;
      chaves por `recipe.PlateKey/MugKey`; `Stats` sem chave 0; `_pendingLoads` com o cogumelo

## Rede
- [x] `CePotState.Canned` no fim; leitor com `Remaining > 0`; `Net.Protocol = 3` — critério: cliente 1.5.0 lê
      mensagem sem o byte (host 1.4.x) sem exceção — invariante: convive

## Cliente / visual
- [x] `L.Soup/Ingredient/Eat` pela tabela — invariante: bilíngue
- [x] Painel: "Despejar na panela", "Colocar cogumelo…", "Tomar/Comer {receita}", "Servir {receita} no
      prato/na caneca", "Atum quente só vai no prato" (mão com caneca e segurando a panela) — invariante: guiado
- [x] Status: latas "aquecendo %"/"ponha no fogo aceso", nunca "falta água" por causa de lata — invariante: guiado
- [x] Avisos `CanPoured`, `PlateOnly`; `Ate`/`Served`/`PotEmpty` com texto genérico — invariante: guiado, bilíngue
- [x] `LiquidVisual`: volume conta latas; cor da receita — critério: lata sem água mostra conteúdo

## Save / compatibilidade
- [x] `PotPatcher.ExtraItems` = frescos da tabela (inclui `Mushroom`)
- [x] `PotStore` grava/lê 7º campo opcional `canned` — critério: arquivo da 1.4.3 carrega; arquivo novo tem
      6 primeiros campos no formato antigo — invariante: convive
- [x] Kit F8: `Mushroom ×2`, `TomatoSoup`, `BakedBeans`, `TunaCan`

## Textos e docs
- [x] Textos novos em PT-BR e EN (painel, status, avisos, nomes de itens)
- [x] `Plugin.Version = "1.5.0"`, `LEIA-ME.txt`, `CHANGELOG.md`, `nexus/descricao-nexus.txt`
- [x] `specs/system/arquitetura.md` (itens/assetIds, `Recipes.cs`, `Canned`, protocolo 3) e `specs/system/jogo.md`
      (fatos novos das comidas)

## Validação final (Definition of Done)
- [x] `dotnet build mods/CozinhaExtra -c Release` sem erros nem avisos novos (copia para o jogo)
- [x] RefCheck sem faltas
- [ ] Teste no jogo, solo: roteiro "Como testar no jogo" do plano, passos 1–7 — **Augusto**
- [ ] Teste em co-op (host + cliente; cliente entrando no meio) — **pendente para o Augusto**
- [ ] Salvar, sair e carregar: itens e panelas voltam certos; save abre sem o mod — **Augusto**
- [ ] `BepInEx\LogOutput.log` sem erros do mod — **Augusto**
- [ ] Merge `--no-ff` em `staging` só depois do "aprovado" do Augusto
