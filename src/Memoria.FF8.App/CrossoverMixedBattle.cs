using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverMixedBattleSnapshot(int Version, CrossoverBattleRoster Roster, Ff7BattleGuestVitalsBinding Vitals, CrossoverGunbladeHostSnapshot Attack)
{
    public CrossoverGuestHostActionsSnapshot? Actions { get; init; }
}

internal sealed record CrossoverMixedBattleGuestSnapshot(Ff7BattleGuestVitalsBinding Vitals, CrossoverGunbladeHostSnapshot Attack, CrossoverGuestActorSnapshot Actions);
internal sealed record CrossoverMixedBattlePartySnapshot(int Version, CrossoverBattleRoster Roster, CrossoverMixedBattleGuestSnapshot[] Guests, CrossoverGuestEncounterSnapshot SharedActions);

internal sealed class CrossoverMixedBattle : IDisposable
{
    private readonly GameAssets game;
    private readonly BattleAssets assets;
    private readonly CrossoverBattleRoster roster;
    private readonly Ff7BattleGuestVitalsBinding[] vitals;
    private readonly CrossoverGuestEncounter encounter;

    public Ff7BattleSession Host { get; }
    public IReadOnlyList<CrossoverGuestHostActions> Guests { get; }
    public CrossoverGuestHostActions Actions => Guests.Single();
    public CrossoverGunbladeHostAttack Attack => Actions.Attack;
    public CrossoverGuestHostActions? ActiveGuest => Guests.SingleOrDefault(guest => guest.Invocation is not null);

    public CrossoverMixedBattle(string directory, CombinedSessionSave source, Ff7BattleSession host)
    {
        game = new(directory);
        assets = new(directory);
        try
        {
            Host = host;
            IReadOnlyList<Ff7MixedCombatBinding> bindings = source.GetMixedCombatBindings();
            if (bindings.Count == 0)
            {
                throw new InvalidOperationException("Select a guest combat binding before entering a mixed encounter.");
            }

            GameplaySaveComponentDocument document = CrossoverSaveAdapter.ReadFf8(source);
            NativeCharacterVitals installed = game.ReadNewGameCharacterVitals();
            NativeCharacterVitals origin = document.Characters.Restore(installed);
            NativeGlobalRandomState originRandom = document.Progression?.Clocks.GlobalRandom ?? throw new NotSupportedException("Mixed combat requires the suspended FFVIII global RNG owner.");
            CrossoverGuestEncounterSnapshot? shared = null;
            Dictionary<int, (CrossoverGunbladeHostSnapshot Attack, CrossoverGuestActorSnapshot? Actions)> savedActors = [];
            NativeGlobalRandomState currentRandom = originRandom;
            if (source.MixedBattlePayload is { Length: > 0 } payload)
            {
                using JsonDocument json = JsonDocument.Parse(payload);
                JsonElement root = json.RootElement;
                if (!root.TryGetProperty(nameof(CrossoverMixedBattleSnapshot.Version), out JsonElement version) || !version.TryGetInt32(out int format))
                {
                    throw new InvalidDataException("The saved mixed encounter lacks an execution version.");
                }
                if (format == 1)
                {
                    CrossoverMixedBattleSnapshot saved = root.Deserialize<CrossoverMixedBattleSnapshot>() ?? throw new InvalidDataException("The saved mixed encounter is empty.");
                    roster = saved.Roster;
                    vitals = [saved.Vitals];
                    savedActors.Add(saved.Vitals.HostSlot, (saved.Attack, saved.Actions?.ActorState()));
                    shared = saved.Actions?.EncounterState();
                    currentRandom = saved.Attack.GlobalRandom;
                }
                else if (format == 2)
                {
                    CrossoverMixedBattlePartySnapshot saved = root.Deserialize<CrossoverMixedBattlePartySnapshot>() ?? throw new InvalidDataException("The saved mixed encounter is empty.");
                    if (saved.Guests is not { Length: >= 2 and <= 3 } || saved.Guests.Any(guest => guest is null || guest.Vitals is null || guest.Attack is null || guest.Actions is null) || saved.Guests.Select(guest => guest.Vitals.HostSlot).Distinct().Count() != saved.Guests.Length || saved.SharedActions is null)
                    {
                        throw new InvalidDataException("The saved mixed encounter lacks its guest owners or shared state.");
                    }
                    roster = saved.Roster;
                    vitals = saved.Guests.Select(guest => guest.Vitals).OrderBy(binding => binding.HostSlot).ToArray();
                    shared = saved.SharedActions;
                    currentRandom = saved.Guests[0].Attack.GlobalRandom;
                    foreach (CrossoverMixedBattleGuestSnapshot guest in saved.Guests)
                    {
                        if (guest.Attack.GlobalRandom != currentRandom)
                        {
                            throw new InvalidDataException("Saved guests disagree about the shared FFVIII global RNG.");
                        }
                        savedActors.Add(guest.Vitals.HostSlot, (guest.Attack, guest.Actions));
                    }
                }
                else
                {
                    throw new InvalidDataException($"Mixed encounter snapshot version {format} is unsupported.");
                }
                ValidateRestoredRoster(bindings);
            }
            else
            {
                CrossoverBattleBinding[] combat = bindings.Select(binding => new CrossoverBattleBinding(host.Actors.Single(actor => actor.Actor.Slot < 3 && actor.Identity == binding.StoryCharacter).Actor.Slot, binding.StoryCharacter, binding.OriginCharacter)).ToArray();
                CharacterAppearanceMapping[] appearances = bindings.Select(binding => new CharacterAppearanceMapping(SessionGame.Ff7, binding.StoryCharacter, SessionGame.Ff8, binding.OriginCharacter, "native-battle")).ToArray();
                roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), combat, appearances);
                vitals = CrossoverBattleVitalsSettlement.ReadBindings(source, roster, installed, assets).OrderBy(binding => binding.HostSlot).ToArray();
            }

