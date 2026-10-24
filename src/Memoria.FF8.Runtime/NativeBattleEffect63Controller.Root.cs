using static Memoria.FF8.Runtime.NativeBattleEffect63Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect63Controller
{
    public static NativeBattleEffect63Step AdvanceRoot6E6CB0(NativeBattleEffect63Frame frame, uint address, NativeBattleEffect63Resources resources, NativeBattleEffect63Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool side = root.ReadByte(15) != 0;
        uint arena = frame.State.ReadDword(WorkGlobal);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(arena + (uint)(side ? FirstPacketOffset : SecondPacketOffset))) };
        frame = Write(frame, address, root.WriteByte(15, side ? (byte)0 : (byte)1));
        if (root.ReadWord(12) == 1 && root.ReadByte(14) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(14, 1));
            NativeBattleCureFamilyPool[] shapes = [new(MainDescriptor, MainOffset, MainStride, 1), new(SpriteDescriptor, 0, SpriteStride, SpriteCount)];
            foreach (NativeBattleCureFamilyPool shape in shapes)
            {
                NativeBattleEffectTaskPool pool = CreatePool(frame, shape, true);
                frame = frame with { Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [pool]), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) } };
            }
            (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
            if (main == 0)
            {
                throw new InvalidDataException("Effect63 mandatory main allocation failed.");
            }
            frame = ClearDwords701200(frame, main + 12, 0x37F);
            frame = NativeBattleEffect63Programme.Initialize(frame, main + 0x18, FirstProgrammeAddress, 0xE8, resources);
            frame = NativeBattleEffect63Programme.Initialize(frame, main + 0x100, FirstProgrammeAddress, 0xE8, resources);
            frame = NativeBattleEffect63Programme.Initialize(frame, main + 0x1E8, SecondProgrammeAddress, 0x6A0, resources);
            frame = NativeBattleEffect63Programme.Initialize(frame, main + 0x888, ThirdProgrammeAddress, 0x580, resources);
            NativeBattleEffect63PointStep anchor = inputs.Anchor571400(frame, frame.State.ReadDword(TargetGlobal), TaskView(Read(frame, main), 0x10, 8));
            frame = Write(anchor.Frame, main, CopyTask(Read(anchor.Frame, main), 0x10, anchor.Output8, 8));
        }
        bool initialized = Read(frame, address).ReadByte(14) != 0;
        int mains = 1;
        if (initialized)
        {
            (frame, mains) = AdvancePool(frame, 1, resources, inputs);
            (frame, _) = AdvancePool(frame, 2, resources, inputs);
        }
        if (initialized && mains == 0)
        {
            return new(frame, 2);
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)))), 0);
    }
}
