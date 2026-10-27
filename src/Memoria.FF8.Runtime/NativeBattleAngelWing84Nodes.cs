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

internal sealed record NativeBattleAngelWing84NodeCallbackStep(NativeBattleAngelWing84Frame Frame, NativeCrtRandomState Random, int Result, NativeBattleLogicalOrdering Ordering);

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

    internal static NativeBattleAngelWing84NodeStep Advance(NativeBattleAngelWing84Frame frame, uint address, NativeCrtRandomState random, Func<int, NativeBattleGraphicsModel> readModel, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors colors, NativeBattleEffectNodeSpawnOrigin spawnOrigin)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        if (hierarchy.Read(address).Callback != 0x7913C0)
        {
            throw new InvalidDataException("Native791F30 requires its original generic node task.");
        }
        NativeCrtRandomState currentRandom = random;
        List<NativeBattleGfGeometryBatch> privatePackets = [];
        var context = NativeBattleEffectNodeLifecycle.AdvanceContext(ReadTask, WriteTask, () =>
        {
            int emitters = frame.State.Emitters ?? throw new NotSupportedException("Native Angel Wing emitter pool has not been cleared.");
            int geometry = frame.State.Geometry ?? throw new NotSupportedException("Native Angel Wing geometry pool has not been cleared.");
            using NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, address, emitters, 432, geometry, 26880, useSharedScratch: true);
            NativeBattleNodeSource bankSource = frame.State.NodeData ?? throw new NotSupportedException("Native Angel Wing requires its current fixed node bank.");
            NativeBattleTaskOperation.AdmitCurrentRetainedMemoryStorage(bankSource);
            using NativeBattleEffectNodeBank bank = new(NativeBattleAngelWing84NodeResources.Address, bankSource);
            ushort emitterCursor = frame.State.EmitterScanCursor ?? throw new NotSupportedException("Native Angel Wing emitter cursor is unknown.");
            ushort geometryCursor = frame.State.GeometryScanCursor ?? throw new NotSupportedException("Native Angel Wing geometry cursor is unknown.");
            NativeBattleEffectNodeInputs inputs = new(frame.State.CopiedCamera ?? throw new NotSupportedException("Native Angel Wing requires its current copied camera."), readModel, (parent, source, target) => NativeBattleEffectNodeAllocation.AllocateEmitter(memory, emitters, 4, ref emitterCursor, parent, source, target), (emitter, source) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, geometry, 40, ref geometryCursor, emitter, source)) { SpawnOrigin = spawnOrigin };
            NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, inputs);
            lifecycle.AdvanceAndDraw(ref currentRandom, projection, cameraRoll, PrepareMesh, (_, batch) => privatePackets.Add(batch));
            NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
            bank.Publish();
            frame = frame with { Memory = written.Memory, Effect = written.Effect, State = frame.State with { NodeData = bankSource, EmitterScanCursor = emitterCursor, GeometryScanCursor = geometryCursor } };

            NativeBattleEffectNodeMeshInstance PrepareMesh(int node, byte[] source)
            {
                short sourceFlags = BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(0x3A));
                short fade = unchecked((short)memory.Word(node + 0x1CE));
                uint flags = sourceFlags == 0 ? 0x30u : 0;
                if (fade != 0)
                {
                    flags |= 0xC0;
                }
                NativeBattleFullLifeMeshContext operands = new(flags, fade == 0 ? null : memory.Dword(node + 0x16C), fade == 0 ? null : fade, null, null, null, 17, 2);
                return (bytes, matrix) => NativeBattleRestorationMeshPackets.EmitBatch884370(bytes, matrix, projection, operands, colors, observeFirstThreeFlags: memory.WriteProjectionFlags);
            }
        });
        foreach (NativeBattleGfGeometryBatch batch in privatePackets)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, frame.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native792020 requires its current private packet cursor."), ordering, batch);
            frame = frame with { Memory = written.Memory, State = frame.State with { PrimaryDrawCursor = written.Cursor } };
            ordering = written.Ordering;
        }
        frame = frame with { State = frame.State with { PassEmitterCount = unchecked((ushort)((frame.State.PassEmitterCount ?? throw new NotSupportedException("Native Angel Wing emitter count was not reset.")) + context.EmitterCount)), PassGeometryCount = unchecked((ushort)((frame.State.PassGeometryCount ?? throw new NotSupportedException("Native Angel Wing geometry count was not reset.")) + context.GeometryCount)) } };
        return new(frame, currentRandom, context.Complete, ordering, privatePackets.ToImmutableArray(), []);

        NativeBattleModulePoolTask ReadTask() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        void WriteTask(NativeBattleModulePoolTask task)
        {
            NativeBattleEffectHierarchy current = new(frame.Effect.Pools);
            frame = frame with { Effect = frame.Effect with { Pools = current.Write(address, task).Pools } };
        }
    }

    internal static NativeBattleAngelWing84NodeCallbackStep AdvanceGeneric(NativeBattleAngelWing84Frame frame, uint address, NativeBattleAngelWing84NodeResources resources, NativeBattleAngelWing84Inputs controller, NativeCrtRandomState random, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors colors, NativeBattleEffectNodeSpawnOrigin spawnOrigin)
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        if (task.Callback != 0x7913C0)
        {
            throw new InvalidDataException("Native7913C0 requires its original callback task.");
        }
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                if (unchecked((short)task.ReadWord(36)) >= unchecked((short)task.ReadWord(664)))
                {
                    frame = Initialize(frame, address, resources, controller, random);
                    AdvanceContext();
                    task = task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
                }
                break;
            case 1:
                if (AdvanceContext())
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) | 1)).WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Native7913C0 phase exceeds its three original states.");
        }
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        task = task.WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, random, complete ? 2 : 0, ordering);

        bool AdvanceContext()
        {
            NativeBattleAngelWing84NodeStep advanced = Advance(frame, address, random, controller.ReadModel, projection, cameraRoll, ordering, colors, spawnOrigin);
            frame = advanced.Frame;
            random = advanced.Random;
            ordering = advanced.Ordering;
            task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
            return advanced.Complete;
        }
    }

    private static NativeBattleRecover93NodeFrame ToNodeFrame(NativeBattleAngelWing84Frame frame, NativeCrtRandomState random)
    {
        NativeBattleNodeSource? data = frame.State.NodeData;
        uint flag = data is { } retained ? retained.ReadUInt32(0) : 0;
        NativeBattleRecover93NodeState state = new(flag, data, frame.State.Emitters, frame.State.Geometry, frame.State.EmitterScanCursor, frame.State.GeometryScanCursor, frame.State.PassEmitterCount, frame.State.PassGeometryCount, frame.State.CopiedCamera, frame.State.PrimaryDrawCursor, frame.State.EmitterResetWord, frame.State.GeometryResetWord);
        return new(frame.Memory, frame.Effect, state, random);
    }

    private static NativeBattleAngelWing84Frame FromNodeFrame(NativeBattleAngelWing84Frame frame, NativeBattleRecover93NodeFrame bridge)
    {
        return frame with { Memory = bridge.Memory, Effect = bridge.Effect, State = frame.State with { NodeData = bridge.State.Data, EmitterScanCursor = bridge.State.EmitterScanCursor, GeometryScanCursor = bridge.State.GeometryScanCursor, PassEmitterCount = bridge.State.PassEmitterCount, PassGeometryCount = bridge.State.PassGeometryCount, PrimaryDrawCursor = bridge.State.PrivateDrawCursor } };
    }
}
