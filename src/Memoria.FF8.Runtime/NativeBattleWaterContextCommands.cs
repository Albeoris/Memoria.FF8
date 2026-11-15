using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterContextServices(
    Func<NativeBattleWaterExecutionFrame, BattleModelOwner, BattleModelOwner?> ReadLinkedModel,
    Func<NativeBattleWaterExecutionFrame, BattleModelOwner, BattleModelOwner> ResolveRing501FF0,
    Func<NativeBattleWaterExecutionFrame, uint, int, byte[]> ReadActionBytes,
    Func<NativeBattleWaterExecutionFrame, BattleContactOccurrence, NativeBattleWaterExecutionFrame> ApplyContact506690,
    Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> ApplyCamera50A730,
    Func<NativeBattleWaterExecutionFrame, int, short, uint, NativeBattleWaterExecutionFrame> WriteStageColor,
    Func<NativeBattleWaterExecutionFrame, BattleActionHeaderReference?> ReadCurrentHeader,
    Func<NativeBattleActionPacketsState> ReadPackets,
    Func<NativeBattleModelAliases> CaptureAliases)
{
    internal BattleActionHeaderReference RequireCombatHeader(NativeBattleWaterExecutionFrame frame)
    {
        return ReadCurrentHeader(frame) ?? throw new InvalidDataException("Presentation-only Ifrit cannot apply offensive contacts before its landing handoff.");
    }
}

/// <summary>Selected current actor context, contact, camera and stage-color operations.</summary>
public static class NativeBattleWaterContextCommands
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterContextServices services)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x1FF) == 0xCF)
        {
            return ExecuteRing(frame, services);
        }
        switch (instruction)
        {
            case 0x1022:
                return NativeBattleIfritContactCommand.ExecuteSavedActors(frame, services);
            case 0x1039:
                frame = services.ApplyCamera50A730(frame);
                return Advance(4);
            case 0x0020:
                uint packed = 0;
                for (int lane = 0; lane < 3; lane++)
                {
                    int color = unchecked((short)SlotWord(frame, 0x94 + 2 * lane));
                    frame = frame.WriteTemporaryDword(0xF0 + 4 * lane, unchecked((uint)color));
                }
                for (int lane = 0; lane < 3; lane++)
                {
                    int color = Math.Clamp(unchecked((int)frame.ReadTemporaryDword(0xF0 + 4 * lane)), 0, 255);
                    frame = frame.WriteTemporaryDword(0xF0 + 4 * lane, (uint)color);
                    packed |= (uint)color << (8 * lane);
                }
                frame = frame.WriteTemporaryDword(0xFC, packed);
                short depth = (short)Math.Clamp(unchecked((short)SlotWord(frame, 0x8C)) << 4, 0, 0x1000);
                for (int i = 0; i < 4; i++)
                {
                    frame = services.WriteStageColor(frame, i, depth, frame.ReadTemporaryDword(0xFC));
                }
                return Advance(2);
            default:
                throw new NotSupportedException("Native context commands admit only their selected raw mode words.");
        }

        NativeBattleWaterExecutionFrame Advance(uint amount) => WriteCoreDword(frame, 0x2797450, unchecked(CoreDword(frame, 0x2797450) + amount));
    }

    public static NativeBattleWaterExecutionFrame ExecuteRing(NativeBattleWaterExecutionFrame frame, NativeBattleWaterContextServices services)
    {
        int mode = frame.ReadTemporaryWord(0x14A) >> 12;
        if (mode > 1)
        {
            return frame;
        }
        BattleModelOwner actor = NativeBattleCinematicActorAccess.ReadSlotActor(frame);
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorAccess.ReadContext(frame);
        uint header = NativeBattleScriptedEffectMemory.Address;
        if (services.ReadLinkedModel(frame, actor) is null)
        {
            NativeBattleScriptedEffectMemory core = frame.Core.Write(header + 0x17A, [0]);
            frame = frame with { Core = core with { PresentationActors = context.WithSavedActors([]) } };
            return NativeBattleScriptedCommandReads.Advance(frame, 2);
        }
        ImmutableArray<BattleModelOwner> saved = mode == 0 ? [actor] : context.CurrentActors;
        BattleModelOwner minimum = services.ResolveRing501FF0(frame, actor);
        NativeBattleCinematicActorContext updated = context.WithSavedActors(saved).WithCurrentActors([minimum]);
        NativeBattleModelAliases aliases = services.CaptureAliases();
        NativeBattleScriptedEffectMemory written = saved.Length <= byte.MaxValue
            ? frame.Core.Write(header + 0x17A, [(byte)saved.Length])
            : frame.Core.MarkUnknown(header + 0x17A, 1);
        int projectedCount = saved.All(owner => aliases.TryEncodeExact(owner, out _)) ? Math.Min(saved.Length, 7) : Math.Min(saved.Length, 6);
        for (int index = projectedCount - 1; index >= 0; index--)
        {
            written = WriteActor(written, header + 0x174 + (uint)index, saved[index], aliases);
        }
        written = WriteActor(written, header + 0x48, minimum, aliases).Write(header + 0x41, [1]);
        frame = frame with { Core = written with { PresentationActors = updated } };
        frame = NativeBattleCinematicActorAccess.WriteSlotActor(frame, minimum, aliases);
        return NativeBattleScriptedCommandReads.Advance(frame, 2);
    }

    internal static NativeBattleScriptedEffectMemory WriteActor(NativeBattleScriptedEffectMemory core, uint address, BattleModelOwner actor, NativeBattleModelAliases aliases)
    {
        return aliases.EncodeOperand(actor, 1) is { } encoded ? core.Write(address, encoded.AsSpan()) : core.MarkUnknown(address, 1);
    }
}
