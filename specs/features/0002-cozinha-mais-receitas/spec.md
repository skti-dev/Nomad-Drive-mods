# [0002] Mais receitas na panela: sopa de cogumelo, enlatados aquecidos e geleia

- Status: fase 1 publicada na 1.5.0 (2026-11-19); fase 2 (geleia, 1.5.1) pendente
- Tipo: melhoria
- Mod: CozinhaExtra
- Versão alvo: **1.5.0** (fase 1: base de receitas, cogumelo, enlatados) e **1.5.1** (fase 2: geleia)
- Origem: sugestão de jogador no Nexus (2026-11): *"could we possibly cook more types of food? For
  example: mushroom soup, jam, or even heating up various canned foods."*

## Problema / motivação
Hoje a panela só faz duas sopas (tomate e batata). O jogo tem outros alimentos que combinam com
cozinhar e que o jogador já carrega por aí: **cogumelo** (`Mushroom`), frutas (`Apple`, `Orange`,
`Banana`) e **enlatados** (`BakedBeans`, `TomatoSoup`, `TunaCan`). Não existem frutas vermelhas, açúcar
nem lata vazia no jogo (ver `specs/system/jogo.md`).

O código atual assume duas sopas em vários lugares (`SoupType` com Tomato/Potato, ternários
`soup == Potato ? … : …` em `Items`, `Cfg.Nutrition/Hydration`, `Game.SoupFor`, `PotPatcher.ExtraItems`,
moldes de prato/caneca por sopa com assetIds `0xCE5A0002–05`). Acrescentar receitas pede uma
**tabela de receitas** em vez de mais ternários.

### O que o jogo tem (lido dos bundles com `tools/unity-dump`, jogo de 2026-11-04)
| Item | Componente | Fome | Sede | Observação |
|---|---|---|---|---|
| Mushroom | `Food` | 20 | – | **venenoso cru** (`FoodConfig.isPoisonous = 1`, veneno inicial 5) |
| Apple / Banana | `Food` | 8 | – | a superfície da panela do jogo já aceita (fica parada, não cozinha) |
| Orange | `Food` | 5 | 5 | |
| Tomato / Potato | `Food` | 6 / 5 | 3 / – | sopas atuais: 2 porções por unidade, 15/15 e 18/12 por porção |
| TomatoSoup (lata) | `Food` | 20 | – | |
| BakedBeans (lata) | `Food` | 25 | – | |
| TunaCan (lata) | `Food` | 25 | – | |
| PeanutButter (pote) | `Food` | 20 | – | visual base do pote de geleia |

Nenhum deles é `OrganicFood` (só esse tipo o jogo cozinha sozinho na panela/frigideira), então as
receitas novas não disputam com o cozimento do jogo. Nenhuma comida do jogo dá energia.

## Experiência do jogador

### Fase 1 (1.5.0)

#### A. Sopa de cogumelo (mesmo fluxo das sopas atuais)
1. Põe água na panela, coloca 1–2 cogumelos (K mirando a panela, ou modo de colocação).
2. Leva ao fogo aceso; em ~60 s vira **sopa de cogumelo** (líquido marrom-claro).
3. Come da panela ou serve em prato/caneca: "Prato de sopa de cogumelo" / "Caneca de sopa de cogumelo".
4. Cogumelo cru envenena; a **sopa não envenena** (o LEIA-ME e o Nexus destacam isso).

#### B. Enlatados aquecidos
1. Com uma lata do jogo (sopa de tomate, feijão, atum) na mão, mira a panela e aperta K:
   "Despejar na panela [K]". A lata **some** (o jogo não tem lata vazia) e o conteúdo vai para a panela.
2. **Não precisa de água.** Se a panela já tiver água, ela continua lá (serve para tomates depois).
3. No fogo aceso, esquenta rápido (~20 s).
4. Resultado por lata:
   - **Sopa de tomate em lata** → vira a **sopa de tomate do mod** (mesmo prato, caneca e valores).
     Pode ir na mesma panela que tomates frescos (é a mesma receita).
   - **Feijão** → "Feijão quente", servido em **prato ou caneca**.
   - **Atum** → "Atum quente", servido **só em prato** (mirar com caneca dá aviso).
5. Quente rende mais que frio e, por ser panela, dá para **dividir** em porções com o amigo.

### Fase 2 (1.5.1)

#### C. Geleia de fruta
1. Coloca frutas na panela **sem água** (maçã, banana ou laranja; um tipo de fruta por vez). Melancia
   não entra (é quase só água).
2. Leva ao fogo: a fruta "derrete" e vira **geleia** (cor da fruta), mais lenta que a sopa.
3. Pega: com a panela de geleia, K com a mão vazia mirando a panela dá um **pote de geleia** — item
   novo com o visual do pote de pasta de amendoim do jogo, tingido na cor da fruta. **1 fruta = 1 pote.**
   O pote sai sem precisar de recipiente vazio e some ao comer (como o pote do jogo).
4. Comer o pote mata a fome e dá um pouco de **energia** (`PlayerStatType.Energy`), sem matar sede.
5. Geleia **não evapora** e não é servida em prato/caneca.
6. Efeito colateral aceito: no jogo puro, maçã/banana colocadas na panela ficam paradas; com o mod,
   numa panela **sem água**, elas entram na geleia. Com água na panela: aviso "Geleia não leva água".

Em tudo: olhando para a panela, a dica embaixo da mira mostra o que tem dentro e quanto falta;
avisos claros quando não dá (ex.: "Já tem outra receita na panela", "Atum só vai no prato").

