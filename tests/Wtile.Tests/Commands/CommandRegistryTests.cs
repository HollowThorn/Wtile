using Wtile.Commands;

namespace Wtile.Tests.Commands;

public class CommandRegistryTests
{
    private sealed class RecordingCommand(string name) : ICommand
    {
        public string Name => name;
        public IReadOnlyList<string>? LastArgs { get; private set; }
        public int CallCount { get; private set; }

        public void Execute(IReadOnlyList<string> args)
        {
            LastArgs = args;
            CallCount++;
        }
    }

    [Fact]
    public void TryExecute_RegisteredCommand_InvokesWithArgs()
    {
        var registry = new CommandRegistry();
        var command = new RecordingCommand("view-tag");
        registry.Register(command);

        bool found = registry.TryExecute("view-tag", ["3"]);

        Assert.True(found);
        Assert.Equal(1, command.CallCount);
        Assert.Equal(["3"], command.LastArgs);
    }

    [Fact]
    public void TryExecute_UnknownCommand_ReturnsFalseAndDoesNotThrow()
    {
        var registry = new CommandRegistry();
        bool found = registry.TryExecute("does-not-exist", []);
        Assert.False(found);
    }

    [Fact]
    public void Register_IsCaseInsensitive()
    {
        var registry = new CommandRegistry();
        var command = new RecordingCommand("focus-next");
        registry.Register(command);

        Assert.True(registry.TryExecute("FOCUS-NEXT", []));
    }

    [Fact]
    public void Register_SameNameTwice_LastWriterWins()
    {
        var registry = new CommandRegistry();
        var first = new RecordingCommand("spawn");
        var second = new RecordingCommand("spawn");
        registry.Register(first);
        registry.Register(second);

        registry.TryExecute("spawn", []);

        Assert.Equal(0, first.CallCount);
        Assert.Equal(1, second.CallCount);
    }
}
