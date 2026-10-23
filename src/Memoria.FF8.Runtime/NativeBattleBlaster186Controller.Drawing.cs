using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBlaster186Controller
{
    public static (NativeBattleBlaster186Frame Frame, int Result) AdvanceMesh(NativeBattleBlaster186Frame frame, uint address, NativeBattleBlaster186Resources resources, NativeBattleBlaster186Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint callback = task.Callback;
        short age = Signed(task, 12);
        short size = Signed(task, 0x1C);
        uint source;
        uint flags;
        if (callback == 0x5C0040)
        {
            frame = SetupSprite186(frame, frame.State.ReadView(0x22B7F98, 8), size, unchecked((short)-(size >> 3)), inputs);
            source = 0xD20204;
            flags = age >= Signed(task, 0x20) ? 0xF3u : 0x33u;
        }
        else if (callback == 0x5C0280)
        {
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, Signed(task, 0x18))) with { X = Word186(frame, 0x22BC108), Y = Word186(frame, 0x22BC10A), Z = Word186(frame, 0x22BC10C) };
            local = NativeBattlePoseMatrices.Scale56BEF0(local, size, size, unchecked(Word186(frame, 0x22BC10E) << 12) / 5000);
            local = local with { Rotation = NativeBattleGfMatrices.Multiply56C090(Matrix186(frame, 0x22BBA70).ReadMatrix(), local).Rotation };
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null));
            source = 0xD1EF4C;
            flags = age >= Signed(task, 0x20) ? 0x20F3u : 0x2033u;
        }
        else if (callback == 0x5C0750)
        {
            NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 0x18), 0)), size, size, size) with { X = Word186(frame, 0x22BC0F0), Y = Word186(frame, 0x22BC0F2), Z = Word186(frame, 0x22BC0F4) };
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null));
            source = 0xD1F754;
            flags = age >= 16 ? 0x14F3u : 0x1433u;
        }
        else
        {
            throw new NotSupportedException("Blaster mesh callback is unsupported.");
        }
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(source)).Write(8, new byte[4]).Write(0x1C, BitConverter.GetBytes(flags));
        if (callback == 0x5C0280)
        {
            context = context.Write(4, BitConverter.GetBytes(resources.VertexPointers[Signed(task, 0x1A) % 20]));
        }
        if (callback == 0x5C0750)
        {
            context = context.Write(0x10, BitConverter.GetBytes((~(age % 8)) & 1)).Write(0x18, BitConverter.GetBytes(resources.Colors[age & 7]));
            if (age >= 16)
            {
                context = context.Write(12, BitConverter.GetBytes((age - 16) << 9));
            }
        }
        else if (age >= Signed(task, 0x20))
        {
            int cue = unchecked((4096 / Signed(task, 0x22)) * (age - Signed(task, 0x20)));
            context = context.Write(12, BitConverter.GetBytes(cue));
        }
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleBlaster186DrawStatic88(temporary, source));
        frame = Release(frame, 88);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        if (callback == 0x5C0750)
        {
            age = Signed(task, 12);
            int offset = unchecked(NativeBattleCameraMath.Sine(unchecked(age * 2048) / 24) * 40) >> 12;
            short baseline = Signed(task, 0x14);
            short z = unchecked((short)(baseline + ((age & 1) == 0 ? -offset : offset)));
            int actor = checked((int)frame.State.ReadDword(0x22B7600));
            frame = inputs.Execute(frame, new NativeBattleBlaster186SetActorZWord(actor, z));
            task = Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)));
            frame = Write(frame, address, task);
            if (Signed(task, 12) >= 24)
            {
                return (inputs.Execute(frame, new NativeBattleBlaster186SetActorZWord(actor, baseline)), 2);
            }
            return (frame, 0);
        }
        if (callback == 0x5C0280)
        {
            task = task.WriteWord(0x1A, unchecked((ushort)(task.ReadWord(0x1A) + 1)));
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= Signed(task, 14) ? 2 : 0);
    }

    public static (NativeBattleBlaster186Frame Frame, int Result) AdvanceSprite(NativeBattleBlaster186Frame frame, uint address, NativeBattleBlaster186Resources resources, NativeBattleBlaster186Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short age = Signed(task, 12);
        uint source;
        ushort flags;
        ushort phase;
        if (task.Callback == 0x5BFEC0)
        {
            NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)), unchecked(Signed(task, 0x1C) * Word186(frame, 0x22B7F9E)) / 820, Signed(task, 0x20), 0) with { X = Word186(frame, 0x22B7F98), Y = Word186(frame, 0x22B7F9A), Z = Word186(frame, 0x22B7F9C) };
            NativeBattleModuleAxisAngle align = NativeBattleModuleAlignment.Between571480(-4096, 0, 0, unchecked((int)frame.State.ReadDword(0x22B7FA0)), unchecked((int)frame.State.ReadDword(0x22B7FA4)), unchecked((int)frame.State.ReadDword(0x22B7FA8)));
            NativeBattleActionMatrix rotation = new(NativeBattleModuleAlignment.Rotation5714F0(align.Angle, align.Axis), 0, 0, 0, 4096);
            local = local with { Rotation = NativeBattleGfMatrices.Multiply56C090(rotation, local).Rotation };
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null));
            source = 0xD1DD5C;
            flags = 8;
            phase = (ushort)(age & 3);
        }
        else if (task.Callback == 0x5C0690)
        {
            short size = Signed(task, 0x1C);
            frame = SetupSprite186(frame, frame.State.ReadView(0x22BC108, 8), size, unchecked((short)-(size >> 4)), inputs);
            source = 0xD1DDD0;
            flags = age >= 14 ? (ushort)4 : (ushort)0;
            phase = (ushort)(age & 15);
        }
        else
        {
            throw new NotSupportedException("Blaster sprite callback is unsupported.");
        }
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(source)).Write(4, BitConverter.GetBytes(phase)).Write(0x24, BitConverter.GetBytes(flags));
        if (task.Callback == 0x5C0690 && age >= 14)
        {
            byte tint = unchecked((byte)((22 - age) << 4));
            context = context.Write(0x1C, [tint, tint, tint]);
        }
        frame = PublishContext(frame, temporary, context, 180);
        frame = inputs.Execute(frame, new NativeBattleBlaster186DrawSprite180(temporary, source));
        frame = Release(frame, 180);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= (task.Callback == 0x5C0690 ? 22 : Signed(task, 14)) ? 2 : 0);
    }

    private static short Word186(NativeBattleBlaster186Frame frame, uint address) => unchecked((short)frame.State.ReadWord(address));
    private static NativeBattlePackedMatrix32 Matrix186(NativeBattleBlaster186Frame frame, uint address)
    {
        (byte[] bytes, bool[] known) = frame.State.ReadView(address, 32).ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static NativeBattleBlaster186Frame SetupSprite186(NativeBattleBlaster186Frame frame, NativeBattleEffectArena point, short size, short depth, NativeBattleBlaster186Inputs inputs)
    {
        (byte[] wBytes, bool[] wKnown) = point.ReadSnapshot(6, 2);
        NativeBattleVector vector = new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), vector, wKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(wBytes) : null, size, depth) };
    }
}
