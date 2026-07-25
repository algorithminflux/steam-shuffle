using SteamShuffle.Models;
using Xunit;

namespace SteamShuffle.Tests.Models;

public class SteamGameTests
{
    [Fact]
    public void PlaytimeDisplay_WhenNotOwned_SaysNotOwnedYet()
    {
        var game = new SteamGame { IsOwned = false, IsWishlisted = true, PlaytimeForeverMinutes = 0 };
        Assert.Equal("Not owned yet", game.PlaytimeDisplay);
    }

    [Fact]
    public void PlaytimeDisplay_WhenOwnedButNeverPlayed_SaysNeverPlayed()
    {
        var game = new SteamGame { IsOwned = true, PlaytimeForeverMinutes = 0 };
        Assert.Equal("Never played", game.PlaytimeDisplay);
    }

    [Theory]
    [InlineData(60, "1 hrs")]
    [InlineData(90, "1.5 hrs")]
    [InlineData(600, "10 hrs")]
    public void PlaytimeDisplay_WhenOwnedAndPlayed_FormatsHours(int minutes, string expected)
    {
        var game = new SteamGame { IsOwned = true, PlaytimeForeverMinutes = minutes };
        Assert.Equal(expected, game.PlaytimeDisplay);
    }

    [Fact]
    public void LastPlayedDisplay_WhenNotOwned_SaysNA()
    {
        var game = new SteamGame { IsOwned = false, IsWishlisted = true };
        Assert.Equal("N/A", game.LastPlayedDisplay);
    }

    [Fact]
    public void LastPlayedDisplay_WhenOwnedButNullTimestamp_SaysNever()
    {
        var game = new SteamGame { IsOwned = true, LastPlayed = null };
        Assert.Equal("Never", game.LastPlayedDisplay);
    }

    [Fact]
    public void LastPlayedDisplay_WhenOwnedWithEpochZero_SaysNever()
    {
        // Steam sometimes reports rtime_last_played as 0/epoch for "never" rather than omitting it.
        var game = new SteamGame { IsOwned = true, LastPlayed = DateTimeOffset.FromUnixTimeSeconds(0) };
        Assert.Equal("Never", game.LastPlayedDisplay);
    }

    [Fact]
    public void LastPlayedDisplay_WhenOwnedWithRealTimestamp_FormatsDate()
    {
        var timestamp = new DateTimeOffset(2026, 3, 14, 12, 0, 0, TimeSpan.Zero);
        var game = new SteamGame { IsOwned = true, LastPlayed = timestamp };
        Assert.Equal(timestamp.LocalDateTime.ToString("MMM d, yyyy"), game.LastPlayedDisplay);
    }

    [Fact]
    public void PriceDisplay_WhenFree_SaysFreeToPlay()
    {
        var game = new SteamGame { IsFree = true, PriceCad = 0m };
        Assert.Equal("Free to Play", game.PriceDisplay);
    }

    [Fact]
    public void PriceDisplay_WhenPriceMissing_SaysNA()
    {
        var game = new SteamGame { IsFree = false, PriceCad = null };
        Assert.Equal("N/A", game.PriceDisplay);
    }

    [Fact]
    public void PriceDisplay_WhenPriced_FormatsAsCad()
    {
        var game = new SteamGame { IsFree = false, PriceCad = 19.99m };
        Assert.Equal("$19.99 CAD", game.PriceDisplay);
    }

    [Theory]
    [InlineData(true, false, "Owned")]
    [InlineData(false, true, "Wishlist")]
    [InlineData(true, true, "Owned + Wishlist")]
    public void SourceBadge_ReflectsProvenance(bool isOwned, bool isWishlisted, string expected)
    {
        var game = new SteamGame { IsOwned = isOwned, IsWishlisted = isWishlisted };
        Assert.Equal(expected, game.SourceBadge);
    }

    [Fact]
    public void CapsuleImageUrl_ContainsAppId()
    {
        var game = new SteamGame { AppId = 570 };
        Assert.Contains("/570/", game.CapsuleImageUrl);
    }
}