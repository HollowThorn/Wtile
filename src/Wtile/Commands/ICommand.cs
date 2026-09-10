namespace Wtile.Commands;

/// <summary>
/// A named, invocable action (e.g. <c>focus-next</c>, <c>view-tag</c>, <c>spawn</c>). Hotkeys and
/// bar clicks both resolve to <c>(commandName, args[])</c> and dispatch through the same
/// <see cref="CommandRegistry"/>, so there is exactly one execution surface for both trigger kinds.
/// </summary>
public interface ICommand
{
    /// <summary>Registry key, referenced by name from YAML hotkey bindings and bar click handlers.</summary>
    string Name { get; }

    void Execute(IReadOnlyList<string> args);
}
