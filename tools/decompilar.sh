#!/usr/bin/env bash
# Descompila as DLLs do Nomad Drive para .game-sources/ (consulta local, fora do git).
# Rode de novo sempre que o jogo atualizar.
# Requer: dotnet tool install -g ilspycmd
# Uso: bash tools/decompilar.sh ["<pasta do jogo>"]
set -euo pipefail

GAME_DIR="${1:-/c/Program Files (x86)/Steam/steamapps/common/Nomad Drive}"
MANAGED="$GAME_DIR/Nomad Drive_Data/Managed"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/.game-sources"

# ilspycmd foi feito para outra versão do .NET; isto deixa rodar no SDK instalado.
export DOTNET_ROLL_FORWARD=Major

for dll in Assembly-CSharp EvilCore EvilCore.Inputs EvilCore.Localization; do
  echo "== $dll"
  rm -rf "$OUT/$dll"
  mkdir -p "$OUT/$dll"
  ilspycmd -p -o "$OUT/$dll" "$MANAGED/$dll.dll" > /dev/null
done

# Registra de qual build do jogo veio a descompilação.
stat -c '%y' "$MANAGED/Assembly-CSharp.dll" > "$OUT/VERSAO-DO-JOGO.txt"
echo "Pronto: $OUT"
