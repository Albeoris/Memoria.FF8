using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverMixedBattleSnapshot(int Version, CrossoverBattleRoster Roster, Ff7BattleGuestVitalsBinding Vitals, CrossoverGunbladeHostSnapshot Attack)
{
    public CrossoverGuestHostActionsSnapshot? Actions { get; init; }
}

internal sealed class CrossoverMixedBattle : IDisposable
{
    private readonly GameAssets game;
    private readonly BattleAssets assets;
    private readonly CrossoverBattleRoster roster;
    private readonly Ff7BattleGuestVitalsBinding vitals;
    public Ff7BattleSession Host { get; }
    public CrossoverGunbladeHostAttack Attack { get; }
    public CrossoverGuestHostActions Actions { get; }

    public CrossoverMixedBattle(string directory, CombinedSessionSave source, Ff7BattleSession host)
    {
        game = new(directory);
        assets = new(directory);
        try
        {
            Ff7MixedCombatBinding binding = source.MixedCombat ?? throw new InvalidOperationException("Select a guest combat binding before entering a mixed encounter.");
            GameplaySaveComponentDocument document = CrossoverSaveAdapter.ReadFf8(source);
            NativeCharacterVitals installed = game.ReadNewGameCharacterVitals();
            NativeCharacterVitals origin = document.Characters.Restore(installed);
            CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.ReadSquall(origin, assets);
            CrossoverGunbladeHostSnapshot? savedAttack = null;
            CrossoverGuestHostActionsSnapshot? savedActions = null;
            if (source.MixedBattlePayload is { Length: > 0 } payload)
            {
                CrossoverMixedBattleSnapshot saved = JsonSerializer.Deserialize<CrossoverMixedBattleSnapshot>(payload) ?? throw new InvalidDataException("The saved mixed encounter is empty.");
                if (saved.Version != 1 || saved.Roster.HostRequestId != host.RequestId || saved.Roster.HostFormationId != host.Formation.FormationId || saved.Vitals != host.GuestVitals)
                {
                    throw new InvalidDataException("The saved mixed encounter does not match its native host.");
                }
                roster = saved.Roster;
                vitals = saved.Vitals;
                savedAttack = saved.Attack;
                savedActions = saved.Actions;
            }
            else
            {
                int slot = host.Actors.Single(actor => actor.Actor.Slot < 3 && actor.Identity == binding.StoryCharacter).Actor.Slot;
                roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), [new(slot, binding.StoryCharacter, binding.OriginCharacter)], [new(SessionGame.Ff7, binding.StoryCharacter, SessionGame.Ff8, binding.OriginCharacter, "native-battle")]);
                vitals = CrossoverBattleVitalsSettlement.ReadBinding(source, roster, installed, assets);
            }
            host.BindGuestVitals(vitals);
            NativeGlobalRandomState random = document.Progression?.Clocks.GlobalRandom ?? throw new NotSupportedException("Mixed combat requires the suspended FFVIII global RNG owner.");
            Attack = new(roster, kit, assets, game, random, () => host.Data.Cameras[0], _ => 128, savedAttack);
            Attack.PrepareStanding(host.VisualPlacement(roster.Guest.HostSlot));
            Actions = new(roster, kit, origin, document.Progression?.Clocks.CrtRandom, Attack, host, game, assets, savedActions);
            host.BindGuestAttack(Actions);
            Host = host;
        }
        catch
        {
            assets.Dispose();
            game.Dispose();
            throw;
        }
    }

    public byte[] Capture() => JsonSerializer.SerializeToUtf8Bytes(new CrossoverMixedBattleSnapshot(1, roster, vitals, Attack.Capture()) { Actions = Actions.Capture() });

    public CrossoverBattleVitalsSettlementResult Settle(CombinedSessionSave source, Ff7Installation installation)
    {
        Actions.CancelAtTerminal();
        CrossoverGuestHostActionsSnapshot actions = Actions.Capture();
        CrossoverGuestMagicSettlement? magic = Actions.UsedMagic ? new(roster, actions.Stock, Actions.OriginCrtRandom, Actions.CurrentCrtRandom, actions.CommittedSpells!.Value, Actions.LegacyBattleMaskContinuation) : null;
        return CrossoverBattleVitalsSettlement.PrepareResult(source with { MixedBattlePayload = Capture() }, installation, game.ReadNewGameCharacterVitals(), presentation: Attack.Capture(), magic: magic);
    }

    public void Dispose()
    {
        assets.Dispose();
        game.Dispose();
    }
}
