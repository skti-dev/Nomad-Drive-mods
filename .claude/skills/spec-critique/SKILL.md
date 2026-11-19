---
name: spec-critique
description: Validador adversarial do planejamento (spec+plan+tasks) de um trabalho nos mods do Nomad Drive. Tenta FALSIFICAR premissas (API que não existe no jogo ou foi removida pelo stripping, lógica no cliente, cliente que entra no meio vendo errado, save que quebra sem o mod, conflito com outro mod, custo por frame escondido, jogador sem dica) e grava o veredito GO | GO com ajustes | NO-GO em critique.md. Não altera os artefatos nem escreve código.
---

# spec-critique

Objetivo: provar que o planejamento está errado antes de virar código.

1. Leia `spec.md`, `plan.md`, `tasks.md` + `specs/system/invariantes.md`, `arquitetura.md` e `jogo.md`.
2. Para premissas de API, dispare **um** subagente leitor (Agent tool) com mandato de falsificar,
   checando em `.game-sources/` (e `.third-party/<Mod>/src/` se houver mod de terceiro).
   Só use mais de um se o trabalho for grande.
3. Dimensões:
   - **Experiência**: o jogador entende sozinho? Painel de ações e avisos cobrem todos os casos?
   - **Host autoridade**: algum valor vem do cliente sem validação? Cliente que entra no meio vê certo?
   - **Convive bem**: save com e sem o mod, save antigo, versões, mod de terceiro presente e ausente,
     gancho que falha.
   - **Jogo enxuto**: API suspeita de não existir no jogo (stripping)?
   - **Performance**: o que roda por frame, alocações, `Find*`, materiais criados sem cache.
   - **Bilíngue/configurável**: texto sem PT ou EN? Número fixo que deveria estar no `.cfg`?
   - **Escopo, tasks↔plano, riscos.**
4. Grave `critique.md` com confiança (`alta|média|baixa`) e veredito. Qualquer invariante não garantida ⇒ **NO-GO**.
