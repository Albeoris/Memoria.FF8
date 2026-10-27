using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa88NodeResources(ImmutableArray<byte> Data)
{
    public const uint Address = 0x19ED018;
    public const int Length = 8356;
    public void Validate()
    {
        if (Data.IsDefault || Data.Length != Length || BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan()) != 0 || BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan().Slice(4)) != 0x1B44 || BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan().Slice(8)) != 0x1C9C || BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan().Slice(12)) != 0x1DF4 || BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan().Slice(16)) != 0x1F4C)
        {
            throw new InvalidDataException("Native76C790 requires its original four-node source and complete physical extent.");
        }
    }
}

public sealed partial record NativeBattleRinoa88State
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleNodeSource? NodeData { get; init; }
    [JsonRequired]
    public ImmutableArray<byte> MorphScratch { get; init; }
}

public sealed record NativeBattleRinoa88NodeStep(NativeBattleRinoa88Frame Frame, NativeCrtRandomState Random, int Result, NativeBattleLogicalOrdering Ordering);
public static class NativeBattleRinoa88Nodes
{
    private static NativeBattleEffectNodeCallerPolicy Policy { get; } = new(NativeBattleRinoa88NodeResources.Address, NativeBattleRinoa88NodeResources.Length, 0x76C700, 20, 70, true, PrivateMeshes: true);

    public static NativeBattleRinoa88NodeStep Advance(NativeBattleRinoa88Frame frame, uint address, NativeBattleRinoa88NodeResources resources, NativeBattleRinoa88Inputs controller, NativeCrtRandomState random, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = Read();
        if (task.Callback != 0x76C700)
        {
            throw new InvalidDataException("Native76C700 requires its original676-byte node task.");
        }
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                if (unchecked((short)task.ReadWord(36)) >= unchecked((short)task.ReadWord(664)))
                {
                    Store();
                    Initialize();
                    AdvanceContext();
                    task = Read().WriteByte(41, unchecked((byte)(Read().ReadByte(41) + 1)));
                }
                break;
            case 1:
                if (AdvanceContext())
                {
                    task = Read().WriteByte(38, (byte)(Read().ReadByte(38) | 1)).WriteByte(41, unchecked((byte)(Read().ReadByte(41) + 1)));
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Native76C700's signed phase exceeds its three callbacks.");
        }
        task = task.WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
        Store();
        bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
        if (complete)
        {
            frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Complete8DC530(address).Pools } };
        }
        return new(frame, random, complete ? 2 : 0, ordering);

        NativeBattleModulePoolTask Read() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        void Store() => frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
        void Initialize()
        {
            resources.Validate();
            if (task.ReadDword(48) != NativeBattleRinoa88NodeResources.Address || task.ReadWord(668) != 1)
            {
                throw new NotSupportedException("Native76C790 requires its reached source and target-center placement mode1.");
            }
            NativeBattleNodeSource data = frame.State.NodeData ?? throw new NotSupportedException("Native76C790 requires the actual completed file410 staging owner.");
            if (frame.State.NodeData is not null)
            {
                NativeBattleTaskOperation.AdmitCurrentRetainedEffectStorage(data);
            }
            using NativeBattleNodeBankOperation bank = new(data);
            uint flag = bank.ReadUInt32(0);
            if (flag == 0)
            {
                NativeBattleEffectNodeBankFixups.Relocate(bank, NativeBattleRinoa88NodeResources.Address);
                bank.WriteUInt32(0, 1);
            }
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            task = NativeBattleEffectNodeContext.Initialize(task, hierarchy.Read(task.ReadDword(16)), frame.Effect.Parameters ?? throw new NotSupportedException("Native76C790 requires its original action parameters."), controller.Packets, controller.ReadModel, controller.ReadPoseSource, NativeBattleRinoa88NodeResources.Address);
            Store();
            bank.Publish(false);
            NativeBattleTaskOperation.AdmitCurrentRetainedEffectStorage(data);
            frame = frame with { State = frame.State with { NodeData = data } };
        }
        bool AdvanceContext()
        {
            NativeBattleRecover93NodeFrame bridge = ToNodeFrame(frame, random);
            NativeBattleRecover93NodeStep next = NativeBattleEffectNodeCaller.Advance74F6E0(Policy, bridge, address, controller.ReadModel, projection, cameraRoll, ordering, colors, prepareMeshOverride: PrepareMesh);
            frame = FromNodeFrame(frame, next.Frame);
            random = next.Frame.Random;
            ordering = next.Ordering;
            task = Read();
            return next.Complete;
        }
        NativeBattleEffectNodeMeshInstance PrepareMesh(NativeBattleEffectNodeMemory memory, int node, byte[] source)
        {
            short sourceFlags = BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(58));
            short fade = unchecked((short)memory.Word(node + 462));
            uint flags = sourceFlags == 0 ? 0x30u : 0;
            if (fade != 0)
            {
                flags |= 0xC0;
            }
            NativeBattleFullLifeMeshContext operands = new(flags, fade == 0 ? null : memory.Dword(node + 364), fade == 0 ? null : fade, null, null, null, 17, 2);
            return (bytes, matrix) => NativeBattleRestorationMeshPackets.EmitBatch884370(bytes, matrix, projection, operands, colors, observeFirstThreeFlags: memory.WriteProjectionFlags);
        }
    }

    private static NativeBattleRecover93NodeFrame ToNodeFrame(NativeBattleRinoa88Frame frame, NativeCrtRandomState random)
    {
        NativeBattleNodeSource? data = frame.State.NodeData;
        uint flag = data is { } bytes ? bytes.ReadUInt32(0) : 0;
        NativeBattleRecover93NodeState state = new(flag, data, frame.State.EmitterOffset, frame.State.GeometryOffset, frame.State.Word25A6BB8, frame.State.Word25AC6E8, frame.State.Word25ACCF0, frame.State.Word25AC6D8, frame.State.CopiedCamera, frame.State.PrimaryDrawCursor, frame.State.Word25AC3A4, frame.State.Word25A4DA0);
        return new(frame.Memory, frame.Effect, state, random);
    }

    private static NativeBattleRinoa88Frame FromNodeFrame(NativeBattleRinoa88Frame frame, NativeBattleRecover93NodeFrame bridge)
    {
        NativeBattleRecover93NodeState state = bridge.State;
        return frame with { Memory = bridge.Memory, Effect = bridge.Effect, State = frame.State with { NodeData = state.Data, EmitterOffset = state.Emitters, GeometryOffset = state.Geometry, Word25A6BB8 = state.EmitterScanCursor ?? throw new InvalidDataException("Native88 emitter cursor was lost."), Word25AC6E8 = state.GeometryScanCursor ?? throw new InvalidDataException("Native88 geometry cursor was lost."), Word25ACCF0 = state.PassEmitterCount ?? throw new InvalidDataException("Native88 pass emitter count was lost."), Word25AC6D8 = state.PassGeometryCount ?? throw new InvalidDataException("Native88 pass geometry count was lost."), PrimaryDrawCursor = state.PrivateDrawCursor } };
    }
}
