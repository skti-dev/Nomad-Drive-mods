# Publicação no Nexus (checklist)

Só com pedido explícito do Augusto. Feature já testada e aprovada no jogo, mergeada em `staging`.

1. **Versão** (SemVer: correção = patch, receita/mecânica nova = minor) em:
   `Plugin.Version` (ou `Plugin.Version` do SpongeWash), `LEIA-ME.txt` (título e nome do zip),
   `CHANGELOG.md`, `specs/system/roadmap.md`.
2. **Build Release:** `dotnet build mods/<Mod> -c Release -p:CopyToGame=false`.
3. **RefCheck** sem faltas na DLL de Release.
4. **Kit de teste desligado** por padrão (`KitDeTeste = false` no `Cfg.Bind`).
5. **Zip** `mods/<Mod>/nexus/<Mod>-<versão>.zip` com:
   `BepInEx/plugins/<Mod>/<Mod>.dll` e `BepInEx/plugins/<Mod>/LEIA-ME.txt`.
6. **Descrição** `mods/<Mod>/nexus/descricao-nexus.txt` atualizada (EN; PT no LEIA-ME) e changelog do Nexus.
7. Teste final: instalar o zip numa pasta limpa do jogo (ou conferir a árvore do zip) e abrir uma partida.
8. Merge `staging` → `main` e tag `<mod>-v<versão>` (ex.: `cozinha-v1.4.3`).
9. Upload no Nexus (o Augusto faz) e, se veio de um reporte, responder quem reportou.
