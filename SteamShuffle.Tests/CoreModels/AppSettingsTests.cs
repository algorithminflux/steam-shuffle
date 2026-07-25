using SteamShuffle.CoreModels;
using Xunit;

namespace SteamShuffle.Tests.CoreModels;

public class AppSettingsTests
{
    [Fact]
    public void IsConfigured_WhenKeyAndIdPresent_IsTrue()
    {
        var settings = new AppSettings { SteamApiKey = "abc123", SteamId64 = "76561198000000000" };
        Assert.True(settings.IsConfigured);
    }

    [Theory]
    [InlineData("", "76561198000000000")]
    [InlineData("abc123", "")]
    [InlineData("", "")]
    [InlineData("   ", "76561198000000000")]
    public void IsConfigured_WhenKeyOrIdMissing_IsFalse(string apiKey, string steamId)
    {
        var settings = new AppSettings { SteamApiKey = apiKey, SteamId64 = steamId };
        Assert.False(settings.IsConfigured);
    }

    [Fact]
    public void CountryCode_DefaultsToCa()
    {
        var settings = new AppSettings();
        Assert.Equal("ca", settings.CountryCode);
    }
}