using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoomtrain191ModelMemory([property: JsonRequired] NativeBattleEffectArena Descriptor, [property: JsonRequired] NativeBattleEffectArena Model, [property: JsonRequired] NativeBattlePackedMatrix32 Anchor, [property: JsonRequired] uint? ScalePointer)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? OrbitAngle { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? OrbitStep { get; init; }

    public const uint DescriptorAddress = 0x24FBD70;
    public const uint ModelAddress = 0x24FC290;
    public const int DescriptorLength = 0x34;
    public const int ModelLength = 0x90;

    public static NativeBattleDoomtrain191ModelMemory FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), new(new byte[32].ToImmutableArray(), Enumerable.Repeat(true, 32).ToImmutableArray()), 0) { OrbitAngle = 0, OrbitStep = 0 };
    public static NativeBattleDoomtrain191ModelMemory Unknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, DescriptorLength), NativeBattleEffectArena.Clear().MarkUnknown(0, ModelLength), new(new byte[32].ToImmutableArray(), Enumerable.Repeat(false, 32).ToImmutableArray()), null);

    public void Validate()
    {
        Check(Descriptor, DescriptorLength);
        Check(Model, ModelLength);
        Anchor.Validate();
        static void Check(NativeBattleEffectArena memory, int length)
        {
            memory.Validate();
            memory.ValidateExtent(length);
        }
    }
}

public sealed record NativeBattleDoomtrain191CreatureResources(ImmutableArray<uint> ClampMasks, ImmutableArray<NativeBattleStaticMeshResource> Meshes, NativeBattleEffectHeapAllocation DebrisStart, NativeBattleEffectHeapAllocation DebrisEnd)
{
    public NativeBattleStaticMeshResource RequireMesh(uint address) => Meshes.SingleOrDefault(mesh => mesh.Address == address) ?? throw new NotSupportedException($"Native Doomtrain creature mesh {address:X8} requires its original resource.");
}
public sealed record NativeBattleDoomtrain191ModelDraw(NativeBattleDoomtrain191Frame Frame, NativeBattleGfGeometryBatch Packets, uint Color, uint? SourceCursor, int ProjectedBytesWritten);

