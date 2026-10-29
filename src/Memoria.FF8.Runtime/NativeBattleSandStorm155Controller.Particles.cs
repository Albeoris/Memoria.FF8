using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSandStorm155Controller
{
    public static (NativeBattleSandStorm155Frame Frame, int Result) AdvanceSpray(NativeBattleSandStorm155Frame frame, uint address, NativeBattleSandStorm155Resources resources, NativeBattleSandStorm155Inputs inputs) => AdvanceParticles(frame, address, false, resources, inputs);
    public static (NativeBattleSandStorm155Frame Frame, int Result) AdvanceSpiral(NativeBattleSandStorm155Frame frame, uint address, NativeBattleSandStorm155Resources resources, NativeBattleSandStorm155Inputs inputs) => AdvanceParticles(frame, address, true, resources, inputs);

    private static (NativeBattleSandStorm155Frame Frame, int Result) AdvanceParticles(NativeBattleSandStorm155Frame frame, uint address, bool spiral, NativeBattleSandStorm155Resources resources, NativeBattleSandStorm155Inputs inputs)
    {
        uint bank = spiral ? 0x23F0828u : 0x23F1188u;
        uint source = spiral ? 0xDB8040u : 0xDB7E94u;
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        int survivors = 0;
        (frame, uint temporary) = Allocate(frame, 180);
        frame = frame with { Staging = RequireStaging(frame).Write(temporary, BitConverter.GetBytes(source)).Write(temporary + 0x24, new byte[2]) };
        for (int index = 0; index < 100; index++)
        {
            uint record = checked(bank + (uint)(index * 24));
            if ((frame.State.ReadDword(record) & 1) == 0)
            {
                continue;
            }
            short age = Word(frame, record + 4);
            short size = Word(frame, record + 6);
            NativeBattleVector position = new(Word(frame, record + 8), Word(frame, record + 10), Word(frame, record + 12));
            if (spiral)
            {
                int angle = Word(frame, record + 20);
                int radius = Word(frame, record + 16);
                position = position with { X = unchecked((short)(position.X + (unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 12))), Z = unchecked((short)(position.Z + (unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12))) };
            }
            frame = frame with { Staging = RequireStaging(frame).Write(temporary + 4, BitConverter.GetBytes(unchecked((ushort)(spiral ? age >> 1 : age)))) };
            frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), position, OptionalWord(frame, record + 14), size, unchecked((short)-(size >> 4))) };
            frame = inputs.Execute(frame, new NativeBattleSandStorm155DrawSprite180(temporary, source) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(155, NativeBattleSandStorm155Controller.Read(frame, address), (spiral ? 100 : 0) + index) with { ResourceGeneration = NativeBattlePilotVisualLifetimes.Identity(155, frame.State.Globals, record, 0).Generation } });
            if (paused)
            {
                continue;
            }
            frame = Word(frame, record + 4, Word(frame, record + 4) + 1);
            if (BinaryPrimitives.ReadInt16LittleEndian(RequireStaging(frame).Read(temporary + 0x28, 2)) < 0)
            {
                frame = Dword(frame, record, 0);
                continue;
            }
            if (spiral)
            {
                frame = Word(frame, record + 10, Word(frame, record + 10) + Word(frame, record + 14));
                int velocity = Word(frame, record + 14);
                frame = Word(frame, record + 14, velocity + (velocity >> 4));
                frame = Word(frame, record + 16, Word(frame, record + 16) + Word(frame, record + 18));
                velocity = Word(frame, record + 18);
                frame = Word(frame, record + 18, velocity - (velocity >> 3));
                frame = Word(frame, record + 20, Word(frame, record + 20) + Word(frame, record + 22));
            }
            else
            {
                frame = Word(frame, record + 8, Word(frame, record + 8) + Word(frame, record + 16));
                frame = Word(frame, record + 12, Word(frame, record + 12) + Word(frame, record + 20));
                int velocity = Word(frame, record + 16);
                frame = Word(frame, record + 16, velocity - (velocity >> 3));
                velocity = Word(frame, record + 20);
                frame = Word(frame, record + 20, velocity - (velocity >> 3));
            }
            survivors++;
        }
        frame = Release(frame, 180);
        if (paused)
        {
            return (frame, 0);
        }
        int taskAge = Signed(Read(frame, address), 12);
        if (taskAge >= 0 && taskAge <= (spiral ? 40 : 52))
        {
            for (int birth = 0; birth < (spiral ? 5 : 3); birth++)
            {
                uint record = 0;
                for (int index = 0; index < 100; index++)
                {
                    uint candidate = checked(bank + (uint)(index * 24));
                    if (frame.State.ReadDword(candidate) == 0)
                    {
                        record = candidate;
                        break;
                    }
                }
                if (record == 0)
                {
                    break;
                }
                frame = Dword(frame, record, 1);
                frame = Word(frame, record + 4, 0);
                frame = DrawRandom(frame, out ushort size);
                frame = Word(frame, record + 6, spiral ? size % 1536 + 1024 : size % 2560 + 1792);
                if (spiral)
                {
                    frame = Copy(frame, 0x23F2948, record + 8, 8);
                    frame = DrawRandom(frame, out ushort y);
                    frame = Word(frame, record + 10, -50 - y % 400);
                    frame = DrawRandom(frame, out ushort velocityY);
                    frame = Word(frame, record + 14, -50 - velocityY % 120);
                    frame = DrawRandom(frame, out ushort radius);
                    frame = Word(Word(frame, record + 16, radius % 240 + 120), record + 18, radius % 240 + 120);
                    frame = DrawRandom(frame, out ushort angle);
                    frame = Word(frame, record + 20, angle & 4095);
                    frame = DrawRandom(frame, out ushort spin);
                    frame = Word(frame, record + 22, -20 - spin % 110);
                }
                else
                {
                    frame = DrawRandom(frame, out ushort angle);
                    int cosine = NativeBattleCameraMath.Cosine(angle & 4095);
                    int sine = NativeBattleCameraMath.Sine(angle & 4095);
                    frame = DrawRandom(frame, out ushort radial);
                    int radius = radial % 200 + 150;
                    frame = Copy(frame, 0x23F2948, record + 8, 8);
                    frame = Word(frame, record + 8, Word(frame, record + 8) + (unchecked(cosine * radius) >> 12));
                    frame = Word(frame, record + 12, Word(frame, record + 12) + (unchecked(sine * radius) >> 12));
                    frame = DrawRandom(frame, out ushort speed);
                    int velocity = speed % 150 + 60;
                    frame = Word(frame, record + 16, unchecked(cosine * velocity) >> 12);
                    frame = Word(frame, record + 20, unchecked(sine * velocity) >> 12);
                }
                NativeBattlePilotVisualLifetimes.Allocated(frame.State.Globals, record, 0);
            }
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 4 && survivors == 0 ? 2 : 0);
    }

    private static short? OptionalWord(NativeBattleSandStorm155Frame frame, uint address)
    {
        (byte[] bytes, bool[] known) = frame.State.ReadView(address, 2).ReadSnapshot(0, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
}
