using System.Net;
using NSubstitute;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service.Matching;
using UpgradedCrawler.Tests.Infrastructure;
using Xunit;

namespace UpgradedCrawler.Tests.Matching;

public class DescriptionFetcherTests
{
    [Fact]
    public async Task ExtractsTextFromArticleTag()
    {
        const string html = "<html><body><article>This is the job description content here.</article></body></html>";
        var handler = new FakeHttpMessageHandler(html);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

        var fetcher = new DescriptionFetcher(factory, Substitute.For<ILogging>());
        var result = await fetcher.FetchAsync("https://example.com/job/123");

        Assert.Contains("job description content", result);
        Assert.DoesNotContain("<article>", result);
    }

    [Fact]
    public async Task HttpError_ReturnsEmptyStringAndLogs()
    {
        var handler = new FakeHttpMessageHandler("", HttpStatusCode.InternalServerError);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));
        var logging = Substitute.For<ILogging>();

        var fetcher = new DescriptionFetcher(factory, logging);
        var result = await fetcher.FetchAsync("https://example.com/job/123");

        Assert.Equal(string.Empty, result);
        logging.Received().Log(Arg.Any<string>());
    }

    [Fact]
    public async Task TruncatesLongContent()
    {
        var longText = new string('x', 5000);
        var html = $"<html><body><article>{longText}</article></body></html>";
        var handler = new FakeHttpMessageHandler(html);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

        var fetcher = new DescriptionFetcher(factory, Substitute.For<ILogging>());
        var result = await fetcher.FetchAsync("https://example.com/job/123");

        Assert.True(result.Length <= 3000);
    }
}
