#!/usr/bin/env bash
# Real-shell check of TigerCli shell completion and console output on Linux.
#
# Starts Bash, Zsh, and Fish interactively inside tmux, types command lines, presses the real Tab
# key, and reads back what the shell's own line editor rendered. It also runs the app at a terminal
# and checks the bytes it writes when redirected. The app under test is the linux-x64 build of
# CommandParserTest (`parser-test`), whose `--city` values (Kraków, Łódź, Gdańsk) are Unicode
# completion candidates and whose pl-PL descriptions are non-ASCII.
#
# Usage: linux-real-shell-check.sh [--fetch-missing] [--windows-app <dir>] <app-dir> <output-dir>
#
# --windows-app names a directory with the Windows build of parser-test (parser-test.exe). Under WSL
# the script then also runs it through Windows interoperability and checks that Linux tools receive
# UTF-8, including characters no legacy Windows code page has.
#
# Shells come from PATH. Where something is not installed and installing needs root,
# --fetch-missing unpacks Ubuntu's own packages into a private prefix (no root, nothing
# system-wide): zsh and fish when they are not on PATH, and ICU when the system has none (.NET needs
# it for the pl-PL culture). Requires tmux.
#
# Writes every captured screen to <output-dir> and a results.tsv of PASS/FAIL lines; exits 1 when
# any check failed and 2 when the environment cannot run the checks.
set -uo pipefail

fetch=0; windows_app=""
while [[ "${1:-}" == --* ]]; do
    case "$1" in
        --fetch-missing) fetch=1; shift ;;
        --windows-app) windows_app=$(cd "$2" && pwd); shift 2 ;;
        *) echo "unknown option $1" >&2; exit 2 ;;
    esac
done
app_dir=$(cd "$1" && pwd); out=$(mkdir -p "$2" && cd "$2" && pwd)
results="$out/results.tsv"; : > "$results"
socket="tigercli-check-$$"
export LANG=C.UTF-8 LC_ALL=C.UTF-8