public static class NativeBattleDoomtrain191Model
{
    public static NativeBattleDoomtrain191Frame Initialize642400(NativeBattleDoomtrain191Frame frame, short parameter)
    {
        NativeBattleDoomtrain191State state = State(frame);
        NativeBattleDoomtrain191ModelMemory memory = state.Model ?? NativeBattleDoomtrain191ModelMemory.Unknown();
        uint source = state.ArenaAddress;
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain model requires its current source arena.");
        NativeBattleEffectArena model = memory.Model.Write(0, BitConverter.GetBytes((ushort)2)).Write(7, new byte[1]).Write(0x28, BitConverter.GetBytes(0x00808080u)).Write(0x61, new byte[1]).Write(0x60, new byte[1]).Write(0x68, BitConverter.GetBytes(NativeBattleDoomtrain191ModelMemory.DescriptorAddress + 12)).Write(0x64, BitConverter.GetBytes(NativeBattleDoomtrain191ModelMemory.DescriptorAddress + 12)).Write(0x78, new byte[4]).Write(0x7C, BitConverter.GetBytes(uint.MaxValue)).Write(0x84, BitConverter.GetBytes(NativeBattleDoomtrain191ModelMemory.DescriptorAddress));
        NativeBattleEffectArena descriptor = memory.Descriptor;
        for (int index = 0; index < 3; index++)
        {
            uint relative = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(4 + 4 * index, 4));
            descriptor = descriptor.Write(12 + 4 * index, BitConverter.GetBytes(unchecked(source + relative)));
        }
        descriptor = descriptor.Write(2, BitConverter.GetBytes(parameter));
        uint fourth = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(16, 4));
        descriptor = descriptor.Write(0x30, BitConverter.GetBytes(unchecked(source + fourth)));
        return Put(frame, memory with { Descriptor = descriptor, Model = model });
    }

    public static NativeBattleDoomtrain191Frame InitializeWorld642E10(NativeBattleDoomtrain191Frame frame)
    {
        NativeBattleDoomtrain191ModelMemory memory = Require(frame);
        NativeBattleEffectArena model = memory.Model.Write(0x4C, new byte[4]).Write(0x48, new byte[4]).Write(0x44, new byte[4]).Write(0x40, new byte[4]).Write(0x50, BitConverter.GetBytes((ushort)4096)).Write(0x48, BitConverter.GetBytes((ushort)4096)).Write(0x40, BitConverter.GetBytes((ushort)4096));
        return Put(frame, memory with { Model = model });
    }

    public static NativeBattleDoomtrain191Frame Select642470(NativeBattleDoomtrain191Frame frame, short animation, int sourceLength)
    {
        NativeBattleDoomtrain191ModelMemory memory = Require(frame);
        byte flags = (byte)(memory.Model.Read(0x6D, 1)[0] & 0xF3);
        if ((flags & 3) != 0)
        {
            throw new NotSupportedException($"Native Doomtrain pose speed flags {flags:X2} require their original cursor contract.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain model requires its current skeleton.");
        int skeleton = Offset(frame, Pointer(memory, 12));
        byte[] animations = Animations(frame, memory, sourceLength);
        int count = arena.Read(skeleton, 1)[0];
        NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(animations, count, animation);
        int entry = BinaryPrimitives.ReadInt32LittleEndian(animations.AsSpan(checked(4 + 4 * animation), 4));
        byte[] control = new byte[8];
        control[0] = unchecked((byte)animation);
        control[1] = (byte)(flags | 4);
        BinaryPrimitives.WriteUInt16LittleEndian(control.AsSpan(2), first.ByteOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(control.AsSpan(4), first.BitOffset);
        control[6] = 1;
        control[7] = animations[entry];
        arena = NativeBattleArenaPose.WritePose(arena, skeleton, first.Pose);
        arena = NativeBattleArenaPose.RestoreBoneMatrices(arena, skeleton, first.Pose);
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        return Put(frame, memory with { Model = memory.Model.Write(0x6C, control) });
    }

    public static NativeBattleDoomtrain191Frame Advance642CA0(NativeBattleDoomtrain191Frame frame, int sourceLength)
    {
        NativeBattleDoomtrain191ModelMemory memory = Require(frame);
        byte current = memory.Model.Read(0x72, 1)[0];
        byte limit = memory.Model.Read(0x73, 1)[0];
        if (current >= limit)
        {
            return (memory.Model.Read(0, 1)[0] & 1) == 0 ? Select642470(frame, memory.Model.Read(0x6C, 1)[0], sourceLength) : frame;
        }
        byte[] control = memory.Model.Read(0x6C, 8);
        byte flags = control[1];
        if ((flags & 3) != 0)
        {
            throw new NotSupportedException($"Native Doomtrain pose speed flags {flags:X2} require their original cursor contract.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain model requires its current skeleton.");
        int skeleton = Offset(frame, Pointer(memory, 12));
        int count = arena.Read(skeleton, 1)[0];
        NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(arena, skeleton, count);
        NativeBattlePoseFirstFrame next = NativeBattlePoseDecoder.AdvanceRegular508F90(Animations(frame, memory, sourceLength), count, control[0], pose, BinaryPrimitives.ReadUInt16LittleEndian(control.AsSpan(2)), BinaryPrimitives.ReadUInt16LittleEndian(control.AsSpan(4)));
        control[1] = (byte)((flags & 0xF3) | ((((flags >> 2) + 1) & 1) << 2));
        BinaryPrimitives.WriteUInt16LittleEndian(control.AsSpan(2), next.ByteOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(control.AsSpan(4), next.BitOffset);
        control[6] = unchecked((byte)(current + 1));
        arena = NativeBattleArenaPose.WritePose(arena, skeleton, next.Pose);
        arena = NativeBattleArenaPose.RestoreBoneMatrices(arena, skeleton, next.Pose);
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        return Put(frame, memory with { Model = memory.Model.Write(0x6C, control) });
    }

    public static (NativeBattleDoomtrain191Frame Frame, uint Clipped) Compose642D80(NativeBattleDoomtrain191Frame frame, NativeBattleActionMatrix view, ImmutableArray<uint> masks)
    {
        NativeBattleDoomtrain191ModelMemory memory = Require(frame);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain model requires its current bone matrices.");
        int skeleton = Offset(frame, Pointer(memory, 12));
        int count = arena.Read(skeleton, 1)[0];
        NativeBattleActionMatrix world = NativeBattleArenaPose.ReadMatrix(memory.Model, 0x40);
        uint clipped = 0;
        for (int index = 0; index < count; index++)
        {
            int record = checked(skeleton + 32 + index * 48);
            NativeBattleActionMatrix bone = NativeBattleGfMatrices.Compose56C2F0(world, NativeBattleArenaPose.ReadMatrix(arena, record));
            if (bone.Z < -32000 || bone.Z > 32000)
            {
                if (masks.IsDefault || index >= masks.Length)
                {
                    throw new NotSupportedException("Native Doomtrain bone clipping requires its actual per-bone object masks.");
                }
                bone = bone with { Z = Math.Clamp(bone.Z, -32000, 32000) };
                clipped |= masks[index];
            }
            bone = NativeBattleGfMatrices.Compose56C2F0(view, bone);
            arena = NativeBattleArenaPose.WriteMatrix(arena, record, bone, true);
        }
        return (frame with { Memory = frame.Memory with { Arena = arena } }, clipped);
    }

    public static NativeBattleDoomtrain191Frame Restore642830(NativeBattleDoomtrain191Frame frame)
    {
        NativeBattleDoomtrain191ModelMemory memory = Require(frame);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain model requires its current local pose.");
        int skeleton = Offset(frame, Pointer(memory, 12));
        NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(arena, skeleton, arena.Read(skeleton, 1)[0]);
        return frame with { Memory = frame.Memory with { Arena = NativeBattleArenaPose.RestoreBoneMatrices(arena, skeleton, pose) } };
    }

    public static NativeBattleDoomtrain191Frame PublishAnchor(NativeBattleDoomtrain191Frame frame)
    {
        NativeBattleDoomtrain191ModelMemory memory = Require(frame);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain model requires its current anchor bone.");
        NativeBattleActionMatrix bone = NativeBattleArenaPose.ReadMatrix(arena, checked(Offset(frame, Pointer(memory, 12)) + 0x50));
        NativeBattleActionMatrix world = NativeBattleArenaPose.ReadMatrix(memory.Model, 0x40);
        return Put(frame, memory with { Anchor = NativeBattleDoomtrain191Controller.Pack(NativeBattleGfMatrices.Compose56C2F0(world, bone), memory.Anchor, true) });
    }

    public static NativeBattleDoomtrain191ModelDraw Render5099D0(NativeBattleDoomtrain191Frame frame, NativeBattleCameraProjection projection, uint clipped, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleDoomtrain191State state = State(frame);
        NativeBattleDoomtrain191ModelMemory memory = Require(frame);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain model requires its current geometry.");
        int skeleton = Offset(frame, Pointer(memory, 12));
        uint geometryAddress = Pointer(memory, 16);
        int geometry = Offset(frame, geometryAddress);
        int count = arena.Read(skeleton, 1)[0];
        List<int> sourceEnds = [];
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => arena.Read(checked(geometry + offset), length), count, (_, _, _, end) => sourceEnds.Add(end));
        ImmutableArray<NativeBattleActionMatrix> bones = Enumerable.Range(0, count).Select(index => NativeBattleArenaPose.ReadMatrix(arena, checked(skeleton + 32 + index * 48))).ToImmutableArray();
        int work = Offset(frame, state.WorkPointers[6]);
        int length = meshes.IsEmpty ? 0 : checked(meshes.Max(mesh => mesh.Vertices.Length) * 8);
        uint color = BinaryPrimitives.ReadUInt32LittleEndian(memory.Model.Read(0x28, 4));
        uint mask = BinaryPrimitives.ReadUInt32LittleEndian(memory.Model.Read(0x7C, 4)) ^ clipped;
        NativeBattleRawModelBodyDraw draw = NativeBattleRawModelBodyDrawing.Draw5099D0(meshes, bones, projection, color, mask, arena.ReadSnapshot(work, length).Data.ToImmutableArray(), colors, 4, new(0, 0, 320, 216), pilotIdentity: pilotIdentity);
        if (draw.ProjectedBytesWritten != 0)
        {
            arena = arena.Write(work, draw.ProjectedScratch.AsSpan().Slice(0, draw.ProjectedBytesWritten));
        }
        uint? cursor = null;
        for (int index = 0; index < sourceEnds.Count; index++)
        {
            if ((mask & (1u << (index & 31))) != 0)
            {
                cursor = unchecked(geometryAddress + (uint)sourceEnds[index]);
            }
        }
        return new(frame with { Memory = frame.Memory with { Arena = arena } }, draw.Packets, color, cursor, draw.ProjectedBytesWritten);
    }

    public static NativeBattleDoomtrain191ModelMemory Require(NativeBattleDoomtrain191Frame frame) => State(frame).Model ?? throw new NotSupportedException("Native Doomtrain model requires its retained physical state.");
    public static NativeBattleDoomtrain191Frame Put(NativeBattleDoomtrain191Frame frame, NativeBattleDoomtrain191ModelMemory model) => frame with { Memory = frame.Memory with { Doomtrain191 = State(frame) with { Model = model } } };
    private static NativeBattleDoomtrain191State State(NativeBattleDoomtrain191Frame frame) => frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain model requires its retained module state.");
    private static uint Pointer(NativeBattleDoomtrain191ModelMemory memory, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Descriptor.Read(offset, 4));
    private static int Offset(NativeBattleDoomtrain191Frame frame, uint address) => checked((int)(address - State(frame).ArenaAddress));
    private static byte[] Animations(NativeBattleDoomtrain191Frame frame, NativeBattleDoomtrain191ModelMemory memory, int sourceLength)
    {
        int offset = Offset(frame, Pointer(memory, 20));
        return (frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain pose requires its current animations.")).Read(offset, checked(sourceLength - offset));
    }
}
