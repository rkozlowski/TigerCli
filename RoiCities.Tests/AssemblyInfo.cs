using Xunit;

// TigerCliAppTestHost redirects the process-global Console.Out/Error for the duration of a run,
// so host-driven tests must not interleave (see docs/guides/app-testing.md, Testing Guidance).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
