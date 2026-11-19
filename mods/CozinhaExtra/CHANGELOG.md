# Changelog — Cozinha Extra

## [1.5.0] — 2026-11-19 (spec 0002, fase 1)
- Novo: **sopa de cogumelo** (água + cogumelo, como as outras sopas). Não envenena.
- Novo: **latas na panela** — "Despejar na panela [K]" com lata de sopa de tomate, feijão ou atum.
  A lata some, não precisa de água e esquenta em 20 s; cada lata rende 2 porções.
  Sopa de tomate em lata vira a sopa de tomate da panela; feijão quente (prato ou caneca) e atum
  quente (só prato) dão fome e energia.
- Textos de painel e avisos genéricos por receita ("Comer feijão quente", "Servir … no prato",
  "Atum quente só vai no prato", "Lata despejada na panela").
- Configuração: seções `[SopaCogumelo]`, `[FeijaoQuente]`, `[AtumQuente]`; `TempoAquecerLataSegundos`,
  `PorcoesPorLata`. As chaves antigas continuam valendo.
- Código: tabela de receitas (`Recipes.cs`) no lugar dos "tomate ou batata"; panela guarda latas
  aquecendo (`Canned`), também no `CozinhaExtra.panelas.txt` (7º campo, opcional).
- Rede: protocolo 3; `CePotState` ganha 1 byte no fim (cliente 1.4.x ignora; cliente 1.5.0 aceita
  host 1.4.x). Itens novos com assetIds `0xCE5A0006–0A`.

## [1.4.3] — 2026-11-19 (spec 0001)
- Corrigido: canecas de outros mods (ex.: coloridas do Nomad Items) viravam a caneca branca do jogo
  ao servir sopa e depois de tomar. A caneca de sopa fica com o modelo e o ícone da caneca usada e a
  mesma caneca volta depois de tomar; vale também depois de salvar e carregar.
- Rede: Hello leva a versão do protocolo (2); mensagem nova `CeMugSkin` só para clientes 1.4.3+.
  Cliente 1.4.2 continua jogando (vê canecas brancas).
- Host esquece quem tinha o mod quando a conexão sai (antes só ao fechar a partida).
- A caneca devolvida não sorteia mais prato ao lado dela.

## [1.4.2] — 2026-11-09
Versão publicada no Nexus e trazida para este repositório (histórico das versões anteriores
na seção CHANGELOG de `nexus/descricao-nexus.txt`):
sopas de tomate e batata na panela, evaporação no fogo, comer da panela, pratos (item novo) e
canecas do jogo, prato/caneca voltam depois de comer, pratos pelo mapa, save dos itens e das
panelas, textos PT-BR/EN, kit de teste (F8, desligado).
