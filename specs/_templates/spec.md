# [NNNN] <título>

- Status: rascunho | aprovada | em implementação | concluída
- Tipo: feature | melhoria | correção
- Mod: CozinhaExtra | SpongeWash
- Versão alvo: x.y.z
- Origem: <quem pediu/reportou e onde (Nexus, autor de outro mod, Augusto)>

## Problema / motivação

## Experiência do jogador
Passo a passo do ponto de vista de quem joga (host e cliente): o que vê, o que aperta, o que acontece.

## Resultado esperado (critérios de aceite)
- [ ]

## Escopo
- Dentro:
- Fora:

## Invariantes (obrigatório; ver system/invariantes.md)
- **Jogador guiado:** que linha aparece no painel de ações? Que aviso quando não dá? LEIA-ME/Nexus?
- **Host autoridade:** o que roda no host? Que pedido o cliente manda e como é validado? Quem entra no meio vê certo?
- **Convive bem:** save (com e sem o mod), co-op com versões iguais, outros mods, ganchos que podem falhar.
- **Jogo enxuto:** alguma API arriscada? (RefCheck no plano)
- **Performance:** o que roda por frame/tick? Algo criado por item (material, malha) precisa de cache?
- **Bilíngue/configurável:** textos PT/EN novos; números novos no `.cfg`.

## Compatibilidade
Save antigo abre? Cliente com versão antiga? Mods de terceiros envolvidos?

## Riscos / questões em aberto
