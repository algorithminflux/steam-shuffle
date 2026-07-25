using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using SteamShuffle.Services;
using SteamShuffle.Tests.TestHelpers;
using Xunit;

namespace SteamShuffle.Tests.Services
{
    public class SteamStoreServiceTests
    {
        [Fact]
        public async Task GetAppDetailsAsync_PaidGame_ParsesPriceInDollarsFromCents()
        {
            const string json = """
                {
                  "440": {
                    "success": true,
                    "data": {
                      "header_image": "https://example.com/440.jpg",
                      "is_free": false,
                      "price_overview": { "currency": "CAD", "final": 1999 },
                      "genres": [ { "description": "Action" } ],
                      "categories": [ { "description": "Multi-player" }, { "description": "Co-op" } ]
                    }
                  }
                }
                """;

            var client = FakeHttpMessageHandler.BuildClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
            var service = new SteamStoreService(client, "ca");

            var details = await service.GetAppDetailsAsync(440);

            Assert.NotNull(details);
            Assert.Equal(19.99m, details!.PriceCad);
            Assert.False(details.IsFree);
            Assert.Equal("https://example.com/440.jpg", details.HeaderImageUrl);
            Assert.Contains("Action", details.Genres);
            Assert.Equal(new[] { "Multi-player", "Co-op" }, details.Tags);
        }

        [Fact]
        public async Task GetAppDetailsAsync_FreeGame_DoesNotSetPriceEvenIfOverviewPresent()
        {
            const string json = """
                {
                  "570": {
                    "success": true,
                    "data": { "is_free": true, "genres": [], "categories": [] }
                  }
                }
                """;

            var client = FakeHttpMessageHandler.BuildClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
            var service = new SteamStoreService(client, "ca");

            var details = await service.GetAppDetailsAsync(570);

            Assert.NotNull(details);
            Assert.True(details!.IsFree);
            Assert.Null(details.PriceCad);
        }

        [Fact]
        public async Task GetAppDetailsAsync_UnsuccessfulLookup_ReturnsNull()
        {
            const string json = """{ "999": { "success": false } }""";
            var client = FakeHttpMessageHandler.BuildClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
            var service = new SteamStoreService(client, "ca");

            var details = await service.GetAppDetailsAsync(999);

            Assert.Null(details);
        }

        [Fact]
        public async Task GetAppDetailsAsync_NonSuccessHttpStatus_ReturnsNull()
        {
            var client = FakeHttpMessageHandler.BuildClient(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
            var service = new SteamStoreService(client, "ca");

            var details = await service.GetAppDetailsAsync(1);

            Assert.Null(details);
        }
    }
}
