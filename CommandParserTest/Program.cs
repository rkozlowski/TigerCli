namespace CommandParserTest;

static class CommandParserTest
{
    private static async Task<int> Main(string[] args)
    {
        return await CommandParserTestApp.Create().RunAsync(args);
    }
}
