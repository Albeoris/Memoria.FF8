using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect301Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect301Controller
{
    public static NativeBattleEffect301Step AdvanceMain66DBD0(NativeBattleEffect301Frame frame, uint address, NativeBattleEffect301Resources resources, NativeBattleEffect301Inputs inputs)
    {
        short age = unchecked((short)(Signed(Read(frame, address), 12) + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)age)));
        uint caster = frame.State.ReadDword(CasterGlobal);
        if (age >= 142)
        {
            return new(inputs.Execute(frame, new NativeBattleEffect301ModelFlags(caster, 2, 0xFFF3, 0)), 2);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect301ModelFlags(caster, 2, 0xFFF7, 0));
        uint morph = checked(frame.State.ReadDword(ArenaGlobal) + MorphOffset);
        if ((uint)age < 122)
        {
            NativeBattleEffectArena caller = CopyBytes(TaskView(Read(frame, address), 20, 8), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 16), 0, 8).Write(8, BitConverter.GetBytes(256)).Write(12, BitConverter.GetBytes(morph));
            NativeBattleEffect301Step programme = NativeBattleEffect301Programme.Advance(frame, address + 0x1C, 0x90, caller, resources, inputs);
            frame = inputs.Execute(programme.Frame, new NativeBattleEffect301ModelFlags(caster, 1, 255, 4));
            frame = inputs.Execute(frame, new NativeBattleEffect301DrawBody66E300(caster, age, 64, morph));
            if ((uint)age < 102)
            {
                frame = NativeBattleEffect301Particles.Birth66EB10(frame, resources, inputs);
                frame = NativeBattleEffect301Particles.Birth66ED70(frame, resources, inputs);
            }
        }
        else
        {
            frame = inputs.Execute(frame, new NativeBattleEffect301ModelFlags(caster, 2, 0xFFF3, 0));
        }
        int fraction = age - 122;
        if ((uint)fraction <= 20)
        {
            frame = FadeModelColor(frame, address, caster, fraction, inputs);
        }
        frame = NativeBattleEffect301Records.Advance66DF20(frame, address, resources, inputs);
        if (Read(frame, address).ReadWord(12) == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect301Sound501330(SoundAddress, 0, 128));
        }
        if (Read(frame, address).ReadWord(12) == 138)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect301VibrationStop4A2940(unchecked((int)Read(frame, address).ReadDword(16))));
        }
        return new(frame, 0);
    }

    public static NativeBattleEffect301Frame FadeModelColor(NativeBattleEffect301Frame frame, uint main, uint model, int fraction, NativeBattleEffect301Inputs inputs)
    {
        int factor = 4096 - NativeBattleCameraMath.Sine(((20 - fraction) << 10) / 20);
        byte[] original = TaskView(Read(frame, main), 0xE10, 3).Read(0, 3);
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor), Ir = [(int)original[0], (int)original[1], (int)original[2]] } };
        frame = Publish(frame, NativeBattleRaldo19BallRendering.Weighted(registers, false, inputs.Colors));
        NativeBattleScriptedColorRegisters output = Registers(frame).Colors;
        NativeBattleEffectArena color = NativeBattleEffectArena.Clear().Write(0, new byte[] { unchecked((byte)output.Ir[0]!.Value), unchecked((byte)output.Ir[1]!.Value), unchecked((byte)output.Ir[2]!.Value) });
        frame = inputs.Execute(frame, new NativeBattleEffect301WriteModelBytes(model, 44, color, 3));
        frame = inputs.Execute(frame, new NativeBattleEffect301WriteModelBytes(model, 47, NativeBattleEffectArena.Clear().Write(0, new byte[] { 0x32 }), 1));
        if (fraction == 20)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect301WriteModelBytes(model, 44, TaskView(Read(frame, main), 0xE10, 4), 4));
        }
        return frame;
    }
}
