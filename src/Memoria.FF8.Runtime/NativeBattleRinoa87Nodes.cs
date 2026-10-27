using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa87NodeResources(ImmutableArray<byte> Data)
{
    public const uint Address = 0x16FBE40;
    public const int Length = 0x1408;
    public void Validate()
    {
        if (Data.IsDefault || Data.Length != Length || BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan()) != 0 || BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan().Slice(4)) != 0x1000 || BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan().Slice(8)) != 0x1158 || BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan().Slice(12)) != 0x12B0)
        {
            throw new InvalidDataException("Native779130 requires its original three-node source and complete physical extent.");
        }
    }
}

public sealed partial record NativeBattleRinoa87State
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleNodeSource? NodeData { get; init; }
    [JsonRequired]
    public ImmutableArray<byte> MorphScratch { get; init; }
}

public sealed record NativeBattleRinoa87NodeStep(NativeBattleRinoa87Frame Frame, NativeCrtRandomState Random, int Result, NativeBattleLogicalOrdering Ordering);
public static class NativeBattleRinoa87Nodes
{
    private static NativeBattleEffectNodeCallerPolicy Policy { get; } = new(NativeBattleRinoa87NodeResources.Address, NativeBattleRinoa87NodeResources.Length, 0x779050, 20, 50, true);

    public static NativeBattleRinoa87NodeStep Advance(NativeBattleRinoa87Frame frame, uint address, NativeBattleRinoa87NodeResources resources, NativeBattleRinoa87Inputs controller, NativeCrtRandomState random, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors colors, Func<uint, byte, NativeBattleVector> readBodyBone502170)
    {
        NativeBattleModulePoolTask task = Read();
        if (task.Callback != 0x779050)
        {
            throw new InvalidDataException("Native779050 requires its original676-byte node task.");
        }
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                if (unchecked((short)task.ReadWord(36)) >= unchecked((short)task.ReadWord(664)))
                {
                    short selector = unchecked((short)task.ReadWord(672));
                    byte bone = selector switch { 0 => 30, 1 => 33, 2 => 28, _ => 29 };
                    NativeBattleRinoa87SceneMemory scene = NativeBattleRinoa87Scene.Read(frame);
                    uint body = BinaryPrimitives.ReadUInt32LittleEndian(scene.Settings.Read(24, 4));
                    NativeBattleVector point = readBodyBone502170(checked(body + 48), bone);
                    task = task.WriteWord(656, unchecked((ushort)point.X)).WriteWord(658, unchecked((ushort)point.Y)).WriteWord(660, unchecked((ushort)point.Z));
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
                throw new InvalidDataException("Native779050's signed phase exceeds its three callbacks.");
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
            if (task.ReadDword(48) != NativeBattleRinoa87NodeResources.Address || task.ReadWord(668) != 4)
            {
                throw new NotSupportedException("Native779130 requires its reached source and body-bone placement mode4.");
            }
            NativeBattleNodeSource data = frame.State.NodeData ?? new NativeBattleNodeSource(resources.Data);
            if (frame.State.NodeData is not null)
            {
                NativeBattleTaskOperation.AdmitCurrentRetainedMemoryStorage(data);
            }
            using NativeBattleNodeBankOperation bank = new(data);
            uint flag = bank.ReadUInt32(0);
            if (flag == 0)
            {
                NativeBattleEffectNodeBankFixups.Relocate(bank, NativeBattleRinoa87NodeResources.Address);
                bank.WriteUInt32(0, 1);
            }
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            task = NativeBattleEffectNodeContext.Initialize(task, hierarchy.Read(task.ReadDword(16)), frame.Effect.Parameters ?? throw new NotSupportedException("Native779130 requires its original action parameters."), controller.Packets, controller.ReadModel, controller.ReadPoseSource, NativeBattleRinoa87NodeResources.Address);
            Store();
            bank.Publish(false);
            NativeBattleTaskOperation.AdmitCurrentRetainedMemoryStorage(data);
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

    private static NativeBattleRecover93NodeFrame ToNodeFrame(NativeBattleRinoa87Frame frame, NativeCrtRandomState random)
    {
        NativeBattleNodeSource? data = frame.State.NodeData;
        uint flag = data is { } bytes ? bytes.ReadUInt32(0) : 0;
        NativeBattleRecover93NodeState state = new(flag, data, frame.State.EmitterOffset, frame.State.GeometryOffset, frame.State.Word25AECD0, frame.State.Word25B4640, frame.State.Word25B4B08, frame.State.Word25B4630, frame.State.CopiedCamera, frame.State.PrimaryDrawCursor, frame.State.Word25B4444, frame.State.Word25ACEB8);
        return new(frame.Memory, frame.Effect, state, random);
    }

    private static NativeBattleRinoa87Frame FromNodeFrame(NativeBattleRinoa87Frame frame, NativeBattleRecover93NodeFrame bridge)
    {
        NativeBattleRecover93NodeState state = bridge.State;
        return frame with { Memory = bridge.Memory, Effect = bridge.Effect, State = frame.State with { NodeData = state.Data, EmitterOffset = state.Emitters, GeometryOffset = state.Geometry, Word25AECD0 = state.EmitterScanCursor ?? throw new InvalidDataException("Native87 emitter cursor was lost."), Word25B4640 = state.GeometryScanCursor ?? throw new InvalidDataException("Native87 geometry cursor was lost."), Word25B4B08 = state.PassEmitterCount ?? throw new InvalidDataException("Native87 pass emitter count was lost."), Word25B4630 = state.PassGeometryCount ?? throw new InvalidDataException("Native87 pass geometry count was lost."), PrimaryDrawCursor = state.PrivateDrawCursor } };
    }
}
