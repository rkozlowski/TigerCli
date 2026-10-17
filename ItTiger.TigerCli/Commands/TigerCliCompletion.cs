using System.Text;

namespace ItTiger.TigerCli.Commands;

/// <summary>What a completion candidate stands for. Shell-neutral; formatters may ignore it.</summary>
internal enum TigerCliCompletionKind
{
    Command,
    Group,
    Alias,
    Option,
    Value,
    Separator
}

/// <summary>
/// What a shell should fall back to when TigerCli offers no candidates for the current word.
/// TigerCli never lists the file system itself; the shell's native file completion does that.
/// </summary>
internal enum TigerCliCompletionFallback
{
    None,
    Files,
    Directories
}

/// <summary>One completion candidate: the exact text to insert and an optional plain-text description.</summary>
internal sealed record TigerCliCompletionCandidate(
    string Value,
    string? Description,
    TigerCliCompletionKind Kind);

/// <summary>
/// The shell-neutral answer to one completion request: candidates already filtered by the current
/// word, plus the fallback the shell applies when there are none.
/// </summary>
internal sealed record TigerCliCompletionResult(
    IReadOnlyList<TigerCliCompletionCandidate> Candidates,
    TigerCliCompletionFallback Fallback);

/// <summary>Shells TigerCli generates activation scripts for and formats completion responses for.</summary>
internal enum TigerCliCompletionShell
{
    Bash,
    Zsh,
    Fish,
    Pwsh
}

/// <summary>
/// The wire protocol between a generated shell script and the app binary. The script runs
/// <c>app --tigercli-complete &lt;shell&gt; _word1 … _wordN</c> with every word after the program
/// name up to and including the word under the cursor, each prefixed with <c>_</c> so that an empty
/// current word, a literal <c>--</c>, and option-like words survive every shell's argument passing.
/// The app answers with one candidate per line in the shell's format and a final <c>:fallback</c> line.
/// </summary>
internal static class TigerCliCompletionProtocol
{
    /// <summary>The hidden first argument that turns a run into a completion request.</summary>
    public const string RequestToken = "--tigercli-complete";

    /// <summary>The framework-owned root option that prints a shell activation script.</summary>
    public const string ScriptOption = "--completion";

    /// <summary>Accepted shell names in help/error order; <c>powershell</c> is also accepted for <c>pwsh</c>.</summary>
    public static readonly IReadOnlyList<string> ShellNames = ["bash", "zsh", "fish", "pwsh"];

    private const char WordPrefix = '_';

    public static bool TryParseShell(string? name, out TigerCliCompletionShell shell)
    {
        switch (name?.Trim().ToLowerInvariant())
        {
            case "bash": shell = TigerCliCompletionShell.Bash; return true;
            case "zsh": shell = TigerCliCompletionShell.Zsh; return true;
            case "fish": shell = TigerCliCompletionShell.Fish; return true;
            case "pwsh":
            case "powershell": shell = TigerCliCompletionShell.Pwsh; return true;
            default: shell = default; return false;
        }
    }

    /// <summary>Removes the transport prefix from one request word.</summary>
    public static string DecodeWord(string word) =>
        word.Length > 0 && word[0] == WordPrefix ? word[1..] : word;

    /// <summary>
    /// Formats a result for one shell. Bash receives bare values (it cannot show descriptions); zsh
    /// receives <c>_describe</c> entries with colons in the value escaped; fish and PowerShell receive
    /// <c>value&lt;TAB&gt;description</c>. The last line is always the fallback directive. Lines end
    /// with LF on every platform because the consuming scripts split on LF.
    /// </summary>
    public static string FormatResponse(TigerCliCompletionShell shell, TigerCliCompletionResult result)
    {
        var builder = new StringBuilder();
        foreach (var candidate in result.Candidates)
        {
            var value = SingleLine(candidate.Value);
            if (value.Length == 0)
                continue;

            var description = candidate.Description is null ? string.Empty : SingleLine(candidate.Description);
            switch (shell)
            {
                case TigerCliCompletionShell.Bash:
                    builder.Append(value);
                    break;
                case TigerCliCompletionShell.Zsh:
                    builder.Append(value.Replace("\\", "\\\\").Replace(":", "\\:"));
                    if (description.Length > 0)
                        builder.Append(':').Append(description);
                    break;
                default:
                    builder.Append(value);
                    if (description.Length > 0)
                        builder.Append('\t').Append(description);
                    break;
            }

            builder.Append('\n');
        }

        builder.Append(':').Append(FormatFallback(result.Fallback)).Append('\n');
        return builder.ToString();
    }

    public static string FormatFallback(TigerCliCompletionFallback fallback) => fallback switch
    {
        TigerCliCompletionFallback.Files => "files",
        TigerCliCompletionFallback.Directories => "directories",
        _ => "none"
    };

