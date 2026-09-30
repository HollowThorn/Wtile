using Wtile.Core;

namespace Wtile.Tests.Core;

public class ByteRateFormatterTests
{
    [Theory]
    [InlineData(0, "0B/s")]
    [InlineData(512, "512B/s")]
    [InlineData(1023, "1023B/s")]
    [InlineData(1024, "1.0KB/s")]
    [InlineData(1536, "1.5KB/s")]
    [InlineData(1048576, "1.0MB/s")]
    [InlineData(1572864, "1.5MB/s")]
    [InlineData(1073741824, "1.0GB/s")]
    public void Format_MatchesExpected(double bytesPerSecond, string expected)
    {
        Assert.Equal(expected, ByteRateFormatter.Format(bytesPerSecond));
    }

    [Fact]
    public void Format_NegativeClampsToZero()
    {
        Assert.Equal("0B/s", ByteRateFormatter.Format(-100));
    }
}
