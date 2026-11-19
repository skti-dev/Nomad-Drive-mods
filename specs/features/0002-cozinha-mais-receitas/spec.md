# [0002] Mais receitas na panela: sopa de cogumelo, geleia e enlatados aquecidos

- Status: rascunho
- Tipo: melhoria
- Mod: CozinhaExtra
- Versão alvo: 1.5.0
- Origem: sugestão de jogador no Nexus (2026-11): *"could we possibly cook more types of food? For
  example: mushroom soup, jam, or even heating up various canned foods."*

## Problema / motivação
Hoje a panela só faz duas sopas (tomate e batata). O jogo tem outros alimentos que combinam com
cozinhar e que o jogador já carrega por aí: **cogumelo** (`Mushroom`), frutas (`Apple`, `Orange`,
`Banana`, `Watermelon`) e **enlatados** (`BakedBeans`, `TomatoSoup`, `TunaCan`). Não existem frutas
vermelhas nem açúcar no jogo (ver `specs/system/jogo.md`).

O código atual assume duas sopas em vários lugares (`SoupType` com Tomato/Potato, ternários
`soup == Potato ? … : …` em `Items`, `Cfg.Nutrition/Hydration`, `Game.SoupFor`, `PotPatcher.ExtraItems`,
moldes de prato/caneca por sopa com assetIds `0xCE5A0002–05`). Acrescentar receitas pede uma
**tabela de receitas** em vez de mais ternários.

## Experiência do jogador

### A. Sopa de cogumelo (mesmo fluxo das sopas atuais)
1. Põe água na panela, coloca 1–2 cogumelos (K mirando a panela, ou modo de colocação).
2. Leva ao fogo aceso; em ~60 s vira **sopa de cogumelo** (líquido marrom-claro).
3. Come da panela ou serve em prato/caneca: "Prato de sopa de cogumelo" / "Caneca de sopa de cogumelo".

### B. Geleia de fruta (proposta — ver questões 1 e 2)
1. Coloca frutas na panela **sem água** (maçã, laranja ou banana; uma fruta por vez, como as sopas).
2. Leva ao fogo: a fruta "derrete" e vira **geleia** (cor da fruta), mais lenta que a sopa.
3. Serve: com a panela de geleia, K com a mão vazia (ou mirando) dá um **pote de geleia** — item novo
   com o visual do pote de pasta de amendoim do jogo na cor da geleia. Comer o pote mata a fome e dá
   um pouco de **energia** (`PlayerStatType.Energy`), sem matar sede.
4. Geleia **não evapora** (não tem água) e não é servida em prato/caneca.

### C. Enlatados aquecidos (proposta — ver questão 3)
1. Com uma lata do jogo (feijão, sopa de tomate, atum) na mão, mira a panela (com ou sem água, ver
   questão) e aperta K: "Despejar na panela [K]". A lata é aberta e o conteúdo vai para a panela.
2. No fogo aceso, esquenta rápido (~20 s).
3. Serve como as sopas: prato ou caneca (sopa de tomate em lata, feijão), ou só prato (atum).
   Quente rende **mais** que comer frio da lata (ex.: +25% de fome, um pouco de energia) e, por ser
   panela, dá para **dividir** em porções com o amigo.

Em tudo: olhando para a panela, a dica embaixo da mira mostra o que tem dentro e quanto falta;
avisos claros quando não dá (ex.: "Geleia não leva água", "Já tem outra receita na panela").

## Resultado esperado (critérios de aceite)
- [ ] Sopa de cogumelo funciona como as de tomate/batata: ingrediente na panela (K e modo de colocação),
      cozimento, evaporação, comer da panela, servir em prato e caneca, prato/caneca voltam depois de comer.
- [ ] Geleia (se aprovada): frutas sem água viram geleia no fogo; servida em pote próprio; pote salva e
      recarrega; comer dá fome + energia configuráveis.
- [ ] Enlatados (se aprovado): lata despejada vira prato quente na panela; quente rende mais que frio;
      a lata some (ou vira lata vazia, ver questão).
