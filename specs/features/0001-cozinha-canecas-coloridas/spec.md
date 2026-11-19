# [0001] Canecas coloridas mantêm a cor ao servir sopa e ao devolver

- Status: concluída — publicada na 1.4.3 (2026-11-19)
- Tipo: correção
- Mod: CozinhaExtra
- Versão alvo: 1.4.3
- Origem: autor do mod **Nomad Items** (mensagem no Nexus, 2026-11-19), reportando caso de um jogador que usa os dois mods.

## Problema / motivação
O Nomad Items acrescenta canecas coloridas (vermelha, laranja, amarela, verde, azul, anil, violeta e
rosa). Elas são a `Mug` do jogo com outro material, então a Cozinha Extra já aceita servir sopa nelas
(o teste é `GetComponent<Mug>() != null`). Mas a cor se perde para sempre:

1. **Ao servir:** a caneca de sopa criada é sempre o molde `TomatoMug`/`PotatoMug`, montado uma vez
   com os materiais da caneca **branca** do jogo (`Items.BuildMug` usa `MugPrefab` = prefab `"Mug"`).
   Vale para os dois caminhos: caneca na mão mirando a panela (`Server.Serve`) e panela na mão mirando
   a caneca no mundo (`Server.ServeFromPot`). A caneca original é destruída e nada dela é guardado.
2. **Ao devolver:** depois de tomar a sopa, `Server.GiveContainerBack(conn, mug: true)` sempre cria
   `Items.MugPrefab` (a branca do jogo) com o GUID de save dela.

Resultado: o jogador entra com uma caneca vermelha e sai com uma branca.

Sugestões do reporte (a confirmar no plano): copiar os materiais do renderer `Model` da caneca
original para a caneca de sopa; lembrar o `NetworkIdentity.assetId` da caneca original e criar esse
mesmo prefab ao devolver. As canecas coloridas mantêm o componente `Mug` do jogo e têm nomes
"Red Mug", "Indigo Mug" etc.

## Experiência do jogador
1. Jogador tem uma caneca vermelha (Nomad Items), vazia, na mão. Mira a panela com sopa pronta.
   O painel mostra "Servir sopa na caneca [K]" (igual hoje).
2. Aperta K: a caneca na mão vira **caneca vermelha** com sopa de tomate dentro (mesmo nome de hoje,
   "Caneca de sopa de tomate").
3. Pode guardar na hotbar, pôr na mesa, salvar e sair: ao voltar, continua **vermelha** com sopa.
4. Toma a sopa: volta para a mão a **caneca vermelha vazia** (a mesma "Red Mug" do Nomad Items, que
   continua pendurável no porta-canecas dele e entra no save normalmente).
5. Mesmo comportamento servindo com a panela na mão numa caneca colorida que está na mesa/prateleira:
   a caneca de sopa aparece no lugar, na mesma cor.
6. Caneca branca do jogo: tudo exatamente como hoje.
7. Em co-op, host e cliente veem a mesma cor, inclusive quem entra na partida depois.

## Resultado esperado (critérios de aceite)
- [ ] Servir (nos dois caminhos) numa caneca colorida gera caneca de sopa com a aparência da caneca
      original (materiais do corpo da caneca); o líquido continua com a cor da sopa.
- [ ] Depois de comer, volta para a mão o **mesmo tipo** de caneca que foi usada (mesmo prefab/assetId),
      que se comporta como uma caneca normal daquele mod (salva, recarrega, encaixa onde encaixava).
- [ ] Caneca de sopa colorida salva e recarregada mantém a cor e, ao ser comida, devolve a caneca colorida.
- [ ] Caneca branca do jogo: comportamento idêntico ao da 1.4.2 (regressão).
- [ ] Co-op: host e clientes (inclusive quem entra depois) veem a mesma cor; clientes com o mod nunca
      recebem mensagem que não conhecem.
