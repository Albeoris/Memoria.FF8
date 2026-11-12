using System.Collections.Immutable;
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
    [Theory]
    [InlineData((ushort)1)]
    [InlineData((ushort)4)]
    [InlineData((ushort)7)]
    [InlineData((ushort)21)]
    public void Embedded_original_spell_source_retains_its_native_random_and_one_contact_through_uninterrupted_presentation(ushort ability)
    {
        bool cure = ability == 21;
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? hostRoot = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(hostRoot), "Select both original game installations.");
        using GameAssets game = new(root!);
        using BattleAssets battle = new(root!);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(game.ReadNewGameCharacterVitals(), battle, 0);
        NativeBattleGraphicsResource resource = battle.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = kit.Weapon });
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.CompleteLoad(new(0, 0)
        {
            Transform = new(new(0, 0, 2200), new(0, 0, 2200), 0, 0, 4096),
            LinkedTransform = new(default, default),
            SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }
        }, 0, 0, ref random);
        model = NativeBattleSequenceMachine.Advance(NativeBattleGraphicsModels.SelectSequence(model, 1), resource, new(0, 0)).Model;
        model = model with { Flags2 = (ushort)(model.Flags2!.Value & ~4), GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, resource.PoseSource!) };
        CrossoverNativeMagicPresentation presentation = new(game, resource, battle);
        BattleNativeAction action = Assert.Single(kit.Spells, spell => spell.ActionId == ability);
        CrossoverNativeMagicPresentationState state = presentation.Begin(model, cure ? 0 : 4, NativeCrtRandomState.FromColdProcess(), cure ? new(0, 4, 0, 1, 0, 282, 0) : new(3, 4, 0, 0, 0, 83, 0), action, random);
        Assert.Equal(700, state.Cast.Model.Transform!.Scale38);
        Assert.Equal(model.GeometryWorldMatrix, state.Cast.Model.GeometryWorldMatrix);
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
        bool castingGround = false;
        bool observedEffect = false;
        bool rejectedLiveCapture = false;
        int renderedShakes = 0;
        for (int callback = 1; callback <= 250; callback++)
        {
            CrossoverNativeMagicPresentationStep next = Step(state);
            state = next.State;
            if (!castingGround && state.Draws.FirstOrDefault(draw => draw.Kind == NativeBattleModuleDrawKind.Mesh && draw.MeshId == 7) is { } ground)
            {
                Assert.Equal(new NativeBattleVector(3078, 4096, 3078), ground.MeshScale);
                castingGround = true;
            }
            Assert.True(state.ViewTranslationY is 0 or 32);
            renderedShakes += state.ViewTranslationY != 0 ? 1 : 0;
            observedEffect |= state.Effect is not null;
            if (!rejectedLiveCapture && state.Effect is not null)
            {
                Assert.Throws<NotSupportedException>(() => CrossoverNativeMagicPresentation.Capture(state));
                rejectedLiveCapture = true;
            }
            contacts += next.Contact ? 1 : 0;
            soundRequests += next.Sounds.Length + next.EffectSounds.Length;
            if (!state.Draws.IsEmpty || state.Effect is not null)
            {
                CrossoverFf8BattleActorVisual visual = new(body, weapon, state.Cast.Model, placement.Matrix) { BodyTextures = state.BodyTextures, Textures = state.Textures };
                CrossoverNativeEffectVisual effect = new(compositor, state.Draws, state.Textures, placement.Matrix, CrossoverGunbladeHostPlacement.OriginToHostScale) { Ordering = state.Ordering, Memory = state.Memory };
                System.Numerics.Vector3 offset = BattleCameraBasis.Create(scene.Cameras[0]).Up * (state.ViewTranslationY * CrossoverGunbladeHostPlacement.OriginToHostScale);
                SceneCameraPlacement displayed = new(scene.Cameras[0].Position - offset, scene.Cameras[0].Focus - offset);
                arena.Render(new OverlayRenderer(gpu, () => triangles += effect.Draw(gpu, displayed, 320, 240)), 320, 240, new(displayed, []) { Guest = visual });
                Color[] frame = GpuTestDevice.Read(gpu.Target);
                changedPixels = Math.Max(changedPixels, baseline.Zip(frame).Count(pair => pair.First != pair.Second));
            }
            if (state.Complete)
            {
                Assert.True(castingGround && observedEffect && rejectedLiveCapture);
                if (ability == 1)
                {
                    Assert.Equal(115, callback);
                }
                if (ability == 4)
                {
                    Assert.Equal(1, renderedShakes);
                    Assert.Equal(0, state.ViewTranslationY);
                }
                Assert.Equal(1, contacts);
                Assert.True(soundRequests >= 3);
                Assert.True(triangles > 0 && changedPixels > 8);
                Assert.Same(state, Step(state).State);
                output.WriteLine($"Native Squall cast ability{ability} returned callback {callback}; original module/effect GPU triangles {triangles}, changed pixels {changedPixels}, sounds {soundRequests}.");
                return;
            }
        }
        throw new InvalidOperationException("Embedded original Fire did not return through its source sequence.");

        CrossoverNativeMagicPresentationStep Step(CrossoverNativeMagicPresentationState current) => presentation.Advance(current, () => new(0, -600, -2200, 500), () => new(new(0, 0, -2200), new(0, 0, -2200), 0, 0, 1024), camera);
    }

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
