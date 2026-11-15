using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicShadow71Step(NativeBattleWaterExecutionFrame Frame, uint OutputAddress);
public sealed record NativeBattleCinematicShadow71Services(Func<BattleModelOwner, ushort> ReadActorFlags, Action<BattleModelOwner, ushort> WriteActorFlags, Func<NativeBattleWaterExecutionFrame, BattleModelOwner, uint, byte, uint, NativeBattleCinematicShadow71Step> DrawShadow);

/// <summary>The selected actor shadow flag and explicit current-output shadow pass.</summary>
public static class NativeBattleCinematicShadow71
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicShadow71Services services)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x1FF) != 0x71)
        {
            throw new NotSupportedException("Native explicit shadow requires its selected Main71 instruction.");
        }
        BattleModelOwner actor = NativeBattleCinematicActorAccess.ReadSlotActor(frame);
        ushort flags = services.ReadActorFlags(actor);
        if ((instruction & 0x8000) != 0)
        {
            services.WriteActorFlags(actor, unchecked((ushort)(flags & 0xFFDF)));
        }
        else
        {
            services.WriteActorFlags(actor, unchecked((ushort)(flags | 0x20)));
            uint control = CoreDword(frame, 0x27973B8);
            if (control != checked(frame.TemporaryFrame.Address + 0x100))
            {
                throw new NotSupportedException("Native explicit shadow requires its actual current dispatch context owner.");
            }
            uint manager = CoreDword(frame, 0x27973EC);
            NativeBattleCinematicShadow71Step step = services.DrawShadow(frame, actor, frame.ReadTemporaryDword(0x14C), 4, CoreDword(frame, unchecked(manager + 0x7C)));
            frame = WriteCoreDword(step.Frame, unchecked(manager + 0x7C), step.OutputAddress);
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 2);
    }
}
