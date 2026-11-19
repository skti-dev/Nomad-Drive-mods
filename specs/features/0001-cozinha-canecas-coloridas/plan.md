# Plano — [0001] Canecas coloridas mantêm a cor ao servir sopa e ao devolver

## Design técnico

Ideia central: a caneca de sopa passa a **lembrar a caneca de origem** — o `assetId` (qual prefab) e o
GUID de save (`PersistentObject.AddressableGuid`) dela. Com isso:
- **aparência**: cada PC copia os materiais do `Model` do prefab de origem para o `Model` da caneca de sopa;
- **devolução**: o host cria o prefab de origem com o GUID de origem;
- **save**: a origem vai junto na chave de save da caneca de sopa.

Caneca branca do jogo = sem origem registrada (`SourceAssetId = 0`) ⇒ tudo exatamente como na 1.4.2.

### 1. Dados da origem (`Items.cs`)
- `CePlate` ganha `public uint SourceAssetId;` e `public string SourceGuid;` (só usados em canecas).
- `MugSource.Read(HeldItem mug, out uint assetId, out string guid)` (host): lê
  `mug.netIdentity.assetId` e `mug.GetComponent<PersistentObject>()?.AddressableGuid`.
  Se `assetId == Items.MugPrefab.assetId` (caneca branca do jogo) ⇒ devolve 0 (sem origem).
- `MugSource.TryGetPrefab(uint assetId, out GameObject prefab)`: `NetworkClient.prefabs[assetId]`
  (o Nomad Items registra lá; ver premissas). Não achou ⇒ false (cai para branca).

### 2. Aparência (`MugSkins`, novo arquivo `MugSkins.cs`)
`MugSkins.Apply(CePlate soupMug, uint sourceAssetId)` — idempotente, roda em todo PC:
0. Não faz nada se a sopa estiver `Rotten` (a `Food` troca o material do primeiro renderer para o de
   estragado — `Food.cs:182-184,341-345`; não brigar com isso).
1. `TryGetPrefab` → `prefab.transform.Find("Model")` com `MeshRenderer`/`MeshFilter`. Só copia se a malha de
   origem tiver o **mesmo número de submalhas** que a da caneca do jogo (caneca de outro mod com malha
   diferente ⇒ fica branca em vez de sair com material errado). Pega `sharedMaterials`.
2. `soupMug.transform.Find("Model").GetComponent<MeshRenderer>().sharedMaterials = materiais` (o líquido
   `CozinhaExtra_SoupFill` é filho do `Model` com renderer próprio: não é tocado).
   Materiais **compartilhados** com o prefab de origem: nenhuma alocação por caneca.
3. Ícone (se aprovado — questão 1 da spec): `held.heldItemConfig` = clone do config do molde com
   `inventoryIcon` do config do prefab de origem; clone cacheado em `Dictionary<(byte kind, uint asset), HeldItemConfig>`
   (máx. 2 sopas × nº de cores). Clone idêntico ao original exceto o ícone ⇒ `SetupFromConfig` dá os mesmos valores.
   Clone com `hideFlags = DontUnloadUnusedAsset` (como `Items.cs:460`) e cache testado com `!= null` do Unity
   antes de reusar — senão um `UnloadUnusedAssets` destrói o clone e a caneca perde `isInventoryItem` (não
   entra mais na hotbar, `HotbarManager.cs:1065`).
4. Grava `soupMug.SourceAssetId`.

### 3. Rede (`Net.cs`, `Server.cs`, `Client.cs`)
- Nova mensagem host → clientes: `CeMugSkin { uint Item; uint Source; }` com Writer/Reader à mão
  (como as outras; o weaver não roda na DLL do mod).
- **Versão de protocolo no Hello:** `Net.Protocol = 2`; o cliente manda `CeRequest{Action=Hello, Target=Net.Protocol}`.
  O host guarda `ProtocolOf[connectionId]` (0 = cliente 1.4.2, que manda Target 0). `CeMugSkin` só vai
  para conexões com protocolo ≥ 2 ⇒ cliente 1.4.2 nunca recebe mensagem desconhecida (só vê caneca branca).
