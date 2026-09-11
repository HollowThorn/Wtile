namespace Wtile.Layouts;

/// <summary>Maps config-referenced layout names (e.g. <c>"master-stack"</c>) to implementations.</summary>
public sealed class LayoutRegistry
{
    private readonly Dictionary<string, ILayout> _layouts = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ILayout layout) => _layouts[layout.Name] = layout;

    public bool TryGet(string name, out ILayout layout) => _layouts.TryGetValue(name, out layout!);

    public IEnumerable<string> Names => _layouts.Keys;

    /// <summary>Registers the built-in layout set.</summary>
    public static LayoutRegistry CreateDefault()
    {
        var registry = new LayoutRegistry();
        registry.Register(new MasterStackLayout());
        registry.Register(new MasterStackLayout(masterOnRight: true));
        registry.Register(new MonocleLayout());
        registry.Register(new CenteredMasterLayout());
        registry.Register(new VerticalStackLayout());
        return registry;
    }
}