- [ ] Panela continua aceitando **uma receita por vez**; misturar dá aviso.
- [ ] Todos os textos novos (nomes de itens, dicas, avisos) em PT-BR e EN.
- [ ] Fome/sede/energia, tempos e rendimento de cada receita configuráveis no `.cfg`.
- [ ] Sopas de tomate e batata, pratos e canecas e saves da 1.4.x funcionam igual (regressão).
- [ ] Co-op: cliente vê conteúdo, cor e itens novos; quem entra no meio vê certo.
- [ ] Sem o mod, o save abre (itens novos só somem).
- [ ] Kit de teste (F8) inclui os ingredientes novos.

## Escopo
- Dentro: tabela de receitas (generalizar `SoupType`), as 3 receitas acima conforme aprovação,
  itens novos de prato/caneca/pote, textos, `.cfg`, LEIA-ME/Nexus, kit de teste.
- Fora: receitas com mais de um tipo de ingrediente (ex.: sopa de tomate com batata); frigideira;
  forno; mudar o cozimento do próprio jogo (bife, coxa); temperatura corporal (o jogo não tem).

## Invariantes
- **Jogador guiado:** linhas novas no painel ("Colocar cogumelo", "Despejar na panela", "Pegar pote de
  geleia"); avisos novos para receita errada/sem água/água demais; dica de estado da panela cobre as
  receitas novas; LEIA-ME e Nexus explicam cada receita.
- **Host autoridade:** host valida ingrediente/lata na mão e decide receita, cozimento e itens criados
  (mesmo padrão `CeRequest` → `Server`). Estado da panela continua indo por `CePotState`.
- **Convive bem:** valores novos de `SoupType`/receita só **acrescentados** (rede e arquivo de panelas);
  assetIds novos `0xCE5A0006+` sem reaproveitar; chaves de save novas `CozinhaExtra:…`; panelas salvas
  na 1.4.x carregam. Mudança de rede ⇒ 1.5.0 em todos os PCs. Não alterar o que a panela do jogo já
  aceita/cozinha (maçã, banana, coxa, bife) de um jeito que quebre o comportamento original.
- **Jogo enxuto:** RefCheck.
- **Performance:** moldes criados uma vez por receita; nada novo por frame além do que já existe.
- **Bilíngue/configurável:** tudo acima.

## Compatibilidade
- Arquivo `CozinhaExtra.panelas.txt` da 1.4.x precisa continuar lendo (tipos 1 e 2 iguais).
- Itens de save `CozinhaExtra:TomatoSoupPlate` etc. inalterados.
- Se a 0001 (canecas coloridas) entrar antes, as canecas das receitas novas herdam o mesmo comportamento.

## Riscos / questões em aberto (para o Augusto decidir)
1. **Geleia entra?** Não há frutas vermelhas nem açúcar no jogo; a proposta usa maçã/laranja/banana.
   Alternativa: deixar geleia de fora e responder ao jogador por quê.
2. **Recipiente da geleia:** pote novo (visual do `PeanutButter`) que sai da panela sem precisar de
   recipiente vazio — recomendado — ou servir no prato.
3. **Enlatados:** (a) despejar a lata na panela (recomendado: reaproveita panela/prato/caneca e permite
   dividir), ou (b) pôr a lata fechada em banho-maria e ela vira "lata quente" (item novo por lata).
   E: a lata vazia some ou volta para a mão como sucata?
4. **Atum:** tem sentido aquecer atum? Proposta: entra, mas só servido em prato.
5. **Balanceamento:** valores atuais das comidas do jogo ainda não foram lidos dos bundles (a lata de
   sopa dá 20 de fome segundo o `.cfg`). O plano lê os valores de `Mushroom`, frutas e latas com
   `tools/unity-dump` e propõe números.
6. **Cozimento do jogo:** maçã e banana já são aceitas na panela pelo jogo puro e pode haver
   cozimento próprio (`OrganicFood`). O plano precisa confirmar se cogumelo/frutas são `OrganicFood`
   cozíveis para não brigar com o jogo.
7. **Tamanho:** pode valer dividir em entregas (1.5.0 cogumelo + base de receitas; 1.5.1 enlatados;
   1.5.2 geleia). Recomendação: sim, uma spec só, plano em fases.
