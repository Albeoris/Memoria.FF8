using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldSceneReferenceExchangeTests
{
    [Fact]
    public void ExportsNativeSlotsExactProjectionAndGuideBasis()
    {
        FieldCamera first = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(3, 5, 256), new Vector2(2, 4), 320);
        FieldCamera second = first with { AxisX = new(1, 0.1f, 0), Pan = new(10, 20) };
        FieldWalkmesh mesh = new([new(Vector3.Zero, new(128, 0, 0), new(0, 128, 0), -1, 4, -1)]);
        FieldAssets field = new(123, "synthetic", [], [], [], first, mesh, []) { Cameras = [first, second] };
        FieldSceneReferenceCapture capture = new(1, second, 2, 2, new byte[16])
        {
            ProjectionScale = new(320, 340),
            PrincipalPoint = new(0.75f, 1.25f),
            OutputViewport = new(0, 0, 2, 2),
            SimulationFrame = 17,
            Actors = [new(42, -1, Vector3.Zero, 0, 0, 0)]
        };
        DirectoryInfo directory = Directory.CreateTempSubdirectory();
        try
        {
            string path = FieldSceneReferenceExchange.Export(directory.FullName + Path.DirectorySeparatorChar, field, capture);
            using JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = metadata.RootElement;
            Assert.Equal("synthetic", root.GetProperty("field").GetProperty("name").GetString());
            Assert.Equal(1, root.GetProperty("capture").GetProperty("cameraSlot").GetInt32());
            Assert.Equal(17, root.GetProperty("capture").GetProperty("simulationFrame").GetInt64());
            Assert.Equal("background.png", root.GetProperty("capture").GetProperty("background").GetString());
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, File.ReadAllBytes(Path.Combine(directory.FullName, "background.png"))[..8]);
            JsonElement[] cameras = root.GetProperty("cameras").EnumerateArray().ToArray();
            Assert.Equal(2, cameras.Length);
            Assert.Equal(0.1f, cameras[1].GetProperty("source").GetProperty("axisX")[1].GetSingle());
            JsonElement active = cameras[1].GetProperty("projection");
            Assert.Equal(0.75f, active.GetProperty("principalPixels")[0].GetSingle());
            Assert.Equal(340, active.GetProperty("focalPixels")[1].GetSingle());
            Assert.True(active.GetProperty("approximate").GetBoolean());
            Assert.Equal(3, active.GetProperty("reprojection").GetProperty("samples").GetInt32());
            Assert.Equal(new[] { -1, 4, -1 }, root.GetProperty("walkmesh").GetProperty("triangles")[0].GetProperty("adjacency").EnumerateArray().Select(value => value.GetInt32()));
            float[] glb = root.GetProperty("calibration").GetProperty("landmarks")[2].GetProperty("glb").EnumerateArray().Select(value => value.GetSingle()).ToArray();
            Assert.Equal(new[] { 0f, 0f, -1f }, glb);

            byte[] bytes = File.ReadAllBytes(Path.Combine(directory.FullName, "reference.glb"));
            Assert.Equal(0x46546c67u, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
            Assert.Equal(bytes.Length, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8)));
            int jsonSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12));
            using JsonDocument glbDocument = JsonDocument.Parse(bytes.AsMemory(20, jsonSize));
            JsonElement[] slots = glbDocument.RootElement.GetProperty("nodes").EnumerateArray().Where(node => node.TryGetProperty("camera", out _)).ToArray();
            Assert.Equal(new[] { 0, 1 }, slots.Select(node => node.GetProperty("extras").GetProperty("memoria_camera_slot").GetInt32()));
            Assert.Throws<IOException>(() => FieldSceneReferenceExchange.Export(directory.FullName, field, capture));
            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(directory.FullName, "reference.glb")));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
