# Tarefas — [0001] Canecas coloridas mantêm a cor ao servir sopa e ao devolver

> Primeira tarefa: criar a branch `fix/0001-cozinha-canecas-coloridas` a partir de `staging`.

## Host / lógica
- [x] `CePlate.SourceAssetId/SourceGuid`; `MugSource.Read` e `TryGetPrefab` — caneca branca do jogo devolve 0 — invariante: convive
- [x] `MugSkins.Apply` (materiais do `Model`, idempotente, sem alocar material) — invariante: performance
- [x] `Serve` e `ServeFromPot` leem a origem antes de destruir e aplicam na caneca de sopa; `SpawnIntoHand` retorna o objeto — invariante: host
- [x] `ConsumePrefix/Postfix` com `__state` = struct `Eaten` (sem referência Unity); `GiveContainerBack` devolve o prefab de origem com o GUID de origem, senão a branca — invariante: convive
- [x] `LootPlates.Suppressed` em `try/finally` durante a devolução — critério: nenhum prato aparece ao lado da caneca devolvida

## Cliente / visual
- [x] Hello manda `Target = Net.Protocol` (2) — invariante: convive
- [x] Handler `CeMugSkin` → `KnownSkins[netId]` (sem expirar, limpo no Reset) e aplica se o objeto existe — invariante: host/convive
- [x] `CePlate.Start` (cliente) consulta `KnownSkins` e aplica — critério: caneca que some por distância e volta continua colorida
- [x] `Apply` ignora sopa estragada e malha com nº de submalhas diferente — invariante: convive
- [x] Ícone da hotbar na cor da origem (**só se aprovado no gate**), config clonado com `DontUnloadUnusedAsset`, cacheado por (kind, assetId) e testado com `!= null`

## Save / rede / compatibilidade
- [x] `CeMugSkin` com Writer/Reader; enviado no spawn e em `SendAll` para todas as conexões com protocolo ≥ 2, nunca para a conexão local — invariante: host/convive
- [x] `ProtocolOf` gravado antes do `SendAll`, sobrescrito a cada Hello e removido (com `ModdedConnections`) quando a conexão some
- [x] Chave `CozinhaExtra:<Molde>|<assetId hex>|<guid>`; `TryGetTemplate`/`SaveRestore.Spawn` entendem o sufixo e reaplicam a origem — critério: salvar/carregar mantém a cor
- [x] Sem origem ⇒ chave e comportamento da 1.4.2 (regressão)

## Textos e docs
- [x] `Plugin.Version = "1.4.3"`; `LEIA-ME.txt` (PT) e `nexus/descricao-nexus.txt` (EN): canecas de outros mods mantêm a cor
- [x] `CHANGELOG.md` 1.4.3; `specs/system/arquitetura.md` (mensagem `CeMugSkin`, protocolo, chave com origem); `roadmap.md`

## Validação final (Definition of Done)
- [x] `dotnet build mods/CozinhaExtra -c Release` sem erros nem avisos novos (copia a DLL para o jogo)
- [x] RefCheck sem faltas
- [ ] Teste no jogo, solo — passos 1–4, 6 e 7 do plano (Augusto)
- [ ] Teste em co-op — passos 2b e 5 do plano (Augusto + amigo)
- [ ] `BepInEx\LogOutput.log` sem erros do mod
