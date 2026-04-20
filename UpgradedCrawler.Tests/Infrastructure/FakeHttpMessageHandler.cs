using System.Net;

namespace UpgradedCrawler.Tests.Infrastructure;

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, HttpResponseMessage> _responses;

    public FakeHttpMessageHandler(Dictionary<string, HttpResponseMessage> responses)
    {
        _responses = responses;
    }

    public FakeHttpMessageHandler(string content, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses = new Dictionary<string, HttpResponseMessage>
        {
            ["*"] = new HttpResponseMessage(status) { Content = new StringContent(content) }
        };
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? "";
        if (_responses.TryGetValue(url, out var response))
            return Task.FromResult(response);
        if (_responses.TryGetValue("*", out var fallback))
            return Task.FromResult(fallback);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
