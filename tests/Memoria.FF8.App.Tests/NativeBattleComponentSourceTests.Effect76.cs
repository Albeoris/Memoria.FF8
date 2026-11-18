using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static string OriginalSourceActorFacts(BattleCombatant actor)
    {
        return $"id={actor.Id}, model={actor.ModelId}, scene={actor.SceneSlot}, slot={actor.Slot}, native={actor.NativeActorIndex}, active={actor.IsActive}, hidden={actor.Hidden}, suspended={actor.Suspended}, untargetable={actor.Untargetable}, template={actor.NativeTemplate is not null}, initialActive={actor.SourceDefinition.InitialActive}, resolvedLevel={actor.ResolvedNativeLevel}, pendingStatistics={actor.NativeAllocationPendingStatistics}, flags={actor.NativeCombatantFlags:X}";
    }

    internal static BattleSession PrepareOriginalEffect76Component(GameWorld world, EffectPreviewRequest request)
    {
        if (request.EnemyId != 126 || request.StageId != 142)
        {
            throw new InvalidDataException("The component requires its original caster and native stage owner.");
        }
        BattleSession source = world.StartDiagnosticBattle(new(981, request.Seed, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            AtbMode = BattleAtbMode.Normal,
            Fixture = new([], [], true) { CombatParty = [0, 255, 255] }
        });
        for (int callback = 0; callback < 3600 && source.Outcome == BattleOutcome.Active; callback++)
        {
            BattlePresentationProcess process = source.Presentation!;
            if (source.NativeEntryReady && process.ActiveActionToken is not null && process.ActionPackets.PublishedHeaders().Any(entry => entry.Slot.Effect == 76))
            {
                BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 76);
                NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(new(admission.HeaderIndex));
                BattleCombatant actor = source.Combatants.Single(candidate => candidate.NativeActorIndex == 3);
                if (actor.ModelId != 126 || admission.Actor != process.FindOriginalActor(NativeBattleModelSlot.Decode(3)) || header.Command != 8 || header.Action != 355 || header.Sequence != 14 || process.NativeStage?.FinalStage142 is null)
                {
                    throw new InvalidDataException("The original startup changed its actual caster, command or stage owner.");
                }
                return source;
            }
            source.Tick();
        }
        throw new InvalidDataException("The original encounter did not publish its unstarted startup component within the preparation budget.");
    }
}
