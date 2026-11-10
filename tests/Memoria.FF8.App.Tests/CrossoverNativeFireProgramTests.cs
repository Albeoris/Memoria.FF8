using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class CrossoverNativeFireProgramTests(ITestOutputHelper output)
{
    [Fact]
    public void Original_Fire_program_emits_one_contact_and_renders_its_original_sprites_in_the_FFVII_arena()
    {
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root) && !string.IsNullOrWhiteSpace(ff8Root), "Select both original game installations.");
        using GameAssets assets = new(ff8Root!);
        using BattleAssets battle = new(ff8Root!);
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleRenderer arena = new(archive, formation.LocationId);
        CrossoverGunbladeHostPlacement placement = new(new(0, 0, 1700), 0);
        NativeBattleCameraProjection camera = CrossoverNativeSpriteRenderer.Camera(scene.Cameras[0], placement.Matrix);
        CrossoverNativeFireState state = CrossoverNativeFireProgram.Begin(assets, 4, NativeCrtRandomState.FromColdProcess(), new(3, 4, 0, 0, 0, 83, 0));
        var resources = assets.ReadNativeBattleModuleResources();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        CrossoverNativeSpriteRenderer sprites = new(battle.ReadNativePrimitiveTables(), assets.ReadNativeEffectMeshes(), assets.ReadNativeBattleGraphicsRules().SummonColors!);
        arena.Render(gpu, 320, 240, new(scene.Cameras[0], []));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);
        int contacts = 0;
        int sounds = 0;
        int changedPixels = 0;
        int triangles = 0;
        int contactCallback = 0;
        bool cold = false;
        for (int callback = 1; callback <= 180 && !state.Complete; callback++)
        {
            CrossoverNativeFireStep next = Step(state);
            state = next.State;
            if (next.Contact)
            {
                contacts++;
                contactCallback = callback;
            }
            sounds += next.Sounds.Length;
            if (!state.Draws.IsEmpty && !cold)
            {
                CrossoverNativeFireState restored = CrossoverNativeFireProgram.Restore(JsonSerializer.Deserialize<CrossoverNativeFireState>(JsonSerializer.Serialize(CrossoverNativeFireProgram.Capture(state)))!);
                Assert.Equal(JsonSerializer.Serialize(CrossoverNativeFireProgram.Capture(Step(state).State)), JsonSerializer.Serialize(CrossoverNativeFireProgram.Capture(Step(restored).State)));
                state = restored;
                cold = true;
            }
            if (!state.Draws.IsEmpty)
            {
                arena.Render(Overlay(), 320, 240, new(scene.Cameras[0], []));
                Color[] frame = GpuTestDevice.Read(gpu.Target);
                changedPixels = Math.Max(changedPixels, baseline.Zip(frame).Count(pair => pair.First != pair.Second));
            }
        }
        Assert.True(state.Complete && cold);
        Assert.Equal(1, contacts);
        Assert.Equal(8, contactCallback);
        Assert.True(triangles > 0 && changedPixels > 8 && sounds > 0);
        Assert.True(state.ContactPublished);
        Assert.Same(state, Step(state).State);
        output.WriteLine($"Native Fire contact callback {contactCallback}, sprite triangles {triangles}, changed GPU pixels {changedPixels}, sound requests {sounds}");

        CrossoverNativeFireStep Step(CrossoverNativeFireState current) => CrossoverNativeFireProgram.Advance(current,
            model => model == 3 ? new(0, -600, -2200, 500) : throw new InvalidOperationException("Fire queried an unbound physical alias."),
            model => model == 3 ? new(new(0, 0, -2200), new(0, 0, -2200), 0, 0, 1024) : throw new InvalidOperationException("Fire queried an unbound transform."), resources);

        void AddSprites()
        {
            triangles += sprites.Draw(gpu, state.Draws, state.Textures, camera, 320, 240, CrossoverGunbladeHostPlacement.OriginToHostScale);
        }

        OverlayRenderer Overlay() => new(gpu, AddSprites);
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
