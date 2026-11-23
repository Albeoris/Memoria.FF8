using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Assets;

public sealed record FieldSceneReferenceActor(int ObjectId, int ModelId, Vector3 Position, float Direction, int AnimationId, float AnimationFrame);

public sealed record FieldSceneReferenceCapture(int CameraSlot, FieldCamera EffectiveCamera, int Width, int Height, byte[]? BackgroundRgba = null)
{
    public string ResourceProvider { get; init; } = "FFVIII";
    public string? Language { get; init; }
    public string Presentation { get; init; } = "native";
    public TimeSpan Elapsed { get; init; }
    public Vector2 ActorScroll { get; init; }
    public Vector2 DrawingOrigin { get; init; } = new(160, 112);
    public Vector2 Jitter { get; init; }
    public Vector4 DrawingRectangle { get; init; } = new(0, 0, 320, 224);
    public Vector4 OutputViewport { get; init; }
    public Vector2 ProjectionScale { get; init; }
    public Vector2 PrincipalPoint { get; init; }
    public string Framing { get; init; } = "native";
    public long? ResourceGeneration { get; init; }
    public long? SimulationFrame { get; init; }
    public IReadOnlyDictionary<int, int> BackgroundStates { get; init; } = new Dictionary<int, int>();
    public IReadOnlyList<FieldSceneReferenceActor> Actors { get; init; } = [];
}

/// <summary>Exports installed-field resources as local Blender guides.</summary>
public static class FieldSceneReferenceExchange
{
    private const float NativeUnitsPerBlenderUnit = 128;
    private const float ApproximationTolerancePixels = 0.5f;
    private static readonly Matrix4x4 FieldToGlb = new(
        1 / NativeUnitsPerBlenderUnit, 0, 0, 0,
        0, 0, -1 / NativeUnitsPerBlenderUnit, 0,
        0, 1 / NativeUnitsPerBlenderUnit, 0, 0,
        0, 0, 0, 1);

