using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAngelWing84NodeResources(ImmutableArray<byte> Data)
{
    public const uint Address = 0x171DE1C;
    public const int Length = 0x1494;

    public void Validate()
    {
        if (Data.IsDefault || Data.Length != Length || BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan()) != 0 || BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan().Slice(4)) != 0x133C || BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan().Slice(0x84)) != 0x104)
        {
            throw new InvalidDataException("Native Angel Wing requires its complete cold node bank and original directory offsets.");
        }
    }
}

internal sealed record NativeBattleAngelWing84NodeStep(NativeBattleAngelWing84Frame Frame, NativeCrtRandomState Random, bool Complete, NativeBattleLogicalOrdering Ordering, ImmutableArray<NativeBattleGfGeometryBatch> PrivateSprites, ImmutableArray<NativeBattleGfGeometryBatch> CommonMeshes);

/// <summary>791450/791F30's node owner, with its four emitters, forty geometry slots and explicit7961xx drawing services.</summary>
internal static class NativeBattleAngelWing84Nodes
{
    private static readonly NativeBattleEffectNodeCallerPolicy Policy = new(NativeBattleAngelWing84NodeResources.Address, NativeBattleAngelWing84NodeResources.Length, 0x7913C0, 4, 40, false);

    internal static NativeBattleAngelWing84Frame Initialize(NativeBattleAngelWing84Frame frame, uint address, NativeBattleAngelWing84NodeResources resources, NativeBattleAngelWing84Inputs inputs, NativeCrtRandomState random)
    {
        resources.Validate();
        NativeBattleRecover93NodeFrame bridge = ToNodeFrame(frame, random);
        NativeBattleAngelWing84SceneMemory scene = NativeBattleAngelWing84Scene.Read(frame);
        NativeBattleVector Origin(int offset) => new(scene.SignedWord(offset), scene.SignedWord(offset + 2), scene.SignedWord(offset + 4));
        bridge = NativeBattleEffectNodeCaller.Initialize74EC00(Policy, bridge, address, new(resources.Data), inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, new(Origin(0), Origin(8)));
        return FromNodeFrame(frame, bridge);
    }

    internal static NativeBattleAngelWing84NodeStep Advance(NativeBattleAngelWing84Frame frame, uint address, NativeCrtRandomState random, Func<int, NativeBattleGraphicsModel> readModel, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors colors, NativeBattleEffectNodeSpawnOrigin spawnOrigin, Func<NativeBattleEffectNodeMemory, int, byte[], NativeBattleEffectNodeMeshInstance> prepareMesh)
    {
        NativeBattleRecover93NodeStep step = NativeBattleEffectNodeCaller.Advance74F6E0(Policy, ToNodeFrame(frame, random), address, readModel, projection, cameraRoll, ordering, colors, prepareMeshOverride: prepareMesh, spawnOrigin: spawnOrigin);
        return new(FromNodeFrame(frame, step.Frame), step.Frame.Random, step.Complete, step.Ordering, step.PrivateSprites, step.CommonMeshes);
    }

    private static NativeBattleRecover93NodeFrame ToNodeFrame(NativeBattleAngelWing84Frame frame, NativeCrtRandomState random)
    {
        ImmutableArray<byte>? data = frame.State.NodeData;
        uint flag = data is { } retained ? BinaryPrimitives.ReadUInt32LittleEndian(retained.AsSpan()) : 0;
        NativeBattleRecover93NodeState state = new(flag, data, frame.State.Emitters, frame.State.Geometry, frame.State.EmitterScanCursor, frame.State.GeometryScanCursor, frame.State.PassEmitterCount, frame.State.PassGeometryCount, frame.State.CopiedCamera, frame.State.PrimaryDrawCursor, frame.State.EmitterResetWord, frame.State.GeometryResetWord);
        return new(frame.Memory, frame.Effect, state, random);
    }

    private static NativeBattleAngelWing84Frame FromNodeFrame(NativeBattleAngelWing84Frame frame, NativeBattleRecover93NodeFrame bridge)
    {
        return frame with { Memory = bridge.Memory, Effect = bridge.Effect, State = frame.State with { NodeData = bridge.State.Data, EmitterScanCursor = bridge.State.EmitterScanCursor, GeometryScanCursor = bridge.State.GeometryScanCursor, PassEmitterCount = bridge.State.PassEmitterCount, PassGeometryCount = bridge.State.PassGeometryCount, PrimaryDrawCursor = bridge.State.PrivateDrawCursor } };
    }
}
