using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect308PrerequisiteTests
{
    [Fact]
    public void Original_preceding_action74_releases_the_trio_ring_through_sequence12()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT308_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT308_PREREQUISITE_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, the intact trio standalone and a distinct prerequisite output directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.Equal(794, source.Definition.EncounterId);
        Assert.Equal(65, source.Definition.StageId);
        NativeBattleGraphicsState graphics = source.NativeGraphics!;
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.Equal(new int?[] { 4, 5, 3 }, graphics.Models.Skip(3).Take(3).Select(model => model.ReadLinkedModelIndex140()));
        BattleNativeAction action = Assert.Single(assets.NativeActions, item => item.Command == 8 && item.ActionId == 74 && item.MagicId == 0);
        BattleCombatantDefinition caster = BattleSession.Restore(source).GetCombatantDefinition(graphics.ActorIds[3]!);
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(caster.ModelId, caster.Level);
        NativeBattleAiAbility ability = ai.Abilities[ai.LevelBand * 16 + 4];
        Assert.Equal((byte)8, ability.Command);
        Assert.Equal((ushort)74, ability.Id);
        Assert.Equal((byte)12, ability.Sequence);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 3).NativeStatus!;
        NativeBattleActionPacketsState packets = source.NativeActionPackets!;
        (packets, int header) = packets.Prepare(new(3, 8, 12, unchecked((sbyte)action.RawRecord[4]), 74, 0, null));
        (packets, _) = packets.AppendContact(new(new(3, action.DamagePresentation, 0, 0, status.Saved, 0, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        BattleSnapshot prepared = source with { NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } };
        NativeBattleRestorationPresentationTests.Publish(output!, "before-prerequisite", prepared, null);
        BattleSession session = NativeBattleEffect308PresentationTests.Restore(prepared, game);
        bool unlinked = false;
        int callbacks = 0;
        try
        {
            for (; callbacks < 600; callbacks++)
            {
                session.Tick();
                BattleSnapshot current = session.Capture();
                Assert.Equal(JsonSerializer.Serialize(source.Combatants), JsonSerializer.Serialize(current.Combatants));
                Assert.Equal(JsonSerializer.Serialize(source.NativeEffectMemory!.Effect308), JsonSerializer.Serialize(current.NativeEffectMemory!.Effect308));
                Assert.Equal(new byte?[] { 121, 122, 123 }, current.NativeGraphics!.Models.Skip(3).Take(3).Select(model => model.ModelId4));
                if (!unlinked && current.NativeGraphics.Models.Skip(3).Take(3).All(model => model.ReadLinkedModelIndex140() is null))
                {
                    unlinked = true;
                    NativeBattleEffect308PresentationTests.VerifyColdNext("native90-unlinked", current, game, assets, output!);
                }
                if (unlinked && !current.NativeGraphics.Descriptors.Queue.Any(index => current.NativeGraphics.Descriptors.Packets[index].Command == 104))
                {
                    break;
                }
            }
            Assert.True(callbacks < 600, "The preceding original action74 did not complete.");
            Assert.True(unlinked);
            BattleSnapshot completed = session.Capture();
            Assert.Equal(0, completed.NativeEffectMemory!.Active);
            Assert.Null(completed.NativeGraphics!.OriginalEffect);
            NativeBattleEffect308PresentationTests.VerifyColdNext("after-prerequisite", completed, game, assets, output!);
            File.WriteAllText(Path.Combine(output!, "prerequisite-result.json"), JsonSerializer.Serialize(new { Status = "completed", Effect = 308, PrecedingAction = 74, Sequence = 12, Callbacks = callbacks + 1, completed.Frame, Callback = completed.NativeGraphics.Callback, RingReleasedByNativeSequence = true, Scope = "Declared zero-amount presentation prerequisite; original death-AI eligibility, KO and damage formula excluded" }));
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output!, "failed-live", session.Capture(), null);
            File.WriteAllText(Path.Combine(output!, "prerequisite-result.json"), JsonSerializer.Serialize(new { Status = "blocked", Callbacks = callbacks, Error = error.ToString() }));
            throw;
        }
    }
}
