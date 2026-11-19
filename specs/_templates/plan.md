# Plano — [NNNN] <título>

## Design técnico

## Verificação das invariantes (evidências)
| Invariante | Como é garantida | Evidência (classe/método/arquivo) |
|---|---|---|
| Jogador guiado | | |
| Host autoridade | | |
| Convive bem | | |
| Jogo enxuto | | |
| Performance | | |
| Bilíngue/configurável | | |

## Verificação de premissas
Para cada API do jogo/Mirror/Unity usada, cite onde ela existe **nesta versão do jogo**
(`.game-sources/...Classe.cs:linha`) ou o valor lido no bundle (`tools/unity-dump`).
Para mod de terceiro, cite o arquivo em `.third-party/<Mod>/src/`. Premissa não confirmada vira risco.

## Arquivos (criar/alterar)
- `mods/<Mod>/...`
- Docs: `LEIA-ME.txt`, `CHANGELOG.md`, `nexus/descricao-nexus.txt`

## Ordem de execução
dados/enums → moldes/itens → lógica do host → rede → cliente (dicas, avisos, visual) → save → textos

## Como testar no jogo
Passos manuais: solo (host), co-op (host + cliente, inclusive cliente entrando no meio), salvar e
recarregar, e com o mod de terceiro envolvido (se houver). Kit de teste F8 quando servir.

## Riscos / rollback
