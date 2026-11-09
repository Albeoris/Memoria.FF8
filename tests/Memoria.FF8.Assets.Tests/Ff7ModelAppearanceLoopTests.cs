using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7ModelAppearanceLoopTests
{
    [Fact]
    public void InstalledReplacementFollowsEachDescriptorScale()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? Environment.GetEnvironmentVariable("FF8_GAME_DIRECTORY");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ff7) || string.IsNullOrWhiteSpace(ff8), "Explicit FFVII and FFVIII installations are required.");
        using GameAssets assets = new(ff8!);
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        Ff7FieldResources station = Ff7FieldResources.Load(installation, "md1stin");
        Ff7FieldResources next = Ff7FieldResources.Load(installation, "md1_1");
        using Memoria.FF7.Formats.LgpArchive archive = Memoria.FF7.Formats.LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources original = Ff7FieldModelResources.Load(archive, station.Models.Models[0]);
        Ff7ModelAppearance appearance = Ff7ModelAppearance.CreateStandardFieldSquall(installation, assets);
        Ff7ActorVisual actor = new(0, 0, 0, 0, 0, 0, 0, 0);
        Assert.True(appearance.TryCreate(actor, station.Models.Models[0], original, out var first));
        Assert.True(appearance.TryCreate(actor, next.Models.Models[0], original, out var second));
        float ratio = float.Parse(next.Models.Models[0].Scale, System.Globalization.CultureInfo.InvariantCulture) / float.Parse(station.Models.Models[0].Scale, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(2, ratio);
        Assert.Equal(first.Count, second.Count);
        for (int index = 0; index < first.Count; index++)
        {
            Assert.True(Vector3.Distance(first[index].A.Position * ratio, second[index].A.Position) < 0.002f);
            Assert.True(Vector3.Distance(first[index].B.Position * ratio, second[index].B.Position) < 0.002f);
            Assert.True(Vector3.Distance(first[index].C.Position * ratio, second[index].C.Position) < 0.002f);
        }
    }

    [Fact]
    public void OptionalInstalledFactorySamplesNativeSquallAcrossWalkAndRunLoopSeams()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? Environment.GetEnvironmentVariable("FF8_GAME_DIRECTORY");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ff7) || string.IsNullOrWhiteSpace(ff8), "Explicit FFVII and FFVIII installations are required.");
        using GameAssets assets = new(ff8!);
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "md1stin");
        using Memoria.FF7.Formats.LgpArchive characters = Memoria.FF7.Formats.LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources original = Ff7FieldModelResources.Load(characters, field.Models.Models[0]);
        FieldModel target = assets.LoadFieldModelSource(assets.GetFieldModelSource("bghall_1", 0));
        Matrix4x4 calibration = Ff7ModelAppearance.CalibrateNeutralGeometry(target, field.Models.Models[0], original, [19, 20], 21, 22).Transform;
        Ff7ModelAppearance appearance = Ff7ModelAppearance.CreateStandardFieldSquall(installation, assets);
        foreach (int slot in new[] { 0, 1, 2 })
        {
            string originalName = Path.GetFileNameWithoutExtension(field.Models.Models[0].Animations[slot]) + ".a";
            int sourceCount = original.Animations[originalName].Frames.Count;
            int targetCount = target.Animations[slot].Frames.Count;
            int sourcePhase16 = slot == 0 ? 0 : sourceCount * 16 - 1;
            Ff7ActorVisual actor = new(0, 0, 100 * 4096, 200 * 4096, 300 * 4096, 73, slot, sourcePhase16);
            Assert.True(appearance.TryCreate(actor, field.Models.Models[0], original, out var triangles));
            float phase = sourcePhase16 / 16f * targetCount / sourceCount;
            Vector3[] nativePose = target.PoseInterpolated(slot, phase, phase, 0, loop: true);
            Vector3[] clampedPose = target.PoseInterpolated(slot, phase, phase, 0, loop: false);
            if (slot != 0)
            {
                Assert.Contains(nativePose.Select((vertex, index) => Vector3.Distance(vertex, clampedPose[index])), distance => distance > 0.01f);
            }
            Matrix4x4 world = calibration * Matrix4x4.CreateRotationZ(actor.Direction * MathF.Tau / 256) * Matrix4x4.CreateTranslation(100, 200, 300);
            List<Vector3> expected = [];
            foreach (ModelFace face in target.Faces)
            {
                Add(face.Vertices[0], face.Vertices[1], face.Vertices[2]);
                if (face.Quad)
                {
                    Add(face.Vertices[1], face.Vertices[3], face.Vertices[2]);
                }
            }
            Vector3[] actual = triangles.SelectMany(triangle => new[] { triangle.A.Position, triangle.B.Position, triangle.C.Position }).ToArray();
            Assert.Equal(expected.Count, actual.Length);
            for (int index = 0; index < actual.Length; index++)
            {
                Assert.True(Vector3.Distance(expected[index], actual[index]) < 0.002f, $"Native Squall slot {slot}, submitted vertex {index} did not sample the native loop seam.");
            }
            Assert.All(triangles.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }), vertex => Assert.Null(vertex.Normal));

            void Add(int first, int second, int third)
            {
                Vector3 a = Vector3.Transform(nativePose[first], world);
                Vector3 b = Vector3.Transform(nativePose[second], world);
                Vector3 c = Vector3.Transform(nativePose[third], world);
                if (Vector3.Cross(b - a, c - a).LengthSquared() != 0)
                {
                    expected.Add(a);
                    expected.Add(b);
                    expected.Add(c);
                }
            }
        }
    }
}
