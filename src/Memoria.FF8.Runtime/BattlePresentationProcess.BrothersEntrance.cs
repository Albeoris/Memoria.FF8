using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    // The root waits through both 87-frame gestures and the result camera before it releases the attack poses.
    private const int BrothersEntranceEnd = 0x126;
    private const byte MinotaurBody = 78;
    private const byte SacredBody = 79;
    private const byte MinotaurRitualShape = 0x0F;
    private const byte SacredRitualShape = 0x10;

    private static bool IsBrothersRitualWait(NativeBattleWaterExecutionFrame frame, uint pc)
    {
        uint primary = CoreDword(frame, 0x2798A68);
        return pc - primary is 0x10E or 0x110 or 0x112 or 0x114 or 0x116 or 0x120 or
            0x16BE or 0x16C2 or 0x16C4 or 0x16CA or 0x16D0 or
            0x1782 or 0x1786 or 0x178A or 0x1790 or 0x1796;
    }

    private static NativeBattleWaterExecutionFrame RegisterBrothersEntranceModel(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleCinematicResources resources, NativeBattleCinematicModelServices services)
    {
        uint offset = CoreDword(frame, 0x2797450) - CoreDword(frame, 0x2798A68);
        ushort role = offset switch { 0x16AA => 0x0110, 0x1766 => 0x000F, _ => 0 };
        if (role == 0)
        {
            return NativeBattleCinematicModel.Register(frame, program, services);
        }
        if (BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2)) != role)
        {
            throw new InvalidDataException("The original Brothers gesture actor does not match its selected entrance role.");
        }
        short artShape = (short)(role & 0xFF);
        short otherShape = artShape == SacredRitualShape ? MinotaurRitualShape : SacredRitualShape;
        byte animation = artShape == SacredRitualShape ? (byte)0 : (byte)1;
        frame = NativeBattleCinematicModel.Register(frame, program, services);
        uint model = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
        uint art = NativeBattleWaterShapeSelection.ResolveCinematic(frame, resources, artShape).Selected;
        uint gesture = NativeBattleWaterShapeSelection.ResolveCinematic(frame, resources, otherShape).Selected;
        uint ownSkeleton = art + ReadShapeWord(frame, art + 4);
        uint otherSkeleton = gesture + ReadShapeWord(frame, gesture + 4);
        int bones = frame.Arena.Read(ArenaOffset(ownSkeleton, 1), 1)[0];
        if (bones != 39 || frame.Arena.Read(ArenaOffset(otherSkeleton, 1), 1)[0] != bones)
        {
            throw new InvalidDataException("The original Brothers gesture clips require their 39-bone rigs.");
        }
        for (int bone = 0; bone < bones; bone++)
        {
            uint transform = checked((uint)(16 + bone * 48));
            if (!frame.Arena.Read(ArenaOffset(ownSkeleton + transform, 2), 2).AsSpan().SequenceEqual(frame.Arena.Read(ArenaOffset(otherSkeleton + transform, 2), 2)))
            {
                throw new InvalidDataException("The original Brothers gesture clips require matching bone parent indices.");
            }
        }
        uint animations = gesture + ReadShapeWord(frame, gesture + 12);
        uint clip = animations + ReadShapeWord(frame, animations + 4u + 4u * animation);
        if (frame.Arena.Read(ArenaOffset(clip, 1), 1)[0] != 87)
        {
            throw new InvalidDataException("The paired Brothers gesture clips must share their original frame count.");
        }
        // Both rigs retain their mesh, scale and proportions. The mirrored result uses the other participant's gesture.
        frame = frame with { Arena = frame.Arena.Write(ArenaOffset(model + 9, 1), [animation]).Write(ArenaOffset(model + 0x38, 4), BitConverter.GetBytes(animations)) };
        return NativeBattleCinematicModel.Select509440(frame, model, animation, services);
    }

    private static NativeBattleWaterExecutionFrame RegisterBrothersFinisherBody(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleCinematicResources resources, NativeBattleCinematicModelServices services)
    {
        ushort role = BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        if (role is not (0x0210 or 0x0310))
        {
            throw new InvalidDataException("Brotherly Love requires its original falling body.");
        }
        frame = NativeBattleCinematicModel.Register(frame, program, services);
        uint model = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
        uint shape = NativeBattleWaterShapeSelection.ResolveCinematic(frame, resources, MinotaurRitualShape).Selected;
        uint skeleton = shape + ReadShapeWord(frame, shape + 4);
        int bones = frame.Arena.Read(ArenaOffset(skeleton, 1), 1)[0];
        if (bones != 39)
        {
            throw new InvalidDataException("Brotherly Love requires the selected participant's 39-bone rig.");
        }
        int length = 16 + bones * 48;
        uint copy;
        (frame, copy) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, checked((uint)length));
        uint mesh = shape + ReadShapeWord(frame, shape + 8);
        frame = frame with { Arena = frame.Arena.Write(ArenaOffset(copy, length), frame.Arena.Read(ArenaOffset(skeleton, length), length)).Write(ArenaOffset(model + 0x30, 4), BitConverter.GetBytes(copy)).Write(ArenaOffset(model + 0x34, 4), BitConverter.GetBytes(mesh)) };
        return NativeBattleCinematicModel.Select509440(frame, model, checked((byte)(role >> 8)), services);
    }

    private static uint ReadBrothersEntrancePoseOwner(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicResources resources, uint shape, uint animation)
    {
        if (animation == shape + ReadShapeWord(frame, shape + 12))
        {
            return shape;
        }
        uint minotaur = NativeBattleWaterShapeSelection.ResolveCinematic(frame, resources, MinotaurRitualShape).Selected;
        uint sacred = NativeBattleWaterShapeSelection.ResolveCinematic(frame, resources, SacredRitualShape).Selected;
        if (shape == minotaur && animation == sacred + ReadShapeWord(frame, sacred + 12))
        {
            return sacred;
        }
        if (shape == sacred && animation == minotaur + ReadShapeWord(frame, minotaur + 12))
        {
            return minotaur;
        }
        throw new InvalidDataException("The Brothers gesture animation leaves its original embedded model owner.");
    }

    private static uint ReadShapeWord(NativeBattleWaterExecutionFrame frame, uint address)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
    }
}
