# Invariantes (não negociáveis)

Toda spec, plano e tarefa tem uma seção dizendo **como** cada invariante é respeitada.
Uma mudança que viola uma delas não passa no `/spec-critique`.

## 1. O jogador nunca fica perdido
- Toda ação nova aparece no **painel de ações do jogo** (`ActionHint`, ex.: "Servir sopa na caneca [K]")
  só quando ela é possível naquele momento.
- Todo pedido recusado pelo host vira **aviso** (`CeFeedback` → toast) dizendo o porquê
  ("Esvazie a caneca primeiro", "Falta água"...). Nada de tecla que "não faz nada".
- Mirar na panela mostra o estado dela (água, cozimento, porções, evaporação).
- `LEIA-ME.txt` e a descrição do Nexus explicam toda mecânica nova, nos dois idiomas.

## 2. Host é a autoridade (Mirror)
- Tudo que muda o mundo roda **no host** (`Server`). O cliente só manda intenção (`CeRequest`).
- O host confere: o item que o cliente diz ter na mão (`Game.ServerHeld`: equipado, fora da hotbar,
  dono = quem pediu), distância (`Game.Near`), estado (panela, porções, recipiente vazio), cooldown.
- Itens novos são criados com `NetworkServer.Spawn` a partir de moldes registrados em **todos** os PCs.
- Mensagens próprias **só para quem disse "olá"** (tem o mod): o Mirror desconecta quem recebe
  mensagem desconhecida.
- Estado visual que o cliente precisa saber (cor, conteúdo) chega por mensagem/SyncVar do host,
  inclusive para quem **entra no meio** da partida.

## 3. Convive bem (save, co-op, outros mods, atualizações do jogo)
- **Save:** itens do mod usam chave própria (`CozinhaExtra:…`); sem o mod o jogo só pula o registro.
  Nunca gravar algo que impeça o save de abrir sem o mod.
- **Versões:** host e clientes precisam da mesma versão; mudar enum/mensagem de rede = subir versão.
  Valores de enum que vão para rede ou arquivo **nunca** são reordenados (só acrescentados).
- **assetIds** do mod são fixos e no intervalo `0xCE5A0000–0xCE5AFFFF` (CozinhaExtra). Não reaproveitar.
- **Outros mods:** reconhecer itens pelo **componente/comportamento** do jogo (ex.: `Mug`), não por nome
  exato; preservar o que o item original tinha (prefab, materiais, guid de save). Nenhuma dependência
  obrigatória de mod de terceiro.
- **Ganchos (Harmony):** cada patch aplicado à parte (`TryPatch`); se o jogo mudar e um falhar, só
  aquela parte desliga, com aviso no log.

## 4. Jogo enxuto (*managed stripping*)
- As DLLs do jogo (mscorlib, System, UnityEngine) vêm **sem** partes da API. Compilar não garante
  que roda (ex.: `Mathf.PI` não existe).
- Toda build que vai para o jogo passa no **RefCheck** (`tools/RefCheck`) sem faltas.
- Evite LINQ/Reflection exóticos; se usar, o RefCheck confirma.

## 5. Performance
- `Update` do mod é O(1) e barato quando não há nada acontecendo.
- Nada de `FindObjectsByType`, `GetComponentsInChildren` ou alocação **por frame**; use listas
  mantidas por eventos (`CePlate.Live`, `PotPatcher.Pots`) e checagens espaçadas.
- Rede: estado só quando muda (`Dirty`), mensagens pequenas.
- Materiais/malhas criados **uma vez** (moldes) ou cacheados por chave; nunca um `new Material` por item
  sem cache (vaza memória).

## 6. Bilíngue e configurável
- Todo texto visível em **PT-BR e EN**, seguindo o idioma escolhido no jogo (`L.T(pt, en)`;
  nomes de item via gancho do tradutor `Items.TryTranslate`).
- Números de gameplay (fome, sede, tempos, chances) no `.cfg` do BepInEx, com descrição. Regras
  da panela valem as do host.
- Kit de teste **desligado** por padrão no zip do Nexus.
