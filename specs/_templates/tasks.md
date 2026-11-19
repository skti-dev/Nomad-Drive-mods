# Tarefas — [NNNN] <título>

> Primeira tarefa: criar a branch `feat|fix/NNNN-slug` a partir de `staging`.

## Host / lógica
- [ ] <tarefa> — critério de aceite — invariante: <guiado|host|convive|enxuto|performance|bilíngue>

## Cliente / visual
- [ ] <tarefa> — critério de aceite

## Save / rede / compatibilidade
- [ ] <tarefa> — critério de aceite

## Textos e docs
- [ ] Textos novos em PT-BR e EN
- [ ] `LEIA-ME.txt`, `CHANGELOG.md`, `nexus/descricao-nexus.txt`, versão em `Plugin.Version`

## Validação final (Definition of Done)
- [ ] `dotnet build -c Release` sem erros nem avisos novos
- [ ] RefCheck sem faltas
- [ ] Teste no jogo, solo: fluxo da spec ok
- [ ] Teste em co-op (host + cliente; cliente entrando no meio) — ou marcado como pendente para o Augusto
- [ ] Salvar, sair e carregar: itens e estado voltam certos; save abre sem o mod
- [ ] `BepInEx\LogOutput.log` sem erros do mod
