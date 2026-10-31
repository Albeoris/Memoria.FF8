using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRenzokukenCastTests
{
    [Fact]
    public void Installed_original141_completes_five_authored_contacts_and_standalones()
    {
        Installed_selected_Renzokuken_completes_original_effect_and_standalones(141, 5, 1);
    }

    [Fact]
    public void Installed_original160_completes_four_authored_contacts_and_standalones()
    {
        Installed_selected_Renzokuken_completes_original_effect_and_standalones(160, 4, 1);
    }

    [Fact]
    public void Installed_original259_completes_six_authored_contacts_and_standalones()
    {
        Installed_selected_Renzokuken_completes_original_effect_and_standalones(259, 6, 2);
    }

    [Fact]
    public void Installed_original332_completes_seven_authored_contacts_at_crisis3_and_standalones()
    {
        Installed_selected_Renzokuken_completes_original_effect_and_standalones(332, 7, 3);
    }

    [Theory]
    [InlineData((ushort)160, 4, (byte)1)]
    [InlineData((ushort)141, 5, (byte)1)]
    [InlineData((ushort)259, 6, (byte)2)]
    [InlineData((ushort)332, 7, (byte)3)]
    [InlineData((ushort)333, 8, (byte)4)]
    public void Installed_selected_Renzokuken_completes_original_effect_and_standalones(ushort effectId, int contacts, byte crisis)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select the installed original game resources.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition source = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, 0, effectId);
        BattleDefinition definition = source with { NativeRenzokukenResources = game.ReadNativeRenzokukenResources(), Combatants = [source.Combatants[0], source.Combatants[1] with { NativeRenzokukenMagicIds = assets.ReadNativeRenzokukenMagicIds(61) }] };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, true);
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_EVIDENCE") ?? Path.GetTempPath(), "renzokuken-" + effectId + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine("Renzokuken evidence: " + output);
        BattleSnapshot before = battle.Capture();
        int capturedFrames = 0;
        try
        {
            for (int callback = 0; callback < 400 && !battle.CanChoosePlayerCommand("party"); callback++)
            {
                before = battle.Capture();
                battle.Tick();
            }
            Assert.True(battle.CanChoosePlayerCommand("party"));
            //A declared component seed selects the selected original contact resource and no optional finisher.
            NativeBattleRandomState random = Enumerable.Range(0, 256).Select(seed => NativeBattleRandomState.Initialize((byte)seed)).First(candidate =>
            {
                candidate = candidate.Draw(out byte resource).Draw(out byte finisher);
                return definition.Combatants[1].NativeRenzokukenMagicIds[crisis + 1 + resource % 2] == effectId && !NativeBattleRenzokukenRules.HasFinisher(crisis, finisher);
            });
            BattleSnapshot ready = battle.Capture();
            const uint transitionColor = 0x00312617;
            battle = BattleSession.Restore(ready with { NativeLimits = [new("party", crisis)], Random = random, NativeStage = ready.NativeStage! with { ModelColors = ready.NativeStage.ModelColors! with { Color40 = transitionColor } } });
            const int actorIndex = 0;
            uint? shadowColor = battle.NativeGraphics!.Models[actorIndex].Color44;
            Assert.NotNull(shadowColor);
            uint interpolatedColor = NativeBattleGteColorInterpolation.CodeConstants45F270.Interpolate(transitionColor, 1 << 9, 0x4060F0);
            bool interpolatedCurrentStageColor = false;
            Publish("before-command");
            battle.QueueNativePlayerCommand("party", "enemy", 5, 0);
            CompareNext("queued");
            Assert.Equal(effectId, battle.GetNativeRenzokuken("party")!.MagicId);
            bool selected = false;

            bool registeredCamera = false;
            bool capturedMidpoint = false;
            for (int callback = 0; callback < 4096; callback++)
            {
                before = battle.Capture();
                battle.Tick();
                Assert.Equal(shadowColor, battle.NativeGraphics!.Models[actorIndex].Color44);
                interpolatedCurrentStageColor |= battle.NativeGraphics.Models[actorIndex].Color40 == interpolatedColor;
                selected |= battle.NativeGraphics!.OriginalEffect?.Binding.Effect == effectId;
                if (effectId is 141 or 160 or 259 or 332 && NativeBattleRenzokukenCaptureAssertions.AssertFrame(battle.Capture(), effectId) > 0)
                {
                    capturedFrames++;
                }
                if (battle.NativeGraphics.OriginalEffect is { ReturnedDescriptor: > 0 } effect && effect.Binding.Effect == effectId)
                {
                    Assert.NotNull(battle.NativeEffectMemory!.Textures!.Animation);
                    Assert.Equal(JsonSerializer.Serialize(battle.NativeEffectMemory.Textures.Animation), JsonSerializer.Serialize(battle.NativeCamera!.ControlResource));
                    Assert.Null(battle.NativeCamera.ControlStagingSource);
                    registeredCamera = true;
                }
                if (!capturedMidpoint && battle.NativeGraphics.TriggerWidget?.Input.ContactIndex > 0)
                {
                    CompareNext("midpoint");
                    capturedMidpoint = true;
                }
                if (selected && battle.GetNativeRenzokuken("party") is null && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.NativeGraphics.OriginalEffect is { ReturnedDescriptor: 0 })
                {
                    if (effectId is 141 or 160 or 259 or 332)
                    {
                        Assert.True(capturedFrames > 1);
                    }
                    Assert.True(capturedMidpoint);
                    Assert.True(registeredCamera);
                    Assert.True(interpolatedCurrentStageColor, "Renzokuken must interpolate the current stage transition color into model DWORD40 without changing the shadow command in DWORD44.");
                    Assert.Equal(contacts, battle.Events.Count(entry => entry.Kind == "Damage" && entry.ActorId == "party" && entry.TargetId == "enemy"));
                    Assert.Null(battle.Capture().NativeTimedAction);
                    Assert.Equal((byte)0, battle.Capture().NativeClock!.ActionPause);
                    CompareNext("completed");
                    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "passed", battle.Frame, NativeCallback = battle.NativeGraphics.Callback }));
                    return;
                }
            }
            Assert.Fail("Renzokuken did not release its original task and timed command owners within the diagnostic bound.");
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "failed", before.Frame, NativeCallback = before.NativeGraphics?.Callback, Error = error.ToString() }));
            throw;
        }

        void Publish(string name)
        {
            NativeBattleRestorationPresentationTests.Publish(output, name, battle.Capture(), scene.Render(battle, 320, 224).ToArray());
        }

        void CompareNext(string name)
        {
            Publish(name);
            BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output, name + ".battle.json")))!);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
            before = battle.Capture();
            battle.Tick();
            clone.Tick();
            if (effectId is 141 or 160 or 259 or 332)
            {
                if (NativeBattleRenzokukenCaptureAssertions.AssertFrame(battle.Capture(), effectId) > 0)
                {
                    capturedFrames++;
                }
                _ = NativeBattleRenzokukenCaptureAssertions.AssertFrame(clone.Capture(), effectId);
            }
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(clone, 320, 224).ToArray());
            Publish(name + "-next");
        }
    }
}