    /// <summary>
    /// Converts trusted TigerCli markup to the visible text a shell can show: tags are dropped and
    /// <c>[[</c> / <c>]]</c> escapes become literal brackets.
    /// </summary>
    public static string MarkupToPlainText(string markup)
    {
        var builder = new StringBuilder(markup.Length);
        for (var i = 0; i < markup.Length; i++)
        {
            var ch = markup[i];
            if (ch == '[')
            {
                if (i + 1 < markup.Length && markup[i + 1] == '[')
                {
                    builder.Append('[');
                    i++;
                    continue;
                }

                var end = markup.IndexOf(']', i + 1);
                if (end < 0)
                {
                    builder.Append(markup, i, markup.Length - i);
                    break;
                }

                i = end;
                continue;
            }

            if (ch == ']' && i + 1 < markup.Length && markup[i + 1] == ']')
            {
                builder.Append(']');
                i++;
                continue;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static string SingleLine(string text) =>
        text.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
}

/// <summary>
/// Generates the per-shell activation scripts. Each script is a thin adapter: it collects the words
/// before the cursor, calls the app through <see cref="TigerCliCompletionProtocol"/>, and hands the
/// candidates (or the requested file/directory fallback) to the shell. All grammar knowledge stays in
/// the app, so an installed script keeps working when the app's commands change.
/// </summary>
internal static class TigerCliCompletionScripts
{
    public static string Generate(TigerCliCompletionShell shell, string commandName)
    {
        var template = shell switch
        {
            TigerCliCompletionShell.Bash => BashTemplate,
            TigerCliCompletionShell.Zsh => ZshTemplate,
            TigerCliCompletionShell.Fish => FishTemplate,
            _ => PwshTemplate
        };

        var identifier = ToIdentifier(commandName);
        var zshFunction = IsPlainCommandName(commandName) ? "_" + commandName : "_" + identifier;
        var pwshNames = commandName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? PwshQuote(commandName)
            : $"{PwshQuote(commandName)}, {PwshQuote(commandName + ".exe")}";

        // Templates live in a CRLF source file; scripts must be LF-only or bash rejects them.
        return template
            .Replace("{{APP}}", commandName)
            .Replace("{{APP_SH}}", ShQuote(commandName))
            .Replace("{{APP_PWSH_NAMES}}", pwshNames)
            .Replace("{{IDENT}}", identifier)
            .Replace("{{ZSH_FUNCTION}}", zshFunction)
            .Replace("{{REQUEST}}", TigerCliCompletionProtocol.RequestToken)
            .Replace("{{SCRIPT_OPTION}}", TigerCliCompletionProtocol.ScriptOption)
            .ReplaceLineEndings("\n");
    }

    /// <summary>A shell-function-safe identifier derived from the command name.</summary>
    private static string ToIdentifier(string commandName)
    {
        var builder = new StringBuilder(commandName.Length);
        foreach (var ch in commandName)
            builder.Append(char.IsAsciiLetterOrDigit(ch) ? ch : '_');
        return builder.ToString();
    }

    /// <summary>Names that are safe unquoted in a zsh <c>#compdef</c> line and as a function name.</summary>
    private static bool IsPlainCommandName(string commandName) =>
        commandName.Length > 0
        && commandName.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or '.' or '+');

    private static string ShQuote(string value) => "'" + value.Replace("'", "'\\''") + "'";

    private static string PwshQuote(string value) => "'" + value.Replace("'", "''") + "'";

    private const string BashTemplate = """
        # bash completion for {{APP}}, generated by TigerCli.
        # Activate for the current shell:   source <({{APP}} {{SCRIPT_OPTION}} bash)
        # Persist it by adding that line to ~/.bashrc. Requires bash 4 or later.

        _tigercli_complete_{{IDENT}}()
        {
            local cur="${COMP_WORDS[COMP_CWORD]}"
            local -a words=() request=() lines=()
            local i word joined=0

            # Bash splits words at '=' and ':' (COMP_WORDBREAKS). Rejoin them so the app sees
            # `--option=value` as the single token it parses.
            for (( i = 1; i <= COMP_CWORD; i++ )); do
                word="${COMP_WORDS[i]}"
                if (( ${#words[@]} > 0 )) && { [[ "$word" == "=" || "$word" == ":" ]] || (( joined )); }; then
                    words[${#words[@]}-1]+="$word"
                else
                    words+=("$word")
                fi
                if [[ "$word" == "=" || "$word" == ":" ]]; then joined=1; else joined=0; fi
            done

            for word in "${words[@]}"; do
                request+=("_$word")
            done

            local out
            out="$("${COMP_WORDS[0]}" {{REQUEST}} bash "${request[@]}" 2>/dev/null)"
            [[ -n "$out" ]] || return 0
            mapfile -t lines <<< "$out"

            local count=${#lines[@]}
            local fallback="${lines[count-1]}"
            local current="${words[${#words[@]}-1]}"
            local strip=$(( ${#current} - ${#cur} ))

            COMPREPLY=()
            for (( i = 0; i < count - 1; i++ )); do
                COMPREPLY+=("${lines[i]:strip}")
            done

            if (( ${#COMPREPLY[@]} == 0 )); then
                local break_prefix="" path_cur="$cur"
                if [[ "$cur" == "=" || "$cur" == ":" ]]; then
                    break_prefix="$cur"
                    path_cur=""
                fi
                case "$fallback" in
                    :files)
                        compopt -o filenames 2>/dev/null
                        mapfile -t COMPREPLY < <(compgen -f -- "$path_cur")
                        ;;
                    :directories)
                        compopt -o filenames 2>/dev/null
                        mapfile -t COMPREPLY < <(compgen -d -- "$path_cur")
                        ;;
                esac
                if [[ -n "$break_prefix" ]]; then
                    for (( i = 0; i < ${#COMPREPLY[@]}; i++ )); do
                        COMPREPLY[i]="$break_prefix${COMPREPLY[i]}"
                    done
                fi
            fi
            return 0
        }

        complete -F _tigercli_complete_{{IDENT}} {{APP_SH}}

        """;

    private const string ZshTemplate = """
        #compdef {{APP}}
        # zsh completion for {{APP}}, generated by TigerCli.
        # Activate for the current shell (after compinit):   source <({{APP}} {{SCRIPT_OPTION}} zsh)
        # Or save it as a file named {{ZSH_FUNCTION}} in a directory on $fpath.

        {{ZSH_FUNCTION}}() {
          local -a request lines candidates
          local word fallback

          for word in "${(@Q)words[2,CURRENT]}"; do
            request+=("_$word")
          done

          lines=("${(@f)$("${(Q)words[1]}" {{REQUEST}} zsh "${request[@]}" 2>/dev/null)}")
          fallback="${lines[-1]}"
          candidates=("${(@)lines[1,-2]}")

          if (( ${#candidates} )); then
            _describe -t tigercli-values 'value' candidates
          else
            case "$fallback" in
              :files) _files ;;
              :directories) _files -/ ;;
              *) return 1 ;;
            esac
          fi
        }

        if [[ "${funcstack[1]}" == "{{ZSH_FUNCTION}}" ]]; then
          {{ZSH_FUNCTION}} "$@"
        else
          compdef {{ZSH_FUNCTION}} {{APP_SH}}
        fi

        """;

    private const string FishTemplate = """
        # fish completion for {{APP}}, generated by TigerCli.
        # Activate for the current shell:   {{APP}} {{SCRIPT_OPTION}} fish | source
        # Or save it as ~/.config/fish/completions/{{APP}}.fish

        function __tigercli_complete_{{IDENT}}
            set -l tokens (commandline -opc)
            set -l program $tokens[1]
            set -e tokens[1]
            set -l current (commandline -ct)

            set -l request
            for token in $tokens
                set -a request "_$token"
            end
            set -a request "_$current"

            set -l lines ($program {{REQUEST}} fish $request 2>/dev/null)
            test (count $lines) -gt 0; or return
            set -l fallback $lines[-1]
            set -e lines[-1]

            if test (count $lines) -gt 0
                printf '%s\n' $lines
            else if test "$fallback" = ":files"
                __fish_complete_path "$current"
            else if test "$fallback" = ":directories"
                __fish_complete_directories "$current"
            end
        end

        complete -c {{APP_SH}} -f -a '(__tigercli_complete_{{IDENT}})'

        """;

    private const string PwshTemplate = """
        # PowerShell completion for {{APP}}, generated by TigerCli.
        # Activate for the current session:   {{APP}} {{SCRIPT_OPTION}} pwsh | Out-String | Invoke-Expression
        # Persist it by adding that line to your $PROFILE. Works in PowerShell 7+ and Windows PowerShell 5.1.

        Register-ArgumentCompleter -Native -CommandName @({{APP_PWSH_NAMES}}) -ScriptBlock {
            param($wordToComplete, $commandAst, $cursorPosition)

            $elements = @($commandAst.CommandElements)
            $words = @()
            foreach ($element in $elements) {
                if ($words.Count -gt 0 -and $element.Extent.EndOffset -ge $cursorPosition) { break }
                if ($element -is [System.Management.Automation.Language.StringConstantExpressionAst]) {
                    $words += $element.Value
                } else {
                    $words += $element.Extent.Text
                }
            }

            $program = $words[0]
            $request = @()
            for ($i = 1; $i -lt $words.Count; $i++) { $request += '_' + $words[$i] }
            $request += '_' + $wordToComplete

            $lines = @(& $program {{REQUEST}} pwsh @request 2>$null)
            if ($lines.Count -lt 2) { return }

            for ($i = 0; $i -lt $lines.Count - 1; $i++) {
                $parts = $lines[$i] -split "`t", 2
                $value = $parts[0]
                $tooltip = $value
                if ($parts.Count -gt 1 -and $parts[1]) { $tooltip = $parts[1] }
                $text = $value
                if ($value -match '[\s''"`$;,(){}@#|&<>]') { $text = "'" + $value.Replace("'", "''") + "'" }
                [System.Management.Automation.CompletionResult]::new($text, $value, 'ParameterValue', $tooltip)
            }
        }

        """;
}