            encounter = new(roster, origin, host, currentRandom, document.Progression?.Clocks.CrtRandom, document.Economy, shared);
            NativeBattleUiRandomState limitUiRandom = (document.Progression?.Clocks.Playtime ?? NativePlaytimeState.FromColdProcess()).CreateBattleUiRandom();
            List<CrossoverGuestHostActions> guests = [];
            foreach (Ff7BattleGuestVitalsBinding binding in vitals)
            {
                host.BindGuestVitals(binding);
                CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(origin, assets, binding.OriginCharacter);
                savedActors.TryGetValue(binding.HostSlot, out var saved);
                CrossoverGunbladeHostAttack attack = new(roster, kit, assets, game, originRandom, () => host.RenderCameraOutput, _ => 128, saved.Attack, host.GuestEffectTargetAnchor, host.GuestEffectTargetBone);
                attack.GlobalRandom = encounter.GlobalRandom;
                attack.PrepareStanding(host.VisualPlacement(binding.HostSlot));
                encounter.GlobalRandom = attack.GlobalRandom;
                ushort? limitOptions = kit.CharacterId == 0 ? document.Limits.Options ?? game.ReadNewGameLimitProgress().Options978 : null;
                CrossoverGuestHostActions actions = new(roster, kit, attack, host, game, assets, encounter, saved.Actions, limitOptions, limitUiRandom);
                host.BindGuestAttack(actions);
                guests.Add(actions);
            }
            Guests = guests;
        }
        catch
        {
            assets.Dispose();
            game.Dispose();
            throw;
        }
    }

    private void ValidateRestoredRoster(IReadOnlyList<Ff7MixedCombatBinding> bindings)
    {
        roster.Validate();
        if (roster.HostRequestId != Host.RequestId || roster.HostFormationId != Host.Formation.FormationId ||
            !vitals.SequenceEqual(Host.GuestVitalsBindings.OrderBy(binding => binding.HostSlot)) || vitals.Length != bindings.Count || roster.Guests.Count() != vitals.Length ||
            vitals.Any(binding => binding.EncounterId != roster.EncounterId || !bindings.Any(source => source.StoryCharacter == binding.StoryCharacter && source.OriginCharacter == binding.OriginCharacter) ||
                !roster.Guests.Any(actor => actor.HostSlot == binding.HostSlot && actor.Story.NativeId == binding.StoryCharacter && actor.Combat.NativeId == binding.OriginCharacter)) ||
            !roster.Participants.Select(actor => (actor.HostSlot, actor.Story.NativeId)).OrderBy(actor => actor.HostSlot).SequenceEqual(Host.Actors.Select(actor => (actor.Actor.Slot, actor.Identity)).OrderBy(actor => actor.Slot)))
        {
            throw new InvalidDataException("The saved mixed encounter does not match its native host.");
        }
    }

    public CrossoverGuestHostActions? GuestAt(int slot) => Guests.SingleOrDefault(guest => guest.ActorSlot == slot);

    public void BindForeignGeometry(Func<int, Ff7BattleActorGeometry> geometry)
    {
        foreach (CrossoverGuestHostActions guest in Guests)
        {
            guest.BindForeignGeometry(geometry);
        }
    }

    public IReadOnlyList<CrossoverGuestAudioReceipt> PeekSoundRequests() => encounter.Audio.Pending;
    public void AcknowledgeSoundRequests(long throughOrdinal) => encounter.Audio = encounter.Audio.Acknowledge(throughOrdinal);

    public byte[] Capture()
    {
        if (Guests is [var guest])
        {
            return JsonSerializer.SerializeToUtf8Bytes(new CrossoverMixedBattleSnapshot(1, roster, vitals[0], CaptureAttack(guest)) { Actions = guest.Capture() });
        }

        CrossoverMixedBattleGuestSnapshot[] captured = vitals.Select((binding, index) => new CrossoverMixedBattleGuestSnapshot(binding, CaptureAttack(Guests[index]), Guests[index].CaptureActor())).ToArray();
        return JsonSerializer.SerializeToUtf8Bytes(new CrossoverMixedBattlePartySnapshot(2, roster, captured, encounter.Capture()));
    }

    private CrossoverGunbladeHostSnapshot CaptureAttack(CrossoverGuestHostActions guest) => guest.Attack.Capture() with { GlobalRandom = encounter.GlobalRandom };

    public CrossoverBattleVitalsSettlementResult Settle(CombinedSessionSave source, Ff7Installation installation)
    {
        foreach (CrossoverGuestHostActions guest in Guests)
        {
            guest.CancelAtTerminal();
        }
        CrossoverGuestEncounterSnapshot actions = encounter.Capture();
        CrossoverGuestMagicSettlement? magic = actions.NextOrdinal > 1 ? new(roster, actions.Stock, actions.OriginCrtRandom, actions.CrtRandom, actions.CommittedSpells!.Value, actions.LegacyBattleMaskContinuation) : null;
        CrossoverGuestItemSettlement? items = actions.Items is { Ledger.IsEmpty: false } inventory ? new(roster.EncounterId, inventory, actions.OriginCrtRandom, actions.CrtRandom) : null;
        CrossoverGuestCrtSettlement? crt = magic is null && items is null && actions.CrtRandom != (actions.OriginCrtRandom ?? NativeCrtRandomState.FromColdProcess()) ? new(actions.OriginCrtRandom, actions.CrtRandom) : null;
        return CrossoverBattleVitalsSettlement.PrepareResult(source with { MixedBattlePayload = Capture() }, installation, game.ReadNewGameCharacterVitals(), presentations: Guests.Select(CaptureAttack).ToArray(), magic: magic, items: items, crt: crt);
    }

    public void Dispose()
    {
        assets.Dispose();
        game.Dispose();
    }
}
