using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattlePoisonGas171Controller
{
    public static (NativeBattlePoisonGas171Frame Frame, int Result) AdvanceBody(NativeBattlePoisonGas171Frame frame, uint address, NativeBattlePoisonGas171Resources resources, NativeBattlePoisonGas171Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x5DB620)
        {
            throw new NotSupportedException("Poison Gas body callback is unsupported.");
        }
        short age = Signed(task, 12);
        int actor = checked((int)frame.State.ReadDword(0x23AF80C));
        if (age < 24)
        {
            int intensity = age < 4 ? age << 5 : age >= 16 ? (24 - age) << 4 : 128;
            uint color = unchecked((uint)(intensity | intensity << 8 | intensity << 16));
            (frame, uint temporary) = Allocate(frame, 176);
            NativeBattleEffectArena context = Context(frame, temporary, 176).Write(0x34, new byte[4]).Write(0x38, BitConverter.GetBytes((ushort)128)).Write(0x3A, BitConverter.GetBytes((ushort)128)).Write(0x2C, new byte[4]).Write(0x30, BitConverter.GetBytes(age << 2)).Write(0x6C, BitConverter.GetBytes(color));
            frame = PublishContext(frame, temporary, context, 176);
            frame = inputs.Execute(frame, new NativeBattlePoisonGas171ActorFlags(actor, 0, 4));
            frame = inputs.Execute(frame, new NativeBattlePoisonGas171DrawBody176(actor, temporary) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(171, task) : null });
            frame = Release(frame, 176);
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        frame = Write(frame, address, task);
        if (Signed(task, 12) < 24)
        {
            return (frame, 0);
        }
        frame = inputs.Execute(frame, new NativeBattlePoisonGas171ActorFlags(actor, 4, task.ReadWord(0x1A)));
        return (frame, 2);
    }

    public static (NativeBattlePoisonGas171Frame Frame, int Result) AdvanceMesh(NativeBattlePoisonGas171Frame frame, uint address, NativeBattlePoisonGas171Resources resources, NativeBattlePoisonGas171Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x5DC5B0)
        {
            throw new NotSupportedException("Poison Gas mesh callback is unsupported.");
        }
        short age = Signed(task, 12);
        int scale = Signed(task, 0x1C);
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(Signed(task, 0x18), 0, 0)), scale + (scale >> 1), scale, scale) with { X = Signed(task, 0x10), Y = 0, Z = inputs.ReadFocalWord(frame) };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        int cue = age < 12 ? 4096 - age * 170 : age >= 42 ? age * 170 - 5092 : 2048;
        (frame, uint temporary) = Allocate(frame, 88);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0xD65A4Cu)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes(cue)).Write(0x1C, BitConverter.GetBytes(0xC3u)), 88);
        frame = inputs.Execute(frame, new NativeBattlePoisonGas171DrawStatic88(temporary, 0xD65A4C, 17, 4) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(171, task) with { ResourceGeneration = 0xD65A4C } : null });
        frame = Release(frame, 88);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address).WriteWord(0x10, unchecked((ushort)(Read(frame, address).ReadWord(0x10) + Signed(Read(frame, address), 0x16)))).WriteWord(0x18, unchecked((ushort)(Read(frame, address).ReadWord(0x18) + Signed(Read(frame, address), 0x1A)))).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 54 ? 2 : 0);
    }

    private static NativeBattlePoisonGas171Frame SetupSprite171(NativeBattlePoisonGas171Frame frame, NativeBattlePoisonGas171Inputs inputs, NativeBattleEffectArena point, short size)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
        NativeBattleVector xyz = new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), xyz, known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null, size, unchecked((short)-(size >> 4))) };
    }
}
