using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7BattleActorUvTests
{
    [Fact]
    public void NativeUvVectorsUseOriginalCoordinatesAndSingleSubtraction()
    {
        (Vector2 Original, ushort? Packed, Vector2 Expected)[] cases =
        [
            (new(0.75f, 1), 0x0078, new(0.21875f, 0)),
            (new(-0.2f, 0.3f), 0x0078, new(0.26875f, 0.3f)),
            (new(1.5f, 2), 0x0078, new(0.96875f, 1)),
            (new(1, 0.75f), 0x7800, new(0, 0.21875f)),
            (new(1.5f, 2), 0, new(1.5f, 2)),
            (new(1.5f, 2), null, new(1.5f, 2)),
            (new(255f / 256, 255f / 256), 0x0101, Vector2.Zero)
        ];
        foreach (var item in cases)
        {
            Vector2 actual = Ff7BattleRenderer.ApplyActorUvOffset(item.Original, item.Packed);
            Assert.Equal(item.Expected.X, actual.X, 6);
            Assert.Equal(item.Expected.Y, actual.Y, 6);
            Assert.Equal(actual, Ff7BattleRenderer.ApplyActorUvOffset(item.Original, item.Packed));
        }
    }

    [Fact]
    public void InstalledActorCaptureKeepsOffsetsLocalAndOriginalUvImmutable()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(directory), "Select the original battle resources.");
        Ff7Installation installation = Ff7Installation.Discover(directory!);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        Ff7BattleRenderer renderer = new(archive, 0);
        Ff7BattleActorVisual actor = new("ij", Vector3.Zero, 0, 0, 0);
        Ff7BattleActorGeometry baseline = renderer.CaptureActorGeometry(actor);
        Vector2[] original = TexturedUv(baseline);
        Assert.NotEmpty(original);
        Ff7BattleActorVisual animated = actor with { ModelModifiers = new Ff7BattleActorModelModifiers { NativeUvOffset = 0x0078 } };
        Ff7BattleActorGeometry shifted = renderer.CaptureActorGeometry(animated);
        Vector2[] shiftedUv = TexturedUv(shifted);
        Assert.Equal(original.Length, shiftedUv.Length);
        Assert.True(original.Where((uv, index) => uv != shiftedUv[index]).Any());
        Assert.Equal(shiftedUv, TexturedUv(renderer.CaptureActorGeometry(animated)));
        Assert.Equal(original, TexturedUv(renderer.CaptureActorGeometry(actor)));
        Assert.Equal(original, TexturedUv(renderer.CaptureActorGeometry(actor with { ModelModifiers = new Ff7BattleActorModelModifiers { NativeUvOffset = 0 } })));
        Assert.Equal(baseline.Faces.Select(face => (face.A, face.B, face.C)), shifted.Faces.Select(face => (face.A, face.B, face.C)));
        Assert.Equal(baseline.Faces.Select(face => ((NativeBattleForeignRenderMaterial)face.Surface!.Material).Material.Sampler), shifted.Faces.Select(face => ((NativeBattleForeignRenderMaterial)face.Surface!.Material).Material.Sampler));
    }

    private static Vector2[] TexturedUv(Ff7BattleActorGeometry geometry)
    {
        return geometry.Faces.Where(face => face.Surface is { Material: NativeBattleForeignRenderMaterial { Material.Texture: not null } }).SelectMany(face => face.Surface!.Uv).ToArray();
    }
}