## Balanceamento inicial (tudo no `.cfg`, vale o do host)
| Receita | Rende | Por porção | Total vs. comer cru/frio |
|---|---|---|---|
| Sopa de cogumelo | 2 porções por cogumelo | 16 fome, 12 sede, sem veneno | 32/24 vs. 20 + veneno |
| Sopa de tomate (lata) | 2 porções por lata | igual à sopa de tomate (15/15) | 30/30 vs. 20 |
| Feijão quente | 2 porções por lata | 16 fome, 5 energia | 32 vs. 25 |
| Atum quente | 2 porções por lata | 16 fome, 5 energia | 32 vs. 25 |
| Geleia (fase 2) | 1 pote por fruta | ~12 fome + energia (valor final no plano, após ver a escala de energia do jogo) | vs. 5–8 |

Tempos: sopa de cogumelo como as sopas atuais (`TempoCozimentoSegundos`); enlatados ~20 s; geleia mais
lenta (~90 s). Máximo de ingredientes na panela segue `MaxIngredientes` (latas contam como ingrediente).

## Resultado esperado (critérios de aceite)
Fase 1:
- [ ] Sopa de cogumelo funciona como as de tomate/batata: ingrediente na panela (K e modo de colocação),
      cozimento, evaporação, comer da panela, servir em prato e caneca, prato/caneca voltam depois de comer.
- [ ] Sopa de cogumelo não envenena.
- [ ] Lata (tomate, feijão, atum) despejada com K: lata some, conteúdo esquenta sem água; sopa de tomate
      em lata vira a sopa de tomate do mod; feijão serve em prato e caneca; atum só em prato.
- [ ] Quente rende mais que frio, conforme a tabela.
- [ ] Panela continua aceitando **uma receita por vez**; misturar dá aviso (tomate fresco + lata de
      tomate é a mesma receita e pode).
- [ ] Canecas de outros mods (0001) funcionam com as receitas novas que vão em caneca.
Fase 2:
- [ ] Frutas sem água viram geleia no fogo; 1 fruta = 1 pote; pote tingido salva e recarrega; comer dá
      fome + energia configuráveis; geleia não evapora; com água dá aviso.
Ambas:
- [ ] Todos os textos novos (nomes de itens, dicas, avisos) em PT-BR e EN.
- [ ] Fome/sede/energia, tempos e rendimento de cada receita configuráveis no `.cfg`.
- [ ] Sopas de tomate e batata, pratos e canecas e saves da 1.4.x funcionam igual (regressão).
- [ ] Co-op: cliente vê conteúdo, cor e itens novos; quem entra no meio vê certo.
- [ ] Sem o mod, o save abre (itens novos só somem).
- [ ] Kit de teste (F8) inclui os ingredientes novos.

## Escopo
- Dentro: tabela de receitas (generalizar `SoupType`), as receitas acima em duas fases, itens novos de
  prato/caneca/pote, textos, `.cfg`, LEIA-ME/Nexus, kit de teste.
- Fora: receitas com mais de um tipo de ingrediente (ex.: sopa de tomate com batata); melancia na geleia;
  lata vazia/sucata; frigideira; forno; mudar o cozimento do próprio jogo (bife, coxa); temperatura
  corporal (o jogo não tem).
- Esta rodada implementa a **fase 1**; a fase 2 vem depois do teste da 1.5.0.

## Invariantes
- **Jogador guiado:** linhas novas no painel ("Colocar cogumelo", "Despejar na panela", "Pegar pote de
  geleia"); avisos novos para receita errada/sem água/água demais/atum na caneca; dica de estado da
  panela cobre as receitas novas; LEIA-ME e Nexus explicam cada receita.
- **Host autoridade:** host valida ingrediente/lata na mão e decide receita, cozimento e itens criados
  (mesmo padrão `CeRequest` → `Server`). Estado da panela continua indo por `CePotState`.
- **Convive bem:** valores novos de `SoupType`/receita só **acrescentados** (rede e arquivo de panelas);
  assetIds novos `0xCE5A0006+` sem reaproveitar; chaves de save novas `CozinhaExtra:…`; panelas salvas
  na 1.4.x carregam. Mudança de rede ⇒ 1.5.0 em todos os PCs (protocolo novo no Hello). Não alterar o
  que a panela do jogo cozinha (coxa, bife); maçã/banana só mudam no caso da geleia (fase 2).
- **Jogo enxuto:** RefCheck.
- **Performance:** moldes criados uma vez por receita; nada novo por frame além do que já existe.
- **Bilíngue/configurável:** tudo acima.

## Compatibilidade
- Arquivo `CozinhaExtra.panelas.txt` da 1.4.x precisa continuar lendo (tipos 1 e 2 iguais).
- Itens de save `CozinhaExtra:TomatoSoupPlate` etc. inalterados.
- Canecas das receitas novas herdam o comportamento das canecas coloridas da 0001.

## Decisões (Augusto, 2026-11-19)
1. **Geleia entra**, na fase 2 (1.5.1), com maçã, banana e laranja; sem melancia.
2. **Recipiente da geleia:** pote novo (visual do `PeanutButter` tingido), sai direto da panela, 1 fruta
   = 1 pote, some ao comer.
3. **Enlatados:** despejar na panela; a lata some; não precisa de água; sopa de tomate em lata vira a
   sopa de tomate do mod.
4. **Atum** entra como "Atum quente", só em prato.
5. **Balanceamento:** tabela acima como ponto de partida, tudo no `.cfg`.
6. **Cozimento do jogo:** resolvido — cogumelo, frutas e latas são `Food`, não `OrganicFood`; sem conflito.
7. **Fases:** uma spec; 1.5.0 = base + cogumelo + enlatados; 1.5.1 = geleia.
