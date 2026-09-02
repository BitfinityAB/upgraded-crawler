namespace UpgradedCrawler.Tests.Infrastructure;

public static class TestFixtureLoader
{
    public static string Load(string filename)
    {
        var assembly = typeof(TestFixtureLoader).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(filename));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
