namespace ItTiger.TigerCli.Tests
{
    public class TigerCliGridRenderingTest : TestBase
    {
        [Fact]
        public void GridTest1()
        {
            var grid = new GridTest1();
            string[] expected =
            [
                "╔══════════════════════════════════╗",
                "║  ABCDEFGHIJKLMNOPQRSTUVWXYZabcd  ║",
                "║1                               1 ║",
                "║2                               2 ║",
                "║3  Lorem ipsum dolor sit amet,  3 ║",
                "║4  consectetur adipiscing elit, 4 ║",
                "║5     sed do eiusmod tempor     5 ║",
                "║6      incididunt ut labore     6 ║",
                "║7    et dolore magna aliqua.    7 ║",
                "║8    Ut enim ad minim veniam,   8 ║",
                "║9   quis nostrud exercitation   9 ║",
                "║10     ullamco laboris nisi     10║",
                "║11       ut aliquip ex ea       11║",
                "║12      commodo consequat.      12║",
                "║13                              13║",
                "║14                              14║",
                "║15                              15║",
                "║  ABCDEFGHIJKLMNOPQRSTUVWXYZabcd  ║",
                "╚══════════════════════════════════╝"
            ];
            AssertSnapshot(grid, expected);
        }

        [Fact]
        public void GridTest2()
        {
            var grid = new GridTest2();
            string[] expected =
            [
                "┌──────────────────────────────────┐",
                "│  ABCDEFGHIJKLMNOPQRSTUVWXYZabcd  │",
                "│ 1                               1│",
                "│ 2                               2│",
                "│ 3 Lorem ipsum dolor sit amet,   3│",
                "│ 4 consectetur adipiscing elit,  4│",
                "│ 5    sed do eiusmod tempor      5│",
                "│ 6     incididunt ut labore      6│",
                "│ 7   et dolore magna aliqua.     7│",
                "│ 8   Ut enim ad minim veniam,    8│",
                "│ 9  quis nostrud exercitation    9│",
                "│10     ullamco laboris nisi     10│",
                "│11       ut aliquip ex ea       11│",
                "│12      commodo consequat.      12│",
                "│13                              13│",
                "│14                              14│",
                "│15                              15│",
                "│  ABCDEFGHIJKLMNOPQRSTUVWXYZabcd  │",
                "└──────────────────────────────────┘"
            ];
            AssertSnapshot(grid, expected);
        }

        [Fact]
        public void GridTest3()
        {            
            var grid = new GridTest3();
            string[] expected =
            [
                "┌────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐",
                "│  A       B       C       D       E       F       G       H       I       J       K       L       M       N       O       P       Q       R       S       T       U       V       W      X      Y      Z      a      b      c      d        │",
                "│ 1                                                                                                                                                                                                                                         1│",
                "│ 2                                                                                                                                                                                                                                         2│",
                "│ 3                                                                                                                                                                                                                                         3│",
                "│ 4                                                                                                                                                                                                                                         4│",
                "│ 5                                                                                                                                                                                                                                         5│",
                "│ 6                                                                                                                                                                                                                                         6│",
                "│ 7                                                                                                                                                                                                                                         7│",
                "│ 8Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation nullamco laboris nisi ut aliquip ex ea commodo consequat. 8│",
                "│ 9                                                                                                                                                                                                                                         9│",
                "│10                                                                                                                                                                                                                                        10│",
                "│11                                                                                                                                                                                                                                        11│",
                "│12                                                                                                                                                                                                                                        12│",
                "│13                                                                                                                                                                                                                                        13│",
                "│14                                                                                                                                                                                                                                        14│",
                "│15                                                                                                                                                                                                                                        15│",
                "│  A       B       C       D       E       F       G       H       I       J       K       L       M       N       O       P       Q       R       S       T       U       V       W      X      Y      Z      a      b      c      d        │",
                "└────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘"
            ];
            AssertSnapshot(grid, expected);
        }

        [Fact]
        public void GridTest3Sm()
        {
            var fixture = new GridTest3();
            var grid = fixture.ToGrid();
            grid.SoftMaxWidth = 36;
            string[] expected =
            [
                "┌──────────────────────────────────┐",
                "│  ABCDEFGHIJKLMNOPQRSTUVWXYZabcd  │",
                "│ 1                               1│",
                "│ 2                               2│",
                "│ 3                               3│",
                "│ 4 Lorem ipsum dolor sit amet,   4│",
                "│ 5 consectetur adipiscing elit,  5│",
                "│ 6    sed do eiusmod tempor      6│",
                "│ 7incididunt ut labore et dolore 7│",
                "│ 8magna aliqua. Ut enim ad minim 8│",
                "│ 9     veniam, quis nostrud      9│",
                "│10exercitation nullamco laboris 10│",
                "│11nisi ut aliquip ex ea commodo 11│",
                "│12          consequat.          12│",
                "│13                              13│",
                "│14                              14│",
                "│15                              15│",
                "│  ABCDEFGHIJKLMNOPQRSTUVWXYZabcd  │",
                "└──────────────────────────────────┘"
            ];
            AssertSnapshot(grid, expected);
        }

