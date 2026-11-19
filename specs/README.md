# Spec-Driven Development — Mods do Nomad Drive

Nenhuma mudança nos mods nasce sem uma **spec aprovada**. Este diretório guarda o contexto do todo
e o histórico de decisões, para nenhuma sessão nova precisar redescobrir o projeto.

```
specs/
  system/                    # o "todo": visão, invariantes, arquitetura, jogo, roadmap, glossário, publicação
  features/NNNN-slug/        # spec.md → plan.md → tasks.md (+ critique.md, handoff.md)
  _templates/
```

Numeração única para os dois mods; o slug começa pelo mod (`cozinha-…`, `esponja-…`).

## Fluxo
**Granular (3 gates):** `/spec-new` → `/spec-plan` → `/spec-tasks` (+ `/spec-critique` opcional) → implementar.

**Rápido (`/kickoff`, 1 gate):** task em linguagem livre → spec+plan+tasks + auto-crítica → um "ok" → implementa.

**Entre sessões:** `/handoff out` fecha com o estado vivo; `/handoff in` retoma numa sessão limpa.

Status: `rascunho → aprovada → em implementação → concluída`. Só o humano aprova.

## Specs
| Nº | Mod | Tipo | Título | Status |
|---|---|---|---|---|
| [0001](features/0001-cozinha-canecas-coloridas/spec.md) | CozinhaExtra | correção | Canecas coloridas mantêm a cor | concluída (staging) |
| [0002](features/0002-cozinha-mais-receitas/spec.md) | CozinhaExtra | melhoria | Mais receitas na panela | rascunho |
