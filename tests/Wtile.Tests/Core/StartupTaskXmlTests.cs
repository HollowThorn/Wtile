using Wtile.Core;

namespace Wtile.Tests.Core;

public class StartupTaskXmlTests
{
    private const string ExePath = @"C:\Users\someone\AppData\Local\Programs\Wtile\Wtile.exe";

    [Fact]
    public void Build_RunsElevatedAtLogonAtNormalPriorityWithoutTimeLimitOrBatteryStop()
    {
        string xml = StartupTaskXml.Build(@"PC\someone", ExePath);

        Assert.Contains("<LogonTrigger>", xml);
        Assert.Contains("<RunLevel>HighestAvailable</RunLevel>", xml);
        Assert.Contains("<ExecutionTimeLimit>PT0S</ExecutionTimeLimit>", xml);
        Assert.Contains("<Priority>4</Priority>", xml);
        Assert.Contains("<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>", xml);
        Assert.Contains("<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>", xml);
        Assert.Contains($"<WorkingDirectory>{Path.GetDirectoryName(ExePath)}</WorkingDirectory>", xml);
    }

    [Fact]
    public void Build_ThenReadCommand_RoundTripsThePath()
    {
        Assert.Equal(ExePath, StartupTaskXml.ReadCommand(StartupTaskXml.Build(@"PC\someone", ExePath)));
    }

    [Fact]
    public void Build_EscapesXmlSpecialCharacters()
    {
        const string oddPath = @"C:\Tools & Stuff\<Wtile>\Wtile.exe";

        string xml = StartupTaskXml.Build(@"PC\someone", oddPath);

        Assert.DoesNotContain("Tools & Stuff", xml);
        Assert.Equal(oddPath, StartupTaskXml.ReadCommand(xml));
    }

    [Fact]
    public void ReadCommand_HandlesQuotedPathsAndMissingCommand()
    {
        Assert.Equal(ExePath, StartupTaskXml.ReadCommand($"<Exec><Command>\"{ExePath}\"</Command></Exec>"));
        Assert.Null(StartupTaskXml.ReadCommand("<Task><Actions /></Task>"));
    }
}
