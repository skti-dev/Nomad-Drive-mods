---
name: spec-plan
description: Gera o plano técnico (plan.md) de uma spec aprovada dos mods do Nomad Drive. Confirma as APIs reais do jogo (código descompilado em .game-sources), valores de prefab (tools/unity-dump) e mods de terceiros (.third-party), preenche a tabela das invariantes e lista arquivos, ordem e como testar no jogo. NÃO escreve código.
---

# spec-plan

1. Leia a `spec.md`, `specs/system/arquitetura.md`, `invariantes.md` e `jogo.md`.
2. **Confirme as premissas** (o jogo atualiza e as DLLs são enxutas). Fontes, em ordem:
   código do próprio mod → `.game-sources/` (se faltar ou estiver velho: `bash tools/decompilar.sh`;
   compare `VERSAO-DO-JOGO.txt`) → bundles com `tools/unity-dump` → mods de terceiros em
   `.third-party/<Mod>/src/`. Cite `arquivo:linha` de cada classe/método/campo usado.
   Se a premissa precisa de teste em jogo, diga como testar.
3. Preencha a tabela das **6 invariantes** com evidências. Performance: diga o que roda por frame e o
   custo; o que é criado por item e como é cacheado.
4. Liste arquivos, a ordem de execução e **como testar no jogo** (solo, co-op, save, mod de terceiro).
5. Gere `plan.md` a partir do template. Resuma e peça aprovação. Próximo: `/spec-tasks`.

Regras: dúvida real vira risco explícito, nunca suposição. Para investigar muito código, use um
subagente leitor e traga só as conclusões com `arquivo:linha`.