- [ ] Sem o Nomad Items (ou se a caneca de origem não existir mais no carregamento): cai para a
      caneca branca, sem erro e sem quebrar o save.
- [ ] Saves da 1.4.2 com canecas de sopa abrem normalmente (viram canecas brancas, como eram).
- [ ] Funciona com qualquer caneca que tenha o componente `Mug` do jogo, não só com o Nomad Items
      (sem dependência nem referência ao Nomad Items).

## Escopo
- Dentro: aparência da caneca de sopa, caneca devolvida, persistência disso no save, rede (cor visível
  para todos), os dois caminhos de servir.
- Fora: pratos (são só do mod); ícone na hotbar (ver questão 1); outras cores/itens do Nomad Items;
  mudar nomes dos itens.

## Invariantes
- **Jogador guiado:** nenhuma dica nova; as linhas "Servir sopa na caneca [K]" e os avisos atuais
  continuam valendo para canecas coloridas. Atualizar LEIA-ME/Nexus: "canecas de outros mods mantêm a cor".
- **Host autoridade:** o host lê a caneca de origem (que ele valida hoje: `Game.ServerHeld` / item no
  mundo perto) e decide a aparência e a caneca devolvida. O cliente não informa cor nem assetId.
  A aparência precisa chegar aos clientes por um meio do host (mensagem/estado sincronizado) que
  também funcione para quem entra no meio.
- **Convive bem:** nenhuma dependência do Nomad Items; reconhecer a caneca pelo componente `Mug` e
  guardar o necessário para recriá-la (prefab e GUID de save dela). Save: a informação da caneca de
  origem precisa ir junto da caneca de sopa sem impedir o save de abrir sem o mod (hoje a chave é
  `CozinhaExtra:TomatoSoupMug`). Mudança de rede ⇒ host e clientes na 1.4.3.
- **Jogo enxuto:** RefCheck na build.
- **Performance:** material copiado/compartilhado (não criar material por caneca sem cache); nada por frame.
- **Bilíngue/configurável:** sem textos novos no jogo; LEIA-ME e descrição em PT/EN.

## Compatibilidade
- Save antigo (1.4.2): canecas de sopa sem informação de origem ⇒ branca (comportamento atual).
- Cliente 1.4.2 com host 1.4.3: não suportado (regra de versões iguais); não pode derrubar ninguém
  que tenha a mesma versão.
- Nomad Items presente só no host/só no cliente: problema de instalação do Nomad Items; o nosso lado
  só não pode dar erro (cai para branca).

## Riscos / questões em aberto
1. **Ícone na hotbar** — *decidido em 2026-11-19: incluir.* a caneca de sopa usa o ícone da caneca branca; o Nomad Items tinge o ícone de
   cada cor. Incluir ícone na cor certa nesta correção? (Recomendação: sim se sair barato, ex.: um
   `HeldItemConfig` por caneca de origem, cacheado; senão fica para depois.)
2. **Identificar o prefab de origem no host:** o Nomad Items registra o prefab com
   `NetworkClient.RegisterPrefab(prefab, assetId)` e zera o `_assetId` do molde. Confirmar no plano se
   a instância no host tem o `assetId` certo; alternativa: achar o prefab pelo
   `PersistentObject.addressableGuid` da caneca (`.third-party/NomadItems-0.2.80/src/NomadItems/Content.cs:1144-1149`).
3. **Quais renderers copiar:** o Nomad Items tinge todos os `MeshRenderer` da caneca exceto o líquido
   (`Mugs.Assemble`); a caneca de sopa do mod tem só o `Model` + o líquido próprio. Copiar só os
   materiais do `Model` (como sugerido) deve bastar — confirmar no jogo.
4. **Como gravar a origem no save** sem quebrar sem o mod (ex.: sufixo na chave `CozinhaExtra:…`).
5. Responder ao autor do Nomad Items quando sair a 1.4.3 (o Augusto faz).
