using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect338Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect338Controller
{
    private static NativeBattleEffect338Frame DrawShimmer(NativeBattleEffect338Frame frame, uint main, int angle, int cue, NativeBattleEffect338Resources resources, NativeBattleEffect338Inputs inputs, int role = 0)
    {
        (frame, uint temporary) = Allocate(frame, 120);
        NativeBattleModulePoolTask task = Read(frame, main);
        NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701220(angle) with { X = unchecked((int)task.ReadDword(0x78)), Y = unchecked((int)task.ReadDword(0x7C)), Z = unchecked((int)task.ReadDword(0x80)) };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix, 0);
        frame = PublishContext(frame, temporary, PackedArena(packed), 32);
        frame = LoadMatrix(frame, packed);
        NativeBattleEffectArena context = Context(frame, temporary + 32, 88).Write(0, BitConverter.GetBytes(0x1371640u)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes(cue)).Write(24, BitConverter.GetBytes(-128)).Write(28, BitConverter.GetBytes(0xF0u));
        NativeBattleDefaultActMeshStep drawn = NativeBattleDefaultActMesh.Draw(frame.Memory, resources.Mesh(0x1371640).Bytes, 0x1371640, 0, context, inputs.ReadProjection(frame), inputs.Colors, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(338, task, unchecked(0x10000 + role)) with { ResourceGeneration = 0x1371640 } : null);
        frame = frame with { Memory = drawn.Memory };
        frame = PublishContext(frame, temporary + 32, drawn.Context88, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect338DeliverBatch(inputs.CapturePresentation ? NativeBattleEffect338Presentation.PreserveNativeDepths(drawn.Batch) : drawn.Batch, CursorGlobal));
        return Release(frame, 120);
    }

    private static NativeBattleEffect338Frame DrawParticles(NativeBattleEffect338Frame frame, uint main, NativeBattleEffect338Resources resources, NativeBattleEffect338Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180);
        foreach (int field in new[] { 72, 80, 88, 96 })
        {
            context = context.Write(field, new byte[2]);
        }
        frame = PublishContext(frame, temporary, context, 180);
        for (int slot = 0; slot < 64; slot++)
        {
            NativeBattleEffectArena row = ReadBytes(frame, checked(ArenaAddress + (uint)(slot * 24)), 24);
            short life = Word338(row, 10);
            if (life == 0)
            {
                continue;
            }
            NativeBattlePackedMatrix32 camera = inputs.ReadView(frame);
            frame = LoadMatrix(frame, camera);
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers338(frame), 0, new(Word338(row, 4), Word338(row, 6), Word338(row, 8)), life));
            frame = Publish338(frame, registers);
            NativeBattleActionMatrix matrix = new([2048, 0, 0, 0, 2048, 0, 0, 0, 2048], registers.Colors.Mac[0]!.Value, registers.Colors.Mac[1]!.Value, registers.Colors.Mac[2]!.Value, 4096);
            context = Context(frame, temporary, 180);
            context = NativeBattleEffectNodeContextMemory.Copy(PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, 0)), 0, context, 132, 32);
            uint sourceAddress = Dword338(row, 0);
            NativeBattleModuleResource sprite = resources.Sprite(sourceAddress);
            int phases = BinaryPrimitives.ReadInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8));
            if (phases <= 0)
            {
                throw new InvalidDataException("Effect338 particle sprite has no positive native frame count.");
            }
            int directory = phases - (life - 1) % phases;
            short frameOffset = BinaryPrimitives.ReadInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8 + directory * 2));
            int count = BinaryPrimitives.ReadInt32LittleEndian(sprite.Bytes.AsSpan().Slice(frameOffset));
            context = context.Write(0, BitConverter.GetBytes(sourceAddress)).Write(44, BitConverter.GetBytes(unchecked(sourceAddress + (uint)frameOffset))).Write(48, BitConverter.GetBytes(count));
            if (count < 0)
            {
                context = context.Write(37, [(byte)(context.Read(37, 1)[0] | 1)]).Write(48, BitConverter.GetBytes(count & int.MaxValue));
            }
            context = context.Write(44, BitConverter.GetBytes(unchecked(sourceAddress + (uint)frameOffset + 4)));
            frame = PublishContext(frame, temporary, context, 180);
            NativeBattlePuffPacketStep drawn = NativeBattlePuffPackets6FC9E0.Draw(RequireStaging(frame), temporary, inputs.ReadProjection(frame), ReadResource, 17, pilotIdentity: inputs.CapturePresentation ? NativeBattleEffect338Presentation.Particle(frame, Read(frame, main), slot, sourceAddress) : null);
            frame = frame with { Staging = drawn.Staging };
            frame = inputs.Execute(frame, new NativeBattleEffect338DeliverBatch(inputs.CapturePresentation ? NativeBattleEffect338Presentation.PreserveNativeDepths(drawn.Packets) : drawn.Packets, CursorGlobal));
        }
        return Release(frame, 180);

        byte[] ReadResource(uint address, int length)
        {
            foreach (NativeBattleModuleResource sprite in resources.Sprites.Values)
            {
                if (address >= sprite.Address && (ulong)address + (uint)length <= (ulong)sprite.Address + (uint)sprite.Bytes.Length)
                {
                    return sprite.Bytes.AsSpan().Slice(checked((int)(address - sprite.Address)), length).ToArray();
                }
            }
            throw new InvalidDataException("Effect338 puff read leaves its selected current sprite source.");
        }
    }

    public static (NativeBattleEffect338Frame Frame, int Result) AdvanceFragment(NativeBattleEffect338Frame frame, uint address, NativeBattleEffect338Resources resources, NativeBattleEffect338Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 152);
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix scale = FragmentScale6ED1E0(unchecked((short)(unchecked((sbyte)task.ReadByte(29)) * 16)), Signed(task, 32));
        NativeBattleActionMatrix rotation = NativeBattlePandemona291Restoration.Rotation701220(unchecked((sbyte)task.ReadByte(28)) * 16);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Multiply56C090(rotation, scale) with { X = Signed(task, 12), Y = Signed(task, 14), Z = Signed(task, 16) };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        frame = PublishContext(frame, temporary + 120, PackedArena(NativeBattlePackedMatrix32.FromMatrix(scale, 0)), 20);
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix, 0);
        frame = PublishContext(frame, temporary + 88, PackedArena(packed), 32);
        frame = LoadMatrix(frame, packed);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0x1372CE8u)).Write(28, new byte[4]);
        NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.Mesh(0x1372CE8), inputs.ReadProjection(frame), context, inputs.Colors, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(338, task) with { ResourceGeneration = 0x1372CE8 } : null);
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect338DeliverBatch(inputs.CapturePresentation ? NativeBattleEffect338Presentation.PreserveNativeDepths(drawn.Batch) : drawn.Batch, CursorGlobal));
        frame = Release(frame, 152);
        task = Read(frame, address);
        byte remaining = unchecked((byte)(task.ReadByte(27) - 1));
        task = task.WriteByte(27, remaining);
        if (remaining == 0)
        {
            return (Write(frame, address, task), 2);
        }
        task = task.WriteByte(28, unchecked((byte)(task.ReadByte(28) + task.ReadByte(30)))).WriteByte(29, unchecked((byte)(task.ReadByte(29) + task.ReadByte(31))));
        for (int axis = 0; axis < 3; axis++)
        {
            int velocity = task.ReadWord(18 + axis * 2);
            task = task.WriteWord(12 + axis * 2, unchecked((ushort)(task.ReadWord(12 + axis * 2) + velocity)));
            task = task.WriteWord(18 + axis * 2, unchecked((ushort)(velocity + unchecked((sbyte)task.ReadByte(24 + axis)))));
        }
        return (Write(frame, address, task), 0);
    }

    public static NativeBattleActionMatrix FragmentScale6ED1E0(short angle, short scale)
    {
        int negative = -angle;
        short sine = unchecked((short)(unchecked(NativeBattleCameraMath.Sine(negative) * scale) >> 12));
        short cosine = unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(negative) * scale) >> 12));
        return new([cosine, 0, unchecked((short)-sine), 0, scale, 0, sine, 0, cosine], 0, 0, 0, 4096);
    }

    private static NativeBattleActionMatrix TaskMatrix(NativeBattleModulePoolTask task, int offset) => new(Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(offset + index * 2))).ToImmutableArray(), unchecked((int)task.ReadDword(offset + 20)), unchecked((int)task.ReadDword(offset + 24)), unchecked((int)task.ReadDword(offset + 28)), 4096);
    private static NativeBattleGeometryRegisterStep Registers338(NativeBattleEffect338Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect338 requires its current geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect338 requires its current arithmetic owner."));
    private static NativeBattleEffect338Frame Publish338(NativeBattleEffect338Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    private static short Word338(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadInt16LittleEndian(view.Read(offset, 2));
    private static uint Dword338(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(view.Read(offset, 4));
    private static NativeBattleVector Vector338(NativeBattleEffectArena view) => new(Word338(view, 0), Word338(view, 2), Word338(view, 4));
    private static NativeBattleEffectArena StoreIr338(NativeBattleEffectArena view, NativeBattleGeometryRegisterStep registers)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            view = view.Write(axis * 2, BitConverter.GetBytes(unchecked((short)registers.Colors.Ir[axis]!.Value)));
        }
        return view;
    }
}
