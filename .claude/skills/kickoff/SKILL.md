---
name: kickoff
description: Orquestrador ponta-a-ponta de uma task dos mods do Nomad Drive. Recebe a task em linguagem livre (ou uma spec já aprovada), gera spec+plan+tasks sem gates intermediários, auto-valida com spec-critique, para em UM gate go/no-go e, após o "ok", implementa na branch feat|fix/NNNN-slug.
---

# kickoff — da task ao código com 1 gate

1. **Situar:** leia `specs/system/*` e o handoff aberto, se houver. Se já existe spec para a task,
   continue dela. Pergunte só o essencial.
2. **Gerar** spec → plan → tasks seguindo `spec-new`, `spec-plan`, `spec-tasks` (sem parar entre eles).
3. **Validar:** rode `/spec-critique`. NO-GO ⇒ corrija e valide de novo.
4. **GATE ÚNICO:** apresente enxuto: o que muda para o jogador, arquivos principais, veredito, riscos.
   Peça um "ok". Sem ok: nada de branch nem código.
5. **Implementar:** status `em implementação`; branch `feat|fix/NNNN-slug` a partir de `staging`;
   siga `tasks.md`. Commits pequenos, Conventional Commits em PT-BR com escopo do mod.
6. **Entregar:** Definition of Done (build Release, RefCheck, `dotnet build` copia para o jogo).
   Teste em jogo e em co-op é do Augusto: diga exatamente o que testar. Após ele aprovar:
   merge `--no-ff` em `staging`. Nunca merge em `main`, push ou publicação no Nexus sem pedido explícito.
   Se a sessão ficou longa, `/handoff out` antes de encerrar.
