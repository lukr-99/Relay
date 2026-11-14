namespace Relay.Agent.Tests;

/// <summary>The About page's constants stay usable: real links, complete credits, a sane monogram.</summary>
public class AboutLinksTests
{
    [Theory]
    [InlineData("Lukáš Krejčí", "LK")]
    [InlineData("Ada", "A")]
    [InlineData("  ", "")]
    [InlineData("ada king lovelace", "AL")]
    public void Monogram_UsesFirstAndLastWord(string name, string expected) =>
        Assert.Equal(expected, AboutLinks.Monogram(name));

    [Fact]
    public void Links_AreHttpsAndPointAtTheRightPlaces()
    {
        Assert.Equal("https://www.instagram.com/lukas9krejci/", AboutLinks.InstagramUrl);
        Assert.Equal("https://github.com/lukr-99", AboutLinks.GitHubUrl);
        Assert.Equal("https://github.com/lukr-99/Relay/issues", AboutLinks.IssuesUrl);
        foreach (var url in new[] { AboutLinks.InstagramUrl, AboutLinks.GitHubUrl, AboutLinks.RepoUrl, AboutLinks.IssuesUrl, AboutLinks.LicenseUrl })
        {
            Assert.True(Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps, url);
        }
    }

    [Fact]
    public void Credits_AreCompleteAndUnique()
    {
        Assert.NotEmpty(AboutLinks.Credits);
        foreach (var c in AboutLinks.Credits)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Name));
            Assert.False(string.IsNullOrWhiteSpace(c.Use));
            Assert.False(string.IsNullOrWhiteSpace(c.License));
            Assert.StartsWith("https://", c.Url);
        }
        Assert.Equal(AboutLinks.Credits.Count, AboutLinks.Credits.Select(c => c.Name).Distinct().Count());
    }

    [Fact]
    public void Version_IsNotEmpty() => Assert.False(string.IsNullOrWhiteSpace(AppInfo.Version));
}
