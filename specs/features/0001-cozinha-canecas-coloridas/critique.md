# Crítica — [0001] Canecas coloridas (v1)

- Confiança: **média** (premissas conferidas em `.game-sources` do jogo e do Mirror, nos fontes descompilados do
  Nomad Items 0.2.80 e no dump dos prefabs `Mug`/`TomatoSoup`; nada testado no jogo ainda)
- Veredito: **GO com ajustes** — 1 ajuste bloqueante (item 8), já incorporado ao `plan.md`/`tasks.md`.

## Achados
1. ✅ **assetId da caneca do Nomad Items no host** = o registrado: Mirror não recalcula (`NetworkIdentity.cs:96-112`),
   `Spawn` usa o valor da instância (`NetworkServer.cs:940`), o Nomad Items grava no prefab antes de registrar
   (`Content.cs:1146-1148`). ⚠️ O `Build` dele é preguiçoso: pode haver um frame sem prefab no host durante o restore.
   **Ajuste:** guardar origem mesmo sem achar o prefab; resolver de novo na devolução/próximo `Apply`. *(incorporado)*
2. ✅ **Caneca branca** = `MugPrefab.assetId` (`NetworkManager.cs:472-474`). Colisão com o hash do Nomad Items é desprezível.
   ⚠️ **Ajuste:** só copiar materiais se o nº de submalhas bater. *(incorporado)*
3. ✅ **Save** não interpreta `AddressableGuid` (`GameSaveService.cs:1294-1310`, `:627-635`); Nomad Items só faz lookup exato.
   ⚠️ `Split('|', 3)`. *(incorporado)*
4. ⚠️ **Materiais:** só `Model` e líquido são visíveis; contorno/fantasma não desfazem a troca
   (`Interactable.cs:1270-1310`, `HighlightModel.cs:18-29`). Risco: material de "estragado" da `Food`
   (`Food.cs:182-184,341-345`). **Ajuste:** `Apply` ignora sopa `Rotten`. *(incorporado)*
5. ⚠️ **Trocar `heldItemConfig`** é seguro (`HeldItem.cs:2469-2486`), mas o clone precisa de `DontUnloadUnusedAsset`
   e teste `!= null` (senão `UnloadUnusedAssets` destrói e a caneca sai da hotbar, `HotbarManager.cs:1065`). *(incorporado)*
6. ✅ `__state` funciona (destroy adiado, `NetworkServer.cs:1194-1198`). Recomendação de struct sem referência Unity. *(incorporado)*
7. ⚠️ **Protocolo no Hello:** host 1.4.2 ignora `Target` (`Server.cs:245-253`). Ajustes: gravar `ProtocolOf` antes do
   `SendAll`; limpar ao desconectar (reuso de `connectionId`); nunca mandar `CeMugSkin` à conexão local. *(incorporados)*
8. ❌→✅ **Culling de loot:** `LootInterestManagement` corta loot longe (`LootInterestManagement.cs:118-134`) e o cliente
   recria do molde branco (`NetworkClient.cs:1032-1035`). A fila de 10 s do plano v1 perderia a cor.
   **Ajuste (bloqueante):** `KnownSkins` por `netId` sem expirar no cliente + reaplicar em `CePlate.Start`; host manda para
   todos com protocolo ≥ 2. *(incorporado; teste 2b no plano)*
9. ⚠️ `LootPlates.Suppressed` em `try/finally` *(incorporado)*. Caneca pendurada no porta-canecas do Nomad Items +
   servir com a panela: reencaixe em pé já existe na 1.4.2 — anotado como fora do escopo.
