namespace Wtile.Commands;

/// <summary>Maps config-referenced command names to their <see cref="ICommand"/> implementation.</summary>
public sealed class CommandRegistry
{
    private readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ICommand command) => _commands[command.Name] = command;

    public bool TryGet(string name, out ICommand command) => _commands.TryGetValue(name, out command!);

    public IEnumerable<string> Names => _commands.Keys;

    /// <summary>Looks up and executes <paramref name="name"/>; returns false if no such command is registered.</summary>
    public bool TryExecute(string name, IReadOnlyList<string> args)
    {
        if (!TryGet(name, out ICommand command))
            return false;
        command.Execute(args);
        return true;
    }
}