- Host aplica direto no objeto dele (`MugSkins.Apply`) e manda `CeMugSkin` para **todas** as conexões
  com protocolo ≥ 2 (perto ou longe; **nunca** para `NetworkServer.localConnection` — não reaproveitar o
  `Broadcast` atual, que inclui a local):
  - ao criar a caneca de sopa colorida (servir nos dois caminhos e restaurar do save);
  - em `SendAll(conn)` (primeiro contato/Hello): uma mensagem por `CePlate.Live` do host com
    `SourceAssetId != 0` (o host tem todas, mesmo as que o cliente não vê) ⇒ cobre quem entra no meio.
  - `ProtocolOf[conn]` é gravado **antes** do `SendAll` e sobrescrito a cada Hello; removido quando a
    conexão some (junto com `ModdedConnections`, na mesma varredura de `SendBeacons`), para um `connectionId`
    reaproveitado não herdar o protocolo de outro cliente.
- Cliente: `ReplaceHandler<CeMugSkin>` junto dos outros handlers. Guarda em `KnownSkins: Dictionary<uint netId, uint source>`
  **sem expirar** (limpo no `Reset` ao sair da partida; netIds não se repetem na sessão e são poucas canecas) e
  aplica se o objeto já existe. **Toda vez que uma caneca de sopa aparece** (`CePlate.Start`, quando o `netId`
  já está definido) consulta `KnownSkins` e aplica. Motivo: o jogo **destrói e recria** loot longe do jogador
  (`LootInterestManagement`, ver premissa 9); a recriação sai do molde branco.
- `Apply` que não acha o prefab de origem (ex.: Nomad Items ainda montando) não perde a informação:
  `SourceAssetId/SourceGuid` ficam gravados e o prefab é resolvido de novo na devolução e numa nova
  tentativa de `Apply` no `CePlate.Start`/próxima mensagem.

### 4. Servir (`Server.Serve` e `Server.ServeFromPot`)
Antes de destruir/consumir a caneca de origem: `MugSource.Read(...)`. Depois de criar a caneca de sopa:
`plate.SourceAssetId/SourceGuid`, `MugSkins.Apply`, broadcast. A chave de save passa a ser a com origem
(ver 6). `SpawnIntoHand` ganha retorno (`GameObject`) para o chamador aplicar a origem.

### 5. Devolver (`Patches.ConsumePrefix/Postfix`, `Server.GiveContainerBack`)
- `__state` passa de `bool` para um struct simples `Eaten { bool Valid; bool IsMug; uint Asset; string Guid; }`
  preenchido no prefix (antes do jogo destruir o item), sem guardar referência Unity.
- `GiveContainerBack(conn, CePlate eaten)`:
  - caneca com `SourceAssetId != 0` e `TryGetPrefab` ok ⇒ `SpawnIntoHand(prefab, conn, guid)` com
    `guid = SourceGuid`, senão `prefab.GetComponent<PersistentObject>()?.AddressableGuid`;
  - senão ⇒ caneca branca, como hoje (LootRegistry `"Mug"`).
  - Após `Instantiate` de prefab de terceiro: `SetActive(true)` (o Nomad Items faz o mesmo em `Spawning.Spawn`).
- Endurecimento barato no mesmo caminho: `LootPlates.Suppressed = true` em `try/finally` durante a
  devolução, para o gancho de "prato ao lado de loot de cozinha" (`Patches.cs:66-70`, categoria Kitchen)
  não sortear prato ao lado da caneca devolvida.

### 6. Save (`WorldItems.SaveRestore`, `Items`)
- Chave da caneca de sopa colorida: `CozinhaExtra:TomatoSoupMug|<assetId hex>|<guid>`
  (`Items.SaveKeyWithSource(template, assetId, guid)`); sem origem, a chave de hoje.
- `SaveRestore.Spawn`: separa a chave com `Split('|', 3)` (guid futuro com `|` não quebra), acha o molde pela base, restaura com a chave
  **inteira** (`PersistentObject.ServerEnsure(go, record.AddressableGuid, record.Guid)` — volta a ser gravada igual),
  preenche `SourceAssetId/SourceGuid` e aplica a aparência.
- Sem o mod: a chave continua começando com `CozinhaExtra:` e o jogo não acha prefab ⇒ pula, como hoje.
- Save 1.4.2: chaves sem `|` ⇒ caneca branca.

