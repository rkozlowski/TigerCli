using ItTiger.Core;

namespace CommandParserTest
{
    [TigerText("Parser test exit codes")]
    public enum ParserTestExitCode
    {
        [TigerText(
            "OK",
            Description = "Operation completed successfully.")]
        Ok = 0,

        [TigerText(
            "Internal error",
            Description = "Internal parser-test failure.")]
        InternalError = 10,

        [TigerText(
            "Invalid arguments",
            Description = "Invalid command-line arguments.")]
        InvalidArguments = 20,

        [TigerText(
            "Missing required argument",
            Description = "A required positional argument was not provided.")]
        MissingRequiredArgument = 22,

        [TigerText(
            "Validation error",
            Description = "Validation failed.")]
        ValidationError = 21,

        [TigerText(
            "Intentional failure",
            Description = "The command intentionally failed.")]
        IntentionalFailure = 30,

        [TigerText(
            "Unhandled exception",
            Description = "Unhandled exception was caught by TigerCli.")]
        UnhandledException = 40,

        [TigerText(
            "Interactive input not allowed",
            Description = "Interactive input is not allowed or prompt input was canceled.")]        InteractiveNotAllowed = 50
    }
}
