---
name: spec-tasks
description: Quebra um plan.md aprovado dos mods do Nomad Drive em um checklist executável (tasks.md), com critério de aceite e invariante por tarefa, mais a Definition of Done (build, RefCheck, teste no jogo, co-op, save). NÃO escreve código.
---

# spec-tasks

1. Leia `plan.md` e `spec.md`.
2. Quebre em tarefas pequenas e verificáveis, agrupadas em host/lógica → cliente/visual →
   save/rede/compatibilidade → textos e docs. Cada uma com **critério de aceite** e a **invariante** que respeita.
3. Primeira tarefa: branch `feat|fix/NNNN-slug` a partir de `staging`.
4. Inclua a Definition of Done do template (build, RefCheck, solo, co-op, save, log limpo) e a
   atualização de versão/LEIA-ME/CHANGELOG/Nexus.
5. Gere `tasks.md` a partir do template. Resuma e peça aprovação.
