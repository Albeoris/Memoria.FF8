using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class CrossoverNativeCastPresentationTests(ITestOutputHelper output)
{
    [Fact]
    public void Embedded_Fire_source_component_retains_its_native_random_and_one_contact_across_cold_restore()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? hostRoot = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(hostRoot), "Select both original game installations.");
        using GameAssets game = new(root!);
        using BattleAssets battle = new(root!);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.ReadSquall(game.ReadNewGameCharacterVitals(), battle);
        NativeBattleGraphicsResource resource = battle.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = kit.Weapon });
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.CompleteLoad(new(0, 0)
        {
            Transform = new(new(0, 0, 2200), new(0, 0, 2200), 0, 0, 4096),
            LinkedTransform = new(default, default),
            SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }
        }, 0, 0, ref random);
        model = NativeBattleSequenceMachine.Advance(NativeBattleGraphicsModels.SelectSequence(model, 1), resource, new(0, 0)).Model;
        CrossoverNativeFirePresentation presentation = new(game, resource);
        CrossoverNativeFirePresentationState state = presentation.Begin(model, 4, NativeCrtRandomState.FromColdProcess(), new(3, 4, 0, 0, 0, 83, 0));
        BattleModel body = battle.LoadPlayer(0);
        BattleModel weapon = battle.ResolveWeaponModel(0, kit.Weapon, body)!;
        Ff7Installation installation = Ff7Installation.Discover(hostRoot!);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleRenderer arena = new(archive, formation.LocationId);
        CrossoverGunbladeHostPlacement placement = new(new(0, 0, 1700), 0);
        NativeBattleCameraProjection camera = CrossoverNativeSpriteRenderer.Camera(scene.Cameras[0], placement.Matrix);
        CrossoverNativeSpriteRenderer compositor = new(battle.ReadNativePrimitiveTables(), game.ReadNativeEffectMeshes(), game.ReadNativeBattleGraphicsRules().SummonColors!);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        arena.Render(gpu, 320, 240, new(scene.Cameras[0], []));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);
        int triangles = 0;
        int changedPixels = 0;
        int contacts = 0;
        int soundRequests = 0;
        bool castingCold = false;
        bool fireCold = false;
        for (int callback = 1; callback <= 150; callback++)
        {
            CrossoverNativeFirePresentationStep next = Step(state);
            state = next.State;
            contacts += next.Contact ? 1 : 0;
            soundRequests += next.Sounds.Length + next.FireSounds.Length;
            if (!state.Draws.IsEmpty)
            {
                CrossoverFf8BattleActorVisual visual = new(body, weapon, state.Cast.Model, placement.Matrix) { BodyTextures = state.BodyTextures, Textures = state.Textures };
                arena.Render(new OverlayRenderer(gpu, () => triangles += compositor.Draw(gpu, state.Draws, state.Textures, camera, 320, 240, CrossoverGunbladeHostPlacement.OriginToHostScale)), 320, 240, new(scene.Cameras[0], []) { Guest = visual });
                Color[] frame = GpuTestDevice.Read(gpu.Target);
                changedPixels = Math.Max(changedPixels, baseline.Zip(frame).Count(pair => pair.First != pair.Second));
            }
            if (!castingCold && state.Modules.PublishedCount > 0 || !fireCold && state.Fire is { ContactPublished: true })
            {
                CrossoverNativeFirePresentationState restored = CrossoverNativeFirePresentation.Restore(RoundTrip(CrossoverNativeFirePresentation.Capture(state)));
                Assert.Equal(JsonSerializer.Serialize(CrossoverNativeFirePresentation.Capture(Step(state).State)), JsonSerializer.Serialize(CrossoverNativeFirePresentation.Capture(Step(restored).State)));
                castingCold = true;
                fireCold |= state.Fire is not null;
                state = restored;
            }
            if (state.Complete)
            {
                Assert.True(castingCold && fireCold);
                Assert.Equal(114, callback);
                Assert.Equal(1, contacts);
                Assert.True(soundRequests >= 3);
                Assert.True(triangles > 0 && changedPixels > 8);
                Assert.Same(state, Step(state).State);
                output.WriteLine($"Native Squall cast and Fire returned callback {callback}; original module/effect GPU triangles {triangles}, changed pixels {changedPixels}, sounds {soundRequests}.");
                return;
            }
        }
        throw new InvalidOperationException("Embedded original Fire did not return through its source sequence.");

        CrossoverNativeFirePresentationStep Step(CrossoverNativeFirePresentationState current) => presentation.Advance(current, () => new(0, -600, -2200, 500), () => new(new(0, 0, -2200), new(0, 0, -2200), 0, 0, 1024));
    }

    private static T RoundTrip<T>(T source) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source))!;

    private sealed class OverlayRenderer(ITriangleRenderer inner, Action overlay) : ITriangleRenderer
    {
        public void Begin(int width, int height, System.Numerics.Vector4 clearColor, float clearDepth = 1) => inner.Begin(width, height, clearColor, clearDepth);
        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material) => inner.Draw(a, b, c, material);
        public void End()
        {
            overlay();
            inner.End();
        }
    }
}
