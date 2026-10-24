using static Memoria.FF8.Runtime.NativeBattleEffect318Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect318Controller
{
    public static NativeBattleEffect318Step AdvanceRoot65E5C0(NativeBattleEffect318Frame frame, uint address, NativeBattleEffect318Resources resources, NativeBattleEffect318Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool side = root.ReadDword(16) != 0;
        uint arena = frame.State.ReadDword(ArenaGlobal);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, arena + (uint)(side ? FirstPacketOffset : SecondPacketOffset)) };
        frame = Write(frame, address, root.WriteDword(16, side ? 0u : 1u));
        if (Signed(root, 12) == 1)
        {
            if (root.ReadByte(14) == 0)
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
                throw new InvalidDataException("Effect318 requires its mandatory main allocation.");
            }
            frame = InitializeMain(frame, main, resources, inputs);
            frame = inputs.Execute(frame, new NativeBattleEffect318Initialize534110(arena + IoOffset));
        }
        bool initialized = Read(frame, address).ReadByte(14) != 0;
        int mains = 1;
        int bones = 1;
        int meshes = 1;
        if (initialized)
        {
            (frame, mains) = AdvancePool(frame, 1, resources, inputs);
            (frame, bones) = AdvancePool(frame, 2, resources, inputs);
            (frame, meshes) = AdvancePool(frame, 3, resources, inputs);
        }
        if (initialized && mains == 0 && bones == 0 && meshes == 0)
        {
            return new(frame, 2);
        }
        if (initialized)
        {
            return new(Write(frame, address, Read(frame, address).WriteWord(12, 2)), 0);
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)))), 0);
    }

    public static NativeBattleEffect318Frame InitializeMain(NativeBattleEffect318Frame frame, uint main, NativeBattleEffect318Resources resources, NativeBattleEffect318Inputs inputs)
    {
        frame = ClearDwords701200(frame, main + 12, 0x4BE);
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleActionHeaderSlot header = CurrentHeader(frame, packets);
        if (header.ReadByte(16) == 0)
        {
            throw new NotSupportedException("Effect318 requires its actual nonempty first-contact source.");
        }
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Effect318 requires its native first contact pointer.");
        uint target = ModelAddress(packets.ReadContact(first).Primary.ActorIndex);
        frame = Write(frame, main, Read(frame, main).WriteDword(0x58, target));
        NativeBattleEffect318PointStep anchor = inputs.Anchor571400(frame, target, TaskView(Read(frame, main), 0x64, 8));
        frame = Write(anchor.Frame, main, CopyTask(Read(anchor.Frame, main), 0x64, anchor.Output8, 8).WriteDword(RecordCursorOffset, checked(main + RecordOffset)));
        frame = NativeBattleEffect318Programme.Initialize(frame, main + FirstWorkspaceOffset, FirstProgrammeAddress, FirstWorkspaceLength, resources);
        frame = NativeBattleEffect318Programme.Initialize(frame, main + SecondWorkspaceOffset, SecondProgrammeAddress, SecondWorkspaceLength, resources);
        return NativeBattleEffect318Programme.Initialize(frame, main + ThirdWorkspaceOffset, ThirdProgrammeAddress, ThirdWorkspaceLength, resources);
    }

    private static NativeBattleActionHeaderSlot CurrentHeader(NativeBattleEffect318Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect318 requires its current action header.");
        return packets.Headers[parameters.HeaderIndex7C];
    }

    private static int FirstContact(NativeBattleEffect318Frame frame, NativeBattleEffect318Inputs inputs) => CurrentHeader(frame, inputs.ReadPackets(frame)).FirstContactIndex ?? throw new NotSupportedException("Effect318 requires its current native contact pointer.");
}
