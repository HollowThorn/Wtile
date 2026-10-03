using System.Net;
using System.Security;
using System.Text.RegularExpressions;

namespace Wtile.Core;

public static partial class StartupTaskXml
{
    public static string Build(string userId, string exePath) => $"""
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <RegistrationInfo>
            <Description>Starts Wtile elevated at logon so it can tile Administrator windows.</Description>
          </RegistrationInfo>
          <Triggers>
            <LogonTrigger>
              <UserId>{SecurityElement.Escape(userId)}</UserId>
            </LogonTrigger>
          </Triggers>
          <Principals>
            <Principal id="Author">
              <UserId>{SecurityElement.Escape(userId)}</UserId>
              <LogonType>InteractiveToken</LogonType>
              <RunLevel>HighestAvailable</RunLevel>
            </Principal>
          </Principals>
          <Settings>
            <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
            <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
            <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
            <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
            <Priority>4</Priority>
            <StartWhenAvailable>true</StartWhenAvailable>
          </Settings>
          <Actions Context="Author">
            <Exec>
              <Command>{SecurityElement.Escape(exePath)}</Command>
              <WorkingDirectory>{SecurityElement.Escape(Path.GetDirectoryName(exePath) ?? "")}</WorkingDirectory>
            </Exec>
          </Actions>
        </Task>
        """;

    public static string? ReadCommand(string taskXml)
    {
        Match match = CommandElement().Match(taskXml);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value).Trim().Trim('"') : null;
    }

    [GeneratedRegex(@"<Command>(.*?)</Command>", RegexOptions.Singleline)]
    private static partial Regex CommandElement();
}