    public static string Export(string directory, FieldAssets field, FieldSceneReferenceCapture capture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (capture.Width is < 1 or > 8192 || capture.Height is < 1 or > 8192 ||
            capture.BackgroundRgba is { } rgba && rgba.Length != checked(capture.Width * capture.Height * 4) ||
            capture.CameraSlot < 0 || capture.CameraSlot >= field.Cameras.Count)
        {
            throw new ArgumentException("Invalid field reference capture dimensions or native camera slot.", nameof(capture));
        }
        directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
        {
            throw new IOException("Choose an empty directory for the field reference export.");
        }
        string staging = directory + "." + Guid.NewGuid().ToString("N") + ".tmp";
        Directory.CreateDirectory(staging);
        try
        {
            WriteReference(staging, field, capture);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory);
            }
            Directory.Move(staging, directory);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                foreach (string file in Directory.EnumerateFiles(staging))
                {
                    File.Delete(file);
                }
                Directory.Delete(staging);
            }
        }
        return Path.Combine(directory, "reference.json");
    }

    private static void WriteReference(string directory, FieldAssets field, FieldSceneReferenceCapture capture)
    {
        FieldWalkmesh walkmesh = field.Walkmesh;
        Vector3[] landmarks = walkmesh.Triangles.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }).Distinct().ToArray();
        Vector4 viewport = capture.OutputViewport is { Z: > 0, W: > 0 } explicitViewport ? explicitViewport : new(0, 0, capture.Width, capture.Height);
        Vector2 drawingScale = new(viewport.Z / capture.DrawingRectangle.Z, viewport.W / capture.DrawingRectangle.W);
        bool hasEffectiveIntrinsics = capture.ProjectionScale is { X: > 0, Y: > 0 };
        Vector2 effectiveFocal = hasEffectiveIntrinsics ? capture.ProjectionScale : drawingScale * capture.EffectiveCamera.FocalLength;
        Vector2 effectivePrincipal = hasEffectiveIntrinsics ? capture.PrincipalPoint :
            new Vector2(viewport.X, viewport.Y) + drawingScale * (capture.DrawingOrigin + capture.EffectiveCamera.Pan + capture.ActorScroll + capture.Jitter);
        ReferenceCamera?[] cameras = new ReferenceCamera?[field.Cameras.Count];
        for (int slot = 0; slot < cameras.Length; slot++)
        {
            FieldCamera native = field.Cameras[slot];
            bool active = slot == capture.CameraSlot;
            FieldCamera source = active ? capture.EffectiveCamera : native;
            Vector2 focal = active ? effectiveFocal : drawingScale * native.FocalLength;
            Vector2 principal = active ? effectivePrincipal : new Vector2(viewport.X, viewport.Y) + drawingScale * (capture.DrawingOrigin + native.Pan);
            cameras[slot] = MakeCamera(source, focal, principal, landmarks);
        }
        string? background = null;
        if (capture.BackgroundRgba is { } bytes)
        {
            background = "background.png";
            Rgba32[] pixels = MemoryMarshal.Cast<byte, Rgba32>(bytes.AsSpan()).ToArray();
            File.WriteAllBytes(Path.Combine(directory, background), LocalizationArtworkExporter.EncodePng(new PixelImage(capture.Width, capture.Height, pixels)));
        }
        Matrix4x4.Invert(FieldToGlb, out Matrix4x4 glbToField);
        string referenceGlb = "reference.glb";
        File.WriteAllBytes(Path.Combine(directory, referenceGlb), BuildGlb(field, capture, cameras));
        object reference = new
        {
            schemaVersion = 1,
            toolVersion = typeof(FieldSceneReferenceExchange).Assembly.GetName().Version?.ToString(),
            referenceGlb,
            field = new { field.Id, field.Name, capture.ResourceProvider, capture.Language, capture.ResourceGeneration, field.WalkmeshHash, field.NativeExecutableHash, scriptSha256 = Convert.ToHexString(SHA256.HashData(field.Jsm)) },
            capture = new
            {
                capture.CameraSlot, capture.Width, capture.Height, pixelAspect = new[] { 1, 1 }, capture.Presentation, capture.Framing, capture.SimulationFrame, elapsedSeconds = capture.Elapsed.TotalSeconds,
                drawingRectangle = Values(capture.DrawingRectangle), outputViewport = Values(viewport), actorScroll = Values(capture.ActorScroll), drawingOrigin = Values(capture.DrawingOrigin), jitter = Values(capture.Jitter),
                effectiveSource = SourceCamera(capture.EffectiveCamera), focalPixels = Values(effectiveFocal), principalPixels = Values(effectivePrincipal),
                background, capture.BackgroundStates
            },
            cameras = field.Cameras.Select((camera, slot) => new
            {
                slot, source = SourceCamera(camera),
                cameraBounds = Bounds(field.Information?.CameraBounds, slot), screenBounds = Bounds(field.Information?.ScreenBounds, slot),
                projection = cameras[slot] is ReferenceCamera exported ? new
                {
                    focalPixels = Values(exported.FocalPixels), principalPixels = Values(exported.PrincipalPixels),
                    origin = Values(exported.Origin), reprojection = exported.Residual,
                    approximate = exported.Residual.Samples == 0 || exported.Residual.MaximumPixels > ApproximationTolerancePixels,
                    tolerancePixels = ApproximationTolerancePixels,
                    captureRelative = slot == capture.CameraSlot
                } : null
            }),
            calibration = new
            {
                matrixConvention = "Row vectors, row-major matrices; multiply on the right.",
                fieldAxes = "XY ground, +Z up",
                glbAxes = "+X right, +Y up, -Z forward",
                blenderAxes = "+X right, +Y forward, +Z up",
                nativeUnitsPerBlenderUnit = NativeUnitsPerBlenderUnit,
                fieldToGlb = Values(FieldToGlb), glbToField = Values(glbToField),
                fieldToBlender = Values(Matrix4x4.CreateScale(1 / NativeUnitsPerBlenderUnit)),
                blenderToField = Values(Matrix4x4.CreateScale(NativeUnitsPerBlenderUnit)),
                landmarks = landmarks.Select((location, id) => new { id, field = Values(location), glb = Values(Vector3.Transform(location, FieldToGlb)) })
            },
            walkmesh = new
            {
                referenceOnly = true,
                triangles = walkmesh.Triangles.Select((triangle, id) => new
                {
                    id, a = Values(triangle.A), b = Values(triangle.B), c = Values(triangle.C),
                    adjacency = new[] { triangle.NeighborA, triangle.NeighborB, triangle.NeighborC }
                })
            },
            gates = field.Information?.Gateways.Select((gate, id) => (gate, id)).Where(item => item.gate.Defined || item.gate.DestinationField < 72).Select(item => new { item.id, start = Values(item.gate.Start), end = Values(item.gate.End), item.gate.DestinationField }),
            triggers = field.Information?.AreaTriggers.Select((trigger, id) => (trigger, id)).Where(item => item.trigger.AreaId != byte.MaxValue).Select(item => new { item.id, item.trigger.AreaId, start = Values(item.trigger.Start), end = Values(item.trigger.End) }),
            actors = capture.Actors.Select(actor => new { actor.ObjectId, actor.ModelId, position = Values(actor.Position), actor.Direction, actor.AnimationId, actor.AnimationFrame }),
            backgroundTiles = field.Background.Select((tile, order) => new
            {
                order, tile.X, tile.Y, tile.Z, tile.Layer, tile.AnimationId, tile.AnimationState, blendMode = tile.BlendMode.ToString(), tile.Blend, tile.Draw,
                selectedByNativeState = tile.Draw && (tile.AnimationId == 255 || tile.AnimationState == capture.BackgroundStates.GetValueOrDefault(tile.AnimationId, -1))
            }),
            limitations = new[] { "Camera nodes approximate fixed-point projection; exact axes and captured intrinsics are in this file.", "Tile drawing depths do not describe physical room surfaces." }
        };
        File.WriteAllText(Path.Combine(directory, "reference.json"), JsonSerializer.Serialize(reference, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
    }

    private static ReferenceCamera? MakeCamera(FieldCamera source, Vector2 focal, Vector2 principal, Vector3[] landmarks)
    {
        Matrix4x4 worldToCamera = new(
            source.AxisX.X, source.AxisY.X, source.AxisZ.X, 0,
            source.AxisX.Y, source.AxisY.Y, source.AxisZ.Y, 0,
            source.AxisX.Z, source.AxisY.Z, source.AxisZ.Z, 0,
            source.Translation.X, source.Translation.Y, source.Translation.Z, 1);
        if (!Matrix4x4.Invert(worldToCamera, out Matrix4x4 inverse) || focal.X <= 0 || focal.Y <= 0)
        {
            return null;
        }
        Vector3 origin = inverse.Translation;
        Vector3 forward = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, inverse));
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, -Vector3.TransformNormal(Vector3.UnitY, inverse)));
        Vector3 up = Vector3.Cross(right, forward);
        Matrix4x4 pose = Matrix4x4.CreateWorld(Vector3.Transform(origin, FieldToGlb), ToGlbDirection(forward), ToGlbDirection(up));
        Quaternion rotation = Quaternion.CreateFromRotationMatrix(pose);
        int count = 0;
        double squaredError = 0;
        float maximum = 0;
        foreach (Vector3 landmark in landmarks)
        {
            Vector3 exact = Vector3.Transform(landmark, worldToCamera);
            if (exact.Z <= 1)
            {
                continue;
            }
            Vector3 relative = landmark - origin;
            float depth = Vector3.Dot(relative, forward);
            if (depth <= 1)
            {
                continue;
            }
            Vector2 projected = new(Vector3.Dot(relative, right) / depth, -Vector3.Dot(relative, up) / depth);
            Vector2 error = focal * (projected - new Vector2(exact.X / exact.Z, exact.Y / exact.Z));
            squaredError += error.LengthSquared();
            maximum = Math.Max(maximum, error.Length());
            count++;
        }
        return new ReferenceCamera(origin, pose.Translation, rotation, focal, principal, new(count, count == 0 ? 0 : Math.Sqrt(squaredError / count), maximum));
    }

    private static byte[] BuildGlb(FieldAssets field, FieldSceneReferenceCapture capture, ReferenceCamera?[] cameras)
    {
        using MemoryStream binary = new();
        using BinaryWriter writer = new(binary);
        List<object> nodes = [];
        List<object> glbCameras = [];
        List<object> meshes = [];
        List<object> views = [];
        List<object> accessors = [];
        void Mesh(string name, IEnumerable<Vector3> geometry, int mode)
        {
            Vector3[] vertices = geometry.Select(point => Vector3.Transform(point, FieldToGlb)).ToArray();
            if (vertices.Length == 0)
            {
                return;
            }
            int offset = checked((int)binary.Position);
            Vector3 minimum = vertices[0];
            Vector3 maximum = vertices[0];
            foreach (Vector3 value in vertices)
            {
                writer.Write(value.X);
                writer.Write(value.Y);
                writer.Write(value.Z);
                minimum = Vector3.Min(minimum, value);
                maximum = Vector3.Max(maximum, value);
            }
            int accessor = accessors.Count;
            accessors.Add(new { bufferView = views.Count, componentType = 5126, count = vertices.Length, type = "VEC3", min = Values(minimum), max = Values(maximum) });
            views.Add(new { buffer = 0, byteOffset = offset, byteLength = checked((int)binary.Position - offset), target = 34962 });
            nodes.Add(new { name, mesh = meshes.Count, extras = new { memoria_reference_only = true } });
            meshes.Add(new { name, primitives = new[] { new { attributes = new Dictionary<string, int> { ["POSITION"] = accessor }, mode } } });
        }
        Mesh("Native walkmesh", field.Walkmesh.Triangles.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }), 4);
        if (field.Information is FieldInformation info)
        {
            Mesh("Native gateways", info.Gateways.Where(gate => gate.Defined || gate.DestinationField < 72).SelectMany(gate => new[] { gate.Start, gate.End }), 1);
            Mesh("Native triggers", info.AreaTriggers.Where(trigger => trigger.AreaId != byte.MaxValue).SelectMany(trigger => new[] { trigger.Start, trigger.End }), 1);
        }
        foreach (FieldSceneReferenceActor actor in capture.Actors)
        {
            float height = field.Models.ElementAtOrDefault(actor.ModelId)?.Vertices.Select(vertex => (float)vertex.Position.Length()).DefaultIfEmpty().Max() ?? 0;
            Vector3 top = actor.Position + Vector3.UnitZ * Math.Max(height, NativeUnitsPerBlenderUnit / 2);
            float yaw = (actor.Direction + 64) * MathF.Tau / 256;
            Vector3 front = actor.Position + new Vector3(MathF.Cos(yaw), MathF.Sin(yaw), 0) * NativeUnitsPerBlenderUnit / 4;
            Mesh($"Native actor {actor.ObjectId}", [actor.Position, top, actor.Position, front], 1);
        }
        for (int slot = 0; slot < cameras.Length; slot++)
        {
            if (cameras[slot] is not ReferenceCamera camera)
            {
                continue;
            }
            string name = $"Native camera {slot}";
            nodes.Add(new { name, camera = glbCameras.Count, translation = Values(camera.GlbPosition), rotation = new[] { camera.Rotation.X, camera.Rotation.Y, camera.Rotation.Z, camera.Rotation.W }, extras = new { memoria_camera_slot = slot, memoria_reference_only = true } });
            glbCameras.Add(new
            {
                name, type = "perspective", perspective = new
                {
                    aspectRatio = capture.Width * camera.FocalPixels.Y / (capture.Height * camera.FocalPixels.X),
                    yfov = 2 * MathF.Atan(capture.Height / (2 * camera.FocalPixels.Y)),
                    znear = 1 / NativeUnitsPerBlenderUnit
                }
            });
        }
        Dictionary<string, object> document = new()
        {
            ["asset"] = new { version = "2.0", generator = "Memoria FF8 field reference exchange" },
            ["scene"] = 0,
            ["scenes"] = new[] { new { name = field.Name, nodes = Enumerable.Range(0, nodes.Count).ToArray() } }
        };
        if (nodes.Count > 0)
        {
            document["nodes"] = nodes;
        }
        if (glbCameras.Count > 0)
        {
            document["cameras"] = glbCameras;
        }
        if (meshes.Count > 0)
        {
            document["meshes"] = meshes;
            document["accessors"] = accessors;
            document["bufferViews"] = views;
            document["buffers"] = new[] { new { byteLength = binary.Length } };
        }

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(document);
        int alignedJsonLength = (json.Length + 3) & ~3;
        using MemoryStream output = new();
        using BinaryWriter container = new(output);
        container.Write(0x46546c67u);
        container.Write(2u);
        container.Write(checked((uint)(20 + alignedJsonLength + (binary.Length == 0 ? 0 : 8 + binary.Length))));
        container.Write(alignedJsonLength);
        container.Write(0x4e4f534au);
        container.Write(json);
        for (int index = json.Length; index < alignedJsonLength; index++)
        {
            container.Write((byte)' ');
        }
        if (binary.Length != 0)
        {
            container.Write(checked((int)binary.Length));
            container.Write(0x004e4942u);
            container.Write(binary.ToArray());
        }
        return output.ToArray();
    }

    private static object SourceCamera(FieldCamera camera) => new { axisX = Values(camera.AxisX), axisY = Values(camera.AxisY), axisZ = Values(camera.AxisZ), translation = Values(camera.Translation), pan = Values(camera.Pan), camera.FocalLength };
    private static FieldBounds? Bounds(IReadOnlyList<FieldBounds>? bounds, int slot) => bounds is not null && slot < bounds.Count ? bounds[slot] : null;
    private static Vector3 ToGlbDirection(Vector3 native) => new(native.X, native.Z, -native.Y);
    private static float[] Values(Vector2 value) => [value.X, value.Y];
    private static float[] Values(Vector3 value) => [value.X, value.Y, value.Z];
    private static float[] Values(Vector4 value) => [value.X, value.Y, value.Z, value.W];
    private static float[] Values(Matrix4x4 matrix) => [matrix.M11, matrix.M12, matrix.M13, matrix.M14, matrix.M21, matrix.M22, matrix.M23, matrix.M24, matrix.M31, matrix.M32, matrix.M33, matrix.M34, matrix.M41, matrix.M42, matrix.M43, matrix.M44];

    private sealed record ReferenceCamera(Vector3 Origin, Vector3 GlbPosition, Quaternion Rotation, Vector2 FocalPixels, Vector2 PrincipalPixels, ReprojectionResidual Residual);
    private sealed record ReprojectionResidual(int Samples, double RmsPixels, float MaximumPixels);
}
