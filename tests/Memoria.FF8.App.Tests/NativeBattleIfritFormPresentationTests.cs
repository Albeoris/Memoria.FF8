using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleIfritFormPresentationTests
{
    [Fact]
    public void Installed_Ifrit_form_entry_lands_without_an_offensive_transaction()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = NativeBattleTargetGuardianCastTests.CreateDefinition(game, assets, 2, 201) with { GuardianFormsEnabled = true, GuardianFormResources = [assets.ReadIfritGuardianFormResources()] };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, true);
        for (int index = 0; index < 400 && (!battle.NativeEntryReady || !battle.CanChoosePlayerCommand("party")); index++)
        {
            battle.Tick();
        }
        Assert.True(battle.CanRequestGuardianForm("party", 2));
        battle.RequestGuardianForm("party", 2);
        for (int index = 0; index < 1000 && battle.GetGuardianForm("party")?.Phase != BattleGuardianFormPhase.Active; index++)
        {
            battle.Tick();
        }
        BattleSnapshot landed = battle.Capture();
        Assert.Equal(BattleGuardianFormPhase.Active, battle.GetGuardianForm("party")?.Phase);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Damage" && item.ActorId == "party");
        Assert.Equal(9999, battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
        Assert.Equal((byte)77, battle.NativeGraphics!.Models[0].ModelId4);
        Assert.Equal("party", battle.NativeGraphics.ActorIds[0]);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_IFRIT_FORM_EVIDENCE");
        NativeBattleRestorationPresentationTests.Publish(output, "landed", landed, output is null ? null : scene.Render(battle, 320, 224).ToArray());
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(landed))!);
        battle.Tick();
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void Installed_Ifrit_presentation_exposes_original_script_and_model_boundaries()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_IFRIT_PRESENTATION_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(output is not null && directory is not null, "Select installed resources and an explicit presentation evidence directory.");
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = NativeBattleTargetGuardianCastTests.CreateDefinition(game, assets, 2, 201);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, true);
        for (int index = 0; index < 400 && (!battle.NativeEntryReady || !battle.CanChoosePlayerCommand("party")); index++)
        {
            battle.Tick();
        }
        battle.QueueAction("party", "enemy", BattleAbility.GuardianForce, 2);
        using StreamWriter trace = new(Path.Combine(output!, "script-model-trace.jsonl"));
        int cinematicCallback = 0;
        bool entered = false;
        for (int callback = 0; callback < 4096; callback++)
        {
            battle.Tick();
            BattleSnapshot snapshot = battle.Capture();
            if (snapshot.NativeGraphics?.OriginalEffect is not { Binding.Effect: 201, ReturnedDescriptor: > 0 })
            {
                if (entered)
                {
                    break;
                }
                continue;
            }
            entered = true;
            NativeBattleEffectMemory memory = snapshot.NativeEffectMemory!;
            NativeBattleScriptedEffectMemory core = memory.ScriptedEffects!;
            NativeBattleEffectArena arena = memory.Arena!;
            uint bank = Read(Read(0x27973EC) + 0x90);
            var slots = Enumerable.Range(0, 32).Select(index =>
            {
                int offset = checked((int)(bank - 0x20DFAB8u) + index * 256);
                byte[] bytes = arena.Read(offset, 256);
                return new { index, Pc = BinaryPrimitives.ReadUInt32LittleEndian(bytes), Pc1 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)), Pc2 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)), Pose = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0xBC)), Position = Convert.ToHexString(bytes.AsSpan(0x50, 12)), Rotation = Convert.ToHexString(bytes.AsSpan(0x8C, 6)) };
            }).Where(slot => slot.Pc != 0 || slot.Pc1 != 0 || slot.Pc2 != 0).ToArray();
            trace.WriteLine(JsonSerializer.Serialize(new { cinematicCallback, snapshot.Frame, Resource = Read(0x2798A68), slots }));
            trace.Flush();
            if (cinematicCallback % 30 == 0)
            {
                NativeBattleRestorationPresentationTests.Publish(output, "presentation-" + cinematicCallback.ToString("D4"), snapshot, scene.Render(battle, 320, 224).ToArray());
            }
            cinematicCallback++;

            uint Read(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(core.Read(address, 4));
        }
        Assert.True(entered);
        Assert.Single(battle.Events, item => item.Kind == "Damage" && item.ActorId == "party");
    }
}


