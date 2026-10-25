using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect308Controller
{
    public static NativeBattleEffect308Step AdvanceMain668DE0(NativeBattleEffect308Frame frame, uint address, NativeBattleEffect308Resources resources, NativeBattleEffect308Inputs inputs)
    {
        short age = Signed(Read(frame, address), 12);
        int intensity = age < 8 ? unchecked(age * 2300) >> 3 : age > 102 ? unchecked((110 - age) * 2300) >> 3 : 2300;
        frame = inputs.Execute(frame, new NativeBattleEffect308Intensity5713E0(0, intensity));
        uint caster = frame.State.ReadDword(CasterGlobal);
        if (age >= 110)
        {
            for (int index = 0; index < 3; index++)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect308ModelFlags(checked(caster + (uint)(index * 156)), 2, 0xFFF7, 0));
            }
            for (int index = 0; index < 3; index++)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect308WriteModelBytes(checked(caster + (uint)(index * 156)), 44, ReadBytes(frame, checked(address + 20 + (uint)(index * 4)), 4), 4));
            }
            return new(inputs.Execute(frame, new NativeBattleEffect308Intensity5713E0(0, 0)), 2);
        }
        if (age < 20)
        {
            int factor = unchecked(age << 12) / 20;
            for (int index = 0; index < 3; index++)
            {
                byte[] first = ReadBytes(frame, checked(address + 20 + (uint)(index * 4)), 3).Read(0, 3);
                byte[] second = frame.State.Read(BlendColorAddress, 3);
                uint firstRgb = first[0] | (uint)first[1] << 8 | (uint)first[2] << 16;
                uint secondRgb = second[0] | (uint)second[1] << 8 | (uint)second[2] << 16;
                NativeBattleWeightedColorStep blend = NativeBattleWeightedColor56CC00.Blend(frame.Memory, firstRgb, secondRgb, unchecked((short)(4096 - factor)), unchecked((short)factor), 0, inputs.Colors);
                frame = frame with { Memory = blend.Memory };
                NativeBattleEffectArena rgb = NativeBattleEffectArena.Clear().Write(0, [(byte)blend.Color, (byte)(blend.Color >> 8), (byte)(blend.Color >> 16)]);
                uint model = checked(caster + (uint)(index * 156));
                frame = inputs.Execute(frame, new NativeBattleEffect308WriteModelBytes(model, 44, rgb, 3));
                frame = inputs.Execute(frame, new NativeBattleEffect308WriteModelBytes(model, 47, NativeBattleEffectArena.Clear().Write(0, [0x32]), 1));
            }
        }
        if ((ushort)age < 86)
        {
            for (int index = 0; index < 3; index++)
            {
                uint model = checked(caster + (uint)(index * 156));
                frame = inputs.Execute(frame, new NativeBattleEffect308DrawOffsetBody669070(model, checked(address + 0x20 + (uint)(index * 0x2D0))));
                frame = inputs.Execute(frame, new NativeBattleEffect308ModelFlags(model, 1, 255, 4));
            }
        }
        int local = age - 70;
        if ((uint)local < 16)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect308White7040B0(local * 256));
            if (local == 15)
            {
                foreach (uint descriptor in new[] { ScriptDescriptor, ParticleDescriptor, ActDescriptor })
                {
                    frame = WriteBytes(frame, descriptor, NativeBattleEffectArena.Clear(), 8);
                }
            }
        }
        local = age - 86;
        if ((uint)local >= 1000)
        {
            frame = NativeBattleEffect308Lower.DrawSmoke669620(frame, address, resources, inputs);
        }
        else if ((uint)local < 16)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect308White7040B0(4096 - local * 256));
        }
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect308Sound501330(SoundAddress, 0, 128));
        }
        else if (age == 106)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect308VibrationStop4A2940(unchecked((int)Read(frame, address).ReadDword(16))));
        }
        else if (age == 103)
        {
            uint destination = checked(frame.State.ReadDword(ArenaGlobal) + FirstPacketOffset);
            frame = inputs.Execute(frame, new NativeBattleEffect308QueueFile5341D0(360, destination, 1));
            frame = inputs.Execute(frame, new NativeBattleEffect308QueueFile5341D0(361, destination, 0));
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }
}
