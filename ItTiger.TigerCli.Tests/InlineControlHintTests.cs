using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Terminal;
using ItTiger.TigerCli.Tui.Abstractions;
using ItTiger.TigerCli.Tui.Controls;
using ItTiger.TigerCli.Tui.Testing;
using ItTiger.TigerCli.Tui.Widgets;

namespace ItTiger.TigerCli.Tests;

public sealed class InlineControlHintTests : TestBase
{
    private sealed class ButtonGroupHostControl : InlineMultiControl
    {
        public ButtonGroupHostControl(ICliAppShell shell)
            : base(shell)
        {
            AddWidget(
                new InlineButtonGroupWidget(shell,
                [
                    new InlineButtonWidget(shell, "OK", DialogResultKind.Ok),
                    new InlineButtonWidget(shell, "Cancel", DialogResultKind.Cancel),
                ]),
                InlineDialogArea.InFrame);
        }

        public override object? Payload => null;
    }

    private sealed class FakeFolderBrowser : IFolderBrowser
    {
        public string? RootLocation => null;

        public (string? location, string? highlightPath) ResolveInitial(string? initialPath)
            => (@"C:\", @"C:\Projects");

        public IReadOnlyList<FolderEntry> GetEntries(string? location)
            =>
            [
                new("Projects", @"C:\Projects", true),
                new("Temp", @"C:\Temp", false),
            ];

        public bool TryGetParent(string? location, out string? parent)
        {
            parent = null;
            return false;
        }
    }

    private static string RenderText(InlineDialog dialog)
        => string.Join("\n", TigerConsole.RenderGridToLines(dialog.ToGrid()));

    private static IReadOnlyList<string> RenderLines(InlineDialog dialog)
        => TigerConsole.RenderGridToLines(dialog.ToGrid());

    private static KeyEvent TabKey => new(ConsoleKey.Tab, ConsoleModifiers.None);

    [Fact]
    public void TextInput_DefaultHint_RendersInStatusRow()
    {
        var shell = new TestShell();
        var dialog = new InlineDialog(shell, title: null, new InlineTextInput(shell, width: 12), "Name");

        var text = RenderText(dialog);

        Assert.Contains("Type text", text);
        Assert.Contains("Enter Confirm", text);
        Assert.Contains("Esc Cancel", text);
    }

    [Fact]
    public void TextInput_ValidationHint_WinsOverDefaultHint()
    {
        var shell = new TestShell();
        var input = new InlineTextInput(shell, initialValue: "", width: 12, validator: _ => "Use 3+ chars.");
        var dialog = new InlineDialog(shell, title: null, input, "Name");

        var text = RenderText(dialog);

        Assert.Contains("Use 3+ chars.", text);
        Assert.DoesNotContain("Type text", text);
    }

    [Fact]
    public void Select_HintMentionsMovementSelectionAndCancel()
    {
        var shell = new TestShell();
        var dialog = new InlineDialog(shell, title: null, new InlineSelect(shell, ["Red", "Green"]));

        var text = RenderText(dialog);

        Assert.Contains("↑↓ Move", text);
        Assert.Contains("Enter Select", text);
        Assert.Contains("Esc Cancel", text);
    }

    [Fact]
    public void MultiSelect_HintMentionsMovementToggleAndConfirm()
    {
        var shell = new TestShell();
        var dialog = new InlineDialog(shell, title: null, new InlineMultiSelect(shell, ["Red", "Green"]));

        var text = RenderText(dialog);

        Assert.Contains("↑↓ Move", text);
        Assert.Contains("Space Toggle", text);
        Assert.Contains("Enter Confirm", text);
        Assert.Contains("Esc Cancel", text);
    }

    [Fact]
    public void ButtonGroup_WidgetHint_RendersThroughCompositeControl()
    {
        var shell = new TestShell();
        var dialog = new InlineDialog(shell, title: null, new ButtonGroupHostControl(shell));

        var text = RenderText(dialog);

        Assert.Contains("← → Move", text);
        Assert.Contains("Enter Activate", text);
    }

    [Fact]
    public void FolderSelect_HintChangesWithFocusedWidget()
    {
        var shell = new TestShell();
        var folderSelect = new InlineFolderSelect(shell, new FakeFolderBrowser(), @"C:\Projects");
        var dialog = new InlineDialog(shell, title: null, folderSelect);

        Assert.Contains("Enter Open", RenderText(dialog));

        folderSelect.HandleKey(TabKey);
        Assert.Contains("Enter Activate", RenderText(dialog));

        folderSelect.HandleKey(TabKey);
        var pathText = RenderText(dialog);
        Assert.Contains("Type path", pathText);
        Assert.Contains("Enter Confirm", pathText);
    }

    [Fact]
    public void StatusRow_RemainsPresentAndStable_ForDefaultHints()
    {
        var shell = new TestShell();
        var dialog = new InlineDialog(shell, title: null, new InlineSelect(shell, ["Red", "Green"]));

        var first = RenderLines(dialog);
        var second = RenderLines(dialog);

        Assert.False(string.IsNullOrWhiteSpace(first[^1]));
        Assert.Contains("Enter Select", first[^1]);
        Assert.Equal(first[^1], second[^1]);
    }
}
