using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect290Controller
{
    public static NativeBattleEffect290Step AdvanceRoot675340(NativeBattleEffect290Frame frame, uint address, NativeBattleEffect290Resources resources, NativeBattleEffect290Inputs inputs)
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
            frame = inputs.Execute(frame, new NativeBattleEffect290Music47E3C0(240));
            foreach (NativeBattleCureFamilyPool shape in CommonPools)
            {
                NativeBattleEffectTaskPool pool = CreatePool(frame, shape, true);
                frame = frame with { Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [pool]), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) } };
            }
            (frame, uint main) = AppendRequired(frame, MainDescriptor, MainCallback);
            frame = ClearDwords701200(frame, main + 12, 0x1A36);
            frame = inputs.Execute(frame, new NativeBattleEffect290VibrationStart4A29A0(VibrationAddress, 1, 128, main + 16));
            frame = WriteBytes(frame, main + 20, CopyBytes(inputs.ReadModelArena9C(frame, frame.State.ReadDword(CasterGlobal)), 44, NativeBattleEffectArena.Clear().MarkUnknown(0, 4), 0, 4), 4);
            frame = Write(frame, main, Read(frame, main).WriteDword(0x64D8, main + 0x1D8).WriteDword(0x64DC, main + 0x5ED8));
            frame = inputs.Execute(frame, new NativeBattleEffect290InitializeProgramme7016B0(main + 0x64E0, FirstProgrammeAddress, 0x110));
            frame = inputs.Execute(frame, new NativeBattleEffect290InitializeProgramme7016B0(main + 0x65F0, SecondProgrammeAddress, 0x22C));
            frame = inputs.Execute(frame, new NativeBattleEffect290InitializeProgramme7016B0(main + 0x681C, ThirdProgrammeAddress, 0xC8));
            foreach ((uint source, _) in NativeBattleEffect290Resources.ScriptRanges)
            {
                frame = BirthScript675650(frame, source, resources);
            }
            frame = inputs.Execute(frame, new NativeBattleEffect290ModelFlags(frame.State.ReadDword(CasterGlobal), 2, 0xFFF7, 0));
            frame = inputs.Execute(frame, new NativeBattleEffect290InitializeIo534110(IoOffset));
        }
        bool initialized = Read(frame, address).ReadByte(15) != 0;
        int mains = 0;
        if (initialized)
        {
            (frame, mains) = AdvanceDescriptor(frame, MainDescriptor, resources, inputs);
            (frame, _) = AdvanceDescriptor(frame, ScriptDescriptor, resources, inputs);
            (frame, _) = AdvanceDescriptor(frame, TargetDescriptor, resources, inputs);
            (frame, _) = AdvanceDescriptor(frame, ParticleDescriptor, resources, inputs);
            (frame, _) = AdvanceDescriptor(frame, SpriteDescriptor, resources, inputs);
        }
        if (age == 80)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect290QueueFile5341D0(360, checked(frame.State.ReadDword(ArenaGlobal) + File360Offset), 0));
        }
        if (initialized)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect290AdvanceIo534210());
            if (mains == 0)
            {
                NativeBattleEffect290PollStep poll = inputs.IoSelect5342C0(frame, 0);
                frame = poll.Frame;
                if (!poll.Complete)
                {
                    return new(frame, 0);
                }
                frame = inputs.Execute(frame, new NativeBattleEffect290ModelFlags(frame.State.ReadDword(CasterGlobal), 2, 0xFFF7, 0));
                return new(frame, 2);
            }
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    public static (NativeBattleEffect290Frame Frame, int Survivors) AdvanceDescriptor(NativeBattleEffect290Frame frame, uint descriptor, NativeBattleEffect290Resources resources, NativeBattleEffect290Inputs inputs)
    {
        int index = Enumerable.Range(0, frame.Effect.Pools.Length).Single(index => frame.Effect.Pools[index].DescriptorAddress == descriptor);
        return AdvancePool(frame, index, resources, inputs);
    }

    public static (NativeBattleEffect290Frame Frame, uint Address) AppendRequired(NativeBattleEffect290Frame frame, uint descriptor, uint callback)
    {
        (frame, uint address) = Append(frame, descriptor, callback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect290 requires an available native task slot.");
        }
        return (frame, address);
    }
}
