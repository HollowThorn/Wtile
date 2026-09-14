using Wtile.Launcher;

namespace Wtile.Tests.Launcher;

public class FuzzyMatcherTests
{
    [Theory]
    [InlineData("ff", "firefox.exe")]
    [InlineData("ffx", "firefox.exe")]
    [InlineData("FF", "firefox.exe")] // case-insensitive
    [InlineData("fire", "FireAlpaca.exe")] // case-insensitive candidate
    [InlineData("wt", "wt.exe")]
    [InlineData("", "anything.exe")] // empty query matches everything
    public void Score_SubsequenceMatch_ReturnsNonNull(string query, string candidate)
    {
        Assert.NotNull(FuzzyMatcher.Score(query, candidate));
    }

    [Theory]
    [InlineData("xyz", "firefox.exe")] // not a subsequence at all
    [InlineData("oxff", "firefox.exe")] // right letters, wrong order
    [InlineData("firefoxpro", "firefox.exe")] // query longer than candidate
    public void Score_NoMatch_ReturnsNull(string query, string candidate)
    {
        Assert.Null(FuzzyMatcher.Score(query, candidate));
    }

    [Fact]
    public void Score_PrefixMatch_RanksAboveScatteredMatch()
    {
        int? prefixScore = FuzzyMatcher.Score("fi", "firefox.exe");
        int? scatteredScore = FuzzyMatcher.Score("fi", "office.exe"); // 'f' then 'i', scattered

        Assert.NotNull(prefixScore);
        Assert.NotNull(scatteredScore);
        Assert.True(prefixScore > scatteredScore);
    }

    [Fact]
    public void Score_ContiguousMatch_RanksAboveNonContiguousMatch()
    {
        int? contiguous = FuzzyMatcher.Score("fire", "firefox.exe");
        int? nonContiguous = FuzzyMatcher.Score("fire", "far infrared explorer.exe");

        Assert.NotNull(contiguous);
        Assert.NotNull(nonContiguous);
        Assert.True(contiguous > nonContiguous);
    }
}
