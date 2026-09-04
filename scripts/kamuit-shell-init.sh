# KamuiT shell init — sourced by every new Linux tab (bash --rcfile).
# Keep this file quiet: it is the interactive rc, so pull in the user bashrc first.

if [ -f "$HOME/.bashrc" ]; then
  # shellcheck disable=SC1091
  . "$HOME/.bashrc"
fi

export TERM="${TERM:-xterm-256color}"
export COLORTERM="${COLORTERM:-truecolor}"
export TERM_PROGRAM="${TERM_PROGRAM:-KamuiT}"
export TERM_PROGRAM_VERSION="${TERM_PROGRAM_VERSION:-0.3.0}"
export KAMUIT="${KAMUIT:-1}"
