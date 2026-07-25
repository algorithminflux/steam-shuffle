using System.Net;
using SteamShuffle.Services;
using SteamShuffle.Tests.TestHelpers;
using Xunit;

namespace SteamShuffle.Tests.Services;

public class SteamWishlistServiceTests
{
    [Fact]
    public async Task GetWishlistAsync_SinglePage_ParsesAppIdsAndNames()
    {
        const string page0 = """
                             {
                               "440": { "name": "Team Fortress 2" },
                               "730": { "name": "Counter-Strike 2" }
                             }
                             """;

        int callCount = 0;
        var client = FakeHttpMessageHandler.BuildClient(req =>
        {
            callCount++;
            var body = req.RequestUri!.Query.Contains("p=0") ? page0 : "[]";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        });

        var service = new SteamWishlistService(client);
        var wishlist = await service.GetWishlistAsync("76561198000000000");

        Assert.Equal(2, wishlist.Count);
        Assert.All(wishlist, g => Assert.True(g.IsWishlisted));
        Assert.All(wishlist, g => Assert.False(g.IsOwned));
        Assert.Contains(wishlist, g => g.AppId == 440 && g.Name == "Team Fortress 2");
        Assert.Contains(wishlist, g => g.AppId == 730 && g.Name == "Counter-Strike 2");
        Assert.True(callCount >= 2, "Should have paged until receiving an empty page.");
    }

    [Fact]
    public async Task GetWishlistAsync_MultiplePages_AggregatesAcrossPages()
    {
        const string page0 = """{ "1": { "name": "Game One" } }""";
        const string page1 = """{ "2": { "name": "Game Two" } }""";

        var client = FakeHttpMessageHandler.BuildClient(req =>
        {
            var query = req.RequestUri!.Query;
            string body = query.Contains("p=0") ? page0 : query.Contains("p=1") ? page1 : "[]";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        });

        var service = new SteamWishlistService(client);
        var wishlist = await service.GetWishlistAsync("76561198000000000");

        Assert.Equal(2, wishlist.Count);
        Assert.Contains(wishlist, g => g.AppId == 1);
        Assert.Contains(wishlist, g => g.AppId == 2);
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
        var client = new HttpClient(FakeHttpMessageHandler.Always("[]"));
        var service = new SteamWishlistService(client);

        var wishlist = await service.GetWishlistAsync("76561198000000000");

        Assert.Empty(wishlist);
    }
}