using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCannonBlow151Controller
{
    public static (NativeBattleCannonBlow151Frame Frame, int Result) AdvanceMesh(NativeBattleCannonBlow151Frame frame, uint address, NativeBattleCannonBlow151Resources resources, NativeBattleCannonBlow151Inputs inputs) => AdvanceMesh151(frame, address, Read(frame, address).Callback switch { 0x60D260 => 0, 0x60D670 => 1, 0x60DE40 => 2, 0x60E060 => 3, _ => throw new NotSupportedException("Cannon Blow mesh callback is unsupported.") }, resources, inputs);

    private static (NativeBattleCannonBlow151Frame Frame, int Result) AdvanceMesh151(NativeBattleCannonBlow151Frame frame, uint address, int kind, NativeBattleCannonBlow151Resources resources, NativeBattleCannonBlow151Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short age = Signed(task, 12);
        short angle = Signed(task, 0x18);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(kind == 0 ? new(0, 0, angle) : new(0, angle, 0));
        if (kind == 1)
        {
            uint matrix = MatrixAddress151(task);
            local = local with { X = unchecked((int)frame.State.ReadDword(matrix + 20)), Y = 0, Z = unchecked((int)frame.State.ReadDword(matrix + 28)) };
        }
        else if (kind >= 2)
        {
            local = local with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
        }
        int scale = Signed(task, 0x1C);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
        if (kind == 0)
        {
            local = NativeBattleGfMatrices.Compose56C2F0(ReadRetainedMatrix151(frame, MatrixAddress151(task)).ReadMatrix(), local);
        }
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(camera, null));
        uint source = kind switch { 0 => 0xDC0E10, 1 => 0xDC0900, 2 => 0xDBF4C0, _ => 0xDC1498 };
        uint flags = 0x33u;
        int fadeStart = kind switch { 0 => 4, 1 => 0, 2 => 8, _ => 3 };
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(source)).Write(8, BitConverter.GetBytes(0u));
        if (age >= fadeStart)
        {
            int cue = unchecked((age - fadeStart) * (kind == 1 ? 1024 : kind == 3 ? 409 : 512));
            context = context.Write(12, BitConverter.GetBytes(cue));
            flags |= 0xC0;
        }
        context = context.Write(0x1C, BitConverter.GetBytes(flags));
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleCannonBlow151DrawStatic88(temporary, source) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(151, task, 0) with { ResourceGeneration = source } });
        frame = Release(frame, 88);
        if (kind == 2)
        {
            NativeBattleActionMatrix scrolling = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, angle, 0)), scale - 256, scale - 256, scale - 256);
            scrolling = scrolling with { Rotation = NativeBattleGfMatrices.Multiply56C090(inputs.ReadView(frame).ReadMatrix(), scrolling).Rotation };
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(scrolling, null), true, false);
            (frame, uint scrollingTemporary) = Allocate(frame, 124);
            NativeBattleEffectArena scrollingContext = Context(frame, scrollingTemporary, 124).Write(0, BitConverter.GetBytes(0xDC00F0u)).Write(8, new byte[4]).Write(0x10, new byte[4]).Write(0x14, BitConverter.GetBytes(age * 14)).Write(0x18, new byte[4]).Write(0x1C, BitConverter.GetBytes((short)64)).Write(0x1E, BitConverter.GetBytes((short)128)).Write(0x20, BitConverter.GetBytes(age >= 8 ? 0xF3u : 0x33u));
            if (age >= 8)
            {
                scrollingContext = scrollingContext.Write(12, BitConverter.GetBytes((age - 8) << 9));
            }
            frame = PublishContext(frame, scrollingTemporary, scrollingContext, 124);
            frame = inputs.Execute(frame, new NativeBattleCannonBlow151DrawScrolling124(scrollingTemporary, 0xDC00F0) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(151, task, 1) with { ResourceGeneration = 0xDC00F0 } });
            frame = Release(frame, 124);
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        if (kind != 1)
        {
            short speed = Signed(task, 0x1E);
            task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + speed)));
            int divisor = kind == 0 ? 10 : kind == 2 ? 5 : 7;
            task = task.WriteWord(0x1E, unchecked((ushort)(speed - speed / divisor)));
            if (kind == 2)
            {
                short spin = Signed(task, 0x1A);
                task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + spin))).WriteWord(0x1A, unchecked((ushort)(spin - (spin >> 3))));
            }
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        bool done = kind switch { 0 => Signed(task, 12) >= 12, 1 => Signed(task, 12) >= 4, 2 => Signed(task, 12) > 16, _ => Signed(task, 12) > 13 };
        return (Write(frame, address, task), done ? 2 : 0);
    }

    private static uint MatrixAddress151(NativeBattleModulePoolTask task) => checked((uint)(0x244F8A8 + Signed(task, 14) * 32));
    private static NativeBattlePackedMatrix32 ReadRetainedMatrix151(NativeBattleCannonBlow151Frame frame, uint address)
    {
        (byte[] bytes, bool[] known) = frame.State.ReadView(address, 32).ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
}
