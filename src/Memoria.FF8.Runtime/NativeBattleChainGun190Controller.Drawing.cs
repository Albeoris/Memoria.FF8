using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleChainGun190Controller
{
    public static (NativeBattleChainGun190Frame Frame, int Result) AdvanceMesh(NativeBattleChainGun190Frame frame, uint address, NativeBattleChainGun190Resources resources, NativeBattleChainGun190Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int kind = task.Callback switch { 0x5BB5A0 => 0, 0x5BBAA0 => 1, 0x5BC080 => 2, _ => throw new NotSupportedException("Chain Gun mesh callback is unsupported.") };
        short age = Signed(task, 12);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(kind == 0 ? new(0, Signed(task, 0x18), Signed(task, 0x1A)) : kind == 1 ? new(0, Signed(task, 0x18), 0) : new(1024, Signed(task, 0x18), 0));
        int scale = Signed(task, 0x1C);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(view.ReadMatrix(), local);
        if (kind == 0)
        {
            frame = SetupSprite190(frame, view, TaskView(task, 0x10, 8), 0, unchecked((short)-(scale >> 3)));
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(camera, null), true, false);
        }
        else
        {
            if (kind == 2)
            {
                NativeBattleActionMatrix extra = NativeBattleGfMatrices.Euler56CD50(new(0, Signed(task, 0x1A), 0));
                camera = camera with { Rotation = NativeBattleGfMatrices.Multiply56C090(camera, extra).Rotation };
            }
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(camera, null));
        }
        uint source = kind switch { 0 => 0xD18CDC, 1 => 0xD18FDC, _ => 0xD192A4 };
        (frame, uint temporary) = Allocate(frame, 88);
        uint flags = age >= (kind == 2 ? 1 : 0) ? 0xF3u : 0x33u;
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(source)).Write(8, new byte[4]).Write(0x1C, BitConverter.GetBytes(flags));
        if (flags == 0xF3)
        {
            int cue = kind == 0 ? age << 11 : kind == 1 ? age << 9 : (age - 1) << 10;
            context = context.Write(12, BitConverter.GetBytes(cue));
        }
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleChainGun190DrawStatic88(temporary, source) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(190, task, 0) with { ResourceGeneration = source } });
        if (kind == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleChainGun190DrawStatic88(temporary, source) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(190, task, 1) with { ResourceGeneration = source } });
        }
        frame = Release(frame, 88);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        if (kind != 0)
        {
            short speed = Signed(task, 0x1E);
            task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + speed))).WriteWord(0x1E, unchecked((ushort)(speed - speed / 8)));
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= (kind == 0 ? 2 : kind == 1 ? 9 : 5) ? 2 : 0);
    }

    public static (NativeBattleChainGun190Frame Frame, int Result) AdvanceSprite(NativeBattleChainGun190Frame frame, uint address, NativeBattleChainGun190Resources resources, NativeBattleChainGun190Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x5BBBE0)
        {
            throw new NotSupportedException("Chain Gun sprite callback is unsupported.");
        }
        short size = Signed(task, 0x1C);
        frame = SetupSprite190(frame, inputs.ReadView(frame), TaskView(task, 0x10, 8), size, unchecked((short)-(size >> 3)));
        (frame, uint temporary) = Allocate(frame, 180);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(0xD187A0u)).Write(4, BitConverter.GetBytes(task.ReadWord(12))).Write(0x24, new byte[2]), 180);
        frame = inputs.Execute(frame, new NativeBattleChainGun190DrawSprite180(temporary, 0xD187A0) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(190, task) with { ResourceGeneration = 0xD187A0 } });
        frame = Release(frame, 180);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 16 ? 2 : 0);
    }

    private static NativeBattleChainGun190Frame SetupSprite190(NativeBattleChainGun190Frame frame, NativeBattlePackedMatrix32 view, NativeBattleEffectArena point, short size, short depth)
    {
        (byte[] wBytes, bool[] wKnown) = point.ReadSnapshot(6, 2);
        short? w = wKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(wBytes) : null;
        NativeBattleVector xyz = new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, view, xyz, w, size, depth) };
    }
}
