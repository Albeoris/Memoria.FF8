using static Memoria.FF8.Runtime.NativeBattleEffect285Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect285Controller
{
    public static NativeBattleEffect285Step AdvanceMain67AC00(NativeBattleEffect285Frame frame, uint address, NativeBattleEffect285Resources resources, NativeBattleEffect285Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        int intensity = age < 8 ? unchecked(age * 300) : age > 127 ? unchecked((135 - age) * 300) : 2400;
        frame = frame with { State = frame.State.WriteDword(IntensityGlobal, unchecked((uint)intensity)) };
        if ((uint)(age - 48) < 39)
        {
            NativeBattleEffectArena caller = ProgrammeCaller(PointView(new(0, -10000, 0)), 0, 0, frame.State.ReadDword(ArenaGlobal) + MorphOffset);
            frame = NativeBattleEffect285Programme.Advance(frame, address + FirstWorkspaceOffset, FirstWorkspaceLength, caller, resources, inputs).Frame;
            if (age == 82)
            {
                frame = DeliverWhite(frame, inputs);
            }
        }
        if ((uint)(age - 86) < 48)
        {
            NativeBattleEffectArena caller = ProgrammeCaller(PointView(new(0, 0, unchecked((short)frame.State.ReadWord(CentroidGlobal)))), -256, (ushort)((-(age - 86) & 3) << 5), frame.State.ReadDword(ArenaGlobal) + MorphOffset);
            frame = NativeBattleEffect285Programme.Advance(frame, address + SecondWorkspaceOffset, SecondWorkspaceLength, caller, resources, inputs).Frame;
        }
        frame = EmitMainEvents(frame, age, inputs);
        if ((uint)age < 24)
        {
            frame = BirthSprites(frame, age, resources, inputs);
        }
        frame = AdvanceCamera(frame, age, resources, inputs);
        frame = EmitContacts(frame, age, inputs);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        if (Signed(Read(frame, address), 12) >= 135)
        {
            frame = RestoreSavedCamera(frame, inputs);
            frame = inputs.Execute(frame, new NativeBattleEffect285IoQueue5341D0(360, frame.State.ReadDword(ArenaGlobal) + ThirdPacketOffset, 0));
            return new(frame, 2);
        }
        return new(frame, 0);
    }

    public static NativeBattleEffect285Frame EmitMainEvents(NativeBattleEffect285Frame frame, int age, NativeBattleEffect285Inputs inputs)
    {
        if (age is 0 or 48)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect285Sound501330(age == 0 ? 0x108FAA8u : 0x108FAACu, 0, 128));
        }
        return frame;
    }

    public static NativeBattleEffect285Frame EmitContacts(NativeBattleEffect285Frame frame, int age, NativeBattleEffect285Inputs inputs)
    {
        if (age == 110)
        {
            NativeBattleActionHeaderSlot header = CurrentHeader(frame, inputs.ReadPackets(frame));
            frame = inputs.Execute(frame, new NativeBattleEffect285AllContacts506BA0(header.FirstContactIndex ?? throw new NotSupportedException("Effect285 requires its current contact pointer."), header.ReadByte(16)));
        }
        return frame;
    }

    public static NativeBattleEffectArena ProgrammeCaller(NativeBattleEffectArena point8, short depth, ushort phase, uint morph)
    {
        return CopyBytes(point8, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 16), 0, 8).Write(8, BitConverter.GetBytes(depth)).Write(10, BitConverter.GetBytes(phase)).Write(12, BitConverter.GetBytes(morph));
    }

    private static NativeBattleEffect285Frame DeliverWhite(NativeBattleEffect285Frame frame, NativeBattleEffect285Inputs inputs) => inputs.Execute(frame, new NativeBattleEffect285DrawBatch(NativeBattleRinoa89FadeDrawing.Draw7040B0(128, 128, 128, 4096), CursorGlobal));

    public static NativeBattleEffect285Frame BirthSprites(NativeBattleEffect285Frame frame, int age, NativeBattleEffect285Resources resources, NativeBattleEffect285Inputs inputs)
    {
        foreach (uint source in new uint[] { 0x108FAB8, 0x108FAB0 })
        {
            (frame, uint main) = Append(frame, ParticleDescriptor, ParticleCallback);
            if (main == 0)
            {
                continue;
            }
            NativeBattleEffectArena local = NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(source, 8));
            NativeBattleEffect285PointStep point = NativeBattleEffect285Lower.BonePoint67B560(frame, local, TaskView(Read(frame, main), 12, 8), inputs);
            frame = Write(point.Frame, main, CopyTask(Read(point.Frame, main), 12, point.Output8, 8).WriteWord(18, 0));
            if ((uint)(age - 16) > 2)
            {
                continue;
            }
            for (int count = 0; count < 4; count++)
            {
                (frame, uint extra) = Append(frame, ParticleDescriptor, ParticleCallback);
                if (extra == 0)
                {
                    break;
                }
                for (int axis = 0; axis < 3; axis++)
                {
                    NativeCrtRandomState random = frame.Random.Draw(out ushort value);
                    frame = frame with { Random = random };
                    int coordinate = Signed(Read(frame, main), 12 + axis * 2) + (value & 511) - 256;
                    frame = Write(frame, extra, Read(frame, extra).WriteWord(12 + axis * 2, unchecked((ushort)coordinate)));
                }
                frame = Write(frame, extra, Read(frame, extra).WriteWord(18, 0));
            }
        }
        return frame;
    }
}
