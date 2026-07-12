using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;

namespace ItTiger.TigerCli.Tests;
internal class GridTest4 : CliRenderableComponent
{
    const string Test = "Zażółć_gęślą_jaźń!_—_już_wesoło_dźwięczy.W_żałosnej_żabie_żar_z_głąbka_się_wyleczy.Drżący_krzak_czerni,_gdzie_pająk_mątew_tkwi,I_źdźbło_trawy_drży,_bo_wiatr_szeleści_w_tle.Mrugnij,_mrugnij,_mrugnij,_wężu,_śliskim_zwojem,Już_się_łania_kryje,_by_nie_przyszła_z_łosiem.Gzęga,_gzęga,_gzęga,_w_brzęczących_kaczystach,A_na_łące_błyska_błękit_w_liściach.Gdzie_szczebiot_ptaków?_Gdzie_żuraw_w_gęstej_trzcinie?Już_gałązka_drgnęła,_już_świerszcz_się_zacznie.Łódź_płynie,_łódź_płynie,_w_odmęcie_jeziora,A_na_brzegu_cicho,_tylko_zżółkła_kora.";
    public override CliGrid ToGrid()
    {
        int cols = 34;
        int rows = 24;
        CliGrid grid = ToGrid(cols, rows);
        grid.DefaultCellStyle = new CliCellStyle()
        {
            CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue),
            FormattingMode = CliFormattingMode.Raw,
            HorizontalAlignment = CliTextAlignment.Left,
            VerticalAlignment = CliVerticalAlignment.Top,
            Wrapping = new CliWrapping(CliWrapMode.SymbolWrap)
        };

        var area = grid.AddFrameArea(CliFrameJoinStyle.SimplifiedCompatible, 0, 0, cols - 1, rows - 1,
            new CliCharStyle(CliColor.Gray, CliColor.DarkBlue));
        var frameStyle = new CliFrameSegment(CliFrameSegmentStyle.SingleFrame);
        area.AddOuterFrame(frameStyle);
        char cc = 'A';
        for (int c = 2; c < 32; c++)
        {
            grid.Set(c, 1, cc);
            grid.Set(c, 22, cc);
            if (cc == 'Z')
                cc = 'a';
            else
                cc++;
        }
        for (int r = 2; r < 22; r++)
        {
            grid.Set(1, r, r - 1);
            grid.Set(32, r, r - 1);
        }
        var colDef = new CliGridColumnDefinition(new CliCellStyle { HorizontalAlignment = CliTextAlignment.Right });
        grid.SetColumn(1, colDef);
        grid.SetColumn(32, colDef);
        var style = new CliCellStyle
        {
            HorizontalAlignment = CliTextAlignment.Center,
            VerticalAlignment = CliVerticalAlignment.Center,
            CharStyle = new CliCharStyle(CliColor.Red, CliColor.DarkCyan)
        };
        grid.Set(2, 2, Test, style, 30, 20);
        return grid;
    }
}
