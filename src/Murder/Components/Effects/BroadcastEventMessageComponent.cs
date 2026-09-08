using Bang.Components;
using Murder.Utilities.Attributes;
using System.Collections.Immutable;

namespace Murder.Components;

public readonly struct BroadcastEventMessageComponent : IComponent
{
    [ChildId]
    public readonly string Target = string.Empty;

    [ChildId]
    public readonly ImmutableArray<string>? OtherTargets = null;

    public BroadcastEventMessageComponent() { }
}
