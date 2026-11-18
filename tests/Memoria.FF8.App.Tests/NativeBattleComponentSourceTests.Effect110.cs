using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect110Component(GameWorld world, EffectPreviewRequest request)
    {
        if (request.EnemyId != 74 || request.StageId != 3)
        {
            throw new InvalidDataException("The component requires its original encounter caster and stage.");
        }
        BattleSession source = world.StartDiagnosticBattle(new(29, request.Seed, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            AtbMode = BattleAtbMode.Normal,
            Fixture = new([], [], true) { CombatParty = [0, 255, 255] }
        });
        for (int callback = 0; callback < 3600 && (!source.NativeEntryReady || source.ObserveDiagnosticDrainBlockers().Count != 0); callback++)
        {
            source.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        }
        if (!source.NativeEntryReady || source.ObserveDiagnosticDrainBlockers().Count != 0)
        {
            throw new InvalidDataException("The original component encounter did not finish its native initialization.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The component requires its initialized presentation process.");
        BattleCombatant biggs = source.Combatants.Single(actor => actor.NativeActorIndex == 3);
        BattleCombatant wedge = source.Combatants.Single(actor => actor.NativeActorIndex == 4);
        if (biggs.ModelId != 74 || wedge.ModelId != 73 || biggs.NativeAi is null || wedge.NativeAi is null || biggs.NativeAi.Texts.Length <= 10 || wedge.NativeAi.Texts.Length <= 1)
        {
            throw new InvalidDataException("The component requires both original actors and their installed message banks.");
        }
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(3)) ?? throw new InvalidDataException("The component caster lacks its original model binding.");
        NativeBattleContactPacket[] contacts = new[] { biggs, wedge }.Select(actor =>
        {
            byte index = checked((byte)actor.RequireNativeActorIndex());
            BattleModelOwner recipient = process.FindOriginalActor(NativeBattleModelSlot.Decode(index)) ?? throw new InvalidDataException("The component recipient lacks its original model binding.");
            NativeBattleStatusMirror cached = source.NativePresentationStatusMemory.Rows[index];
            ushort saved = cached.Saved ?? throw new InvalidDataException("The component recipient lacks its current physical saved-status owner.");
            uint battle = cached.Battle ?? throw new InvalidDataException("The component recipient lacks its current physical battle-status owner.");
            return new NativeBattleContactPacket(new(recipient, 0, 1, 0, saved, 0, battle), null);
        }).ToArray();
        process.BeginDiagnosticAction(new(caster, 252, 0, -1, 1, 110, null), [.. contacts]);
        return source;
    }
}
