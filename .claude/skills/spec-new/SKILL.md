---
name: spec-new
description: Cria a spec (spec.md) de uma nova feature/melhoria/correção dos mods do Nomad Drive (CozinhaExtra, SpongeWash). Use quando o usuário quer INICIAR um trabalho (pedido de jogador, bug reportado no Nexus, ideia) e ainda não existe spec. Lê o contexto do sistema, pergunta o essencial e gera specs/features/NNNN-slug/spec.md a partir do template. NÃO escreve código.
---

# spec-new

1. Leia `specs/system/visao.md`, `invariantes.md`, `arquitetura.md`, `glossario.md` e `roadmap.md`.
2. Entenda o pedido. Se veio de reporte externo, registre a **origem** e preserve os detalhes técnicos
   do reporte. Pergunte só o que for essencial e indecidível; não invente requisitos.
3. Olhe o código do mod envolvido o suficiente para descrever o **comportamento atual** com precisão
   (arquivo/método), sem desenhar a solução ainda.
4. Descreva a **experiência do jogador** passo a passo (host e cliente).
5. Preencha as **6 invariantes** e a seção de **compatibilidade** (save, versões, outros mods).
   O que não fechar vai para riscos/questões em aberto.
6. Número: maior `NNNN` em `specs/features/` + 1 (4 dígitos), slug começando pelo mod (`cozinha-`, `esponja-`).
   Gere a partir de `specs/_templates/spec.md`, `Status: rascunho`. Atualize a tabela de `specs/README.md`
   e o `roadmap.md`.
7. Resuma e peça aprovação. Próximo: `/spec-plan`.

Regras: PT-BR; termos do glossário; nunca marque `aprovada` sozinho; um trabalho por pasta.
