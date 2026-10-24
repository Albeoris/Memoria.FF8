using static Memoria.FF8.Runtime.NativeBattleEffect319Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect319Controller
{
    public static NativeBattleEffect319Step AdvanceRoot65DBE0(NativeBattleEffect319Frame frame, uint address, NativeBattleEffect319Resources resources, NativeBattleEffect319Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        int age = Signed(root, 12);
        bool side = root.ReadDword(16) != 0;
        uint arena = frame.State.ReadDword(ArenaGlobal);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, arena + (uint)(side ? FirstPacketOffset : SecondPacketOffset)) };
        frame = Write(frame, address, root.WriteDword(16, side ? 0u : 1u));
        if (age == 1)
        {
            if (Read(frame, address).ReadByte(14) == 0)
            {
                frame = Write(frame, address, Read(frame, address).WriteByte(14, 1));
                foreach (NativeBattleCureFamilyPool shape in CommonPools)
                {
                    NativeBattleEffectTaskPool pool = CreatePool(frame, shape, true);
                    frame = frame with { Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [pool]), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) } };
                }
            }
            (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
            if (main == 0)
            {
                throw new InvalidDataException("Effect319 requires its mandatory main task allocation.");
            }
            frame = ClearDwords701200(frame, main + 12, 0x223);
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            int contact = FirstContact(frame, packets);
            uint target = ModelAddress((packets.Contacts[contact] ?? throw new NotSupportedException("Effect319 requires its current first contact.")).Primary.ActorIndex);
            frame = Write(frame, main, Read(frame, main).WriteDword(0x58, target));
            NativeBattleEffect319PointStep anchor = inputs.Anchor571400(frame, target, TaskView(Read(frame, main), 0x64, 8));
            frame = Write(anchor.Frame, main, CopyTask(Read(anchor.Frame, main), 0x64, anchor.Output8, 8));
            frame = NativeBattleEffect319Programme.Initialize(frame, main + 0xC4, 0xFF24F8, 0x368, resources);
            frame = NativeBattleEffect319Programme.Initialize(frame, main + 0x42C, 0xFF4350, 0x46C, resources);
            frame = inputs.Execute(frame, new NativeBattleEffect319Initialize534110(arena + IoOffset));
        }
        bool initialized = Read(frame, address).ReadByte(14) != 0;
        int survivors = 0;
        if (initialized)
        {
            for (int index = 1; index <= 3; index++)
            {
                (frame, int count) = AdvancePool(frame, index, resources, inputs);
                survivors += count;
            }
        }
        if (age >= 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect319Advance534210());
        }
        if (initialized)
        {
            if (survivors == 0)
            {
                NativeBattleEffect319PollStep poll = inputs.IoSelect5342C0(frame, 0);
                frame = poll.Frame;
                if (poll.Complete)
                {
                    return new(frame, 2);
                }
            }
            frame = Write(frame, address, Read(frame, address).WriteWord(12, 0x40));
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)))), 0);
    }

    private static int FirstContact(NativeBattleEffect319Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect319 requires its current action header.");
        return packets.Headers[parameters.HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Effect319 requires its actual first contact pointer.");
    }
}
