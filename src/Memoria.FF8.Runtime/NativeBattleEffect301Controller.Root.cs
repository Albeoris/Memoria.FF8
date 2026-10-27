using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect301Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect301Controller
{
    public static NativeBattleEffect301Step AdvanceRoot66D9F0(NativeBattleEffect301Frame frame, uint address, NativeBattleEffect301Resources resources, NativeBattleEffect301Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        bool side = Read(frame, address).ReadDword(16) != 0;
        uint arena = frame.State.ReadDword(ArenaGlobal);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, arena + (uint)(side ? FirstPacketOffset : SecondPacketOffset)) };
        frame = Write(frame, address, Read(frame, address).WriteDword(16, side ? 0u : 1u));
        uint caster = frame.State.ReadDword(CasterGlobal);
        frame = inputs.Execute(frame, new NativeBattleEffect301ModelFlags(caster, 1, 255, 12));
        if (age == 1 && Read(frame, address).ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            foreach (NativeBattleCureFamilyPool shape in CommonPools)
            {
                NativeBattleEffectTaskPool pool = CreatePool(frame, shape, true);
                frame = frame with { Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [pool]), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) } };
            }
            (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
            if (main == 0)
            {
                throw new InvalidDataException("Effect301 requires its mandatory main task.");
            }
            frame = ClearDwords701200(frame, main + 12, 0x382);
            frame = WriteBytes(frame, main + 0x4AC, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)255, 2400).ToArray()), 2400);
            frame = inputs.Execute(frame, new NativeBattleEffect301VibrationStart4A29A0(VibrationAddress, 1, 128, main + 16));
            frame = NativeBattleEffect301Programme.Initialize(frame, main + 0x1C, ProgrammeAddress, 0x90, resources);
            NativeBattleEffect301PointStep anchor = inputs.Anchor571400(frame, caster, TaskView(Read(frame, main), 20, 8));
            frame = Write(anchor.Frame, main, CopyTask(Read(anchor.Frame, main), 20, anchor.Output8, 8).WriteWord(22, 0));
            NativeBattleEffectArena color = CopyBytes(inputs.ReadModelArena9C(frame, caster), 44, NativeBattleEffectArena.Clear().MarkUnknown(0, 4), 0, 4);
            frame = Write(frame, main, CopyTask(Read(frame, main), 0xE10, color, 4).WriteDword(0xE0C, main + 0xAC));
        }
        bool initialized = Read(frame, address).ReadByte(15) != 0;
        int mains = 0;
        if (initialized)
        {
            (frame, mains) = AdvancePool(frame, 1, resources, inputs);
            (frame, _) = AdvancePool(frame, 2, resources, inputs);
            (frame, _) = AdvancePool(frame, 3, resources, inputs);
        }
        if (initialized && mains == 0)
        {
            for (int actor = 0; actor < 3; actor++)
            {
                ushort current = BinaryPrimitives.ReadUInt16LittleEndian(inputs.ReadModelArena9C(frame, ModelAddress(actor)).Read(0, 2));
                ushort saved = unchecked((ushort)frame.State.ReadDword(SavedPartyFlagsAddress + (uint)(actor * 4)));
                ushort restored = (ushort)(current ^ ((current ^ saved) & 4));
                frame = inputs.Execute(frame, new NativeBattleEffect301WriteModelBytes(ModelAddress(actor), 0, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(restored)), 2));
            }
            frame = inputs.Execute(frame, new NativeBattleEffect301ModelFlags(caster, 2, 0xFFF3, 0));
            return new(frame, 2);
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }
}
