using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleNorgOrbTests
{
    [Theory]
    [InlineData("enemy-1", 1u)]
    [InlineData("enemy-2", 1u)]
    [InlineData("enemy-1", 2u)]
    [InlineData("enemy-2", 2u)]
    public void Original_orbs_reset_after_physical_contact(string targetId, uint color)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for the original encounter.");
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        using GameAssets game = new(directory!);
        DiagnosticBattleRequest request = new(63, 0, 0x80, 0, 0) { BattleMusic = 5, PrimaryMusicChannel = -1, PrepareEntryBeforePresentation = true };
        BattleSession battle = world.StartDiagnosticBattle(request);
        try
        {
            world.PrepareDiagnosticEntryBeforePresentation(request);
            BattleAudioPresentation audio = new(_ => { }, playModule: _ => { }, stopSounds: () => { });
            for (int step = 0; step < 4096; step++)
            {
                BattleSnapshot snapshot = battle.Capture();
                BattleCombatantSnapshot orb = snapshot.Combatants.Single(actor => actor.Id == targetId);
                if (orb.NativeParticipant!.Variables[0] == color && battle.ObserveDiagnosticDrainBlockers().Count == 0 && battle.CanChoosePlayerCommand("party-0"))
                {
                    Assert.Equal(0u, orb.NativeParticipant.Variables[1]);
                    int physical = orb.NativeParticipant.ActorIndex;
                    NativeBattleGraphicsModel model = battle.NativeGraphics!.Models[physical];
                    NativeBattleModelCpuResources cpu = battle.NativeModelCpuResources!;
                    NativeBattleModelCpuResourceRow row = cpu.Rows[model.TextureResourceIndex!.Value];
                    byte[] table = NativeBattleModelEnemyHeap.Read(cpu, battle.NativeFileStaging!, row.Require(48), row.Lengths[48]);
                    NativeBattleEffectTextureState beforeTextures = battle.NativeEffectMemory!.Textures!;
                    world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: "party-0", TargetId: orb.Id, Ability: BattleAbility.Attack));
                    BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
                    restored.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
                    BattleAudioPresentation restoredAudio = new(_ => { }, playModule: _ => { }, stopSounds: () => { });
                    int resetSequence = 0;
                    for (int hit = 0; hit < 1024; hit++)
                    {
                        battle.Tick();
                        audio.Update(battle);
                        restored.Tick();
                        restoredAudio.Update(restored);
                        BattleEvent? reset = battle.Events.LastOrDefault(entry => entry.Kind == "NativeDamagePresentation" && entry.ActorId == targetId && entry.Amount is 14 or 15 or 16);
                        if (reset is not null)
                        {
                            resetSequence = reset.Amount;
                        }
                        if (hit == 0)
                        {
                            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
                        }
                        if (battle.ObserveDiagnosticDrainBlockers().Count == 0)
                        {
                            BattleSnapshot after = battle.Capture();
                            Assert.Contains(resetSequence, color == 1 ? [15] : new[] { 14, 16 });
                            Assert.Equal(resetSequence == 14 ? 1u : 0u, after.Combatants.Single(actor => actor.Id == orb.Id).NativeParticipant!.Variables[0]);
                            byte selector = resetSequence switch { 14 => 3, 15 => 4, _ => 1 };
                            int variant = resetSequence == 16 ? 0 : 6;
                            NativeBattleTextureCopy copy = NativeBattleTextureMotion.Plan50C780(table.ToImmutableArray(), model.TextureAllocationMask, selector, variant)!;
                            NativeBattleEffectTextureState textures = battle.NativeEffectMemory!.Textures!;
                            for (int y = 0; y < copy.Height; y++)
                            {
                                for (int x = 0; x < copy.Width; x++)
                                {
                                    Assert.Equal(beforeTextures.ReadVramWord(copy.SourceX + x, copy.SourceY + y), textures.ReadVramWord(copy.DestinationX + x, copy.DestinationY + y));
                                }
                            }
                            Assert.Equal((byte)1, battle.NativeGraphics!.Models[physical].Sequence);
                            Assert.Equal(1, battle.NativeGraphics.Models[physical].SequenceState!.Default2);
                            Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(restored.Capture()));
                            return;
                        }
                    }
                    Assert.Fail("Hit did not drain.");
                }
                if (battle.ObserveDiagnosticDrainBlockers().Count == 0)
                {
                    battle.AdvanceTurn(1024);
                }
                battle.Tick();
                audio.Update(battle);
            }
            Assert.Fail("The original orb did not reach the selected color with a ready player within the callback budget.");
        }
        finally
        {
            world.AbandonFaultedDiagnosticBattle();
            world.EndDiagnosticBattle();
        }
    }
}
