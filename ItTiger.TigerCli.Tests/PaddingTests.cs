using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests;

public class PaddingTests : TestBase
{
    // Builds a 3-column × 3-row vertical CliTable (header + 2 data rows) with wrappable content.
    // Each data column is capped at MaxWidth = 6 to force wrapping. The total rendered width
    // is fixed (outer frame + 3 columns of 6 + 2 internal separators = 22), so cell content
    // that overflows the column boundary is visible as misalignment of the frame characters.
    private static CliTable BuildTable(CliCellPadding? dataPadding)
    {
        var table = new CliTable();
        table.Orientation = CliTableOrientation.Vertical;
        table.Header.Elements.Add(new CliTableElement("One", null));
        table.Header.Elements.Add(new CliTableElement("Two", null));
        table.Header.Elements.Add(new CliTableElement("Three", null));

        for (int i = 0; i < 3; i++)
        {
            var dataStyle = new CliCellStyle
            {
                MaxWidth = 6,
                Wrapping = new CliWrapping(CliWrapMode.WordWrap, false, "…")
            };
            if (dataPadding is CliCellPadding p)
                dataStyle.Padding = p;
            table.Header.Elements[i].DataStyle = dataStyle;
        }

        table.Records.Add(["aa bb cc", "dd ee ff", "gg hh ii"]);
        table.Records.Add(["jj kk ll", "mm nn oo", "pp qq rr"]);

        return table;
    }

    [Fact]
    public void Padding_None_BaselineSnapshot()
    {
        var table = BuildTable(dataPadding: null);

        // Baseline: each data column is exactly 6 chars; total width 22; no overflow.
        string[] expected =
        [
            "╔════════════════════╗",
            "║One   │Two   │Three ║",
            "║──────┼──────┼──────║",
            "║aa bb │dd ee │gg hh ║",
            "║cc    │ff    │ii    ║",
            "║jj kk │mm nn │pp qq ║",
            "║ll    │oo    │rr    ║",
            "╚════════════════════╝"
        ];
        AssertSnapshot(table, expected);
    }

    [Fact]
    public void Padding_Both_ProducesCleanLayout()
    {
        var table = BuildTable(dataPadding: CliCellPadding.Both);

        // With Both padding, each cell reserves 1 space on each side WITHIN its 6-char column.
        // Content must wrap to fit (column width - 2 = 4), so "aa bb cc" wraps as
        // "aa" / "bb" / "cc" (three single-token lines), then each line gets padded back to 6.
        // "Three" (5 chars) no longer fits the 4-char content area, so it CharWraps to
        // "Thre" / "e".
        // Critically: every rendered line is exactly 22 chars wide — no frame separator
        // ever appears inside cell content.
        string[] expected =
        [
            "╔════════════════════╗",
            "║ One  │ Two  │ Thre ║",
            "║      │      │ e    ║",
            "║──────┼──────┼──────║",
            "║ aa   │ dd   │ gg   ║",
            "║ bb   │ ee   │ hh   ║",
            "║ cc   │ ff   │ ii   ║",
            "║ jj   │ mm   │ pp   ║",
            "║ kk   │ nn   │ qq   ║",
            "║ ll   │ oo   │ rr   ║",
            "╚════════════════════╝"
        ];
        AssertSnapshot(table, expected);
    }
}
