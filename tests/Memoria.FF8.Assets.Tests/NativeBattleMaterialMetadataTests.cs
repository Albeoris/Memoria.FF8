using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMaterialMetadataTests(ITestOutputHelper output)
{
    [Fact]
    public void OriginalStage31AndSquallRetainCompleteNativeTexturePageWords()
    {
        using BattleAssets assets = new(LaunchOptions.DefaultGameDirectory);
        Check("stage31", assets.LoadStage(31).Meshes);
        Check("Squall", assets.LoadPlayer(0).Meshes);

        void Check(string name, IReadOnlyList<BattleMesh> meshes)
        {
            BattleFace[] faces = meshes.SelectMany(mesh => mesh.Faces).ToArray();
            Assert.NotEmpty(faces);
            Assert.All(faces, face =>
            {
                Assert.NotNull(face.NativeMaterial);
                Assert.Equal(face.Uv.Length, face.NativeMaterial!.Uv.Length);
                Assert.All(face.NativeMaterial.Uv, uv =>
                {
                    Assert.InRange(uv.X, 0, 255);
                    Assert.InRange(uv.Y, 0, 255);
                });
            });
            string pages = string.Join(", ", faces.GroupBy(face => face.NativeMaterial!.TexturePage).OrderBy(group => group.Key).Select(group => $"{group.Key:X4}:{group.Count()}"));
            string abr = string.Join(", ", faces.GroupBy(face => (face.NativeMaterial!.TexturePage >> 5) & 3).OrderBy(group => group.Key).Select(group => $"{group.Key}:{group.Count()}"));
            output.WriteLine($"{name}: faces={faces.Length}; TPAGE={pages}; ABR={abr}");
        }
    }
}