        [Fact]
        public void GridTest4()
        {            
            var grid = new GridTest4();            
            string[] expected =
            [
                "┌──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐",
                "│  A                 B                 C                 D                 E                 F                 G                 H                 I                 J                 K                 L                 M                 N                 O                 P                 Q                 R                 S                 T                 U                V                W                X                Y                Z                a                b                c                d                  │",
                "│ 1                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   1│",
                "│ 2                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   2│",
                "│ 3                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   3│",
                "│ 4                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   4│",
                "│ 5                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   5│",
                "│ 6                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   6│",
                "│ 7                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   7│",
                "│ 8                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   8│",
                "│ 9                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   9│",
                "│10Zażółć_gęślą_jaźń!_—_już_wesoło_dźwięczy.W_żałosnej_żabie_żar_z_głąbka_się_wyleczy.Drżący_krzak_czerni,_gdzie_pająk_mątew_tkwi,I_źdźbło_trawy_drży,_bo_wiatr_szeleści_w_tle.Mrugnij,_mrugnij,_mrugnij,_wężu,_śliskim_zwojem,Już_się_łania_kryje,_by_nie_przyszła_z_łosiem.Gzęga,_gzęga,_gzęga,_w_brzęczących_kaczystach,A_na_łące_błyska_błękit_w_liściach.Gdzie_szczebiot_ptaków?_Gdzie_żuraw_w_gęstej_trzcinie?Już_gałązka_drgnęła,_już_świerszcz_się_zacznie.Łódź_płynie,_łódź_płynie,_w_odmęcie_jeziora,A_na_brzegu_cicho,_tylko_zżółkła_kora.10│",
                "│11                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  11│",
                "│12                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  12│",
                "│13                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  13│",
                "│14                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  14│",
                "│15                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  15│",
                "│16                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  16│",
                "│17                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  17│",
                "│18                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  18│",
                "│19                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  19│",
                "│20                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  20│",
                "│  A                 B                 C                 D                 E                 F                 G                 H                 I                 J                 K                 L                 M                 N                 O                 P                 Q                 R                 S                 T                 U                V                W                X                Y                Z                a                b                c                d                  │",
                "└──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘"
            ];
            AssertSnapshot(grid, expected);
        }

        [Fact]
        public void GridTest4Sm()
        {
            var fixture = new GridTest4();
            var grid = fixture.ToGrid();
            grid.SoftMaxWidth = 36;
            string[] expected =
            [
                "┌──────────────────────────────────┐",
                "│  ABCDEFGHIJKLMNOPQRSTUVWXYZabcd  │",
                "│ 1  Zażółć_gęślą_jaźń!_—_już_    1│",
                "│ 2 wesoło_dźwięczy.W_żałosnej_   2│",
                "│ 3żabie_żar_z_głąbka_się_wyleczy 3│",
                "│ 4 .Drżący_krzak_czerni,_gdzie_  4│",
                "│ 5  pająk_mątew_tkwi,I_źdźbło_   5│",
                "│ 6trawy_drży,_bo_wiatr_szeleści_ 6│",
                "│ 7   w_tle.Mrugnij,_mrugnij,_    7│",
                "│ 8mrugnij,_wężu,_śliskim_zwojem, 8│",
                "│ 9 Już_się_łania_kryje,_by_nie_  9│",
                "│10przyszła_z_łosiem.Gzęga,_gzęga10│",
                "│11   ,_gzęga,_w_brzęczących_    11│",
                "│12 kaczystach,A_na_łące_błyska_ 12│",
                "│13   błękit_w_liściach.Gdzie_   13│",
                "│14szczebiot_ptaków?_Gdzie_żuraw_14│",
                "│15w_gęstej_trzcinie?Już_gałązka_15│",
                "│16 drgnęła,_już_świerszcz_się_  16│",
                "│17  zacznie.Łódź_płynie,_łódź_  17│",
                "│18płynie,_w_odmęcie_jeziora,A_na18│",
                "│19_brzegu_cicho,_tylko_zżółkła_ 19│",
                "│20            kora.             20│",
                "│  ABCDEFGHIJKLMNOPQRSTUVWXYZabcd  │",
                "└──────────────────────────────────┘"
            ];
            AssertSnapshot(grid, expected);
        }
    }
}
