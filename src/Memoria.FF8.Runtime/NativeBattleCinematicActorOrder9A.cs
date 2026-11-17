using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicActorOrder9A
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterContextServices services)
    {
        int count = new NativeBattleCinematicInstructionLayout(frame.ReadTemporaryWord(0x14A)).ActorOrderCount;
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorAccess.ReadContext(frame);
        NativeBattleModelAliases aliases = services.CaptureAliases();
        bool original = services.StorageContract == BattleContactTaskStorageContract.OriginalArena;
        uint header = NativeBattleScriptedEffectMemory.Address;
        frame = frame with { Core = frame.Core.Write(header + 0x41, [(byte)count]) };
        frame = NativeBattleScriptedCommandReads.Advance(frame, 2);
        ImmutableArray<BattleModelOwner>.Builder actors = ImmutableArray.CreateBuilder<BattleModelOwner>(count);
        for (int index = 0; index < count; index++)
        {
            byte literal = NativeBattleScriptedCommandReads.Program(frame, program, 0, 1)[0];
            BattleModelOwner actor = aliases.DecodeAuthoredOperand(literal);
            actors.Add(actor);
            if (original)
            {
                frame = frame with { Core = frame.Core.Write(header + 0x48 + (uint)index, [literal]) };
            }
            else if (index < 6)
            {
                frame = frame with { Core = NativeBattleWaterContextCommands.WriteActor(frame.Core, header + 0x48 + (uint)index, actor, aliases) };
            }
            frame = NativeBattleScriptedCommandReads.Advance(frame, 2);
        }
        context = context.WithCurrentActors(actors.MoveToImmutable());
        if (original && count > 6)
        {
            foreach (BattleModelOwner actor in context.Owners)
            {
                if (aliases.TryEncodeExact(actor, out byte slot) && 6 + 2 * slot < count)
                {
                    NativeBattleCinematicActorCache cached = context.ReadCachedActor(actor);
                    ushort flags = CoreWord(frame, header + 0x4E + 2u * slot);
                    context = context with { CachedActors = context.CachedActors.SetItem(actor, cached with { Flags0 = flags }) };
                }
            }
        }
        return frame with { Core = frame.Core with { PresentationActors = context } };
    }
}
