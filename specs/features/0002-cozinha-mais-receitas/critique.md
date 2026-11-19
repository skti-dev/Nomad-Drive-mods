# Crítica — [0002] Mais receitas na panela, fase 1 (v1)

- Confiança: **alta** (premissas conferidas em `.game-sources` do jogo e do Mirror e no dump dos prefabs; nada testado no jogo ainda)
- Veredito: **GO com ajustes** — nenhum bloqueante; ajustes 4, 5, 7 e 8 obrigatórios antes do código, todos incorporados
  ao `plan.md` (§9) e ao `tasks.md`.

## Achados
1. ✅ **Ids dos itens** (`_displayName` no bundle): `TomatoSoup`, `BakedBeans`, `TunaCan`, `Mushroom`, `Tomato`, `Potato`;
   batem com `Game.ItemId` (`Game.cs:19-31`). Itens do mod viram `CE_…` (`Items.cs:517`) ⇒ prato ≠ lata.
2. ✅ **Premissas do jogo**: só `OrganicFood` cozinha (`CookingHeatZone.cs:99`, `Pan.cs:158-170`); energia tratada em
   `ApplyConsumableEffects` (`PlayerStatsManager.cs:393`) e no pulso da barra (`PeakStatusBarDisplayer.cs:301`);
   `ServerConsumeFromHand` só destrói, sem efeito de comer/veneno (`HeldItem.cs:675-698`); cogumelo sem `Model`.
3. ✅ **Rede mista**: tamanho por mensagem (`Unbatcher.cs:65-70`), leitor limitado ao segmento (`NetworkClient.cs:256`),
   `WrapHandler` não exige consumir tudo mas desconecta em exceção (`NetworkMessages.cs:79-83`) ⇒ `Remaining > 0` obrigatório
   (`NetworkReader.cs:19`); assetId desconhecido só loga (`NetworkClient.cs:880,1044-1046`); feedback 14/15 sem `case` no
   cliente antigo não derruba; `KnowsMugSkins (>= 2)` segue válido com protocolo 3.
4. ⚠️ **Lotação no modo de colocação** só conferia `Raw` (`Server.cs:602`). Ajuste: `Raw + Canned`. *(incorporado)*
5. ⚠️ **Panela só com lata e sem água tratada como vazia** no status (`Client.cs:419`), no `Cook` (`Server.cs:185`) e no
   `TryRestore` (`Server.cs:986-994`). Ajuste: incluir `Canned` nos três. *(incorporado)*
6. ⚠️ **Progresso compartilhado** com velocidades diferentes (lata a 90% + tomate = sopa em 6 s). Ajuste: reescalar
   `Progress` ao entrar o primeiro fresco depois de lata. *(incorporado)*
7. ⚠️ **`SoupType` desconhecido** (rollback da 1.5.1, arquivo editado) ⇒ NRE/texto vazio/`PlateOnly` errado. Ajuste:
   descartar no `PotStore.Load`, reservas em `L.Soup`/`SoupColor`, `PotEmpty` em `Serve`. *(incorporado)*
8. ⚠️ **Chave/nome do molde** sai do nome do objeto (`Items.cs:429,573`); laço genérico geraria `BeansSoupMug`. Ajuste:
   passar `PlateKey/MugKey`; conferir chaves de tomate/batata inalteradas. *(incorporado)*
9. ⚠️ **`PlateOnly` fixo em "Atum"**. Ajuste: "{receita} só vai no prato". *(incorporado)*
10. ⚠️ **Cliente 1.4.3 + host 1.5.0, caneca × panela de atum**: tecla muda (cliente ignora código 15). Ajuste: `PotEmpty`
    para protocolo < 3. *(incorporado)*
11. ⚠️ **Cliente 1.5.0 + host 1.4.x**: despejar lata/cogumelo sem resposta. Aceito e registrado em Riscos. *(incorporado)*
12. ⚠️ **`EatPortionLocally`** reserva sem energia (`Client.cs:222-223`); `Stats()` com hidratação 0. Ajuste. *(incorporado)*
13. ⚠️ **Escala de energia**: máximo 100 (`PlayerStatsManager.cs:199`); bebidas dão energia (`PlayerLiquidEffectsManager.cs:205-227`).
    5/porção mantido como inicial, ajustável. *(registrado)*
14. ⚠️ **Textos**: descrições do `.cfg` com "tomate/batata" (`Plugin.cs`); `L.Ingredient` null para latas; "evaporou" para
    feijão/atum. Ajustes de texto. *(incorporado)*
15. ✅ **`Kind`** = sopa + 10 cabe até a receita 9; `MugSkins` genérico (`MugSkins.cs:157,169`).
16. ✅ **`Serve`/`ServeFromPot`** checam o molde antes de consumir/destruir (`Server.cs:435-439,494-498`).
17. ✅ **`panelas.txt` e save** compatíveis (`Server.cs:958`, `WorldItems.cs:32`).
18. ✅ **Performance**: nada novo por frame; materiais/ícones uma vez por receita.
19. ⚠️ **Faltas menores no tasks.md**: `_pendingLoads` com cogumelo, chaves Addressables das latas, cabeçalho do
    `panelas.txt`, nome do zip. *(incorporado; zip fica para a publicação)*
