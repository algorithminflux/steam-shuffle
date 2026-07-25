using System.Net;
using SteamShuffle.ApiClients;
using SteamShuffle.Tests.TestHelpers;
using Xunit;

namespace SteamShuffle.Tests.ApiClients;

public class SteamWishlistServiceTests
{
    [Fact]
    public async Task GetWishlistAsync_ParsesAppIds()
    {
        const string json = """
                            {
                              "response": {
                                "items": [
                                  { "appid": 440, "priority": 0, "date_added": 1700000000 },
                                  { "appid": 730, "priority": 1, "date_added": 1700000001 }
                                ]
                              }
                            }
                            """;

        var client = FakeHttpMessageHandler.BuildClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        var service = new SteamWishlistService(client);

        var wishlist = await service.GetWishlistAsync("76561198000000000");

        Assert.Equal(2, wishlist.Count);
        Assert.All(wishlist, g => Assert.True(g.IsWishlisted));
        Assert.All(wishlist, g => Assert.False(g.IsOwned));
        Assert.Contains(wishlist, g => g.AppId == 440);
        Assert.Contains(wishlist, g => g.AppId == 730);
    }

    [Fact]
    public async Task GetWishlistAsync_PrivateProfile_ThrowsHelpfulError()
    {
        var client = FakeHttpMessageHandler.BuildClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var service = new SteamWishlistService(client);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetWishlistAsync("76561198000000000"));

        Assert.Contains("Public", ex.Message);
    }

    [Fact]
    public async Task GetWishlistAsync_EmptyWishlist_ReturnsEmptyList()
    {
        const string json = """{ "response": {} }""";
        var client = new HttpClient(FakeHttpMessageHandler.Always(json));
        var service = new SteamWishlistService(client);

        var wishlist = await service.GetWishlistAsync("76561198000000000");

        Assert.Empty(wishlist);
    }
}