## Verificação das invariantes (evidências)
| Invariante | Como é garantida | Evidência |
|---|---|---|
| Jogador guiado | dicas/avisos atuais já valem para canecas coloridas (teste por componente `Mug`) | `Client.cs:297,342`, `Server.cs:411,468` |
| Host autoridade | origem lida pelo host do item que ele já valida; cliente não manda cor | `Game.ServerHeld`, `Server.ServeFromPot` (checagens 452-477) |
| Convive bem | sem referência ao Nomad Items; protocolo no Hello evita derrubar cliente 1.4.2; chave de save com prefixo | `Patches.SpawnFromRecordPrefix`, `SaveRestore.IsModKey` |
| Jogo enxuto | só APIs já usadas pelo mod + `NetworkClient.spawned/prefabs`; RefCheck | RefCheck na DoD |
| Performance | materiais compartilhados; ícone cacheado; pendências só quando houver | §2, §3 |
| Bilíngue/configurável | sem texto novo no jogo; LEIA-ME/Nexus PT+EN | — |

## Verificação de premissas
1. **Caneca do Nomad Items tem `assetId` próprio também no host.** `RegisterPrefab(prefab, newAssetId)` grava
   `component.assetId = newAssetId` no prefab (`.game-sources/Mirror/Mirror/NetworkClient.cs:477-500`) e o
   Nomad Items chama com `AssetFor(id)` depois de zerar `_assetId` (`.third-party/.../Content.cs:1144-1148`);
   as instâncias são `Instantiate` desse prefab (`Spawning.cs:21`) ⇒ copiam o `_assetId`. ✅
   `AssetFor` é hash FNV do id (`Content.cs:2220`) ⇒ estável entre sessões e PCs. ✅
2. **Prefab acessível em `NetworkClient.prefabs`** nos clientes e no host (registro explícito, `Content.cs:1148`;
   re-registro ao trocar de partida, `Content.cs:1199-1201`). ✅
3. **GUID de save da caneca**: instância tem `PersistentObject.AddressableGuid` (`PersistentObject.cs:40`),
   carimbado por `ServerEnsure(obj, s.Guid, …)` no Nomad Items (`Spawning.cs:24`) e por
   `ApplyPostSpawnWorldIntegration` no loot do jogo (`LootSpawningService.cs:518-529`). ✅
   O prefab do Nomad Items também tem o GUID (`Content.cs:1149`) ⇒ fallback. ✅
4. **Visual**: caneca colorida = clone da `Mug` com todos os `MeshRenderer` (exceto líquido) tingidos
   (`Mugs.cs` `Assemble`); o corpo é o `Model`. A caneca de sopa usa a mesma malha no `Model`
   (`Items.BuildMug`) ⇒ copiar `sharedMaterials` do `Model` basta. ⚠️ confirmar no jogo que a caneca do jogo
   não tem outro renderer visível além de `Model` e `Mug-01-LiquidMesh`.
5. **Caneca devolvida não vem cheia**: `ItemRuntime.Start` só enche itens `FoundPartFull`
   (`ItemRuntime.cs:522-530`) e as canecas não têm essa flag (`Content.cs:536-590`). ✅
6. **Ícone por instância**: `HeldItem.InventoryIconOverride` é virtual e devolve null (`HeldItem.cs:445`), sem
   setter ⇒ o caminho é trocar o `heldItemConfig` da instância; hotbar lê `heldItemConfig.inventoryIcon`
   (`HotbarPanel.cs:694`). ⚠️ se a hotbar já mostrou o item antes da troca (cliente entrando no meio com a
   caneca na hotbar), o ícone pode só atualizar no próximo redesenho.
7. **Mensagem desconhecida desconecta** (`Mirror/NetworkClient.cs:228` + `Disconnect` em falha) ⇒ justificativa
   do protocolo no Hello. Clientes novos sempre mandam Hello antes de qualquer pedido (`Client.SendHello` no
   `Tick` e antes de cada `CeRequest`). ✅
8. **Ordem das mensagens**: `NetworkServer.Spawn` envia o spawn antes do nosso `CeMugSkin` no mesmo canal
   confiável ⇒ normalmente o objeto já existe; `KnownSkins` + `CePlate.Start` cobrem o resto.
9. **Loot longe some no cliente** (crítica): `LootInterestManagement.TryGetCullableRanges`
   (`LootInterestManagement.cs:118-134`) corta loot fora do alcance, exceto o que está preso a veículo não
   cortável; ao voltar, o cliente recria do molde (`Mirror/NetworkClient.cs:1032-1035`). A caneca de sopa é loot
   (herda `LootItemDefinition`). ⇒ cor guardada por `netId` no cliente e reaplicada a cada aparição. ✅ (desenho ajustado)
