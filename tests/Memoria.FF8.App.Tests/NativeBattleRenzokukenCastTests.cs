using static Memoria.FF8.Tests.NativeBattleTypedFixture;
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
    public void Installed_original160_completes_four_authored_contacts_and_private_continuation()
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

    [Fact]
    public void Installed_original333_completes_eight_authored_contacts_at_crisis4_and_standalones()
    {
        Installed_selected_Renzokuken_completes_original_effect_and_standalones(333, 8, 4);
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
        BattleCombatantDefinition ifrit = source.Combatants[1] with { NativeRenzokukenMagicIds = assets.ReadNativeRenzokukenMagicIds(61) };
        BattleDefinition definition = source with { NativeRenzokukenResources = game.ReadNativeRenzokukenResources(), NativeCommandNames = assets.ReadNativeCommandNames(), Combatants = [source.Combatants[0], ifrit] };
        if (effectId == 160)
        {
            BattleCombatantDefinition zell = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, 1, effectId).Combatants[0] with { Id = "party-1", InitialAtb = 0 };
            BattleCombatantDefinition irvine = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, 2, effectId).Combatants[0] with { Id = "party-2", InitialAtb = 0 };
            BattleCombatantDefinition[] actors = [definition.Combatants[0], zell, irvine, ifrit];
            definition = definition with { Combatants = actors, NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), assets.LoadEncounter(94)) };
        }
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition);
        int capturedFrames = 0;
        for (int callback = 0; callback < 400 && !battle.CanChoosePlayerCommand("party"); callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.CanChoosePlayerCommand("party"));
        if (effectId == 160)
        {
            Assert.False(battle.UsesExpandedRoster);
            Assert.Equal(new[] { 0, 1, 2 }, battle.Combatants.Where(ally => ally.IsPlayer).Select(ally => ally.ModelId));
            string[] partyIds = ["party", "party-1", "party-2"];
            for (byte slot = 0; slot < partyIds.Length; slot++)
            {
                BattleModelOwner owner = battle.ReadPresentationModelOwner(partyIds[slot]);
                Assert.Same(battle.Presentation!.Graphics.ActorModels.Get(owner), ReadOriginalModelCell(battle.Presentation?.Graphics, slot));
                Assert.True(battle.NativeActionPackets.ModelAliases.TryEncodeExact(owner, out byte alias));
                Assert.Equal(slot, alias);
            }
        }
        //A declared component seed selects the selected original contact resource and no optional finisher.
        NativeBattleRandomState random = Enumerable.Range(0, 256).Select(seed => NativeBattleRandomState.Initialize((byte)seed)).First(candidate =>
        {
            candidate = candidate.Draw(out byte resource).Draw(out byte finisher);
            return ifrit.NativeRenzokukenMagicIds[crisis + 1 + resource % 2] == effectId && !NativeBattleRenzokukenRules.HasFinisher(crisis, finisher);
        });
        BattleSnapshot ready = battle.Capture();
        const uint transitionColor = 0x00312617;
        battle = BattleSession.Restore(NativeBattleStorageSnapshots.Capture(ready with { NativeLimits = [new("party", crisis)], Random = random, NativeStage = ready.NativeStage! with { ModelColors = ready.NativeStage.ModelColors! with { Color40 = transitionColor } } }));
        const int actorIndex = 0;
        uint? shadowColor = ReadOriginalModelCell(battle.Presentation!.Graphics, actorIndex).Color44;
        Assert.NotNull(shadowColor);
        uint interpolatedColor = NativeBattleGteColorInterpolation.CodeConstants45F270.Interpolate(transitionColor, 1 << 9, 0x4060F0);
        bool interpolatedCurrentStageColor = false;
        battle.QueueNativePlayerCommand("party", "enemy", 5, 0);
        CompareNext();
        Assert.Equal(effectId, battle.GetNativeRenzokuken("party")!.MagicId);
        bool selected = false;

        bool registeredCamera = false;
        bool capturedMidpoint = false;
        for (int callback = 0; callback < 4096; callback++)
        {
            battle.Tick();
            Assert.Equal(shadowColor, ReadOriginalModelCell(battle.Presentation!.Graphics, actorIndex).Color44);
            interpolatedCurrentStageColor |= ReadOriginalModelCell(battle.Presentation!.Graphics, actorIndex).Color40 == interpolatedColor;
            selected |= battle.Presentation!.Graphics.OriginalEffect?.Binding.Effect == effectId;
            if (effectId is 141 or 160 or 259 or 332 or 333 && NativeBattleRenzokukenCaptureAssertions.AssertFrame(battle.Capture(), effectId) > 0)
            {
                capturedFrames++;
            }
            if (battle.Presentation!.Graphics.OriginalEffect is { ReturnedDescriptor: > 0 } effect && effect.Binding.Effect == effectId)
            {
                NativeBattleEffectAnimationRegisters registers = Assert.IsType<NativeBattleEffectAnimationRegisters>(battle.Presentation!.Memory.Textures!.Animation);
                NativeBattleEffectAnimationRegisters camera = Assert.IsType<NativeBattleEffectAnimationRegisters>(battle.Presentation!.Camera.ControlResource);
                Assert.Equal((registers.BaseOffset, registers.StreamOffset, registers.ModelTableOffset), (camera.BaseOffset, camera.StreamOffset, camera.ModelTableOffset));
                Assert.Equal(registers.Source.Identity, camera.Source.Identity);
                Assert.Equal(registers.Source.Data, camera.Source.Data);
                Assert.Null(battle.Presentation!.Camera.ControlStagingSource);
                registeredCamera = true;
            }
            if (!capturedMidpoint && battle.Presentation!.Graphics.TriggerWidget?.Input.ContactIndex > 0)
            {
                CompareNext();
                capturedMidpoint = true;
            }
            if (selected && battle.GetNativeRenzokuken("party") is null && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.Presentation!.Graphics.OriginalEffect is { ReturnedDescriptor: 0 })
            {
                if (effectId is 141 or 160 or 259 or 332 or 333)
                {
                    Assert.True(capturedFrames > 1);
                }
                Assert.True(capturedMidpoint);
                Assert.True(registeredCamera);
                Assert.True(interpolatedCurrentStageColor, "Renzokuken must interpolate the current stage transition color into model DWORD40 without changing the shadow command in DWORD44.");
                Assert.Equal(contacts, battle.Events.Count(entry => entry.Kind == "Damage" && entry.ActorId == "party" && entry.TargetId == "enemy"));
                Assert.Null(battle.Capture().NativeTimedAction);
                Assert.Equal((byte)0, battle.Capture().NativeClock!.ActionPause);
                CompareNext();
                return;
            }
        }
        Assert.Fail("Renzokuken did not release its original task and timed command owners within the diagnostic bound.");

        void CompareNext()
        {
            BattleSnapshot snapshot = battle.Capture();
            BattleSession clone = BattleSession.Restore(NativeBattleStorageSnapshots.Capture(snapshot));
            CompareState(battle, clone);
            battle.Tick();
            clone.Tick();
            if (NativeBattleRenzokukenCaptureAssertions.AssertFrame(battle.Capture(), effectId) > 0)
            {
                capturedFrames++;
            }
            _ = NativeBattleRenzokukenCaptureAssertions.AssertFrame(clone.Capture(), effectId);
            CompareState(battle, clone);
            Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, definition).Render(clone, 320, 224));
        }

        static void CompareState(BattleSession actual, BattleSession copy)
        {
            BattleSnapshot expected = actual.Capture();
            BattleSnapshot observed = copy.Capture();
            Assert.Equal(expected.Frame, observed.Frame);
            Assert.Equal(expected.Outcome, observed.Outcome);
            Assert.Equal(expected.NativeGraphics!.Callback, observed.NativeGraphics!.Callback);
            Assert.Equal(expected.NativeTimedAction, observed.NativeTimedAction);
            Assert.Equal(expected.NativeLimits.Select(limit => limit.Renzokuken), observed.NativeLimits.Select(limit => limit.Renzokuken));
            Assert.Equal(expected.NativeBattleUiRandom, observed.NativeBattleUiRandom);
            NativeBattleRandomState expectedRandom = Assert.IsType<NativeBattleRandomState>(expected.Random);
            NativeBattleRandomState observedRandom = Assert.IsType<NativeBattleRandomState>(observed.Random);
            Assert.Equal(expectedRandom.Lane, observedRandom.Lane);
            Assert.Equal(expectedRandom.Indexes, observedRandom.Indexes);
            Assert.Equal(actual.Events, copy.Events);
            Assert.Equal(actual.NativeActionPackets.PublishedHeaders(), copy.NativeActionPackets.PublishedHeaders());
        }
    }
}
