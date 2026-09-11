using YamlDotNet.Serialization;

namespace Wtile.Config;

/// <summary>
/// AOT-safe (reflection-free) YAML (de)serialization context. Every type reachable from
/// <see cref="WtileConfig"/> must be listed here via <see cref="YamlSerializableAttribute"/>.
/// </summary>
[YamlStaticContext]
[YamlSerializable(typeof(WtileConfig))]
[YamlSerializable(typeof(GeneralConfig))]
[YamlSerializable(typeof(LayoutConfig))]
[YamlSerializable(typeof(BarConfig))]
[YamlSerializable(typeof(BarSegmentsConfig))]
[YamlSerializable(typeof(BarColorsConfig))]
[YamlSerializable(typeof(BarModuleConfig))]
[YamlSerializable(typeof(HotkeyBinding))]
[YamlSerializable(typeof(BlacklistRule))]
public partial class WtileYamlContext : StaticContext
{
}
