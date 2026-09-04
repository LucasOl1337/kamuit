#!/usr/bin/env bash
# Instala dependências nativas, publica o host Linux e coloca `kamuit` em ~/.local/bin.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BIN_DIR="${HOME}/.local/bin"
APP_DIR="${HOME}/.local/share/kamuit"
DESKTOP_DIR="${HOME}/.local/share/applications"

need_cmd() {
  command -v "$1" >/dev/null 2>&1
}

if ! need_cmd dotnet; then
  echo "Instale o .NET 8 SDK: https://dot.net/download" >&2
  exit 2
fi

echo "==> pacotes nativos (GTK4 + VTE)"
if need_cmd apt-get; then
  sudo apt-get update -y
  sudo apt-get install -y \
    libgtk-4-1 \
    libvte-2.91-gtk4-0 \
    || sudo apt-get install -y libgtk-4-1 libvte-2.91-0
elif need_cmd dnf; then
  sudo dnf install -y gtk4 vte291-gtk4 || sudo dnf install -y gtk4 vte291
elif need_cmd pacman; then
  sudo pacman -S --needed --noconfirm gtk4 vte3 vte4 || sudo pacman -S --needed --noconfirm gtk4 vte3
else
  echo "Gerenciador de pacotes não reconhecido. Instale GTK 4 e libvte-2.91-gtk4 manualmente." >&2
fi

echo "==> publish linux-x64"
dotnet publish "$ROOT/linux/KamuiT.Linux.csproj" -c Release -r linux-x64 --self-contained false -o "$APP_DIR" --nologo

mkdir -p "$BIN_DIR" "$DESKTOP_DIR"
ln -sfn "$APP_DIR/KamuiT" "$BIN_DIR/KamuiT"
cp -f "$ROOT/scripts/kamuit.sh" "$BIN_DIR/kamuit"
chmod +x "$BIN_DIR/kamuit" "$APP_DIR/KamuiT"

cat > "$DESKTOP_DIR/kamuit.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=KamuiT
Comment=Workspace de agentes (Grok, Claude, Codex, Pi)
Exec=$APP_DIR/KamuiT
Icon=utilities-terminal
Terminal=false
Categories=System;TerminalEmulator;Development;
StartupWMClass=KamuiT
EOF

if [[ ":$PATH:" != *":$BIN_DIR:"* ]]; then
  echo "Aviso: $BIN_DIR não está no PATH. Adicione: export PATH=\"\$HOME/.local/bin:\$PATH\""
fi

echo "Instalado: $BIN_DIR/KamuiT  e  $BIN_DIR/kamuit"
echo "Abra: KamuiT    ou    kamuit open grok"
