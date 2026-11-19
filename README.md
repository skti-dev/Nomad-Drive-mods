# Mods do Nomad Drive

Mods BepInEx para o **Nomad Drive**, feitos para co-op e publicados no Nexus.

| Mod | Versão | O que faz |
|---|---|---|
| [Cozinha Extra](mods/CozinhaExtra/LEIA-ME.txt) (*Extra Cooking*) | 1.4.2 | sopas na panela, pratos e canecas |
| [Sponge Wash](mods/SpongeWash/LEIA-ME.txt) | 1.2.0 | lavar a esponja com água |

## Requisitos
- Nomad Drive (Steam) com o **BepInExPack_NomadDrive** instalado.
- .NET SDK 8 (compila `net472` contra as DLLs do jogo).
- Para pesquisa: `dotnet tool install -g ilspycmd`; Python 3 para `tools/unity-dump`.

## Desenvolvimento
```bash
dotnet build mods/CozinhaExtra -c Release   # compila e copia para BepInEx/plugins do jogo
bash tools/decompilar.sh                    # código do jogo em .game-sources/ (consulta)
```

Fluxo de trabalho e regras: [CLAUDE.md](CLAUDE.md) e [specs/](specs/README.md).
