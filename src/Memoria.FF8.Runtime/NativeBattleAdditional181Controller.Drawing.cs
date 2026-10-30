using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAdditional181Controller
{
    public static (NativeBattleAdditional181Frame Frame, int Result) AdvanceBody(NativeBattleAdditional181Frame frame, uint address, NativeBattleAdditional181Resources resources, NativeBattleAdditional181Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x5CB9B0)
        {
            throw new NotSupportedException("Additional body callback is unsupported.");
        }
        short age = Signed(task, 12);
        if (age < 15)
        {
            (frame, uint temporary) = Allocate(frame, 128);
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Additional body requires its current color controls.");
            NativeBattleModelRendererRegisters far = (process.ModelRendererRegisters ?? throw new NotSupportedException("Additional body requires its current FAR controls.")) with { FarRed = 240u << 4, FarGreen = 96u << 4, FarBlue = 96u << 4 };
            uint factor = unchecked((uint)(age < 14 ? age * 292 : 4096));
            NativeBattleScriptedDpcsResult color = NativeBattleScriptedDpcs.Transform45F270(frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Additional body requires its current GTE color registers."), 0, factor, far, inputs.Colors);
            frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = color.Registers, PrimitiveProcess = process with { ModelRendererRegisters = far } } };
            frame = PublishContext(frame, temporary, Context(frame, temporary, 128).Write(0x5C, BitConverter.GetBytes(color.PackedRgb)), 128);
            int actor = checked((int)frame.State.ReadDword(0x2307D84));
            frame = inputs.Execute(frame, new NativeBattleAdditional181ActorFlags(actor, 0, 0x804));
            frame = inputs.Execute(frame, new NativeBattleAdditional181DrawBody128(actor, temporary) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(181, task) : null });
            frame = Release(frame, 128);
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) > 15 ? 2 : 0);
    }

    public static (NativeBattleAdditional181Frame Frame, int Result) AdvanceMesh(NativeBattleAdditional181Frame frame, uint address, NativeBattleAdditional181Resources resources, NativeBattleAdditional181Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool first = task.Callback == 0x5CC000;
        if (!first && task.Callback != 0x5CC160)
        {
            throw new NotSupportedException("Additional mesh callback is unsupported.");
        }
        short age = Signed(task, 12);
        int scale = Signed(task, 0x1C);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 0x18), 0)), scale, scale, scale) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null));
        (frame, uint temporary) = Allocate(frame, 88);
        uint source = first ? 0xD3E5C4u : 0xD405FCu;
        int fadeStart = first ? 8 : 10;
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(source)).Write(8, new byte[4]).Write(0x1C, BitConverter.GetBytes(age < fadeStart ? 0x33u : 0xF3u));
        if (age >= fadeStart)
        {
            context = context.Write(12, BitConverter.GetBytes((age - fadeStart) << 9));
        }
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleAdditional181DrawStatic88(temporary, source) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(181, task) with { ResourceGeneration = source } : null });
        frame = Release(frame, 88);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        if (first)
        {
            short spin = Signed(task, 0x1A);
            task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + spin))).WriteWord(0x1A, unchecked((ushort)(spin - (spin >> 4))));
        }
        short speed = Signed(task, 0x1E);
        task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + speed))).WriteWord(0x1E, unchecked((ushort)(speed - speed / 7))).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= (first ? 16 : 18) ? 2 : 0);
    }

    public static (NativeBattleAdditional181Frame Frame, int Result) AdvanceSprite(NativeBattleAdditional181Frame frame, uint address, NativeBattleAdditional181Resources resources, NativeBattleAdditional181Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x5CC720)
        {
            throw new NotSupportedException("Additional sprite callback is unsupported.");
        }
        short size = Signed(task, 0x1C);
        frame = SetupSprite181(frame, inputs.ReadView(frame), TaskView(task, 0x10, 8), size, unchecked((short)-(size >> 3)));
        (frame, uint temporary) = Allocate(frame, 180);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(0xD3E500u)).Write(4, BitConverter.GetBytes(task.ReadWord(12))).Write(0x24, new byte[2]), 180);
        frame = inputs.Execute(frame, new NativeBattleAdditional181DrawSprite180(temporary, 0xD3E500) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(181, task) with { ResourceGeneration = 0xD3E500 } : null });
        frame = Release(frame, 180);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address).WriteWord(0x10, unchecked((ushort)(Read(frame, address).ReadWord(0x10) + Signed(Read(frame, address), 0x18)))).WriteWord(0x14, unchecked((ushort)(Read(frame, address).ReadWord(0x14) + Signed(Read(frame, address), 0x1A)))).WriteWord(0x12, unchecked((ushort)(Read(frame, address).ReadWord(0x12) + 2))).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), BinaryPrimitives.ReadInt16LittleEndian(Context(frame, temporary, 180).Read(0x28, 2)) < 0 ? 2 : 0);
    }

    private static NativeBattleAdditional181Frame SetupSprite181(NativeBattleAdditional181Frame frame, NativeBattlePackedMatrix32 view, NativeBattleEffectArena point, short size, short depth)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
        NativeBattleVector xyz = new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, view, xyz, known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null, size, depth) };
    }
}
