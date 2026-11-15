using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverNativeGuardianInvocationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Shiva", (ushort)65)]
    [InlineData("Ifrit", (ushort)66)]
    [InlineData("Siren", (ushort)67)]
    [InlineData("Brothers", (ushort)68)]
    [InlineData("Diablos", (ushort)69)]
    [InlineData("Leviathan", (ushort)71)]
    [InlineData("Pandemona", (ushort)72)]
    [InlineData("Alexander", (ushort)74)]
    [InlineData("Bahamut", (ushort)76)]
    [InlineData("Cactuar", (ushort)77)]
    [InlineData("Eden", (ushort)79)]
    public void Original_guardian_invocation_hides_source_dispatches_contacts_and_reloads_source(string name, ushort ability)
    {
        string? hostRoot = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(hostRoot), "Select the original host game installation.");
        using GameAssets game = new(TestResources.GameDirectory);
        (CrossoverNativeForeignTarget foreign, NativeBattleCameraProjection camera, NativeBattleArenaHost arena) = ReadHostTarget(hostRoot!, game.ReadNativeBattleGraphicsRules());
        using BattleAssets battle = new(TestResources.GameDirectory);
        NativeBattleGraphicsResource resource = battle.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = 0 });
        NativeBattleGraphicsModel source = ReadSource(resource);
        BattleNativeAction action = Assert.Single(battle.NativeActions, action => action.Command == 254 && action.ActionId == ability);
        CrossoverNativeMagicPresentation presentation = new(game, resource, battle, 0, slot => slot switch { 0 => OriginalOwner(0), 4 => OriginalOwner(3), _ => throw new ArgumentOutOfRangeException(nameof(slot)) });
        CrossoverNativeMagicPresentationState state = presentation.Begin(source, 4, NativeCrtRandomState.FromColdProcess(), new(OriginalOwner(3), 4, 0, 0, 0, 83, 0), action, new(0), arena);
        Assert.True(state.Cast.GuardianInvocation);
        Assert.Equal((byte)14, state.Cast.Model.Sequence);
        bool hidden = false;
        bool prepared = false;
        bool invoked = false;
        int contacts = 0;
        int visualCallbacks = 0;
        int callback = 0;
        for (; callback < 4096 && !state.Complete; callback++)
        {
            CrossoverNativeMagicPresentationStep step = presentation.Advance(state, () => foreign.ToActor(OriginalOwner(3)).Anchor, () => foreign.Transform, camera, () => foreign);
            state = step.State;
            prepared |= state.Effect is { Invoked: false };
            if (state.Effect is { Invoked: true })
            {
                invoked = true;
                Assert.Equal((byte)254, state.Command);
                Assert.Equal(0, state.Effect.Graphics.OriginalEffect!.Parameters!.Header.Ordinal);
            }
            hidden |= (state.Cast.Model.Flags0 & 8) != 0;
            contacts += step.ContactTargets.Length;
            visualCallbacks += !state.Draws.IsEmpty || state.Ordering is { Proxies.Count: > 0 } ? 1 : 0;
            Assert.Equal(0, state.ViewTranslationY);
        }
        Assert.True(state.Complete, $"{name}: phase {state.Cast.Phase}; sequence {state.Cast.Model.Sequence}; service {state.Cast.Model.Kind5}/{state.Cast.Model.Phase6}; source flags {state.Cast.Model.Flags0:X4}; resource flags {state.Cast.Model.ResourceFlags44:X4}; invoked {state.Effect?.Invoked}; effect complete {state.Effect?.Complete}.");
        Assert.True(prepared && invoked && hidden);
        Assert.Equal(1, contacts);
        Assert.Equal(1, state.PublishedContacts);
        Assert.True(visualCallbacks > 0);
        Assert.Equal(2, state.Cast.Model.Flags0 & 0xE);
        Assert.Equal(source.Transform!.Initial20, state.Cast.Model.Transform!.Initial20);
        Assert.Equal(source.Transform.Position28, state.Cast.Model.Transform.Position28);
        Assert.Null(state.Cast.Model.ResourceMagicId);
        Assert.Null(state.Cast.Model.ActiveStagedResourceRow100);
        Assert.NotNull(state.Cast.Model.TextureResourceIndex);
        Assert.NotNull(state.Cast.Model.BodyTextureSlots);
        Assert.Equal(state.Cast.Model.ActiveResourceRow100, state.Cast.Model.OriginalResourceRow104);
        Assert.Equal((byte)1, state.Cast.Model.Sequence);
        Assert.Same(state, presentation.Advance(state, () => foreign.ToActor(OriginalOwner(3)).Anchor, () => foreign.Transform, camera, () => foreign).State);
        output.WriteLine($"{name}: {callback} callbacks; {visualCallbacks} visual callbacks; {contacts} contact; original source body and linked resources restored.");
    }

    [Fact]
    public void Source_animation_return_does_not_complete_an_action_before_its_effect_returns()
    {
        using BattleAssets battle = new(TestResources.GameDirectory);
        NativeBattleGraphicsResource resource = battle.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = 0 });
        CrossoverNativeCastState state = new(NativeBattleGraphicsModels.SelectSequence(ReadSource(resource), 5), 6, false);
        bool returned = false;
        for (int callback = 0; callback < 256 && !returned; callback++)
        {
            var step = CrossoverNativeCastPresentation.AdvanceModel(OriginalOwner(0), state, resource, new(0, 0));
            state = step.State;
            returned = step.Cues.Any(cue => cue.Kind == NativeBattleSequenceCueKind.RestoreStanding);
        }
        Assert.True(returned);
        Assert.False(state.ReturnedToStanding);
        Assert.False(state.Complete);
        Assert.Equal((byte)6, CrossoverNativeCastPresentation.Poll(state, false, false).State.Phase);
        state = CrossoverNativeCastPresentation.Poll(state, false, true).State;
        state = CrossoverNativeCastPresentation.Poll(state, false, true).State;
        for (int callback = 0; callback < 256 && !state.Complete; callback++)
        {
            state = CrossoverNativeCastPresentation.AdvanceModel(OriginalOwner(0), state, resource, new(0, 0)).State;
        }
        Assert.True(state.Complete);
        Assert.Equal((byte)1, state.Model.Sequence);
    }

    [Fact]
    public void Host_geometric_ribbon_policy_allows_preflight_without_changing_source_or_publishing_contacts()
    {
        string? hostRoot = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(hostRoot), "Select the original host game installation.");
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets battle = new(TestResources.GameDirectory);
        (CrossoverNativeForeignTarget foreign, NativeBattleCameraProjection camera, NativeBattleArenaHost arena) = ReadHostTarget(hostRoot!, game.ReadNativeBattleGraphicsRules());
        NativeBattleGraphicsResource resource = battle.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = 0 });
        BattleNativeAction action = Assert.Single(battle.NativeActions, action => action.Command == 254 && action.ActionId == 64);
        CrossoverNativeMagicPresentation presentation = new(game, resource, battle, 0, slot => slot switch { 0 => OriginalOwner(0), 4 => OriginalOwner(3), _ => throw new ArgumentOutOfRangeException(nameof(slot)) });
        CrossoverNativeMagicPresentationState state = presentation.Begin(ReadSource(resource), 4, NativeCrtRandomState.FromColdProcess(), new(OriginalOwner(3), 4, 0, 0, 0, 83, 0), action, new(0), arena);
        NativeBattleGraphicsModel before = state.Cast.Model;
        NativeCrtRandomState random = state.Random;
        NativeGlobalRandomState? globalRandom = state.GlobalRandom;
        object texturePublication = state.Textures.VramPublicationIdentity;
        Assert.True(presentation.TryPreflight(state, () => foreign.ToActor(OriginalOwner(3)).Anchor, () => foreign.Transform, camera, () => foreign, out string? reason), reason);
        Assert.Null(reason);
        Assert.Equal(before, state.Cast.Model);
        Assert.Equal(random, state.Random);
        Assert.Equal(globalRandom, state.GlobalRandom);
        Assert.Same(texturePublication, state.Textures.VramPublicationIdentity);
        Assert.Null(state.Effect);
        Assert.Equal(0, state.PublishedContacts);
        Assert.Empty(state.PublishedContactOrdinals);
        Assert.Equal((byte)3, state.Cast.Phase);
        output.WriteLine($"{action.Name}: host geometric-ribbon preflight completed without changing source, random, texture or published-contact owners.");
    }

    [Fact]
    public void Guardian_entry_waits_for_disappearance_and_restores_through_sequence_twenty()
    {
        NativeBattleGraphicsModel model = new(2) { ModelId4 = 0, ResourceFlags44 = 0x20, Flags2 = 0, Flags8 = 0, SequenceState = new() };
        CrossoverNativeCastState state = new(model, 4, false) { GuardianInvocation = true };
        Assert.False(CrossoverNativeCastPresentation.Poll(state, false, false).InvokeEffect);
        state = state with { Model = model with { Flags0 = 6 } };
        Assert.False(CrossoverNativeCastPresentation.Poll(state, true, false).InvokeEffect);
        CrossoverNativeCastPoll entered = CrossoverNativeCastPresentation.Poll(state, false, false);
        Assert.True(entered.InvokeEffect);
        Assert.Equal(12, entered.State.Model.Flags0 & 0xE);
        Assert.Equal(0, entered.State.Model.ResourceFlags44!.Value & 0x20);
        Assert.False(CrossoverNativeCastPresentation.Poll(entered.State, false, false).ReloadSourceResources);
        CrossoverNativeCastPoll completed = CrossoverNativeCastPresentation.Poll(entered.State, false, true);
        Assert.True(completed.ReloadSourceResources);
        CrossoverNativeCastState returning = CrossoverNativeCastPresentation.Poll(completed.State, false, true).State;
        Assert.Equal((byte)20, returning.Model.Sequence);
        Assert.Equal(2, returning.Model.Flags0 & 0xE);
        Assert.False(returning.Complete);
    }

    private static NativeBattleGraphicsModel ReadSource(NativeBattleGraphicsResource resource)
    {
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel source = NativeBattleGraphicsModels.CompleteLoad(new(0)
        {
            FileSelector128 = 0,
            FileSelector129 = 0,
            Transform = new(new(0, 0, 2200), new(0, 0, 2200), 0, 0, 4096),
            LinkedTransform = new(default, default),
            SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }
        }, 0, 0, true, ref random);
        source = NativeBattleSequenceMachine.Advance(OriginalOwner(0), NativeBattleGraphicsModels.SelectSequence(source, 1), resource, new(0, 0)).Model;
        return source with { Flags2 = (ushort)(source.Flags2!.Value & ~4), GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(source, resource.PoseSource!) };
    }

    internal static (CrossoverNativeForeignTarget Foreign, NativeBattleCameraProjection Camera, NativeBattleArenaHost Arena) ReadHostTarget(string directory, NativeBattleGraphicsRules graphicsRules, CrossoverGunbladeHostPlacement? sourcePlacement = null)
    {
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7BattleSession host = new(formation, original.Progress with { Party = [0, 1, 255] }, KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path)), executable.NativeMateriaStatModifiers, 1, requestFlags: 4, animations: animations);
        Ff7BattleRenderer renderer = new(archive, formation.LocationId);
        Ff7BattleActorState selected = host.Actors.Single(actor => actor.Actor.Slot == 4);
        Ff7BattlePlacement hostPlacement = host.RenderActorPlacement(4);
        (int clip, int frame) = host.RenderActorPose(4);
        Ff7BattleActorVisual visual = new(Ff7BattleAnimationResources.ModelPrefix(selected.Identity, true), hostPlacement.Position, hostPlacement.Yaw * MathF.Tau / 4096, clip, frame, selected.Identity == 16 ? 0 : null, host.RenderActorLimit(4)) { ModelModifiers = host.ActorModelOutput(4) };
        Ff7BattleActorGeometry geometry = renderer.CaptureActorGeometry(visual);
        CrossoverGunbladeHostPlacement placement = sourcePlacement ?? new(Ff7BattlePosition.FromVector(host.RenderActorPlacement(0).Position), host.RenderActorPlacement(0).Yaw);
        CrossoverNativeForeignTarget foreign = CrossoverNativeForeignTarget.Read(host, 4, geometry, placement.Matrix, graphicsRules);
        return (foreign, CrossoverNativeSpriteRenderer.Camera(host.RenderCameraOutput, placement.Matrix), new(CrossoverNativeArena.Read(renderer.CaptureArenaGeometry(), placement.Matrix)));
    }
}
