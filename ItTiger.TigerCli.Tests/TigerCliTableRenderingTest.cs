using ItTiger.TigerCli.Enums;
using ItTiger.TigerCli.Primitives;
using ItTiger.TigerCli.Rendering;
using ItTiger.TigerCli.Terminal;

namespace ItTiger.TigerCli.Tests
{
    public class TigerCliTableRenderingTest : TestBase
    {
        [Fact]
        public void TableTest01()
        {
            var table = new CliTable();
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));            
            
            string[] expected = 
            [ 
                "╔═════════════╗", 
                "║One│Two│Three║", 
                "╚═════════════╝" 
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest02()
        {
            var table = new CliTable();
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Horizontal;
            
            string[] expected = 
            [
                "╔═════╗",
                "║ One ║",
                "║─────║",
                "║ Two ║",
                "║─────║",
                "║Three║",
                "╚═════╝"
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest03()
        {
            var table = new CliTable();
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Horizontal;
            table.Records.Add([1, null, "333-333"]);
            
            string[] expected =
            [
                "╔═════════════╗",
                "║ One │1      ║",
                "║─────┼───────║",
                "║ Two │       ║",
                "║─────┼───────║",
                "║Three│333-333║",
                "╚═════════════╝"
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest04()
        {
            var table = new CliTable();
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Vertical;
            table.Records.Add([1, null, "333-333"]);
            
            string[] expected =
            [
                "╔═══════════════╗",
                "║One│Two│ Three ║",
                "║───┼───┼───────║",
                "║1  │   │333-333║",
                "╚═══════════════╝"
            ];
            AssertSnapshot(table, expected);
        }


        [Fact]
        public void TableTest05()
        {
            var table = new CliTable();
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Vertical;
            table.Records.Add([1, null, "333-333"]);
            table.Header.Elements[0].DataStyle = new CliCellStyle { HorizontalAlignment = CliTextAlignment.Right };
            table.DefaultCellStyle.NullDisplayValue = "<null>";
            table.Records.Add([2, "Abc Def", "44-444\n5-555"]);
            
            string[] expected =
            [
                "╔═══════════════════╗",
                "║One│  Two  │ Three ║",
                "║───┼───────┼───────║",
                "║  1│<null> │333-333║",
                "║  2│Abc Def│44-444 ║",
                "║   │       │5-555  ║",
                "╚═══════════════════╝"
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest06()
        {
            var table = new CliTable();
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Horizontal;
            table.Records.Add([1, null, "333-333"]);
            table.Header.Elements[0].DataStyle = new CliCellStyle { HorizontalAlignment = CliTextAlignment.Right };
            table.DefaultCellStyle.NullDisplayValue = "<null>";
            table.Records.Add([2, "Abc Def", "44-444\n5-555"]);

            string[] expected =
            [
                "╔════════════════════╗",
                "║ One │      1      2║",
                "║─────┼──────────────║",
                "║ Two │<null> Abc Def║",
                "║─────┼──────────────║",
                "║Three│333-33344-444 ║",
                "║     │       5-555  ║",
                "╚════════════════════╝"
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest07()
        {
            var table = new CliTable();
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.DoubleFrame);
            table.FrameConfig.BetweenRecords = new CliFrameSegment(CliFrameSegmentStyle.SingleFrame);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Vertical;
            table.Records.Add([1, null, "333-333"]);
            table.Header.Elements[0].DataStyle = new CliCellStyle { HorizontalAlignment = CliTextAlignment.Right };
            table.DefaultCellStyle.NullDisplayValue = "<null>";
            table.Records.Add([2, "Abc Def", "44-444\n5-555"]);

            string[] expected =
            [                
                "╔═══════════════════╗",
                "║One│  Two  │ Three ║",
                "║───┼───────┼───────║",
                "║  1│<null> │333-333║",
                "║───┼───────┼───────║",
                "║  2│Abc Def│44-444 ║",
                "║   │       │5-555  ║",
                "╚═══════════════════╝"            
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest08()
        {
            var table = new CliTable();
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.DoubleFrame);
            table.FrameConfig.BetweenRecords = new CliFrameSegment(CliFrameSegmentStyle.SingleFrame);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Horizontal;
            table.Records.Add([1, null, "333-333"]);
            table.Header.Elements[0].DataStyle = new CliCellStyle { HorizontalAlignment = CliTextAlignment.Right };
            table.DefaultCellStyle.NullDisplayValue = "<null>";
            table.Records.Add([2, "Abc Def", "44-444\n5-555"]);
            
            string[] expected =
            [
                "╔═════════════════════╗",
                "║ One │      1│      2║",
                "║─────┼───────┼───────║",
                "║ Two │<null> │Abc Def║",
                "║─────┼───────┼───────║",
                "║Three│333-333│44-444 ║",
                "║     │       │5-555  ║",
                "╚═════════════════════╝"
            ];
            AssertSnapshot(table, expected);
        }




        ////////
        ///
        [Fact]
        public void TableTest11()
        {
            var table = new CliTable();
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));

            string[] expected =
            [                
                "One│Two│Three",
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest12()
        {
            var table = new CliTable();
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Horizontal;

            string[] expected =
            [                
                " One ",
                "─────",
                " Two ",
                "─────",
                "Three",             
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest13()
        {
            var table = new CliTable();
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Horizontal;
            table.Records.Add([1, null, "333-333"]);

            string[] expected =
            [
                " One │1      ",
                "─────┼───────",
                " Two │       ",
                "─────┼───────",
                "Three│333-333",
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest14()
        {
            var table = new CliTable();
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Vertical;
            table.Records.Add([1, null, "333-333"]);

            string[] expected =
            [                
                "One│Two│ Three ",
                "───┼───┼───────",
                "1  │   │333-333",             
            ];
            AssertSnapshot(table, expected);
        }


        [Fact]
        public void TableTest15()
        {
            var table = new CliTable();
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Vertical;
            table.Records.Add([1, null, "333-333"]);
            table.Header.Elements[0].DataStyle = new CliCellStyle { HorizontalAlignment = CliTextAlignment.Right };
            table.DefaultCellStyle.NullDisplayValue = "<null>";
            table.Records.Add([2, "Abc Def", "44-444\n5-555"]);

            string[] expected =
            [                
                "One│  Two  │ Three ",
                "───┼───────┼───────",
                "  1│<null> │333-333",
                "  2│Abc Def│44-444 ",
                "   │       │5-555  ",             
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest16()
        {
            var table = new CliTable();
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Horizontal;
            table.Records.Add([1, null, "333-333"]);
            table.Header.Elements[0].DataStyle = new CliCellStyle { HorizontalAlignment = CliTextAlignment.Right };
            table.DefaultCellStyle.NullDisplayValue = "<null>";
            table.Records.Add([2, "Abc Def", "44-444\n5-555"]);

            string[] expected =
            [
                " One │      1      2",
                "─────┼──────────────",
                " Two │<null> Abc Def",
                "─────┼──────────────",
                "Three│333-33344-444 ",
                "     │       5-555  ",
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest17()
        {
            var table = new CliTable();
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            table.FrameConfig.BetweenRecords = new CliFrameSegment(CliFrameSegmentStyle.SingleFrame);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Vertical;
            table.Records.Add([1, null, "333-333"]);
            table.Header.Elements[0].DataStyle = new CliCellStyle { HorizontalAlignment = CliTextAlignment.Right };
            table.DefaultCellStyle.NullDisplayValue = "<null>";
            table.Records.Add([2, "Abc Def", "44-444\n5-555"]);

            string[] expected =
            [
                "One│  Two  │ Three ",
                "───┼───────┼───────",
                "  1│<null> │333-333",
                "───┼───────┼───────",
                "  2│Abc Def│44-444 ",
                "   │       │5-555  ",
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest18()
        {
            var table = new CliTable();
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            table.FrameConfig.BetweenRecords = new CliFrameSegment(CliFrameSegmentStyle.SingleFrame);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Horizontal;
            table.Records.Add([1, null, "333-333"]);
            table.Header.Elements[0].DataStyle = new CliCellStyle { HorizontalAlignment = CliTextAlignment.Right };
            table.DefaultCellStyle.NullDisplayValue = "<null>";
            table.Records.Add([2, "Abc Def", "44-444\n5-555"]);

            string[] expected =
            [
                " One │      1│      2",
                "─────┼───────┼───────",
                " Two │<null> │Abc Def",
                "─────┼───────┼───────",
                "Three│333-333│44-444 ",
                "     │       │5-555  "
            ];
            AssertSnapshot(table, expected);
        }


        // =======================================================

        [Fact]
        public void TableTest21()
        {
            var table = new CliTable();
            table.Title = new CliTableTitle("Hello, World!", CliTextAlignment.Center, new CliCharStyle(CliColor.Gray, CliColor.DarkGreen));
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));

            string[] expected =
            [
                " Hello, World! ",
                "╔═════════════╗",
                "║One│Two│Three║",
                "╚═════════════╝"
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest22()
        {
            var table = new CliTable();
            table.Title = new CliTableTitle("Hello, World!", CliTextAlignment.Center, new CliCharStyle(CliColor.Gray, CliColor.DarkGreen));
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Horizontal;

            string[] expected =
            [
                "Hello, World!",
                "╔═══════════╗",
                "║    One    ║",
                "║───────────║",
                "║    Two    ║",
                "║───────────║",
                "║   Three   ║",
                "╚═══════════╝"
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest23()
        {
            var table = new CliTable();
            table.Title = new CliTableTitle("Hello, World!", CliTextAlignment.Center, new CliCharStyle(CliColor.Gray, CliColor.DarkGreen));
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Horizontal;
            table.Records.Add([1, null, "333-333"]);

            string[] expected =
            [
                " Hello, World! ",
                "╔═════════════╗",
                "║ One │1      ║",
                "║─────┼───────║",
                "║ Two │       ║",
                "║─────┼───────║",
                "║Three│333-333║",
                "╚═════════════╝"
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest24()
        {
            var table = new CliTable();
            table.Title = new CliTableTitle("Hello, World!", CliTextAlignment.Center, new CliCharStyle(CliColor.Gray, CliColor.DarkGreen));
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Vertical;
            table.Records.Add([1, null, "333-333"]);

            string[] expected =
            [
                "  Hello, World!  ",
                "╔═══════════════╗",
                "║One│Two│ Three ║",
                "║───┼───┼───────║",
                "║1  │   │333-333║",
                "╚═══════════════╝"
            ];
            AssertSnapshot(table, expected);
        }


        [Fact]
        public void TableTest25()
        {
            var table = new CliTable();
            table.Title = new CliTableTitle("Hello, World!", CliTextAlignment.Center, new CliCharStyle(CliColor.Gray, CliColor.DarkGreen));
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Vertical;
            table.Records.Add([1, null, "333-333"]);
            table.Header.Elements[0].DataStyle = new CliCellStyle { HorizontalAlignment = CliTextAlignment.Right };
            table.DefaultCellStyle.NullDisplayValue = "<null>";
            table.Records.Add([2, "Abc Def", "44-444\n5-555"]);

            string[] expected =
            [
                "    Hello, World!    ",
                "╔═══════════════════╗",
                "║One│  Two  │ Three ║",
                "║───┼───────┼───────║",
                "║  1│<null> │333-333║",
                "║  2│Abc Def│44-444 ║",
                "║   │       │5-555  ║",
                "╚═══════════════════╝"
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest26()
        {
            var table = new CliTable();
            table.Title = new CliTableTitle("Hello, World!", CliTextAlignment.Center, new CliCharStyle(CliColor.Gray, CliColor.DarkGreen));
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Horizontal;
            table.Records.Add([1, null, "333-333"]);
            table.Header.Elements[0].DataStyle = new CliCellStyle { HorizontalAlignment = CliTextAlignment.Right };
            table.DefaultCellStyle.NullDisplayValue = "<null>";
            table.Records.Add([2, "Abc Def", "44-444\n5-555"]);

            string[] expected =
            [
                "    Hello, World!     ",
                "╔════════════════════╗",
                "║ One │      1      2║",
                "║─────┼──────────────║",
                "║ Two │<null> Abc Def║",
                "║─────┼──────────────║",
                "║Three│333-33344-444 ║",
                "║     │       5-555  ║",
                "╚════════════════════╝"
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest27()
        {
            var table = new CliTable();
            table.Title = new CliTableTitle("Hello, World!", CliTextAlignment.Center, new CliCharStyle(CliColor.Gray, CliColor.DarkGreen));
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.DoubleFrame);
            table.FrameConfig.BetweenRecords = new CliFrameSegment(CliFrameSegmentStyle.SingleFrame);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Vertical;
            table.Records.Add([1, null, "333-333"]);
            table.Header.Elements[0].DataStyle = new CliCellStyle { HorizontalAlignment = CliTextAlignment.Right };
            table.DefaultCellStyle.NullDisplayValue = "<null>";
            table.Records.Add([2, "Abc Def", "44-444\n5-555"]);

            string[] expected =
            [
                "    Hello, World!    ",
                "╔═══════════════════╗",
                "║One│  Two  │ Three ║",
                "║───┼───────┼───────║",
                "║  1│<null> │333-333║",
                "║───┼───────┼───────║",
                "║  2│Abc Def│44-444 ║",
                "║   │       │5-555  ║",
                "╚═══════════════════╝"
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest28()
        {
            var table = new CliTable();
            table.Title = new CliTableTitle("Hello, World!", CliTextAlignment.Center, new CliCharStyle(CliColor.Gray, CliColor.DarkGreen));
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.DoubleFrame);
            table.FrameConfig.BetweenRecords = new CliFrameSegment(CliFrameSegmentStyle.SingleFrame);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Horizontal;
            table.Records.Add([1, null, "333-333"]);
            table.Header.Elements[0].DataStyle = new CliCellStyle { HorizontalAlignment = CliTextAlignment.Right };
            table.DefaultCellStyle.NullDisplayValue = "<null>";
            table.Records.Add([2, "Abc Def", "44-444\n5-555"]);

            string[] expected =
            [
                "     Hello, World!     ",
                "╔═════════════════════╗",
                "║ One │      1│      2║",
                "║─────┼───────┼───────║",
                "║ Two │<null> │Abc Def║",
                "║─────┼───────┼───────║",
                "║Three│333-333│44-444 ║",
                "║     │       │5-555  ║",
                "╚═════════════════════╝"
            ];
            AssertSnapshot(table, expected);
        }

        // -----------------------------
        

        [Fact]
        public void TableTest31()
        {
            var table = new CliTable();
            table.Title = new CliTableTitle("Hello, World!", CliTextAlignment.Center, new CliCharStyle(CliColor.Gray, CliColor.DarkGreen));
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };            
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));

            string[] expected =
            [
                "Hello, World!",
                "One│Two│Three",
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest32()
        {
            var table = new CliTable();
            table.Title = new CliTableTitle("Hello, World!", CliTextAlignment.Center, new CliCharStyle(CliColor.Gray, CliColor.DarkGreen));
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };            
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Horizontal;

            string[] expected =
            [
                "Hello, World!",
                "     One     ",
                "─────────────",
                "     Two     ",
                "─────────────",
                "    Three    ",
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest33()
        {
            var table = new CliTable();
            table.Title = new CliTableTitle("Hello, World!", CliTextAlignment.Center, new CliCharStyle(CliColor.Gray, CliColor.DarkGreen));
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Horizontal;
            table.Records.Add([1, null, "333-333"]);

            string[] expected =
            [
                "Hello, World!",
                " One │1      ",
                "─────┼───────",
                " Two │       ",
                "─────┼───────",
                "Three│333-333",
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest34()
        {
            var table = new CliTable();
            table.Title = new CliTableTitle("Hello, World!", CliTextAlignment.Center, new CliCharStyle(CliColor.Gray, CliColor.DarkGreen));
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Vertical;
            table.Records.Add([1, null, "333-333"]);

            string[] expected =
            [
                " Hello, World! ",
                "One│Two│ Three ",
                "───┼───┼───────",
                "1  │   │333-333",
            ];
            AssertSnapshot(table, expected);
        }


        [Fact]
        public void TableTest35()
        {
            var table = new CliTable();
            table.Title = new CliTableTitle("Hello, World!", CliTextAlignment.Center, new CliCharStyle(CliColor.Gray, CliColor.DarkGreen));
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Vertical;
            table.Records.Add([1, null, "333-333"]);
            table.Header.Elements[0].DataStyle = new CliCellStyle { HorizontalAlignment = CliTextAlignment.Right };
            table.DefaultCellStyle.NullDisplayValue = "<null>";
            table.Records.Add([2, "Abc Def", "44-444\n5-555"]);

            string[] expected =
            [
                "   Hello, World!   ",
                "One│  Two  │ Three ",
                "───┼───────┼───────",
                "  1│<null> │333-333",
                "  2│Abc Def│44-444 ",
                "   │       │5-555  ",
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest36()
        {
            var table = new CliTable();
            table.Title = new CliTableTitle("Hello, World!", CliTextAlignment.Center, new CliCharStyle(CliColor.Gray, CliColor.DarkGreen));
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Horizontal;
            table.Records.Add([1, null, "333-333"]);
            table.Header.Elements[0].DataStyle = new CliCellStyle { HorizontalAlignment = CliTextAlignment.Right };
            table.DefaultCellStyle.NullDisplayValue = "<null>";
            table.Records.Add([2, "Abc Def", "44-444\n5-555"]);

            string[] expected =
            [
                "   Hello, World!    ",
                " One │      1      2",
                "─────┼──────────────",
                " Two │<null> Abc Def",
                "─────┼──────────────",
                "Three│333-33344-444 ",
                "     │       5-555  ",
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest37()
        {
            var table = new CliTable();
            table.Title = new CliTableTitle("Hello, World!", CliTextAlignment.Center, new CliCharStyle(CliColor.Gray, CliColor.DarkGreen));
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            table.FrameConfig.BetweenRecords = new CliFrameSegment(CliFrameSegmentStyle.SingleFrame);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Vertical;
            table.Records.Add([1, null, "333-333"]);
            table.Header.Elements[0].DataStyle = new CliCellStyle { HorizontalAlignment = CliTextAlignment.Right };
            table.DefaultCellStyle.NullDisplayValue = "<null>";
            table.Records.Add([2, "Abc Def", "44-444\n5-555"]);

            string[] expected =
            [
                "   Hello, World!   ",
                "One│  Two  │ Three ",
                "───┼───────┼───────",
                "  1│<null> │333-333",
                "───┼───────┼───────",
                "  2│Abc Def│44-444 ",
                "   │       │5-555  ",
            ];
            AssertSnapshot(table, expected);
        }

        [Fact]
        public void TableTest38()
        {
            var table = new CliTable();
            table.Title = new CliTableTitle("Hello, World!", CliTextAlignment.Center, new CliCharStyle(CliColor.Gray, CliColor.DarkGreen));
            table.DefaultCellStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.Gray, CliColor.DarkBlue)
            };
            table.Header.HeaderStyle = new CliCellStyle
            {
                CharStyle = new CliCharStyle(CliColor.White, CliColor.DarkGray),
                HorizontalAlignment = CliTextAlignment.Center
            };
            table.FrameConfig.CharStyle = new CliCharStyle(CliColor.Yellow, CliColor.DarkBlue);
            table.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            table.FrameConfig.BetweenRecords = new CliFrameSegment(CliFrameSegmentStyle.SingleFrame);
            table.Header.Elements.Add(new CliTableElement("One", null));
            table.Header.Elements.Add(new CliTableElement("Two", null));
            table.Header.Elements.Add(new CliTableElement("Three", null));
            table.Orientation = CliTableOrientation.Horizontal;
            table.Records.Add([1, null, "333-333"]);
            table.Header.Elements[0].DataStyle = new CliCellStyle { HorizontalAlignment = CliTextAlignment.Right };
            table.DefaultCellStyle.NullDisplayValue = "<null>";
            table.Records.Add([2, "Abc Def", "44-444\n5-555"]);

            string[] expected =
            [
                "    Hello, World!    ",
                " One │      1│      2",
                "─────┼───────┼───────",
                " Two │<null> │Abc Def",
                "─────┼───────┼───────",
                "Three│333-333│44-444 ",
                "     │       │5-555  "
            ];
            AssertSnapshot(table, expected);
        }

        // ****************************

        [Fact]
        public void Table_HiddenHeader_Vertical_NoOuter_NoRecords()
        {
            var t = new CliTable
            {                
                Orientation = CliTableOrientation.Vertical
            };
            t.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            t.Header.IsVisible = false;
            t.Header.Elements.Add(new CliTableElement("One", null));
            t.Header.Elements.Add(new CliTableElement("Two", null));
            t.Header.Elements.Add(new CliTableElement("Three", null));

            // With no header row to show, vertical orientation + no records => no output.
            // If your table guarantees at least one row of content, adjust expected accordingly.
            string[] expected = [];
            AssertSnapshot(t, expected);
        }

        [Fact]
        public void Table_HiddenHeader_Vertical_SingleOuter_NoRecords()
        {
            var t = new CliTable
            {
                Orientation = CliTableOrientation.Vertical
            };
            t.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.SingleFrame);
            t.FrameConfig.BetweenElements = new CliFrameSegment(CliFrameSegmentStyle.None);
            t.Header.IsVisible = false;            
            t.Header.Elements.Add(new CliTableElement("One", null));
            t.Header.Elements.Add(new CliTableElement("Two", null));
            t.Header.Elements.Add(new CliTableElement("Three", null));
            
            string[] expected = 
            [
                "┌───┐", 
                "└───┘"
            ];
            AssertSnapshot(t, expected);
        }


        [Fact]
        public void Table_HiddenHeader_Horizontal_NoOuter_OneRecord()
        {
            var t = new CliTable
            {
                Orientation = CliTableOrientation.Horizontal
            };
            t.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            t.Header.IsVisible = false;
            t.Header.Elements.Add(new CliTableElement("One", null));
            t.Header.Elements.Add(new CliTableElement("Two", null));
            t.Header.Elements.Add(new CliTableElement("Three", null));
            t.Records.Add([1, null, "333-333"]);

            // Expect just the data “stack”, no header, no after-header rule.
            string[] expected =
            [
                "1      ",
                "───────",
                "       ",
                "───────",
                "333-333",
            ];
            AssertSnapshot(t, expected);
        }


        [Fact]
        public void Table_HiddenHeader_Vertical_OuterFrame_OneRecord()
        {
            var t = new CliTable
            {                
                Orientation = CliTableOrientation.Vertical
            };
            t.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.DoubleFrame);
            t.Header.IsVisible = false;
            t.Header.Elements.Add(new CliTableElement("One", null));
            t.Header.Elements.Add(new CliTableElement("Two", null));
            t.Header.Elements.Add(new CliTableElement("Three", null));
            t.Records.Add([1, null, "333-333"]);

            string[] expected =
            [
                "╔═══════════╗",
                "║1│ │333-333║",
                "╚═══════════╝"
            ];
            AssertSnapshot(t, expected);
        }


        [Fact]
        public void Table_NoAfterHeaderFrame_Vertical_OuterFrame()
        {
            var t = new CliTable
            {                
                Orientation = CliTableOrientation.Vertical
            };
            t.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.SingleFrame);
            t.FrameConfig.BetweenElements = new CliFrameSegment(CliFrameSegmentStyle.SingleFrame);
            t.FrameConfig.AfterHeader = new CliFrameSegment(CliFrameSegmentStyle.None);
            t.Header.IsVisible = true;
            t.Header.Elements.Add(new CliTableElement("One", null));
            t.Header.Elements.Add(new CliTableElement("Two", null));
            t.Header.Elements.Add(new CliTableElement("Three", null));
            t.Records.Add([1, 2, 3]);

            string[] expected =
            [
                "┌───┬───┬─────┐",
                "│One│Two│Three│", // no horizontal rule here
                "│1  │2  │3    │",
                "└───┴───┴─────┘"
            ];
            AssertSnapshot(t, expected);
        }


        [Fact]
        public void Table_NoBetweenElements_Vertical_NoOuter_AfterHeaderLine()
        {
            var t = new CliTable
            {                
                Orientation = CliTableOrientation.Vertical
            };
            t.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            t.FrameConfig.BetweenElements = new CliFrameSegment(CliFrameSegmentStyle.None);
            t.FrameConfig.AfterHeader = new CliFrameSegment(CliFrameSegmentStyle.SingleFrame);
            t.Header.IsVisible = true;
            t.Header.Elements.Add(new CliTableElement("One", null));
            t.Header.Elements.Add(new CliTableElement("Two", null));
            t.Header.Elements.Add(new CliTableElement("Three", null));
            t.Records.Add([1, 2, 3]);

            string[] expected =
            [
                "OneTwoThree",
                "───────────",
                "1  2  3    ",
            ];
            AssertSnapshot(t, expected);
        }



        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, false)]
        public void Table_Smoke_Invariants_NoExceptions(bool hideHeader, bool noAfterHeader, bool noBetweenElements)
        {
            var t = new CliTable { Orientation = CliTableOrientation.Vertical };
            t.FrameConfig.OuterFrame = new CliFrameSegment(CliFrameSegmentStyle.None);
            t.FrameConfig.AfterHeader = new CliFrameSegment(noAfterHeader ? CliFrameSegmentStyle.None : CliFrameSegmentStyle.SingleFrame);
            t.FrameConfig.BetweenElements = new CliFrameSegment(noBetweenElements ? CliFrameSegmentStyle.None : CliFrameSegmentStyle.SingleFrame);
            t.Header.IsVisible = !hideHeader;
            t.Header.Elements.Add(new CliTableElement("One", null));
            t.Header.Elements.Add(new CliTableElement("Two", null));
            t.Header.Elements.Add(new CliTableElement("Three", null));
            t.Records.Add([1, 2, 3]);

            var lines = TigerConsole.RenderToLines(t);
            // Invariants: no nulls; all lines same width
            Assert.NotNull(lines);
            if (lines.Count > 0)
            {
                var w = lines[0].Length;
                Assert.All(lines, L => Assert.Equal(w, L.Length));
            }
        }



    }
}
