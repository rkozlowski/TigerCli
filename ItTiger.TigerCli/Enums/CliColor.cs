namespace ItTiger.TigerCli.Enums
{
    /// <summary>
    /// The full ANSI 256-color palette as a single enum. The numeric value of every member
    /// equals its ANSI palette index.
    /// <para>Values 0–15 preserve the standard <see cref="System.ConsoleColor"/>-compatible
    /// names and ordering. Values 16–255 map directly to ANSI 256-color indexes: 16–231 form
    /// the 6×6×6 color cube and 232–255 form the grayscale ramp. Only indexes 0–15 are
    /// renderable by the legacy <see cref="System.Console"/> sinks; 16–255 are down-converted to
    /// the nearest standard color until a 256-color sink exists (see CliColorMapper / CliColorPalette).</para>
    /// </summary>
    public enum CliColor
    {
        // ---- 0–15: standard ConsoleColor-compatible colors (do not rename) ----
        /// <summary>
        /// Hex value: <c>#000000</c>.
        /// </summary>
        Black = 0,
        /// <summary>
        /// Hex value: <c>#000080</c>.
        /// </summary>
        DarkBlue = 1,
        /// <summary>
        /// Hex value: <c>#008000</c>.
        /// </summary>
        DarkGreen = 2,
        /// <summary>
        /// Hex value: <c>#008080</c>.
        /// </summary>
        DarkCyan = 3,
        /// <summary>
        /// Hex value: <c>#800000</c>.
        /// </summary>
        DarkRed = 4,
        /// <summary>
        /// Hex value: <c>#800080</c>.
        /// </summary>
        DarkMagenta = 5,
        /// <summary>
        /// Hex value: <c>#808000</c>.
        /// </summary>
        DarkYellow = 6,
        /// <summary>
        /// Hex value: <c>#C0C0C0</c>.
        /// </summary>
        Gray = 7,
        /// <summary>
        /// Hex value: <c>#808080</c>.
        /// </summary>
        DarkGray = 8,
        /// <summary>
        /// Hex value: <c>#0000FF</c>.
        /// </summary>
        Blue = 9,
        /// <summary>
        /// Hex value: <c>#00FF00</c>.
        /// </summary>
        Green = 10,
        /// <summary>
        /// Hex value: <c>#00FFFF</c>.
        /// </summary>
        Cyan = 11,
        /// <summary>
        /// Hex value: <c>#FF0000</c>.
        /// </summary>
        Red = 12,
        /// <summary>
        /// Hex value: <c>#FF00FF</c>.
        /// </summary>
        Magenta = 13,
        /// <summary>
        /// Hex value: <c>#FFFF00</c>.
        /// </summary>
        Yellow = 14,
        /// <summary>
        /// Hex value: <c>#FFFFFF</c>.
        /// </summary>
        White = 15,

        // ---- 16–231: ANSI 6×6×6 color cube ----
        /// <summary>
        /// Hex value: <c>#000000</c>.
        /// </summary>
        Black1 = 16,
        /// <summary>
        /// Hex value: <c>#00005F</c>.
        /// </summary>
        Navy = 17,
        /// <summary>
        /// Hex value: <c>#000087</c>.
        /// </summary>
        Navy2 = 18,
        /// <summary>
        /// Hex value: <c>#0000AF</c>.
        /// </summary>
        MediumBlue = 19,
        /// <summary>
        /// Hex value: <c>#0000D7</c>.
        /// </summary>
        MediumBlue2 = 20,
        /// <summary>
        /// Hex value: <c>#0000FF</c>.
        /// </summary>
        Blue1 = 21,
        /// <summary>
        /// Hex value: <c>#005F00</c>.
        /// </summary>
        ForestGreen = 22,
        /// <summary>
        /// Hex value: <c>#005F5F</c>.
        /// </summary>
        Teal = 23,
        /// <summary>
        /// Hex value: <c>#005F87</c>.
        /// </summary>
        OceanBlue = 24,
        /// <summary>
        /// Hex value: <c>#005FAF</c>.
        /// </summary>
        OceanBlue2 = 25,
        /// <summary>
        /// Hex value: <c>#005FD7</c>.
        /// </summary>
        RoyalBlue = 26,
        /// <summary>
        /// Hex value: <c>#005FFF</c>.
        /// </summary>
        DodgerBlue = 27,
        /// <summary>
        /// Hex value: <c>#008700</c>.
        /// </summary>
        ForestGreen2 = 28,
        /// <summary>
        /// Hex value: <c>#00875F</c>.
        /// </summary>
        SeaGreen = 29,
        /// <summary>
        /// Hex value: <c>#008787</c>.
        /// </summary>
        Teal2 = 30,
        /// <summary>
        /// Hex value: <c>#0087AF</c>.
        /// </summary>
        TealBlue = 31,
        /// <summary>
        /// Hex value: <c>#0087D7</c>.
        /// </summary>
        DodgerBlue2 = 32,
        /// <summary>
        /// Hex value: <c>#0087FF</c>.
        /// </summary>
        DodgerBlue3 = 33,
        /// <summary>
        /// Hex value: <c>#00AF00</c>.
        /// </summary>
        ForestGreen3 = 34,
        /// <summary>
        /// Hex value: <c>#00AF5F</c>.
        /// </summary>
        SeaGreen2 = 35,
        /// <summary>
        /// Hex value: <c>#00AF87</c>.
        /// </summary>
        SeaGreen3 = 36,
        /// <summary>
        /// Hex value: <c>#00AFAF</c>.
        /// </summary>
        LightSeaGreen = 37,
        /// <summary>
        /// Hex value: <c>#00AFD7</c>.
        /// </summary>
        DarkTurquoise = 38,
        /// <summary>
        /// Hex value: <c>#00AFFF</c>.
        /// </summary>
        DeepSkyBlue = 39,
        /// <summary>
        /// Hex value: <c>#00D700</c>.
        /// </summary>
        Green2 = 40,
        /// <summary>
        /// Hex value: <c>#00D75F</c>.
        /// </summary>
        SpringGreen = 41,
        /// <summary>
        /// Hex value: <c>#00D787</c>.
        /// </summary>
        MediumSpringGreen = 42,
        /// <summary>
        /// Hex value: <c>#00D7AF</c>.
        /// </summary>
        DarkTurquoise2 = 43,
        /// <summary>
        /// Hex value: <c>#00D7D7</c>.
        /// </summary>
        DarkTurquoise3 = 44,
        /// <summary>
        /// Hex value: <c>#00D7FF</c>.
        /// </summary>
        DeepSkyBlue2 = 45,
        /// <summary>
        /// Hex value: <c>#00FF00</c>.
        /// </summary>
        Green1 = 46,
        /// <summary>
        /// Hex value: <c>#00FF5F</c>.
        /// </summary>
        SpringGreen2 = 47,
        /// <summary>
        /// Hex value: <c>#00FF87</c>.
        /// </summary>
        SpringGreen3 = 48,
        /// <summary>
        /// Hex value: <c>#00FFAF</c>.
        /// </summary>
        MediumSpringGreen2 = 49,
        /// <summary>
        /// Hex value: <c>#00FFD7</c>.
        /// </summary>
        Cyan2 = 50,
        /// <summary>
        /// Hex value: <c>#00FFFF</c>.
        /// </summary>
        Cyan1 = 51,
        /// <summary>
        /// Hex value: <c>#5F0000</c>.
        /// </summary>
        Maroon = 52,
        /// <summary>
        /// Hex value: <c>#5F005F</c>.
        /// </summary>
        Indigo = 53,
        /// <summary>
        /// Hex value: <c>#5F0087</c>.
        /// </summary>
        Indigo2 = 54,
        /// <summary>
        /// Hex value: <c>#5F00AF</c>.
        /// </summary>
        Indigo3 = 55,
        /// <summary>
        /// Hex value: <c>#5F00D7</c>.
        /// </summary>
        BlueViolet2 = 56,
        /// <summary>
        /// Hex value: <c>#5F00FF</c>.
        /// </summary>
        BlueViolet = 57,
        /// <summary>
        /// Hex value: <c>#5F5F00</c>.
        /// </summary>
        Olive = 58,
        /// <summary>
        /// Hex value: <c>#5F5F5F</c>.
        /// </summary>
        Gray37 = 59,
        /// <summary>
        /// Hex value: <c>#5F5F87</c>.
        /// </summary>
        BlueGray = 60,
        /// <summary>
        /// Hex value: <c>#5F5FAF</c>.
        /// </summary>
        SlateBlue = 61,
        /// <summary>
        /// Hex value: <c>#5F5FD7</c>.
        /// </summary>
        SlateBlue2 = 62,
        /// <summary>
        /// Hex value: <c>#5F5FFF</c>.
        /// </summary>
        MediumSlateBlue = 63,
        /// <summary>
        /// Hex value: <c>#5F8700</c>.
        /// </summary>
        Olive2 = 64,
        /// <summary>
        /// Hex value: <c>#5F875F</c>.
        /// </summary>
        GrayGreen = 65,
        /// <summary>
        /// Hex value: <c>#5F8787</c>.
        /// </summary>
        SlateGray = 66,
        /// <summary>
        /// Hex value: <c>#5F87AF</c>.
        /// </summary>
        SteelBlue = 67,
        /// <summary>
        /// Hex value: <c>#5F87D7</c>.
        /// </summary>
        CornflowerBlue = 68,
        /// <summary>
        /// Hex value: <c>#5F87FF</c>.
        /// </summary>
        CornflowerBlue2 = 69,
        /// <summary>
        /// Hex value: <c>#5FAF00</c>.
        /// </summary>
        OliveDrab = 70,
        /// <summary>
        /// Hex value: <c>#5FAF5F</c>.
        /// </summary>
        MediumSeaGreen = 71,
        /// <summary>
        /// Hex value: <c>#5FAF87</c>.
        /// </summary>
        Mint2 = 72,
        /// <summary>
        /// Hex value: <c>#5FAFAF</c>.
        /// </summary>
        SoftTeal = 73,
        /// <summary>
        /// Hex value: <c>#5FAFD7</c>.
        /// </summary>
        CornflowerBlue3 = 74,
        /// <summary>
        /// Hex value: <c>#5FAFFF</c>.
        /// </summary>
        CornflowerBlue4 = 75,
        /// <summary>
        /// Hex value: <c>#5FD700</c>.
        /// </summary>
        LawnGreen = 76,
        /// <summary>
        /// Hex value: <c>#5FD75F</c>.
        /// </summary>
        MediumSeaGreen2 = 77,
        /// <summary>
        /// Hex value: <c>#5FD787</c>.
        /// </summary>
        MediumSeaGreen3 = 78,
        /// <summary>
        /// Hex value: <c>#5FD7AF</c>.
        /// </summary>
        MediumTurquoise = 79,
        /// <summary>
        /// Hex value: <c>#5FD7D7</c>.
        /// </summary>
        MediumTurquoise2 = 80,
        /// <summary>
        /// Hex value: <c>#5FD7FF</c>.
        /// </summary>
        LightSkyBlue = 81,
        /// <summary>
        /// Hex value: <c>#5FFF00</c>.
        /// </summary>
        LawnGreen2 = 82,
        /// <summary>
        /// Hex value: <c>#5FFF5F</c>.
        /// </summary>
        LightGreen = 83,
        /// <summary>
        /// Hex value: <c>#5FFF87</c>.
        /// </summary>
        LightGreen2 = 84,
        /// <summary>
        /// Hex value: <c>#5FFFAF</c>.
        /// </summary>
        Aquamarine = 85,
        /// <summary>
        /// Hex value: <c>#5FFFD7</c>.
        /// </summary>
        Aquamarine2 = 86,
        /// <summary>
        /// Hex value: <c>#5FFFFF</c>.
        /// </summary>
        PaleTurquoise3 = 87,
        /// <summary>
        /// Hex value: <c>#870000</c>.
        /// </summary>
        Maroon2 = 88,
        /// <summary>
        /// Hex value: <c>#87005F</c>.
        /// </summary>
        Purple = 89,
        /// <summary>
        /// Hex value: <c>#870087</c>.
        /// </summary>
        Purple2 = 90,
        /// <summary>
        /// Hex value: <c>#8700AF</c>.
        /// </summary>
        DarkViolet2 = 91,
        /// <summary>
        /// Hex value: <c>#8700D7</c>.
        /// </summary>
        DarkViolet = 92,
        /// <summary>
        /// Hex value: <c>#8700FF</c>.
        /// </summary>
        DarkViolet3 = 93,
        /// <summary>
        /// Hex value: <c>#875F00</c>.
        /// </summary>
        SaddleBrown = 94,
        /// <summary>
        /// Hex value: <c>#875F5F</c>.
        /// </summary>
        OldRose = 95,
        /// <summary>
        /// Hex value: <c>#875F87</c>.
        /// </summary>
        Mauve = 96,
        /// <summary>
        /// Hex value: <c>#875FAF</c>.
        /// </summary>
        SlateBlue3 = 97,
        /// <summary>
        /// Hex value: <c>#875FD7</c>.
        /// </summary>
        MediumPurple = 98,
        /// <summary>
        /// Hex value: <c>#875FFF</c>.
        /// </summary>
        MediumSlateBlue2 = 99,
        /// <summary>
        /// Hex value: <c>#878700</c>.
        /// </summary>
        Olive3 = 100,
        /// <summary>
        /// Hex value: <c>#87875F</c>.
        /// </summary>
        OliveGray = 101,
        /// <summary>
        /// Hex value: <c>#878787</c>.
        /// </summary>
        Gray53 = 102,
        /// <summary>
        /// Hex value: <c>#8787AF</c>.
        /// </summary>
        LightSlateGray = 103,
        /// <summary>
        /// Hex value: <c>#8787D7</c>.
        /// </summary>
        MediumPurple2 = 104,
        /// <summary>
        /// Hex value: <c>#8787FF</c>.
        /// </summary>
        MediumSlateBlue3 = 105,
        /// <summary>
        /// Hex value: <c>#87AF00</c>.
        /// </summary>
        Olive4 = 106,
        /// <summary>
        /// Hex value: <c>#87AF5F</c>.
        /// </summary>
        DarkSeaGreen = 107,
        /// <summary>
        /// Hex value: <c>#87AF87</c>.
        /// </summary>
        DarkSeaGreen2 = 108,
        /// <summary>
        /// Hex value: <c>#87AFAF</c>.
        /// </summary>
        SeafoamGray = 109,
        /// <summary>
        /// Hex value: <c>#87AFD7</c>.
        /// </summary>
        SkyBlue = 110,
        /// <summary>
        /// Hex value: <c>#87AFFF</c>.
        /// </summary>
        LightSkyBlue2 = 111,
        /// <summary>
        /// Hex value: <c>#87D700</c>.
        /// </summary>
        LawnGreen3 = 112,
        /// <summary>
        /// Hex value: <c>#87D75F</c>.
        /// </summary>
        YellowGreen2 = 113,
        /// <summary>
        /// Hex value: <c>#87D787</c>.
        /// </summary>
        LightGreen3 = 114,
        /// <summary>
        /// Hex value: <c>#87D7AF</c>.
        /// </summary>
        Mint = 115,
        /// <summary>
        /// Hex value: <c>#87D7D7</c>.
        /// </summary>
        SkyBlue2 = 116,
        /// <summary>
        /// Hex value: <c>#87D7FF</c>.
        /// </summary>
        LightSkyBlue3 = 117,
        /// <summary>
        /// Hex value: <c>#87FF00</c>.
        /// </summary>
        Chartreuse = 118,
        /// <summary>
        /// Hex value: <c>#87FF5F</c>.
        /// </summary>
        LightGreen4 = 119,
        /// <summary>
        /// Hex value: <c>#87FF87</c>.
        /// </summary>
        LightGreen5 = 120,
        /// <summary>
        /// Hex value: <c>#87FFAF</c>.
        /// </summary>
        PaleGreen = 121,
        /// <summary>
        /// Hex value: <c>#87FFD7</c>.
        /// </summary>
        Aquamarine3 = 122,
        /// <summary>
        /// Hex value: <c>#87FFFF</c>.
        /// </summary>
        PaleTurquoise4 = 123,
        /// <summary>
        /// Hex value: <c>#AF0000</c>.
        /// </summary>
        Maroon3 = 124,
        /// <summary>
        /// Hex value: <c>#AF005F</c>.
        /// </summary>
        Purple3 = 125,
        /// <summary>
        /// Hex value: <c>#AF0087</c>.
        /// </summary>
        Purple4 = 126,
        /// <summary>
        /// Hex value: <c>#AF00AF</c>.
        /// </summary>
        DarkOrchid = 127,
        /// <summary>
        /// Hex value: <c>#AF00D7</c>.
        /// </summary>
        DarkViolet4 = 128,
        /// <summary>
        /// Hex value: <c>#AF00FF</c>.
        /// </summary>
        DarkViolet5 = 129,
        /// <summary>
        /// Hex value: <c>#AF5F00</c>.
        /// </summary>
        DarkGoldenrod = 130,
        /// <summary>
        /// Hex value: <c>#AF5F5F</c>.
        /// </summary>
        IndianRed = 131,
        /// <summary>
        /// Hex value: <c>#AF5F87</c>.
        /// </summary>
        Rose = 132,
        /// <summary>
        /// Hex value: <c>#AF5FAF</c>.
        /// </summary>
        MediumOrchid = 133,
        /// <summary>
        /// Hex value: <c>#AF5FD7</c>.
        /// </summary>
        MediumOrchid2 = 134,
        /// <summary>
        /// Hex value: <c>#AF5FFF</c>.
        /// </summary>
        MediumOrchid3 = 135,
        /// <summary>
        /// Hex value: <c>#AF8700</c>.
        /// </summary>
        DarkGoldenrod2 = 136,
        /// <summary>
        /// Hex value: <c>#AF875F</c>.
        /// </summary>
        Peru = 137,
        /// <summary>
        /// Hex value: <c>#AF8787</c>.
        /// </summary>
        RosyBrown = 138,
        /// <summary>
        /// Hex value: <c>#AF87AF</c>.
        /// </summary>
        RosyBrown2 = 139,
        /// <summary>
        /// Hex value: <c>#AF87D7</c>.
        /// </summary>
        MediumPurple3 = 140,
        /// <summary>
        /// Hex value: <c>#AF87FF</c>.
        /// </summary>
        MediumPurple4 = 141,
        /// <summary>
        /// Hex value: <c>#AFAF00</c>.
        /// </summary>
        DarkGoldenrod3 = 142,
        /// <summary>
        /// Hex value: <c>#AFAF5F</c>.
        /// </summary>
        DarkKhaki = 143,
        /// <summary>
        /// Hex value: <c>#AFAF87</c>.
        /// </summary>
        DarkKhaki2 = 144,
        /// <summary>
        /// Hex value: <c>#AFAFAF</c>.
        /// </summary>
        Gray69 = 145,
        /// <summary>
        /// Hex value: <c>#AFAFD7</c>.
        /// </summary>
        LightSteelBlue = 146,
        /// <summary>
        /// Hex value: <c>#AFAFFF</c>.
        /// </summary>
        LightSteelBlue2 = 147,
        /// <summary>
        /// Hex value: <c>#AFD700</c>.
        /// </summary>
        YellowGreen = 148,
        /// <summary>
        /// Hex value: <c>#AFD75F</c>.
        /// </summary>
        DarkKhaki3 = 149,
        /// <summary>
        /// Hex value: <c>#AFD787</c>.
        /// </summary>
        YellowGreen3 = 150,
        /// <summary>
        /// Hex value: <c>#AFD7AF</c>.
        /// </summary>
        PaleGreen4 = 151,
        /// <summary>
        /// Hex value: <c>#AFD7D7</c>.
        /// </summary>
        LightBlue = 152,
        /// <summary>
        /// Hex value: <c>#AFD7FF</c>.
        /// </summary>
        LightBlue2 = 153,
        /// <summary>
        /// Hex value: <c>#AFFF00</c>.
        /// </summary>
        GreenYellow = 154,
        /// <summary>
        /// Hex value: <c>#AFFF5F</c>.
        /// </summary>
        GreenYellow2 = 155,
        /// <summary>
        /// Hex value: <c>#AFFF87</c>.
        /// </summary>
        PaleGreen2 = 156,
        /// <summary>
        /// Hex value: <c>#AFFFAF</c>.
        /// </summary>
        PaleGreen3 = 157,
        /// <summary>
        /// Hex value: <c>#AFFFD7</c>.
        /// </summary>
        PaleTurquoise = 158,
        /// <summary>
        /// Hex value: <c>#AFFFFF</c>.
        /// </summary>
        PaleTurquoise2 = 159,
        /// <summary>
        /// Hex value: <c>#D70000</c>.
        /// </summary>
        Red2 = 160,
        /// <summary>
        /// Hex value: <c>#D7005F</c>.
        /// </summary>
        Crimson = 161,
        /// <summary>
        /// Hex value: <c>#D70087</c>.
        /// </summary>
        DeepPink = 162,
        /// <summary>
        /// Hex value: <c>#D700AF</c>.
        /// </summary>
        DeepPink2 = 163,
        /// <summary>
        /// Hex value: <c>#D700D7</c>.
        /// </summary>
        Magenta4 = 164,
        /// <summary>
        /// Hex value: <c>#D700FF</c>.
        /// </summary>
        Magenta3 = 165,
        /// <summary>
        /// Hex value: <c>#D75F00</c>.
        /// </summary>
        Chocolate = 166,
        /// <summary>
        /// Hex value: <c>#D75F5F</c>.
        /// </summary>
        IndianRed2 = 167,
        /// <summary>
        /// Hex value: <c>#D75F87</c>.
        /// </summary>
        Rose2 = 168,
        /// <summary>
        /// Hex value: <c>#D75FAF</c>.
        /// </summary>
        Mauve2 = 169,
        /// <summary>
        /// Hex value: <c>#D75FD7</c>.
        /// </summary>
        Orchid = 170,
        /// <summary>
        /// Hex value: <c>#D75FFF</c>.
        /// </summary>
        Orchid2 = 171,
        /// <summary>
        /// Hex value: <c>#D78700</c>.
        /// </summary>
        Orange2 = 172,
        /// <summary>
        /// Hex value: <c>#D7875F</c>.
        /// </summary>
        Peru2 = 173,
        /// <summary>
        /// Hex value: <c>#D78787</c>.
        /// </summary>
        Rose3 = 174,
        /// <summary>
        /// Hex value: <c>#D787AF</c>.
        /// </summary>
        Rose4 = 175,
        /// <summary>
        /// Hex value: <c>#D787D7</c>.
        /// </summary>
        Orchid3 = 176,
        /// <summary>
        /// Hex value: <c>#D787FF</c>.
        /// </summary>
        Violet = 177,
        /// <summary>
        /// Hex value: <c>#D7AF00</c>.
        /// </summary>
        Goldenrod = 178,
        /// <summary>
        /// Hex value: <c>#D7AF5F</c>.
        /// </summary>
        Goldenrod2 = 179,
        /// <summary>
        /// Hex value: <c>#D7AF87</c>.
        /// </summary>
        Tan = 180,
        /// <summary>
        /// Hex value: <c>#D7AFAF</c>.
        /// </summary>
        SoftPink = 181,
        /// <summary>
        /// Hex value: <c>#D7AFD7</c>.
        /// </summary>
        Thistle = 182,
        /// <summary>
        /// Hex value: <c>#D7AFFF</c>.
        /// </summary>
        Plum = 183,
        /// <summary>
        /// Hex value: <c>#D7D700</c>.
        /// </summary>
        Gold = 184,
        /// <summary>
        /// Hex value: <c>#D7D75F</c>.
        /// </summary>
        DarkKhaki4 = 185,
        /// <summary>
        /// Hex value: <c>#D7D787</c>.
        /// </summary>
        Khaki = 186,
        /// <summary>
        /// Hex value: <c>#D7D7AF</c>.
        /// </summary>
        Wheat = 187,
        /// <summary>
        /// Hex value: <c>#D7D7D7</c>.
        /// </summary>
        Gray84 = 188,
        /// <summary>
        /// Hex value: <c>#D7D7FF</c>.
        /// </summary>
        Lavender = 189,
        /// <summary>
        /// Hex value: <c>#D7FF00</c>.
        /// </summary>
        GreenYellow3 = 190,
        /// <summary>
        /// Hex value: <c>#D7FF5F</c>.
        /// </summary>
        GreenYellow4 = 191,
        /// <summary>
        /// Hex value: <c>#D7FF87</c>.
        /// </summary>
        GreenYellow5 = 192,
        /// <summary>
        /// Hex value: <c>#D7FFAF</c>.
        /// </summary>
        Wheat2 = 193,
        /// <summary>
        /// Hex value: <c>#D7FFD7</c>.
        /// </summary>
        Beige = 194,
        /// <summary>
        /// Hex value: <c>#D7FFFF</c>.
        /// </summary>
        Azure = 195,
        /// <summary>
        /// Hex value: <c>#FF0000</c>.
        /// </summary>
        Red1 = 196,
        /// <summary>
        /// Hex value: <c>#FF005F</c>.
        /// </summary>
        Crimson2 = 197,
        /// <summary>
        /// Hex value: <c>#FF0087</c>.
        /// </summary>
        DeepPink3 = 198,
        /// <summary>
        /// Hex value: <c>#FF00AF</c>.
        /// </summary>
        DeepPink4 = 199,
        /// <summary>
        /// Hex value: <c>#FF00D7</c>.
        /// </summary>
        Magenta2 = 200,
        /// <summary>
        /// Hex value: <c>#FF00FF</c>.
        /// </summary>
        Magenta1 = 201,
        /// <summary>
        /// Hex value: <c>#FF5F00</c>.
        /// </summary>
        OrangeRed = 202,
        /// <summary>
        /// Hex value: <c>#FF5F5F</c>.
        /// </summary>
        Tomato = 203,
        /// <summary>
        /// Hex value: <c>#FF5F87</c>.
        /// </summary>
        Salmon = 204,
        /// <summary>
        /// Hex value: <c>#FF5FAF</c>.
        /// </summary>
        HotPink = 205,
        /// <summary>
        /// Hex value: <c>#FF5FD7</c>.
        /// </summary>
        HotPink2 = 206,
        /// <summary>
        /// Hex value: <c>#FF5FFF</c>.
        /// </summary>
        Violet2 = 207,
        /// <summary>
        /// Hex value: <c>#FF8700</c>.
        /// </summary>
        DarkOrange = 208,
        /// <summary>
        /// Hex value: <c>#FF875F</c>.
        /// </summary>
        Coral = 209,
        /// <summary>
        /// Hex value: <c>#FF8787</c>.
        /// </summary>
        Salmon2 = 210,
        /// <summary>
        /// Hex value: <c>#FF87AF</c>.
        /// </summary>
        HotPink3 = 211,
        /// <summary>
        /// Hex value: <c>#FF87D7</c>.
        /// </summary>
        Violet3 = 212,
        /// <summary>
        /// Hex value: <c>#FF87FF</c>.
        /// </summary>
        Violet4 = 213,
        /// <summary>
        /// Hex value: <c>#FFAF00</c>.
        /// </summary>
        Orange = 214,
        /// <summary>
        /// Hex value: <c>#FFAF5F</c>.
        /// </summary>
        SandyBrown = 215,
        /// <summary>
        /// Hex value: <c>#FFAF87</c>.
        /// </summary>
        LightSalmon = 216,
        /// <summary>
        /// Hex value: <c>#FFAFAF</c>.
        /// </summary>
        Pink = 217,
        /// <summary>
        /// Hex value: <c>#FFAFD7</c>.
        /// </summary>
        Pink2 = 218,
        /// <summary>
        /// Hex value: <c>#FFAFFF</c>.
        /// </summary>
        Plum2 = 219,
        /// <summary>
        /// Hex value: <c>#FFD700</c>.
        /// </summary>
        Gold2 = 220,
        /// <summary>
        /// Hex value: <c>#FFD75F</c>.
        /// </summary>
        Sand2 = 221,
        /// <summary>
        /// Hex value: <c>#FFD787</c>.
        /// </summary>
        Sand = 222,
        /// <summary>
        /// Hex value: <c>#FFD7AF</c>.
        /// </summary>
        NavajoWhite = 223,
        /// <summary>
        /// Hex value: <c>#FFD7D7</c>.
        /// </summary>
        MistyRose = 224,
        /// <summary>
        /// Hex value: <c>#FFD7FF</c>.
        /// </summary>
        Lavender2 = 225,
        /// <summary>
        /// Hex value: <c>#FFFF00</c>.
        /// </summary>
        Yellow1 = 226,
        /// <summary>
        /// Hex value: <c>#FFFF5F</c>.
        /// </summary>
        Yellow2 = 227,
        /// <summary>
        /// Hex value: <c>#FFFF87</c>.
        /// </summary>
        Yellow3 = 228,
        /// <summary>
        /// Hex value: <c>#FFFFAF</c>.
        /// </summary>
        PaleYellow = 229,
        /// <summary>
        /// Hex value: <c>#FFFFD7</c>.
        /// </summary>
        LightYellow = 230,
        /// <summary>
        /// Hex value: <c>#FFFFFF</c>.
        /// </summary>
        White1 = 231,

        // ---- 232–255: grayscale ramp ----
        /// <summary>
        /// Hex value: <c>#080808</c>.
        /// </summary>
        Gray3 = 232,
        /// <summary>
        /// Hex value: <c>#121212</c>.
        /// </summary>
        Gray7 = 233,
        /// <summary>
        /// Hex value: <c>#1C1C1C</c>.
        /// </summary>
        Gray11 = 234,
        /// <summary>
        /// Hex value: <c>#262626</c>.
        /// </summary>
        Gray15 = 235,
        /// <summary>
        /// Hex value: <c>#303030</c>.
        /// </summary>
        Gray19 = 236,
        /// <summary>
        /// Hex value: <c>#3A3A3A</c>.
        /// </summary>
        Gray23 = 237,
        /// <summary>
        /// Hex value: <c>#444444</c>.
        /// </summary>
        Gray27 = 238,
        /// <summary>
        /// Hex value: <c>#4E4E4E</c>.
        /// </summary>
        Gray31 = 239,
        /// <summary>
        /// Hex value: <c>#585858</c>.
        /// </summary>
        Gray35 = 240,
        /// <summary>
        /// Hex value: <c>#626262</c>.
        /// </summary>
        Gray38 = 241,
        /// <summary>
        /// Hex value: <c>#6C6C6C</c>.
        /// </summary>
        Gray42 = 242,
        /// <summary>
        /// Hex value: <c>#767676</c>.
        /// </summary>
        Gray46 = 243,
        /// <summary>
        /// Hex value: <c>#808080</c>.
        /// </summary>
        Gray50 = 244,
        /// <summary>
        /// Hex value: <c>#8A8A8A</c>.
        /// </summary>
        Gray54 = 245,
        /// <summary>
        /// Hex value: <c>#949494</c>.
        /// </summary>
        Gray58 = 246,
        /// <summary>
        /// Hex value: <c>#9E9E9E</c>.
        /// </summary>
        Gray62 = 247,
        /// <summary>
        /// Hex value: <c>#A8A8A8</c>.
        /// </summary>
        Gray66 = 248,
        /// <summary>
        /// Hex value: <c>#B2B2B2</c>.
        /// </summary>
        Gray70 = 249,
        /// <summary>
        /// Hex value: <c>#BCBCBC</c>.
        /// </summary>
        Gray74 = 250,
        /// <summary>
        /// Hex value: <c>#C6C6C6</c>.
        /// </summary>
        Gray78 = 251,
        /// <summary>
        /// Hex value: <c>#D0D0D0</c>.
        /// </summary>
        Gray82 = 252,
        /// <summary>
        /// Hex value: <c>#DADADA</c>.
        /// </summary>
        Gray85 = 253,
        /// <summary>
        /// Hex value: <c>#E4E4E4</c>.
        /// </summary>
        Gray89 = 254,
        /// <summary>
        /// Hex value: <c>#EEEEEE</c>.
        /// </summary>
        Gray93 = 255
    }
}
