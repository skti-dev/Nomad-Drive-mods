# CLAUDE.md — Mods do Nomad Drive (BepInEx, co-op)

Mods do Augusto para o **Nomad Drive** (Unity + Mirror), publicados no Nexus para jogar em co-op.
Dono: Augusto (dev web fullstack; explique conceitos de C#/Unity/Mirror quando surgirem).

## Regra de ouro
> Antes de qualquer feature, melhoria ou correção:
> 1. Leia `specs/system/invariantes.md`, `specs/system/arquitetura.md` e `specs/system/jogo.md`.
> 2. Garanta uma `spec.md` **aprovada** em `specs/features/NNNN-slug/`.
> 3. Sem spec aprovada, rode `/spec-new` ou `/kickoff`.

## Mods
| Mod | Pasta | Versão | Nexus |
|---|---|---|---|
| Cozinha Extra (*Extra Cooking*) | `mods/CozinhaExtra/` | 1.4.2 | sopas, pratos e canecas |
| Sponge Wash | `mods/SpongeWash/` | 1.2.0 | lavar a esponja |

## As 6 invariantes (detalhes em `specs/system/invariantes.md`)
1. **Jogador nunca perdido**: toda ação aparece no painel de ações do jogo e todo "não deu" vira aviso.
2. **Host é a autoridade**: cliente só manda intenção; host valida item, dono, distância e estado.
3. **Convive bem**: sem quebrar save, sem quebrar co-op, sem quebrar outros mods; gancho que falha só desliga a parte dele.
4. **Jogo enxuto**: as DLLs do jogo são *stripped*; tudo passa no `RefCheck` antes de publicar.
5. **Performance**: nada pesado por frame; sem `FindObjectsByType`/alocação em caminho quente.
6. **Bilíngue e configurável**: textos PT-BR + EN conforme o idioma do jogo; números no `.cfg`.

## Fluxo
`/kickoff "<task>"` (1 gate) ou `/spec-new` → `/spec-plan` → `/spec-tasks` (+ `/spec-critique`).
Sessão longa? `/handoff out` e continue numa sessão nova com `/handoff in`.
Publicar no Nexus: checklist em `specs/system/publicacao.md`.

## Mapa
| Caminho | Papel |
|---|---|
| `specs/system/` | visão, invariantes, arquitetura dos mods, conhecimento do jogo, roadmap, glossário, publicação |
| `specs/features/` | specs numeradas (uma pasta por trabalho; o slug começa pelo mod: `cozinha-…`, `esponja-…`) |
| `mods/<Mod>/` | código do mod (`.csproj`, `.cs`, `LEIA-ME.txt`, `CHANGELOG.md`) |
| `mods/<Mod>/nexus/` | descrição do Nexus, imagens e zips publicados |
| `tools/RefCheck/` | confere se a DLL só usa o que existe no jogo (*stripping*) |
| `tools/unity-dump/` | lê os bundles do jogo (prefabs, componentes, valores) com UnityPy |
| `tools/decompilar.sh` | descompila as DLLs do jogo em `.game-sources/` |
| `.game-sources/` | **fora do git**: código descompilado do jogo, para consulta |
| `.third-party/` | **fora do git**: mods de terceiros para depurar compatibilidade (ex.: Nomad Items) |

## Antes de usar uma API do jogo
- Não chute: procure em `.game-sources/` (se não existir ou o jogo atualizou: `bash tools/decompilar.sh`).
- Valores de prefab (nomes, fome/sede, filhos, materiais): `tools/unity-dump`.
- Mod de terceiro envolvido? Descompile em `.third-party/<Mod>/src` e leia antes de supor.

## Convenções
- Código (identificadores) em inglês; comentários, docs, specs e commits em PT-BR.
- Conventional Commits em PT-BR, com escopo do mod: `fix(cozinha): …`, `feat(esponja): …`, `docs: …`.
- Branches: `main` ← `staging` ← `feat|fix|chore/NNNN-slug` (criada a partir de `staging`).
- Commits na branch da feature durante a implementação: ok.
- Merge da feature em `staging` (`--no-ff`): **só depois que o Augusto testar e aprovar** no jogo.
- Merge de `staging` em `main`, qualquer **push** e qualquer **publicação no Nexus**: só com pedido explícito.
- Versão sobe junto (SemVer): `Plugin.Version`, `LEIA-ME.txt`, `CHANGELOG.md`, nome do zip.
- Ao trabalhar em PowerShell, **não** use `Get-Content`/`Set-Content` em arquivos UTF-8 (estraga acentos).

## Comandos
```bash
dotnet build mods/CozinhaExtra -c Release                     # compila e copia para BepInEx/plugins do jogo
dotnet build mods/CozinhaExtra -c Release -p:CopyToGame=false # só compila
dotnet run --project tools/RefCheck -- "<jogo>/Nomad Drive_Data/Managed" mods/CozinhaExtra/bin/Release/net472/CozinhaExtra.dll
bash tools/decompilar.sh                                      # (re)gera .game-sources/
```
Jogo: `C:\Program Files (x86)\Steam\steamapps\common\Nomad Drive` · log: `BepInEx\LogOutput.log`.
