using System.Net;

namespace SteamShuffle.Tests.TestHelpers;

/// <summary>
/// A minimal fake HttpMessageHandler so services that take an HttpClient can be
/// unit tested without touching the network. Each call to SendAsync is routed
/// through the supplied responder function, which receives the outgoing request
/// and returns the response to hand back.
/// </summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    /// <summary>Convenience factory for a handler that always returns the same body/status.</summary>
    public static FakeHttpMessageHandler Always(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(body) });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_responder(request));
    }

    public static HttpClient BuildClient(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(new FakeHttpMessageHandler(responder));
}