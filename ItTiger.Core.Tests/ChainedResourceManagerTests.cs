using System.Globalization;
using System.Resources;
using ItTiger.Core.Resources;

namespace ItTiger.Core.Tests;

public sealed class ChainedResourceManagerTests
{
    [Fact]
    public void GetString_ReturnsValueFromFirstManagerWhenPresent()
    {
        var first = new StubResourceManager((_, _) => "app");
        var later = new StubResourceManager((_, _) => "library");
        var manager = new ChainedResourceManager(first, later);

        Assert.Equal("app", manager.GetString("Title"));
        Assert.Equal(0, later.CallCount);
    }

    [Fact]
    public void GetString_FallsBackToLaterManagerWhenFirstMisses()
    {
        var manager = new ChainedResourceManager(
            new StubResourceManager((_, _) => null),
            new StubResourceManager((_, _) => "library"));

        Assert.Equal("library", manager.GetString("Title"));
    }

    [Fact]
    public void GetString_FirstManagerOverridesLaterManager()
    {
        var manager = new ChainedResourceManager(
            new StubResourceManager((_, _) => "application title"),
            new StubResourceManager((_, _) => "library title"));

        Assert.Equal("application title", manager.GetString("Title"));
    }

    [Fact]
    public void GetString_TreatsEmptyStringAsMissing()
    {
        var manager = new ChainedResourceManager(
            new StubResourceManager((_, _) => string.Empty),
            new StubResourceManager((_, _) => "fallback"));

        Assert.Equal("fallback", manager.GetString("Title"));
    }

    [Fact]
    public void GetString_ReturnsNullWhenKeyIsMissingFromAllManagers()
    {
        var manager = new ChainedResourceManager(
            new StubResourceManager((_, _) => null),
            new StubResourceManager((_, _) => null));

        Assert.Null(manager.GetString("Missing"));
    }

    [Fact]
    public void Constructor_ThrowsWhenParamsArrayIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ChainedResourceManager(null!));
    }

    [Fact]
    public void Constructor_IgnoresNullEntries()
    {
        var manager = new ChainedResourceManager(
            null!,
            new StubResourceManager((_, _) => "value"));

        Assert.Equal("value", manager.GetString("Title"));
    }

    [Fact]
    public void GetString_SkipsManagerThatThrowsMissingManifestResourceException()
    {
        var manager = new ChainedResourceManager(
            new StubResourceManager((_, _) => throw new MissingManifestResourceException()),
            new StubResourceManager((_, _) => "fallback"));

        Assert.Equal("fallback", manager.GetString("Title"));
    }

    [Fact]
    public void GetString_PassesCultureThroughToManagers()
    {
        var culture = CultureInfo.GetCultureInfo("pl-PL");
        var first = new StubResourceManager((_, _) => null);
        var second = new StubResourceManager((_, _) => "wartosc");
        var manager = new ChainedResourceManager(first, second);

        Assert.Equal("wartosc", manager.GetString("Title", culture));
        Assert.Same(culture, first.LastCulture);
        Assert.Same(culture, second.LastCulture);
    }

    [Fact]
    public void GetStringWithoutCulture_DelegatesWithNullCulture()
    {
        var resourceManager = new StubResourceManager((_, culture) => culture is null ? "value" : null);
        var manager = new ChainedResourceManager(resourceManager);

        Assert.Equal("value", manager.GetString("Title"));
        Assert.Null(resourceManager.LastCulture);
    }

    private sealed class StubResourceManager(
        Func<string, CultureInfo?, string?> getString) : ResourceManager
    {
        public int CallCount { get; private set; }

        public CultureInfo? LastCulture { get; private set; }

        public override string? GetString(string name, CultureInfo? culture)
        {
            CallCount++;
            LastCulture = culture;
            return getString(name, culture);
        }
    }
}
