#!/usr/bin/env sh
set -eu

REPO_RAW_BASE="https://raw.githubusercontent.com/github/awesome-copilot/main/chatmodes"
TARGET_DIR=".github/chatmodes"

FILES="
expert-dotnet-software-engineer.chatmode.md
csharp-dotnet-janitor.chatmode.md
tdd-red.chatmode.md
tdd-green.chatmode.md
tdd-refactor.chatmode.md
plan.chatmode.md
specification.chatmode.md
implementation-plan.chatmode.md
debug.chatmode.md
tech-debt-remediation-plan.chatmode.md
task-planner.chatmode.md
address-comments.chatmode.md
"

mkdir -p "$TARGET_DIR"

fetch() {
  url="$1"
  dest="$2"
  if command -v curl >/dev/null 2>&1; then
    curl -fsSL "$url" -o "$dest"
  elif command -v wget >/dev/null 2>&1; then
    wget -qO "$dest" "$url"
  else
    echo "Error: ni curl ni wget disponibles" >&2
    exit 1
  fi
}

printf 'Sincronizando chatmodes en %s\n' "$TARGET_DIR"
for f in $FILES; do
  url="$REPO_RAW_BASE/$f"
  dest="$TARGET_DIR/$f"
  printf 'Descargando %s...\n' "$f"
  fetch "$url" "$dest"
  printf '  ✓ Guardado en %s\n' "$dest"
done

printf '\nListo. Archivos sincronizados en %s\n' "$TARGET_DIR"
#!/usr/bin/env sh
set -eu

REPO_RAW_BASE="https://raw.githubusercontent.com/github/awesome-copilot/main/chatmodes"
TARGET_DIR=".github/chatmodes"

FILES="
expert-dotnet-software-engineer.chatmode.md
csharp-dotnet-janitor.chatmode.md
tdd-red.chatmode.md
tdd-green.chatmode.md
tdd-refactor.chatmode.md
plan.chatmode.md
specification.chatmode.md
implementation-plan.chatmode.md
debug.chatmode.md
tech-debt-remediation-plan.chatmode.md
task-planner.chatmode.md
address-comments.chatmode.md
"

mkdir -p "$TARGET_DIR"

fetch() {
  url="$1"
  dest="$2"
  if command -v curl >/dev/null 2>&1; then
    curl -fsSL "$url" -o "$dest"
  elif command -v wget >/dev/null 2>&1; then
    wget -qO "$dest" "$url"
  else
    echo "Error: ni curl ni wget disponibles" >&2
    exit 1
  fi
}

printf 'Sincronizando chatmodes en %s\n' "$TARGET_DIR"
for f in $FILES; do
  url="$REPO_RAW_BASE/$f"
  dest="$TARGET_DIR/$f"
  printf 'Descargando %s...\n' "$f"
  fetch "$url" "$dest"
  printf '  ✓ Guardado en %s\n' "$dest"
done

printf '\nListo. Archivos sincronizados en %s\n' "$TARGET_DIR"
