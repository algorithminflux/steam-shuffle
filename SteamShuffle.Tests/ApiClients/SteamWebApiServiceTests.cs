using System.Net;
using SteamShuffle.ApiClients;
using SteamShuffle.Tests.TestHelpers;
using Xunit;

namespace SteamShuffle.Tests.ApiClients;

public class SteamWebApiServiceTests
{
    [Fact]
    public async Task GetOwnedGamesAsync_ParsesNamesPlaytimeAndLastPlayed()
    {
        const string json = """
                            {
                              "response": {
                                "game_count": 2,
                                "games": [
                                  { "appid": 570, "name": "Dota 2", "playtime_forever": 6000, "rtime_last_played": 1750000000 },
                                  { "appid": 620, "name": "Portal 2", "playtime_forever": 0, "rtime_last_played": 0 }
                                ]
                              }
                            }
                            """;

        var client = FakeHttpMessageHandler.BuildClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        var service = new SteamWebApiService(client, "fake-key");

        var games = await service.GetOwnedGamesAsync("76561198000000000");

        Assert.Equal(2, games.Count);

        var dota = games.Find(g => g.AppId == 570)!;
        Assert.Equal("Dota 2", dota.Name);
        Assert.Equal(6000, dota.PlaytimeForeverMinutes);
        Assert.True(dota.IsOwned);
        Assert.NotNull(dota.LastPlayed);
        Assert.Equal(1750000000, dota.LastPlayed!.Value.ToUnixTimeSeconds());

        var portal = games.Find(g => g.AppId == 620)!;
        Assert.Null(portal.LastPlayed); // rtime_last_played of 0 should not become a real timestamp
    }

    [Fact]
    public async Task GetOwnedGamesAsync_EmptyResponse_ReturnsEmptyList()
    {
        const string json = """{ "response": {} }""";
        var client = FakeHttpMessageHandler.BuildClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        var service = new SteamWebApiService(client, "fake-key");

        var games = await service.GetOwnedGamesAsync("76561198000000000");

        Assert.Empty(games);
    }

    [Fact]
    public async Task GetOwnedGamesAsync_NonSuccessStatus_ThrowsWithHelpfulMessage()
    {
        var client = FakeHttpMessageHandler.BuildClient(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var service = new SteamWebApiService(client, "bad-key");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetOwnedGamesAsync("76561198000000000"));

        Assert.Contains("API key", ex.Message);
    }
}