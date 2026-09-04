#!/usr/bin/env bash
# KamuiT CLI (Linux) — JSON-lines no socket $XDG_RUNTIME_DIR/kamuit.sock
set -euo pipefail

SOCK="${XDG_RUNTIME_DIR:-/tmp}/kamuit.sock"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BIN=""
for c in \
  "$HOME/.local/bin/KamuiT" \
  "$ROOT/publish-linux/KamuiT" \
  "$ROOT/linux/bin/Release/net8.0/KamuiT" \
  "$ROOT/linux/bin/Release/net8.0/linux-x64/KamuiT"
do
  if [[ -x "$c" ]]; then BIN="$c"; break; fi
done

json_escape() {
  python3 -c 'import json,sys; print(json.dumps(sys.argv[1]))' "$1"
}

send() {
  local payload="$1"
  python3 - "$SOCK" "$payload" <<'PY'
import json, os, socket, sys
sock_path, payload = sys.argv[1], sys.argv[2]
s = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
s.settimeout(8)
s.connect(sock_path)
s.sendall((payload + "\n").encode("utf-8"))
buf = b""
while b"\n" not in buf:
    chunk = s.recv(4096)
    if not chunk:
        break
    buf += chunk
s.close()
sys.stdout.write(buf.decode("utf-8", "replace"))
PY
}

ensure_running() {
  if [[ -S "$SOCK" ]]; then
    return 0
  fi
  if [[ -z "$BIN" ]]; then
    echo "KamuiT não encontrado. Publique: dotnet publish linux/KamuiT.Linux.csproj -c Release -r linux-x64 -o publish-linux" >&2
    exit 2
  fi
  nohup "$BIN" "$@" >/dev/null 2>&1 &
  local i
  for i in $(seq 1 50); do
    if [[ -S "$SOCK" ]]; then return 0; fi
    sleep 0.1
  done
  echo "KamuiT iniciou mas o socket $SOCK não apareceu." >&2
  exit 2
}

cmd="${1:-help}"
shift || true

case "$cmd" in
  -h|--help|help)
    cat <<'EOF'
KamuiT CLI — controla o workspace de agentes (Linux)

  kamuit open <agent> [-C <cwd>] [-n <count>]
  kamuit list
  kamuit focus <slot|id>
  kamuit type <slot> <text> [--enter]
  kamuit close [slot]
  kamuit show
  kamuit agents
  kamuit ping
EOF
    exit 0
    ;;
  grok|claude|codex|pi|jcode|shell)
    set -- "$cmd" "$@"
    cmd=open
    ;;
esac

op="$cmd"
agent=""
cwd=""
count=1
slot=""
text=""
enter=false
show=true
id=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    -a|--agent) agent="$2"; shift 2 ;;
    -C|--cwd|--dir) cwd="$2"; shift 2 ;;
    -n|--count) count="$2"; shift 2 ;;
    -s|--slot) slot="$2"; shift 2 ;;
    -t|--text) text="$2"; shift 2 ;;
    --enter|-enter) enter=true; shift ;;
    --no-show) show=false; shift ;;
    --) shift; break ;;
    -*) shift ;;
    *)
      if [[ "$op" == "open" && -z "$agent" ]]; then agent="$1"
      elif [[ "$op" =~ ^(focus|close)$ && -z "$slot" && -z "$id" ]]; then
        if [[ "$1" =~ ^[0-9]+$ ]]; then slot="$1"; else id="$1"; fi
      elif [[ "$op" == "type" && -z "$text" ]]; then
        if [[ "$1" =~ ^[0-9]+$ && -z "$slot" ]]; then slot="$1"; else text="$1"; fi
      fi
      shift
      ;;
  esac
done

payload="{\"op\":$(json_escape "$op")"
[[ -n "$agent" ]] && payload+=",\"agent\":$(json_escape "$agent")"
[[ -n "$cwd" ]] && payload+=",\"cwd\":$(json_escape "$cwd")"
[[ "$op" == "open" ]] && payload+=",\"count\":$count"
[[ -n "$slot" ]] && payload+=",\"slot\":$slot"
[[ -n "$id" ]] && payload+=",\"id\":$(json_escape "$id")"
[[ -n "$text" ]] && payload+=",\"text\":$(json_escape "$text")"
[[ "$enter" == true ]] && payload+=",\"enter\":true"
[[ "$show" == true ]] && payload+=",\"show\":true"
payload+="}"

was_up=false
[[ -S "$SOCK" ]] && was_up=true

if [[ "$op" == "open" ]]; then
  boot=("$op")
  [[ -n "$agent" ]] && boot+=("$agent")
  [[ "$count" != "1" ]] && boot+=(-n "$count")
  [[ -n "$cwd" ]] && boot+=(-C "$cwd")
  ensure_running "${boot[@]}"
  if [[ "$was_up" == true ]]; then
    send "$payload"
  else
    send "{\"op\":\"list\"}"
  fi
else
  ensure_running
  send "$payload"
fi
