using static Memoria.FF8.Runtime.NativeBattleEffect322Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect322Controller
{
    public static NativeBattleEffect322Step AdvanceRoot65AC20(NativeBattleEffect322Frame frame, uint address, NativeBattleEffect322Resources resources, NativeBattleEffect322Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        int age = Signed(root, 12);
        uint arena = frame.State.ReadDword(ArenaGlobal);
        bool side = root.ReadDword(16) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, arena + (uint)(side ? FirstPacketOffset : SecondPacketOffset)) };
        frame = Write(frame, address, root.WriteDword(16, side ? 0u : 1u));
        if (age == 1 && root.ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            foreach (NativeBattleCureFamilyPool shape in CommonPools)
            {
                NativeBattleEffectTaskPool pool = CreatePool(frame, shape, true);
                frame = frame with { Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [pool]), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) } };
            }
            (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
            if (main != 0)
            {
                frame = ClearDwords701200(frame, main + 12, 0x1C);
                int first = FirstContact(frame, inputs);
                uint target = ModelAddress(inputs.ReadPackets(frame).ReadContact(first).Primary.ActorIndex);
                frame = Write(frame, main, Read(frame, main).WriteDword(0x10, target));
                NativeBattleEffect322PointStep point = inputs.Anchor571400(frame, target, TaskView(Read(frame, main), 0x14, 8));
                frame = Write(point.Frame, main, CopyTask(Read(point.Frame, main), 0x14, point.Output8, 8));
            }
            frame = inputs.Execute(frame, new NativeBattleEffect322IoInitialize534110(arena + IoOffset));
        }
        bool initialized = Read(frame, address).ReadByte(15) != 0;
        if (initialized)
        {
            for (int index = 1; index <= CommonPools.Length; index++)
            {
                (frame, _) = AdvancePool(frame, index, resources, inputs);
            }
        }
        if (age == 59)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect322IoQueue5341D0(360, arena + ThirdPacketOffset, 0));
        }
        if (initialized)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect322IoAdvance534210());
        }
        if (age > 59)
        {
            NativeBattleEffect322IoCompletionStep completion = inputs.Select5342C0(frame, 0);
            return new(completion.Frame, completion.Complete ? 2 : 0);
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    private static int FirstContact(NativeBattleEffect322Frame frame, NativeBattleEffect322Inputs inputs)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect322 requires its current action header.");
        return inputs.ReadPackets(frame).Headers[parameters.HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Effect322 requires its native first contact pointer.");
    }
}
