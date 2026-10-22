using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDeathResources(NativeBattleEffectHeapAllocation Animation, NativeBattleEffectOwnedModelResource Model, ImmutableArray<byte> AuraActors, ImmutableArray<byte> DarkActors, NativeBattleEffectHeapAllocation FirstTim, NativeBattleEffectHeapAllocation SecondTim, NativeBattleStaticMeshResource Spark, ImmutableArray<byte> SparkTables, uint AppearanceSound, uint StrikeSound)
{
    public ushort Effect { get; init; } = 14;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleModuleResource? CasterResource { get; init; }
    [JsonIgnore]
    public NativeBattleDeathFamilyProfile Profile => NativeBattleDeathFamilyProfile.ForEffect(Effect);
}
public sealed record NativeBattleDeathState([property: JsonRequired] NativeBattleEffectArena Module, [property: JsonRequired] ImmutableArray<byte>? AuraActors, [property: JsonRequired] ImmutableArray<byte>? DarkActors, [property: JsonRequired] bool PendingColdSources, [property: JsonRequired] ImmutableArray<NativeBattleEffectOwnedModelState> Models, [property: JsonRequired] int? Source, [property: JsonRequired] int? Cursor, [property: JsonRequired] int? Emitters, [property: JsonRequired] int? Geometry, [property: JsonRequired] ushort EmitterScan, [property: JsonRequired] ushort GeometryScan, [property: JsonRequired] ushort PassEmitters, [property: JsonRequired] ushort PassGeometry)
{
    public static NativeBattleDeathState FromColdProcess() => new(NativeBattleEffectArena.Clear().MarkUnknown(4, 4), null, null, true, [], null, 0, null, null, 0, 0, 0, 0);
}
public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDeathState? Death14 { get; init; }
}
public sealed record NativeBattleDeathFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random)
{
    public NativeBattleFileStaging? Staging { get; init; }
}
public abstract record NativeBattleDeathOperation;
public sealed record NativeBattleDeathPublishPose(NativeBattleEffectOwnedModelState Model) : NativeBattleDeathOperation;
public sealed record NativeBattleDeathObserveAnchor(NativeBattleBoneAnchorProducer Producer) : NativeBattleDeathOperation;
public sealed record NativeBattleDeathPackets(NativeBattleGfGeometryBatch Batch) : NativeBattleDeathOperation;
public sealed record NativeBattleDeathSound(uint Descriptor) : NativeBattleDeathOperation;
public sealed record NativeBattleDeathContact(int Index) : NativeBattleDeathOperation;
public sealed record NativeBattleDeathBindCaster(byte Actor, uint SavedRow, uint Directory) : NativeBattleDeathOperation;
public sealed record NativeBattleDeathRingSequence(byte Actor, ushort Sequence) : NativeBattleDeathOperation;
public sealed record NativeBattleDeathStage(short Fade, bool ClearRgb) : NativeBattleDeathOperation;
public sealed record NativeBattleDeathTextureLoad(int Source, uint Completion) : NativeBattleDeathOperation;
public sealed record NativeBattleDeathModelStep(NativeBattleDeathFrame Frame, NativeBattleModulePoolTask Task);
public sealed record NativeBattleDeathInputs(NativeBattleActionPacketsState Packets, NativeBattleCameraProjection Projection, short CameraRoll, NativeBattleSummonColors Colors, uint DefaultColor, uint TransitionColor, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleDeathFrame, NativeBattleModulePoolTask, NativeBattleEffectOwnedModelState, NativeBattleDeathModelStep> DrawModel, Func<NativeBattleDeathFrame, NativeBattleDeathOperation, NativeBattleDeathFrame> Execute)
{
    public Func<NativeBattlePackedMatrix32>? ReadPackedCamera { get; init; }
}
