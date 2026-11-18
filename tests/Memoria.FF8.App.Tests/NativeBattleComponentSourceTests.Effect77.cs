using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect77Component(GameWorld world, EffectPreviewRequest request)
    {
        if (request.EnemyId != 126 || request.StageId != 142)
        {
            throw new InvalidDataException("The component requires its original caster and native stage owner.");
        }
        BattleSession initialized = world.StartDiagnosticBattle(new(981, request.Seed, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            AtbMode = BattleAtbMode.Normal,
            Fixture = new([], [new(0, request.AllyId, [], [])], true) { CombatParty = [0, 255, 255] }
        });
        for (int callback = 0; callback < 3600 && (!initialized.NativeEntryReady || initialized.ObserveDiagnosticDrainBlockers().Count != 0); callback++)
        {
            initialized.AdvanceNativeGraphicsPresentation(PresentationTiming.SimulationStep);
        }
        if (!initialized.NativeEntryReady || initialized.ObserveDiagnosticDrainBlockers().Count != 0)
        {
            throw new InvalidDataException("The original component encounter did not finish its native initialization.");
        }
        BattleSnapshot baseline = initialized.CaptureForContinuation();
        NativeBattleGraphicsState baselineGraphics = baseline.ReadFixturePresentation().NativeGraphics ?? throw new InvalidDataException("The component requires its initialized native graphics owner.");
        // The private fixture retains unknown outcome bytes; instruction C1 publishes its own values.
        BattleSnapshot continuation = baseline.WithFixturePresentation(facts => facts with { NativeGraphics = baselineGraphics with { NativeOutcome = baselineGraphics.NativeOutcome ?? new NativeBattleOutcomeState() } });
        BattleSession source = InstalledBattlePresentation.Restore(continuation, world.ValidationAssets, world.BattleResources);
        BattleCombatant actor = source.Combatants.Single(candidate => !candidate.IsPlayer && candidate.NativeActorIndex == 3);
        if (actor.ModelId != 126 || actor.SceneSlot != 0 || source.ReadDefinition().EncounterId != 981 || source.ReadDefinition().StageId != 142)
        {
            throw new InvalidDataException("The component requires its original formation and initialized caster cell.");
        }
        NativeBattleAiDefinition ai = world.BattleResources.ReadEnemyAi(actor.ModelId, actor.Level);
        if (!ai.Abilities.Any(ability => ability.Command == 8 && ability.Id == 356 && ability.Sequence == 18) ||
            !world.BattleResources.NativeActions.Any(action => action.Command == 8 && action.ActionId == 356 && action.MagicId == 77))
        {
            throw new InvalidDataException("The component requires its installed original command, action and sequence metadata.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The component requires its initialized presentation process.");
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(3)) ?? throw new InvalidDataException("The component caster has no original model binding.");
        NativeBattlePoseSource pose = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(actor.Id)).PoseSource ?? throw new InvalidDataException("The component caster lacks its installed original skeleton.");
        if (pose.BoneCount != 71 || NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(caster), pose).Length != pose.BoneCount)
        {
            throw new InvalidDataException("The component caster lacks its required original bone anchors.");
        }
        NativeBattleStatusMirror cached = source.NativePresentationStatusMemory.Rows[actor.RequireNativeActorIndex()];
        ushort saved = cached.Saved ?? throw new InvalidDataException("The component caster lacks its current cached saved-status owner.");
        uint battle = cached.Battle ?? throw new InvalidDataException("The component caster lacks its current cached battle-status owner.");
        NativeBattleActionHeaderPreparation header = new(caster, 8, 18, -1, 356, 77, null);
        NativeBattleContactPacket contact = new(new(caster, 0, 1, 0, saved, 0, battle), null);
        process.BeginDiagnosticAction(header, [contact]);
        return source;
    }
}
