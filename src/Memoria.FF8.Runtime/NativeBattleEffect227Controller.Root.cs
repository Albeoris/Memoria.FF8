using static Memoria.FF8.Runtime.NativeBattleEffect227Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect227Controller
{
    public static NativeBattleEffect227Step AdvanceRoot693840(NativeBattleEffect227Frame frame, uint address, NativeBattleEffect227Resources resources, NativeBattleEffect227Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool side = root.ReadByte(14) != 0;
        uint arena = frame.State.ReadDword(ArenaGlobal);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, arena + (uint)(side ? FirstPacketOffset : SecondPacketOffset)).WriteDword(SecondCursorGlobal, arena + (uint)(side ? SecondPacketOffset : ThirdPacketOffset)) };
        frame = Write(frame, address, root.WriteByte(14, side ? (byte)0 : (byte)1));
        if (Signed(root, 12) == 1 && root.ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            frame = InitializeRecords693960(frame);
            foreach (NativeBattleCureFamilyPool shape in CommonPools)
            {
                NativeBattleEffectTaskPool pool = CreatePool(frame, shape, true);
                frame = frame with { Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [pool]), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) } };
            }
            (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
            if (main == 0)
            {
                throw new InvalidDataException("Effect227 mandatory main allocation exhausted its newly initialized bank.");
            }
            frame = ClearDwords701200(frame, main + 12, 0x124);
            frame = inputs.Execute(frame, new NativeBattleEffect227VibrationStart4A29A0(VibrationAddress, 1, 128, main + 16));
        }
        bool initialized = Read(frame, address).ReadByte(15) != 0;
        if (initialized)
        {
            NativeBattleEffectArena local = NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(AnchorPointAddress, 8));
            NativeBattleEffect227PointStep anchor = inputs.BonePoint693990(frame, frame.State.ReadDword(CasterGlobal), local, frame.State.ReadView(AnchorAddress, 8));
            frame = anchor.Frame with { State = anchor.Frame.State.WriteView(AnchorAddress, anchor.Output8, 8) };
            (frame, int mainSurvivors) = AdvancePool(frame, 1, resources, inputs);
            (frame, _) = AdvancePool(frame, 2, resources, inputs);
            if (mainSurvivors == 0)
            {
                return new(frame, 2);
            }
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Signed(Read(frame, address), 12) + 1)))), 0);
    }

    public static NativeBattleEffect227Frame InitializeRecords693960(NativeBattleEffect227Frame frame)
    {
        uint first = frame.State.ReadDword(ArenaGlobal) + RecordOffset;
        for (int index = 0; index < RecordCount; index++)
        {
            frame = WriteBytes(frame, first + (uint)(index * RecordStride + 15), NativeBattleEffectArena.Clear(), 1);
        }
        return frame with { State = frame.State.WriteDword(RecordCursorGlobal, first) };
    }
}
