using static Memoria.FF8.Runtime.NativeBattleEffect183Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect183Children
{
    public static (NativeBattleEffect183Frame Frame, int Result) Advance5C9C40(NativeBattleEffect183Frame frame, uint address, NativeBattleEffect183Resources resources, NativeBattleEffect183Inputs inputs) => Advance(frame, address, resources, inputs, false);
    public static (NativeBattleEffect183Frame Frame, int Result) Advance5C9DD0(NativeBattleEffect183Frame frame, uint address, NativeBattleEffect183Resources resources, NativeBattleEffect183Inputs inputs) => Advance(frame, address, resources, inputs, true);

    private static (NativeBattleEffect183Frame Frame, int Result) Advance(NativeBattleEffect183Frame frame, uint address, NativeBattleEffect183Resources resources, NativeBattleEffect183Inputs inputs, bool second)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short age = Signed(task, 12);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 0x18), 0));
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(local, new(0, 0, second ? (short)200 : (short)-600));
        local = local with { X = Signed(task, 0x10) + offset.X, Y = Signed(task, 0x12), Z = Signed(task, 0x14) + offset.Z };
        int scale = Signed(task, 0x1C);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        (frame, uint temporary) = Allocate(frame, 144);
        uint source = second ? 0xD34CE4u : 0xD348C4u;
        uint flags = 0x33;
        int? fade = age < 4 ? unchecked((4 - age) << 10) : second && age >= 16 ? unchecked((age - 16) << 10) : null;
        if (fade.HasValue)
        {
            flags = 0xF3;
        }
        NativeBattleEffectArena context = Context(frame, temporary, 144)
            .Write(0, BitConverter.GetBytes(source))
            .Write(8, new byte[4])
            .Write(0x10, new byte[4])
            .Write(0x14, BitConverter.GetBytes(unchecked(age * (second ? -6 : -10))))
            .Write(0x18, BitConverter.GetBytes(second ? (ushort)64 : (ushort)0))
            .Write(0x1A, new byte[2])
            .Write(0x1C, BitConverter.GetBytes((ushort)64))
            .Write(0x1E, BitConverter.GetBytes((ushort)128))
            .Write(0x20, BitConverter.GetBytes(flags));
        if (fade is int value)
        {
            context = context.Write(12, BitConverter.GetBytes(value));
        }
        if (second && (resources.Colors.Length != 10 || age % 10 < 0))
        {
            throw new InvalidDataException("Native effect183 color selector leaves its actual ten-entry table.");
        }
        context = context.Write(0x24, BitConverter.GetBytes(second ? resources.Colors[age % 10] : 0x808080u));
        frame = PublishContext(frame, temporary, context, 144);
        frame = inputs.Execute(frame, new NativeBattleEffect183DrawWindowed144(temporary, source) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(183, task, second ? 1 : 0) with { ResourceGeneration = source } : null });
        frame = Release(frame, 144);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        if (!second)
        {
            task = task.WriteWord(0x1C, unchecked((ushort)(Signed(task, 0x1C) - Signed(task, 0x1E))));
        }
        task = task.WriteWord(12, unchecked((ushort)(Signed(task, 12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= (second ? 20 : 16) ? 2 : 0);
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
