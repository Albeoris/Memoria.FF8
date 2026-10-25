using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect308Controller
{
    public static NativeBattleEffect308Step AdvanceRoot667720(NativeBattleEffect308Frame frame, uint address, NativeBattleEffect308Resources resources, NativeBattleEffect308Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        short age = Signed(root, 12);
        bool side = root.ReadDword(16) != 0;
        uint arena = frame.State.ReadDword(ArenaGlobal);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(arena + (uint)(side ? FirstPacketOffset : SecondPacketOffset))) };
        frame = Write(frame, address, root.WriteDword(16, side ? 0u : 1u));
        if (age == 1 && Read(frame, address).ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            frame = inputs.Execute(frame, new NativeBattleEffect308Music47E3C0(240));
            frame = InitializeCommonBanks(frame);
            (frame, uint main) = AppendRequired(frame, MainDescriptor, MainCallback);
            frame = ClearDwords701200(frame, main + 12, 0x3A2);
            frame = inputs.Execute(frame, new NativeBattleEffect308VibrationStart4A29A0(VibrationAddress, 1, 128, main + 16));
            uint model = frame.State.ReadDword(CasterGlobal);
            for (int index = 0; index < 3; index++)
            {
                uint current = checked(model + (uint)(index * 156));
                _ = Actor(current);
                frame = WriteBytes(frame, checked(main + 20 + (uint)(index * 4)), CopyBytes(inputs.ReadModelArena9C(frame, current), 44, NativeBattleEffectArena.Clear().MarkUnknown(0, 4), 0, 4), 4);
            }
            frame = Write(frame, main, Read(frame, main).WriteDword(0xE90, main + 0x890));
            for (int index = 0; index < ScriptRanges.Length; index++)
            {
                frame = BirthScript667940(frame, checked(model + (uint)(index * 156)), checked(main + 0x20 + (uint)(index * 0x2D0)), ScriptRanges[index].Address);
            }
            frame = inputs.Execute(frame, new NativeBattleEffect308InitializeIo534110(IoOffset));
        }
        bool initialized = Read(frame, address).ReadByte(15) != 0;
        int mains = 0;
        if (initialized)
        {
            (frame, mains) = AdvanceDescriptor(frame, MainDescriptor, resources, inputs);
            (frame, _) = AdvanceDescriptor(frame, ScriptDescriptor, resources, inputs);
            (frame, _) = AdvanceDescriptor(frame, ActDescriptor, resources, inputs);
            (frame, _) = AdvanceDescriptor(frame, ParticleDescriptor, resources, inputs);
            frame = inputs.Execute(frame, new NativeBattleEffect308AdvanceIo534210());
            if (mains == 0)
            {
                NativeBattleEffect308PollStep poll = inputs.IoPoll5342C0(frame, 1);
                frame = poll.Frame;
                if (poll.Busy)
                {
                    return new(frame, 0);
                }
                return new(inputs.Execute(frame, new NativeBattleEffect308Intensity5713E0(0, 0)), 2);
            }
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    public static (NativeBattleEffect308Frame Frame, int Survivors) AdvanceDescriptor(NativeBattleEffect308Frame frame, uint descriptor, NativeBattleEffect308Resources resources, NativeBattleEffect308Inputs inputs)
    {
        int index = Enumerable.Range(0, frame.Effect.Pools.Length).Single(index => frame.Effect.Pools[index].DescriptorAddress == descriptor);
        return AdvancePool(frame, index, resources, inputs);
    }

    public static (NativeBattleEffect308Frame Frame, uint Address) AppendRequired(NativeBattleEffect308Frame frame, uint descriptor, uint callback)
    {
        (frame, uint address) = Append(frame, descriptor, callback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect308 requires an available native task slot.");
        }
        return (frame, address);
    }
}
