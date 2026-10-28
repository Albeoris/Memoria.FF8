using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStickyWeb153Resources(NativeBattleStaticMeshResource Web, NativeBattleStaticMeshResource Burst, NativeBattleModuleResource Sprite, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<int> ClipSelectors);
public sealed record NativeBattleStickyWeb153State([property: JsonRequired] ImmutableArray<byte> Globals, [property: JsonRequired] NativeBattleEffectArena Packets)
{
    public static NativeBattleStickyWeb153State FromColdProcess() => new(new byte[0x1994].ToImmutableArray(), NativeBattleEffectArena.Clear());
}
public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleStickyWeb153State? StickyWeb153
    {
        get => effectState.StickyWeb153;
        init => effectState = effectState with { StickyWeb153 = value };
    }
}

