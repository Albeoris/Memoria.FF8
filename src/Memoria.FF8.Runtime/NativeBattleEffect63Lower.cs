using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect63Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect63Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect63Lower
{
    public static NativeBattleEffect63Step Advance(NativeBattleEffect63Frame frame, uint address, NativeBattleEffect63Resources resources, NativeBattleEffect63Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != SpriteCallback)
        {
            throw new NotSupportedException("Effect63 requires its selected lower callback.");
        }
        NativeBattleEffectArena point = TaskView(task, 12, 8);
        (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
        short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), Vector(point), w, 4096, -1024) };
        (byte[] packedPoint, bool[] packedKnown) = point.ReadSnapshot(0, 8);
        frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = frame.Memory.ScriptedColorRegisters! with { NormalInput = packedPoint.ToImmutableArray(), NormalInputKnown = packedKnown.ToImmutableArray() } } };
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = PutWord63(PutWord63(Put63(Context(frame, temporary, 180), 0, SpriteAddress), 4, unchecked((sbyte)task.ReadByte(0x1B))), 0x24, 0);
        frame = PublishContext(frame, temporary, context, 180);
        NativeBattleModuleSpriteDraw draw = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(63, task) with { ResourceGeneration = SpriteAddress } : null);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect63DrawBatch(draw.Batch, CursorGlobal));
        frame = Release(frame, 180);
        task = Read(frame, address);
        sbyte phase = unchecked((sbyte)(task.ReadByte(0x1B) + 1));
        task = task.WriteByte(0x1B, unchecked((byte)phase));
        if (phase >= 12)
        {
            return new(Write(frame, address, task), 2);
        }
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(12 + axis * 2, unchecked((ushort)(Signed(task, 12 + axis * 2) + Signed(task, 18 + axis * 2))));
            task = task.WriteWord(18 + axis * 2, unchecked((ushort)(Signed(task, 18 + axis * 2) + unchecked((sbyte)task.ReadByte(24 + axis)))));
        }
        return new(Write(frame, address, task), 0);
    }

    public static NativeBattleVector Direction6E7920(int x, int y, int z)
    {
        int horizontal = Sqrt56BEC0(unchecked(x * x + z * z));
        return new(unchecked((short)(NativeWorldScriptedPathMath.NativeAngle(y, horizontal) & 0xFFF)), unchecked((short)-NativeWorldScriptedPathMath.NativeAngle(x, z)), 0);
    }

    public static NativeBattleEffect63Frame Envelope6E7A30(NativeBattleEffect63Frame frame, int age, int red, int green, int blue, int rise, int hold, int fall, NativeBattleEffect63Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null) => inputs.Execute(frame, new NativeBattleEffect63DrawBatch(NativeBattleRinoa89FadeDrawing.Draw7040B0(red, green, blue, NativeBattleRinoa89FadeDrawing.Factor6FC450(age, rise, hold, fall), pilotIdentity), CursorGlobal));

    public static NativeBattleEffect63Frame DrawBeam6E7790(NativeBattleEffect63Frame frame, NativeBattleEffectArena start8, NativeBattleEffectArena end8, int palette, int pitch, int yaw, int scale, int fade, NativeBattleEffect63Resources resources, NativeBattleEffect63Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleVector start = Vector(start8);
        NativeBattleVector end = Vector(end8);
        int x = end.X - start.X;
        int y = end.Y - start.Y;
        int z = end.Z - start.Z;
        NativeBattleVector direction = Direction6E7920(x, y, z);
        int length = unchecked(Sqrt56BEC0(unchecked(x * x + y * y + z * z)) * scale) >> 12;
        (frame, uint temporary) = Allocate(frame, 188);
        NativeBattleEffectArena arena = Put63(Put63(Put63(Context(frame, temporary, 188), 0, 4096), 4, 4096), 8, unchecked((uint)length));
        NativeBattleActionMatrix first = NativeBattleGfMatrices.Euler701310(new(unchecked((short)(pitch - direction.X)), 0, 0));
        //56BEF0 receives DWORD scale components, including the full signed length.
        first = ScaleColumns(first, 4096, 4096, length);
        arena = StoreMatrix(arena, 0x10, first);
        NativeBattleActionMatrix second = NativeBattleGfMatrices.Euler701310(new(0, unchecked((short)(yaw - direction.Y + 2048)), 0));
        second = NativeBattleGfMatrices.Multiply56C090(second, first) with { X = start.X, Y = start.Y, Z = start.Z };
        second = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), second);
        arena = StoreMatrix(arena, 0x30, second);
        frame = PublishContext(frame, temporary, arena, 188);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(second, null));
        NativeBattleEffectArena context = Context(frame, temporary + 0x50, 108);
        context = Put63(Put63(Put63(context, 0, BeamAddress), 0x1C, fade == 0 ? 0x30u : 0xF0u), 0x18, unchecked((uint)-128));
        if (fade != 0)
        {
            context = Put63(Put63(context, 8, 0), 12, unchecked((uint)fade));
        }
        context = PutWord63(PutWord63(PutWord63(PutWord63(PutWord63(PutWord63(PutWord63(PutWord63(PutWord63(PutWord63(context, 0x20, 0), 0x22, palette & 0x7F), 0x24, 0), 0x26, 0), 0x28, 256), 0x2A, 256), 0x2C, 0), 0x2E, 128), 0x30, 256), 0x32, 128);
        NativeBattleStaticMeshResource mesh = resources.ReadMesh(BeamAddress);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        frame = DrawWindow108(frame, temporary + 0x50, context, mesh, count, offset => NativeBattleEffectArena.Clear().Write(0, mesh.Bytes.AsSpan().Slice(8 + offset, 8)), inputs, pilotIdentity is { } identity ? identity with { ResourceGeneration = BeamAddress } : null);
        return Release(frame, 188);
    }

    public static NativeBattleEffect63Frame DrawWindow108(NativeBattleEffect63Frame frame, uint temporary, NativeBattleEffectArena context, NativeBattleStaticMeshResource mesh, int count, Func<int, NativeBattleEffectArena> readPacked, NativeBattleEffect63Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        frame = PublishContext(frame, temporary, context, 108);
        (NativeBattleVector Vertex, short? W) ReadRow(int offset)
        {
            NativeBattleEffectArena row = readPacked(offset);
            (byte[] bytes, bool[] known) = row.ReadSnapshot(6, 2);
            return (Vector(row), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null);
        }
        NativeBattleWindowedMeshContext108Step draw = NativeBattleWindowedMeshContext108.DrawLoaded(frame.Memory, mesh, inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, count, ReadRow, readSelectedPackedVertex: readPacked, pilotIdentity: pilotIdentity);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Context108, 108);
        return inputs.Execute(frame, new NativeBattleEffect63DrawBatch(draw.Batch, CursorGlobal));
    }

    public static NativeBattleEffectArena Put63(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    public static NativeBattleEffectArena PutWord63(NativeBattleEffectArena arena, int offset, int value) => arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    public static uint Dword63(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    public static short Word63(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    public static NativeBattleEffectArena StoreMatrix(NativeBattleEffectArena arena, int offset, NativeBattleActionMatrix matrix) => CopyBytes(PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)), 0, arena, offset, 32);
    public static NativeBattleActionMatrix ScaleColumns(NativeBattleActionMatrix matrix, int x, int y, int z) => NativeBattlePoseMatrices.Scale56BEF0(matrix, x, y, z);
    private static int Sqrt56BEC0(int value) => unchecked((int)(long)Math.Sqrt(value));
}
