using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRecover93NodeResource(ImmutableArray<byte> Data);
public sealed record NativeBattleRecover93NodeState([property: JsonRequired] uint InitializationFlag, [property: JsonRequired] NativeBattleNodeSource? Data, [property: JsonRequired] int? Emitters, [property: JsonRequired] int? Geometry, [property: JsonRequired] ushort? EmitterScanCursor, [property: JsonRequired] ushort? GeometryScanCursor, [property: JsonRequired] ushort? PassEmitterCount, [property: JsonRequired] ushort? PassGeometryCount, [property: JsonRequired] NativeBattleActionMatrix? CopiedCamera, [property: JsonRequired] int? PrivateDrawCursor, [property: JsonRequired] ushort? Word2594BB0, [property: JsonRequired] ushort? Word2592550)
{
    public static NativeBattleRecover93NodeState FromColdProcess() => new(0, null, null, null, null, null, null, null, null, null, null, null);

    public void Validate(NativeBattleAngeloRestorationProfile profile)
    {
        Validate(profile.SourceLength, profile.EmitterBytes, profile.GeometryBytes);
    }
    internal void Validate(int sourceLength, int emitterBytes, int geometryBytes)
    {
        if (Data is { } bytes && (bytes.IsDefault || bytes.Length != sourceLength || bytes.ReadUInt32(0) != InitializationFlag))
        {
            throw new InvalidDataException("Native Recover node bank contradicts its exact retained initialization owner.");
        }
        if (InitializationFlag != 0 && Data is null)
        {
            throw new NotSupportedException("Native Recover fixed node data is missing; original data cannot replace it.");
        }
        if (Emitters is int first && (first < 0 || first > NativeBattleEffectArena.Length - emitterBytes - geometryBytes || Geometry != first + emitterBytes))
        {
            throw new InvalidDataException("Native Recover node ranges leave their actual contiguous arena owner.");
        }
        CopiedCamera?.Validate();
    }
}
public sealed record NativeBattleRecover93NodeFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleRecover93NodeState State, NativeCrtRandomState Random);
public sealed record NativeBattleRecover93NodeStep(NativeBattleRecover93NodeFrame Frame, bool Complete, NativeBattleLogicalOrdering Ordering, ImmutableArray<NativeBattleGfGeometryBatch> PrivateSprites, ImmutableArray<NativeBattleGfGeometryBatch> CommonMeshes);

/// <summary>Recover's explicit node-bank owner over the single normalized restoration lifecycle.</summary>
public static class NativeBattleRecover93Nodes
{
    public const uint SourceAddress = 0x16B62B8;
    public const int SourceLength = 0x21C8;
    public const uint ContextCallback = 0x74EB70;
    public const int EmitterBytes = 0x870;
    public const int GeometryBytes = 0xB7C0;

    private static NativeBattleEffectNodeCallerPolicy Policy(NativeBattleOriginalEffectState effect)
    {
        NativeBattleAngeloRestorationProfile profile = NativeBattleAngeloRestorationProfile.ForBinding(effect.Binding);
        return new(profile.SourceAddress, profile.SourceLength, profile.Code(ContextCallback), profile.EmitterCapacity, profile.GeometryCapacity, false);
    }
    public static NativeBattleRecover93NodeFrame ClearStorage(NativeBattleRecover93NodeFrame frame, int initializerTimArenaOffset) => NativeBattleEffectNodeCaller.ClearStorage(Policy(frame.Effect), frame, initializerTimArenaOffset);
    public static NativeBattleRecover93NodeFrame BeginPass(NativeBattleRecover93NodeFrame frame, NativeBattleActionMatrix copiedCamera, int privateDrawCursor) => NativeBattleEffectNodeCaller.BeginPass(Policy(frame.Effect), frame, copiedCamera, privateDrawCursor);
    public static NativeBattleRecover93NodeFrame Initialize74EC00(NativeBattleRecover93NodeFrame frame, uint taskAddress, NativeBattleRecover93NodeResource resource, NativeBattleActionPacketsState packets, Func<int, NativeBattleGraphicsModel> readModel, Func<int, NativeBattlePoseSource> readPoseSource) => NativeBattleEffectNodeCaller.Initialize74EC00(Policy(frame.Effect), frame, taskAddress, resource, packets, readModel, readPoseSource);
    public static NativeBattleRecover93NodeStep Advance74F6E0(NativeBattleRecover93NodeFrame frame, uint taskAddress, Func<int, NativeBattleGraphicsModel> readModel, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors colors) => NativeBattleEffectNodeCaller.Advance74F6E0(Policy(frame.Effect), frame, taskAddress, readModel, projection, cameraRoll, ordering, colors);
}
