using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalGuest261263Component(GameWorld world, EffectPreviewRequest request)
    {
        ushort effect = request.EffectId ?? throw new InvalidDataException("The component requires its selected guest limit.");
        byte character = effect switch
        {
            261 => 8,
            262 => 9,
            263 => 10,
            _ => throw new InvalidDataException("The selected original guest component is unsupported.")
        };
        if (request.AllyId != character)
        {
            throw new InvalidDataException("The component requires its original party caster and selected limit.");
        }
        GameAssets game = world.ValidationAssets;
        NativeCharacterVitals temporary = game.ReadNewGameCharacterVitals().WithGuestBattleRecords().WithExtendedRoster(true).WithBattleParty([character], 3);
        GameState temporaryState = new() { CharacterVitals = temporary, CharacterAppearance = game.ReadNewGameCharacterAppearance() };
        BattleCombatantDefinition guest = BattlePartyProjection.Create(temporaryState, null, LiveMenuCatalog.ReadInstalled(game), world.BattleResources, [character]).Single();
        BattleDefinition seed = NativeBattleTestDefinitions.CreateTargetGuardianDefinition(game, world.BattleResources, 0, 116, out _);
        byte fieldRules = seed.NativeFieldBattleRules ?? throw new InvalidDataException("The guest component requires its current native field battle prohibition byte.");
        BattleDefinition definition = seed with { Combatants = [guest, seed.Combatants[1]], MagicVitals = temporary, GuardianForces = [], ActionDefinitions = [], NativeLimitsDisabled = (fieldRules & 0x20) != 0 };
        BattleSession source = InstalledBattlePractice.Create(definition, BattleAtbMode.Normal, game, world.BattleResources);
        source.ConfigureDiagnosticScheduling(holdEnemyCommands: true);
        for (int callback = 0; callback < 3600 && (!source.NativeEntryReady || source.ObserveDiagnosticDrainBlockers().Count != 0); callback++)
        {
            source.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        }
        if (!source.NativeEntryReady || source.ObserveDiagnosticDrainBlockers().Count != 0)
        {
            throw new InvalidDataException("The temporary guest component did not finish its native initialization.");
        }
        BattleCombatant actor = source.Combatants.Single(candidate => candidate.IsPlayer && candidate.NativeActorIndex == 0);
        BattleNativeAction action = world.BattleResources.NativeActions.Single(candidate => candidate.CharacterId == character && candidate.MagicId == effect && candidate.KernelSection == 18);
        if (actor.ModelId != character || action.HitCount == 0)
        {
            throw new InvalidDataException("The component requires its installed original caster and limit metadata.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The component requires its initialized presentation process.");
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(0)) ?? throw new InvalidDataException("The component caster has no original model binding.");
        BattleModelOwner recipient = process.FindOriginalActor(NativeBattleModelSlot.Decode(3)) ?? throw new InvalidDataException("The component recipient has no original model binding.");
        byte sequence = NativeBattleActionGraphics.ResolvePlayerSequence(action.Command, 0, action.ActionId, source.ReadDefinition().NativeCommandSequences);
        NativeBattleStatusMirror cached = source.NativePresentationStatusMemory.Rows[3];
        ushort saved = cached.Saved ?? throw new InvalidDataException("The component recipient lacks its current physical saved-status owner.");
        uint battle = cached.Battle ?? throw new InvalidDataException("The component recipient lacks its current physical battle-status owner.");
        NativeBattleContactPacket contact = new(new(recipient, 0, 1, 0, saved, 0, battle), null);
        NativeBattleContactPacket[] contacts = Enumerable.Range(0, action.HitCount).Select(_ => contact).ToArray();
        process.BeginDiagnosticAction(new(caster, action.Command, sequence, -1, action.ActionId, action.MagicId, null), [.. contacts]);
        return source;
    }
}
