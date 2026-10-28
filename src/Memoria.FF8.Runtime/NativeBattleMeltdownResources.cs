using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMeltdownResources(NativeBattleEffectHeapAllocation Animation, NativeBattleStaticMeshResource Disc, NativeBattleStaticMeshResource FlatGlow, NativeBattleStaticMeshResource Glow, NativeBattleModuleResource AcidA, NativeBattleModuleResource AcidB, uint Sound);
public sealed record NativeBattleMeltdownState([property: JsonRequired] NativeBattleEffectArena Packets, [property: JsonRequired] ImmutableArray<byte> Globals, [property: JsonRequired] int? Cursor)
{
    public static NativeBattleMeltdownState FromColdProcess() => new(NativeBattleEffectArena.Clear(), new byte[0x1AB0].ToImmutableArray(), 0);
}
public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMeltdownState? Meltdown148
    {
        get => effectState.Meltdown148;
        init => effectState = effectState with { Meltdown148 = value };
    }
}
public sealed record NativeBattleMeltdownFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleMeltdownOperation;
public sealed record NativeBattleMeltdownPackets(NativeBattleGfGeometryBatch Batch) : NativeBattleMeltdownOperation;
public sealed record NativeBattleMeltdownSprite(NativeBattleModulePacketBatch Batch) : NativeBattleMeltdownOperation;
public sealed record NativeBattleMeltdownSound(uint Sound) : NativeBattleMeltdownOperation;
public sealed record NativeBattleMeltdownContact(int Index) : NativeBattleMeltdownOperation;
public sealed record NativeBattleMeltdownFade : NativeBattleMeltdownOperation;
public sealed record NativeBattleMeltdownActor(int Actor, NativeBattleModuleAnchor Target, NativeBattleModuleAnchor Caster, int Strength, bool Draw) : NativeBattleMeltdownOperation;
public sealed record NativeBattleMeltdownFlags(int Actor, ushort Flags) : NativeBattleMeltdownOperation;
public sealed record NativeBattleMeltdownInputs(NativeBattleActionPacketsState Packets, NativeBattleCameraProjection Projection, NativeBattleSummonColors Colors, bool DrawOnly, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleMeltdownFrame, NativeBattleMeltdownOperation, NativeBattleMeltdownFrame> Execute);
