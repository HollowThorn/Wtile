using Wtile.Core;

namespace Wtile.Tests.Core;

public class SegmentFormatTests
{
    [Fact]
    public void Apply_ValidFormat_SubstitutesArgs()
    {
        string result = SegmentFormat.Apply("CPU {0}%", "CPU {0}%", 42);
        Assert.Equal("CPU 42%", result);
    }

    [Fact]
    public void Apply_MultipleArgs_SubstitutesAllPositions()
    {
        string result = SegmentFormat.Apply("down {0} up {1}", "{0}/{1}", "1.2M", "512K");
        Assert.Equal("down 1.2M up 512K", result);
    }

    [Fact]
    public void Apply_MalformedFormat_FallsBackInsteadOfThrowing()
    {
        // {5} is out of range for a single arg -- string.Format would throw FormatException.
        string result = SegmentFormat.Apply("CPU {5}%", "CPU {0}%", 42);
        Assert.Equal("CPU 42%", result);
    }

    [Fact]
    public void Apply_UnterminatedBrace_FallsBack()
    {
        string result = SegmentFormat.Apply("CPU {0", "CPU {0}%", 42);
        Assert.Equal("CPU 42%", result);
    }
}
