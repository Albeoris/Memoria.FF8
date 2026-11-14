using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7WaterInitializationPresentationTests
{
    [Fact]
    public void OriginalDolphinModeZeroRetainsNormalMaterialsAcrossRepeatedAndColdDraws()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for the reached water initialization presentation.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        Ff7FieldResources original = Ff7FieldResources.Load(installation, "ujunon2");
        byte[] instruction = original.Scripts.Data.Span.Slice(0x0C22, 30).ToArray();
        Assert.Equal(new byte[] { 0x28, 0x1E, 8 }, instruction[..3]);
        byte[] payload = instruction[3..];
        Assert.Equal(0, payload[0]);
        Ff7Actor baseline = new() { Slot = 0, Entity = 13, Model = 8, Visible = true };
        Ff7NativeWaterEffectState pending = Ff7NativeWaterEffect.Request(null, payload, baseline.Slot);
        Ff7Actor initialized = baseline with { NativeWaterEffect = Ff7NativeWaterEffect.Advance(pending, true) };
        Assert.Equal((short)-32, initialized.NativeWaterEffect!.Parameter0);
        Assert.Equal((short)-64, initialized.NativeWaterEffect.Parameter1);
        Assert.Equal((short)-32, initialized.NativeWaterEffect.Parameter2);
        Assert.Equal((short)0, initialized.NativeWaterEffect.Parameter3);
        Assert.Equal((byte)224, initialized.NativeWaterEffect.Flags);
        Assert.Equal(0, initialized.NativeWaterEffect.CycleIndex);
        Assert.Equal(1, initialized.NativeWaterEffect.SubcycleIndex);
        Assert.True(initialized.NativeWaterEffect.Active);
        Assert.True(initialized.NativeWaterEffect.Initialized);
        Assert.Equal(payload, initialized.NativeWaterEffect.RequestedPayload);
        string saved = JsonSerializer.Serialize(initialized);
        Ff7Actor restored = JsonSerializer.Deserialize<Ff7Actor>(saved)!;
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        using LgpArchive fields = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7FieldModelResources model = Ff7FieldModelResources.Load(characters, original.Models.Models[8], executable, fields);
        Assert.NotEmpty(model.Geometry);
        Assert.Contains(model.Geometry, part => part.Polygon.Normals.Count > 0);
        // An authored neutral camera isolates the original mesh/material submission from gameplay placement.
        PcFieldCamera camera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 10000), 0, 100);
        Ff7FieldResources selected = original with { Background = original.Background with { Layers = [] } };
        Ff7FieldRenderer renderer = new(selected, new Dictionary<int, Ff7FieldModelResources> { [8] = model }, new(50, 32000));
        Capture before = Draw(baseline);
        Capture after = Draw(initialized);
        Capture repeated = Draw(initialized);
        Capture cold = Draw(restored);
        Assert.NotEmpty(before.Vertices);
        Assert.Equal(before.Vertices, after.Vertices);
        Assert.Equal(before.Materials, after.Materials);
        Assert.Equal(after.Vertices, repeated.Vertices);
        Assert.Equal(after.Materials, repeated.Materials);
        Assert.Equal(after.Vertices, cold.Vertices);
        Assert.Equal(after.Materials, cold.Materials);
        Assert.Equal(saved, JsonSerializer.Serialize(initialized));
        Assert.Equal(saved, JsonSerializer.Serialize(restored));

        Capture Draw(Ff7Actor actor)
        {
            Ff7ModelRenderOverrides? effects = Ff7Scene.CreateModelEffects(actor.ModelEffect, camera, null, actor.PartVisibility, actor.NativeColorOffset);
            Assert.Null(effects);
            Ff7ActorVisual visual = new(actor.Entity, actor.Model, 0, 0, 0, 0, 0, 0) { ModelEffects = effects };
            Ff7FieldRenderFrame frame = new(0, Vector2.Zero, [visual], new ushort[256], CameraOverride: camera);
            Capture capture = new();
            renderer.Render(capture, 320, 240, frame);
            return capture;
        }
    }

    private sealed class Capture : ITriangleRenderer
    {
        public List<TriangleVertex> Vertices { get; } = [];
        public List<TriangleMaterial> Materials { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.AddRange([a, b, c]);
            Materials.Add(material);
        }

        public void End()
        {
        }
    }
}
