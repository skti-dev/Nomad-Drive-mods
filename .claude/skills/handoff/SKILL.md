---
name: handoff
description: Passa o bastão entre sessões dos mods do Nomad Drive. Escreve (out) ou lê (in) um handoff curto com o estado VIVO de um trabalho (branch, próximo passo, pendências, armadilhas) para a próxima sessão retomar limpa, sem reconstruir contexto. Use quando a sessão está longa ou ao retomar. Não substitui a memória nem as specs.
---

# handoff

Onde mora: `specs/features/NNNN-slug/handoff.md` (trabalho de spec) ou `.claude/handoffs/<tag>.md` (solto).
Uso: `/handoff out [alvo]`, `/handoff in [alvo]`, `/handoff list`.

## out
1. Destino: arg `NNNN-...` → pasta da spec; tag livre → `.claude/handoffs/`; sem arg → infira da branch.
2. Levante o estado **real**: `git branch --show-current`, `git log -1 --oneline`, `git status -s`,
   versão em `Plugin.Version` e se a DLL instalada no jogo é a da branch.
3. Escreva o template abaixo, **curto** (cabe numa tela).
4. Pergunte se algo virou fato durável para promover 1 linha a `specs/system/jogo.md` ou à memória.

## in
1. Leia o handoff. 2. Confira o terreno (`git` bate com o esperado?). Divergiu ⇒ pare e avise.
3. Comece pelo **Próximo passo**; não releia o projeto inteiro.

## list
Liste `specs/features/*/handoff.md` e `.claude/handoffs/*.md` com branch e próximo passo.

Quando o trabalho fecha: apague o handoff.

## Template
```markdown
# Handoff — <alvo> · <AAAA-MM-DD>
- Branch: … · Último commit: … · Working tree: limpo | N arquivos · DLL no jogo: <versão/branch>
## Próximo passo (1 ação concreta)
## Em voo
## Aguardando teste do Augusto
## Decisões pendentes
## Não faça (armadilhas conhecidas)
## Promover à memória / jogo.md
```