prefix="$HOME/.cache/tigercli-shells/prefix"
if (( fetch )); then
    packages=()
    command -v zsh >/dev/null || packages+=(zsh zsh-common)
    command -v fish >/dev/null || packages+=(fish fish-common libpcre2-32-0)
    ldconfig -p | grep -q libicuuc || packages+=($(apt-cache search --names-only '^libicu[0-9]+$' | cut -d' ' -f1 | head -n 1))
    if (( ${#packages[@]} )); then
        mkdir -p "$prefix" "$HOME/.cache/tigercli-shells/debs"
        ( cd "$HOME/.cache/tigercli-shells/debs" && apt-get download "${packages[@]}" >/dev/null \
            && for deb in *.deb; do dpkg-deb -x "$deb" "$prefix"; done ) || { echo "fetching ${packages[*]} failed" >&2; exit 2; }
    fi
fi

zsh_bin=$(command -v zsh || true); fish_bin=$(command -v fish || true)
zsh_modules=""; zsh_functions=""
if [[ -z "$zsh_bin" && -x "$prefix/usr/bin/zsh" ]]; then
    zsh_bin="$prefix/usr/bin/zsh"
    zsh_modules=$(ls -d "$prefix"/usr/lib/x86_64-linux-gnu/zsh/*/ | head -n 1)
    zsh_functions="$prefix/usr/share/zsh"
fi
[[ -z "$fish_bin" && -x "$prefix/usr/bin/fish" ]] && fish_bin="$prefix/usr/bin/fish"
# Libraries unpacked into the prefix (fish's PCRE2, ICU) for everything started from here.
[[ -d "$prefix/usr/lib/x86_64-linux-gnu" ]] && export LD_LIBRARY_PATH="$prefix/usr/lib/x86_64-linux-gnu${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"

if ! "$app_dir/parser-test" --help >/dev/null 2>"$out/app-start.err"; then
    echo "parser-test does not start here; see $out/app-start.err (try --fetch-missing)" >&2
    exit 2
fi

record() { printf '%s\t%s\t%s\t%s\n' "$1" "$2" "$3" "$4" | tee -a "$results"; }

# ── Shell configuration: nothing but a plain prompt and the TigerCli activation line ──
cfg=$(mktemp -d); trap 'tmux -L "$socket" kill-server 2>/dev/null; rm -rf "$cfg"' EXIT
cat > "$cfg/bashrc" <<'EOF'
PS1='$ '
PATH="$TIGERCLI_APP_DIR:$PATH"
source <(parser-test --completion bash)
EOF
mkdir -p "$cfg/zsh"
cat > "$cfg/zsh/.zshenv" <<'EOF'
# Only for shells unpacked into a private prefix: point zsh at its own modules and functions.
[[ -n $TIGERCLI_ZSH_MODULES ]] && module_path=($TIGERCLI_ZSH_MODULES)
[[ -n $TIGERCLI_ZSH_FUNCTIONS ]] && fpath=($TIGERCLI_ZSH_FUNCTIONS/**/*(N/) $fpath)
EOF
cat > "$cfg/zsh/.zshrc" <<'EOF'
PS1='$ '
path=("$TIGERCLI_APP_DIR" $path)
autoload -Uz compinit && compinit -u -D
source <(parser-test --completion zsh)
EOF

# ── tmux helpers ──
# tmux replaces PATH in a new session's environment, so the app directory travels as
# TIGERCLI_APP_DIR and each shell's startup file puts it on PATH.
pane() { tmux -L "$socket" capture-pane -p -t check; }
settle() {  # wait until the screen stops changing (or 10 s), then print it
    local previous="" current tries=0
    sleep 0.3
    while (( tries++ < 50 )); do
        current=$(pane)
        [[ "$current" == "$previous" && -n "$current" ]] && break
        previous="$current"; sleep 0.2
    done
    printf '%s\n' "$current"
}
keys() { tmux -L "$socket" send-keys -t check "$@"; }
type_line() { keys -l "$1"; }
reset_line() { keys C-u; keys C-l; settle >/dev/null; }

start_shell() {  # <name> <command...>
    tmux -L "$socket" kill-server 2>/dev/null
    tmux -L "$socket" -f /dev/null -u new-session -d -s check -x 160 -y 40 \
        -e "TIGERCLI_APP_DIR=$app_dir" -e "LANG=C.UTF-8" -e "LC_ALL=C.UTF-8" -e "TERM=xterm-256color" \
        -e "ZDOTDIR=$cfg/zsh" -e "TIGERCLI_ZSH_MODULES=$zsh_modules" -e "TIGERCLI_ZSH_FUNCTIONS=$zsh_functions" \
        -e "LD_LIBRARY_PATH=${LD_LIBRARY_PATH:-}" -- "${@:2}"
    settle >/dev/null
}

check() {  # <shell> <scenario> <screen> <expected text>...
    local shell=$1 scenario=$2 screen=$3; shift 3
    printf '%s\n' "$screen" > "$out/$shell-$scenario.txt"
    local text
    for text in "$@"; do
        if [[ "$screen" != *"$text"* ]]; then record FAIL "$shell" "$scenario" "missing: $text"; return; fi
    done
    record PASS "$shell" "$scenario" "$*"
}

# Presses Tab after a typed line; $2 Tabs (bash lists candidates on the second press).
tab_screen() { reset_line; type_line "$1"; local i; for (( i = 0; i < $2; i++ )); do keys Tab; sleep 0.4; done; settle; }

run_shell_checks() {  # <shell> <list-tabs> <describes>
    local shell=$1 list_tabs=$2 describes=$3
    check "$shell" unique-command "$(tab_screen 'parser-test ec' 1)" 'parser-test echo'
    check "$shell" unicode-candidates "$(tab_screen 'parser-test --city ' "$list_tabs")" 'Kraków' 'Łódź' 'Gdańsk'
    check "$shell" unicode-prefix "$(tab_screen 'parser-test --city Ł' 1)" 'parser-test --city Łódź'
    # Typed up to the common prefix: Bash and Zsh would otherwise spend the first Tab inserting it.
    if (( describes )); then
        check "$shell" localized-descriptions "$(tab_screen 'parser-test --culture pl-PL --help' "$list_tabs")" '--help-errors' 'Wyświetl pomoc'
    else
        check "$shell" localized-options "$(tab_screen 'parser-test --culture pl-PL --help' "$list_tabs")" '--help-errors' '--help-env'
    fi
    [[ "$shell" == fish ]] && keys Escape   # closes fish's pager; elsewhere Escape is a Meta prefix
    reset_line
    type_line 'parser-test --city Łódź'; keys Enter
    check "$shell" terminal-output "$(settle)" 'Hello, Łódź!'
    reset_line; type_line 'parser-test --culture pl-PL'; keys Enter
    check "$shell" terminal-localized-output "$(settle)" 'Witaj, Świecie!'
}

if command -v bash >/dev/null; then
    start_shell bash bash --noprofile --rcfile "$cfg/bashrc" -i
    run_shell_checks bash 2 0
else record FAIL bash setup "bash not found"; fi

if [[ -n "$zsh_bin" ]]; then
    start_shell zsh "$zsh_bin" -i
    run_shell_checks zsh 1 1
else record FAIL zsh setup "zsh not found (try --fetch-missing)"; fi

if [[ -n "$fish_bin" ]]; then
    start_shell fish "$fish_bin" --no-config --init-command \
        'set -g fish_greeting ""; set -gx PATH $TIGERCLI_APP_DIR $PATH; function fish_prompt; echo -n "\$ "; end; parser-test --completion fish | source'
    run_shell_checks fish 1 1
else record FAIL fish setup "fish not found (try --fetch-missing)"; fi
tmux -L "$socket" kill-server 2>/dev/null

# ── Redirected bytes: UTF-8 under a UTF-8 locale and under the POSIX locale alike ──
expected_greeting=$'Witaj, \xc5\x9awiecie!'
expected_cities=$'Krak\xc3\xb3w\n\xc5\x81\xc3\xb3d\xc5\xba\nGda\xc5\x84sk\n:none'
for locale in C.UTF-8 C; do
    greeting=$(LANG=$locale LC_ALL=$locale "$app_dir/parser-test" --culture pl-PL)
    [[ "$greeting" == "$expected_greeting" ]] && record PASS redirected "greeting-$locale" "UTF-8 bytes" \
        || record FAIL redirected "greeting-$locale" "$(printf '%s' "$greeting" | od -An -tx1 | tr -d '\n')"
    cities=$(LANG=$locale LC_ALL=$locale "$app_dir/parser-test" --tigercli-complete bash _--city _)
    [[ "$cities" == "$expected_cities" ]] && record PASS redirected "completion-$locale" "UTF-8 bytes" \
        || record FAIL redirected "completion-$locale" "$(printf '%s' "$cities" | od -An -tx1 | tr -d '\n')"
done

# ── Windows interoperability: a Windows TigerCli executable read by Linux tools ──
if [[ -n "$windows_app" ]]; then
    beyond=$'\xe2\x82\xac \xe2\x95\xad\xe2\x94\x80\xe2\x95\xae \xe2\x9c\x93 \xe6\x9d\xb1\xe4\xba\xac \xf0\x9f\x98\x80'   # € ╭─╮ ✓ 東京 😀
    echoed=$("$windows_app/parser-test.exe" echo -m "$beyond" 2>/dev/null | tr -d '\r')
    [[ "$echoed" == "$beyond" ]] && record PASS interop beyond-legacy-code-pages "UTF-8 bytes" \
        || record FAIL interop beyond-legacy-code-pages "$(printf '%s' "$echoed" | od -An -tx1 | tr -d '\n')"
    greeting=$("$windows_app/parser-test.exe" --culture pl-PL 2>/dev/null | tr -d '\r')
    [[ "$greeting" == "$expected_greeting" ]] && record PASS interop localized-greeting "UTF-8 bytes" \
        || record FAIL interop localized-greeting "$(printf '%s' "$greeting" | od -An -tx1 | tr -d '\n')"
fi
! grep -q '^FAIL' "$results"