10. **Save não interpreta a chave**: a captura copia `AddressableGuid` como string (`GameSaveService.cs:1294-1310`);
    sem o mod, o load falha no Addressables e pula o registro (`GameSaveService.cs:627-635`). O Nomad Items só faz
    lookup exato das chaves dele (`Content.cs:2186-2211`). ✅ Obs.: `SourceGuid` é o GUID do **tipo** de prefab
    (o que o save usa para recriar), não o da instância.

## Arquivos (criar/alterar)
- `mods/CozinhaExtra/MugSkins.cs` (novo): `MugSource`, `MugSkins.Apply`, cache de ícones, `KnownSkins`.
- `Items.cs` (`CePlate.Start`): reaplica a origem conhecida quando a caneca (re)aparece no cliente.
- `Items.cs`: campos em `CePlate`; `SaveKeyWithSource`; `TryGetTemplate` aceita chave com sufixo.
- `Net.cs`: `CeMugSkin` + serializadores; `Net.Protocol`.
- `Server.cs`: `Serve`, `ServeFromPot`, `GiveContainerBack(conn, CePlate)`, `SpawnIntoHand` retorna `GameObject`,
  `SendAll` manda skins, `ProtocolOf`, limpeza no fim da partida.
- `Client.cs`: Hello com protocolo, handler `CeMugSkin`, pendências.
- `Patches.cs`: `__state` = `CePlate`.
- `WorldItems.cs`: `SaveRestore.Spawn` com origem.
- `Plugin.cs`: `Version = "1.4.3"`.
- Docs: `LEIA-ME.txt`, `CHANGELOG.md`, `nexus/descricao-nexus.txt`, `specs/system/arquitetura.md` (tabela de chaves/mensagens).

## Ordem de execução
dados (`CePlate`, `MugSource`) → aparência (`MugSkins`) → servir → devolver → rede (mensagem, protocolo,
SendAll, cliente) → save → versão e docs → build + RefCheck → teste no jogo.

## Como testar no jogo
Pré: Nomad Items 0.2.80 instalado (copiar `.third-party/NomadItems-0.2.80/BepInEx` para a pasta do jogo) +
Cozinha Extra 1.4.3; kit de teste ligado. Pegar canecas coloridas pelo console (`world.spawnLoot <guid>` ou
achando no mapa).
1. Solo: caneca vermelha na mão → servir → sopa vermelha (mão e hotbar); tomar → volta "Red Mug" vazia.
2. Solo: caneca azul na mesa, panela na mão → servir → caneca de sopa azul no lugar.
2b. Co-op: deixar caneca de sopa colorida no chão (fora do veículo), cliente se afasta > 100 m e volta → continua colorida.
3. Caneca branca: servir e tomar → como na 1.4.2.
4. Salvar com caneca de sopa colorida na mesa, sair, carregar → continua colorida; tomar → volta colorida.
   A caneca devolvida salva e recarrega como "Red Mug" e pendura no porta-canecas do Nomad Items.
5. Co-op (Augusto + amigo, ambos 1.4.3): cliente vê a cor; cliente entra depois de servida e vê a cor.
6. Sem Nomad Items, carregar o save do passo 4: caneca de sopa aparece branca; tomar devolve branca; sem erro no log.
7. Save da 1.4.2 com caneca de sopa: abre e funciona.

## Riscos / rollback
- **Fora do escopo, anotado:** servir com a panela numa caneca pendurada no porta-canecas do Nomad Items
  reencaixa a caneca de sopa no mesmo plano em pé (`Server.cs:486-507`), o que já acontece na 1.4.2. Vai
  ficar mais comum; se der problema no teste, abrir spec própria (ex.: não reencaixar em plano de outro mod).
- Hotbar com ícone antigo até redesenhar (premissa 6): aceitável; se incomodar, fica só a cor do modelo.
- Outro mod com caneca que tem `Mug` mas sem `Model`/`MeshRenderer`: `Apply` não faz nada (fica branca),
  devolução usa o prefab dele mesmo.
- Rollback: reverter a branch; saves feitos na 1.4.3 com chave `…|…|…` abertos na 1.4.2 perdem só essas canecas
  de sopa (o jogo pula o registro), sem quebrar o save.
