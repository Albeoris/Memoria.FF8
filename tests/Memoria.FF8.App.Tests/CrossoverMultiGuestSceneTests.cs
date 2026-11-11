using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class CrossoverMultiGuestSceneTests
{
    [Fact]
    public void Three_mapped_guests_keep_distinct_kits_stock_and_turns_through_one_cold_restored_battle()
    {
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root) && !string.IsNullOrWhiteSpace(ff8Root), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using GameAssets game = new(ff8Root!);
        using BattleAssets assets = new(ff8Root!);
        NativeCharacterVitals origin = game.ReadNewGameCharacterVitals().DeepCopy();
        int[] ids = [0, 1, 4];
        int[] rows = ids.Select(id => (int)origin.FindPhysicalRow((byte)id)).ToArray();
        int magicCommand = origin.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2) + 20;
        for (int slot = 0; slot < rows.Length; slot++)
        {
            int row = rows[slot];
            origin = origin.ChangeMagicStock(row, 21, 3, MagicOperationReason.ScriptReward, out _);
            origin = origin.ChangeMagicStock(row, 1, (slot + 1) * 20, MagicOperationReason.ScriptReward, out _);
            NativeCharacterVital saved = origin.Records[row];
            byte[] junctions = saved.Junction.MagicJunctions.ToArray();
            junctions[9] = 1;
            origin.Records[row] = saved with
            {
                Weapon = (byte)assets.WeaponDefinitions.Where(weapon => weapon.CharacterId == ids[slot]).OrderBy(weapon => weapon.Tier).Skip(1).First().Id,
                Junction = saved.Junction with { Commands = (uint)magicCommand, MagicJunctions = junctions }
            };
            origin = origin.WithSavedHitPoints(row, (ushort)(110 + slot * 20));
        }
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(game, world: false) with { Characters = GameplaySaveCharacters.Capture(origin) };
        using MemoryStream payload = new();
        GameplaySaveCodec.Write(payload, document);
        using Ff7Session initial = Ff7Session.NewGame(installation);
        Ff7SessionSave field = initial.Capture();
        CharacterAppearanceMapping[] mappings = [];
        for (int slot = 0; slot < ids.Length; slot++)
        {
            mappings = CharacterMappingAssociations.Assign(mappings, slot, ids[slot]);
        }
        CombinedSessionSave source = new()
        {
            ActiveGame = SessionGame.Ff7,
            Ff7 = field,
            Ff8Payload = payload.ToArray(),
            Ff8PayloadFormat = CrossoverSaveAdapter.Ff8Codec,
            Mappings = mappings
        };
        using GpuTestDevice device = new();
        using SpriteBatch batch = new(device.Device);
        using Ff7Scene scene = new(device.Device, installation, "", new(50, 32000), _ => throw new InvalidOperationException("The battle fixture does not play field movies."), source, audioEnabled: false);
        scene.ConfigureCharacterIdentity(ff8Root!);
        scene.ConfigureSharedDialogueDefaults(batch, ff8Root!, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        scene.Continue(source);
        scene.PrepareMappedCombatForSwitch();
        Ff7Session session = scene.Session!;
        session.Host.Execute(new(1L << 59, 0, 7, 0, new FieldPartyCommand(FieldPartyOperation.Replace, 0, 1, 2)));
        Assert.Equal(new[] { 0, 1, 2 }, session.Progress.Party);
        Assert.Equal(3, scene.Capture().GetMixedCombatBindings().Count);
        session.Host.StartEncounter(new(session.Progress.NextEncounterRequestId, 0, 7, 0, new FieldBattleCommand(364)), 0);
        using RenderTarget2D target = device.CreateTarget(640, 480);
        Draw();
        Ff7BattleSession battle = Assert.IsType<Ff7BattleSession>(session.Battle);
        Assert.Equal(ids, battle.GuestVitalsBindings.OrderBy(binding => binding.HostSlot).Select(binding => binding.OriginCharacter));
        Assert.Equal(new uint[] { 110, 130, 150 }, battle.Actors.Where(actor => actor.Actor.Slot < 3).Select(actor => actor.Actor.HitPoints));
        CombinedSessionSave first = scene.Capture();
        Assert.Equal(3, first.GetMixedCombatBindings().Count);
        CrossoverMixedBattlePartySnapshot mixed = Assert.IsType<CrossoverMixedBattlePartySnapshot>(JsonSerializer.Deserialize<CrossoverMixedBattlePartySnapshot>(first.MixedBattlePayload!));
        foreach (CrossoverMixedBattleGuestSnapshot guest in mixed.Guests)
        {
            int row = guest.Vitals.OriginRow;
            Assert.Equal((byte)guest.Vitals.OriginCharacter, guest.Attack.Presentation!.Model.ModelId4);
            Assert.Equal((byte)assets.WeaponDefinitions[origin.Records[row].Weapon].Tier, guest.Attack.Presentation.Model.FileSelector128);
            Assert.NotNull(battle.GetGuestProjection(guest.Vitals.HostSlot)?.OriginElementDefenses);
        }

        HashSet<int> casts = [];
        HashSet<int> actions = [];
        HashSet<int> contacts = [];
        bool restored = false;
        for (int tick = 0; tick < 12000 && !battle.ResultReady; tick++)
        {
            if (battle.ReadyPlayer is int ready && battle.Outcome == Ff7BattleOutcome.Fighting)
            {
                if (ready < 2 && !casts.Contains(ready))
                {
                    int ally = ready + 1;
                    Assert.True(battle.Submit(ready, Ff7BattleCommand.Magic, 21, (ushort)(1 << ally)));
                    casts.Add(ready);
                }
                else if (battle.Commands(ready).FirstOrDefault(option => option.Command == Ff7BattleCommand.Attack && option.ValidTargets != 0) is { } attack)
                {
                    int enemy = battle.Actors.First(actor => actor.Actor.Slot >= 4 && (attack.ValidTargets & 1 << actor.Actor.Slot) != 0).Actor.Slot;
                    Assert.True(battle.SubmitGuestAttack(ready, enemy));
                    actions.Add(ready);
                }
            }
            if (!restored && battle.Action is { Command.GuestAbility: 21 } && battle.Tick > battle.Action.StartedAt + 10)
            {
                CombinedSessionSave current = RoundTrip(scene.Capture());
                scene.Continue(current);
                session = scene.Session!;
                battle = Assert.IsType<Ff7BattleSession>(session.Battle);
                Assert.Equal(ids, battle.GuestVitalsBindings.OrderBy(binding => binding.HostSlot).Select(binding => binding.OriginCharacter));
                Draw();
                restored = true;
            }
            scene.Update(new(TimeSpan.FromSeconds((tick + 1) / 60d), TimeSpan.FromSeconds(1d / 60)), default);
            Assert.Null(scene.Error);
            if (battle.LastHit is { Source: < 3, Target: >= 4 } hit)
            {
                contacts.Add(hit.Source);
            }
            if (tick % 101 == 0)
            {
                Draw();
            }
        }
        Assert.True(restored);
        Assert.Equal(new[] { 0, 1 }, casts.Order());
        Assert.Equal(new[] { 0, 1, 2 }, actions.Order());
        Assert.Equal(new[] { 0, 1, 2 }, contacts.Order());
        Assert.Equal(Ff7BattleOutcome.Victory, battle.Outcome);
        Assert.True(battle.ResultReady);
        Assert.False(session.ConfirmBattleVictory());
        CombinedSessionSave? settled = null;
        scene.MixedCombatSettled += value => settled = value;
        scene.Update(new(TimeSpan.Zero, TimeSpan.Zero), new KeyboardState(Keys.Enter));
        Assert.Null(scene.Error);
        CombinedSessionSave final = RoundTrip(Assert.IsType<CombinedSessionSave>(settled));
        Assert.Null(final.Ff7!.Battle);
        Assert.Single(final.Ff7.Progress.CompletedBattles);
        Assert.Equal(field.Progress.Characters.Select(character => JsonSerializer.Serialize(character)), final.Ff7.Progress.Characters.Select(character => JsonSerializer.Serialize(character)));
        Ff7GuestBattleSettlementReceipt receipt = Assert.Single(final.GuestBattleSettlements!);
        Assert.Equal(rows, receipt.GetGuests().Select(guest => guest.OriginRow));
        GameplaySaveComponentDocument result = CrossoverSaveAdapter.ReadFf8(final);
        NativeCharacterVitals finalVitals = result.Characters.Restore(game.ReadNewGameCharacterVitals());
        Assert.Equal(new[] { 2, 2, 3 }, rows.Select(row => finalVitals.GetMagicCount(row, 21)));
        for (int slot = 0; slot < ids.Length; slot++)
        {
            int row = rows[slot];
            Assert.Equal(origin.Records[row].Weapon, finalVitals.Records[row].Weapon);
            Assert.Equal(origin.Records[row].Junction, finalVitals.Records[row].Junction);
            Assert.Equal(receipt.GetGuests()[slot].SettledHp, finalVitals.Records[row].HitPoints.Minimum);
        }
        Assert.Same(final, CrossoverBattleVitalsSettlement.PrepareResult(final, installation, game.ReadNewGameCharacterVitals(), receipt).Save);

        void Draw()
        {
            device.Device.SetRenderTarget(target);
            scene.Draw(batch, new(0, 0, target.Width, target.Height));
            Assert.Null(scene.Error);
            Assert.True(GpuTestDevice.Read(target).Select(pixel => pixel.PackedValue).Distinct().Count() > 16);
        }
    }

    private static CombinedSessionSave RoundTrip(CombinedSessionSave save)
    {
        using MemoryStream buffer = new();
        CombinedSessionCodec.Write(buffer, save);
        buffer.Position = 0;
        return CombinedSessionCodec.Read(buffer);
    }
}
