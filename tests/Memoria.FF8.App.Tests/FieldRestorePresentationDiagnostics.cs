using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class FieldRestorePresentationDiagnostics
{
    private static readonly JsonSerializerOptions Fields = new() { IncludeFields = true };

    public static void Equal(GameWorld expectedWorld, GameWorld actualWorld, byte[] expected, byte[] actual)
    {
        if (!expected.AsSpan().SequenceEqual(actual))
        {
            int common = Math.Min(expected.Length, actual.Length);
            int first = Enumerable.Range(0, common).FirstOrDefault(index => expected[index] != actual[index], -1);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Diagnostic = "field-restore-presentation",
                FirstDifferentByte = first,
                ExpectedImage = Hash(expected),
                ActualImage = Hash(actual),
                Expected = DescribeSafely(expectedWorld),
                Actual = DescribeSafely(actualWorld)
            }, Fields));
        }
        Assert.Equal(expected, actual);
    }

    private static object DescribeSafely(GameWorld world)
    {
        try
        {
            return Describe(world);
        }
        catch (Exception error)
        {
            return new { DiagnosticError = error.ToString() };
        }
    }

    private static object Describe(GameWorld world)
    {
        var field = world.Field!;
        GameSession session = (GameSession)typeof(GameWorld).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        Dictionary<PixelImage, string> imageHashes = new(ReferenceEqualityComparer.Instance);
        string ImageHash(PixelImage image)
        {
            if (!imageHashes.TryGetValue(image, out string? hash))
            {
                hash = Hash(MemoryMarshal.AsBytes(image.Pixels.AsSpan()));
                imageHashes.Add(image, hash);
            }
            return hash;
        }
        object Images(IReadOnlyList<PixelImage> images)
        {
            var entries = images.Select(image => new { image.Width, image.Height, Sha256 = ImageHash(image) }).ToArray();
            return new { Count = entries.Length, Digest = Digest(entries), First = entries.Take(16).ToArray() };
        }
        var camera = field.GetCamera(world.Events.Scene.CameraIndex);
        MethodInfo actorPose = typeof(FieldRenderer).GetMethod("ActorPose", BindingFlags.Static | BindingFlags.NonPublic)!;
        var actors = world.Events.Actors.Select(actor => (Actor: actor, Sample: world.CaptureFieldActor(actor))).Where(pair => pair.Sample.Visual.Visible && (uint)pair.Sample.Visual.ModelId < field.Models.Count).Select(pair =>
        {
            ActorVisual visual = pair.Sample.Visual;
            var model = field.Models[visual.ModelId];
            IReadOnlyList<PixelImage> selected = model.EditableOverride is not null ? model.Textures : visual.RegisteredTextures ?? model.Textures;
            Vector3[] pose = (Vector3[])actorPose.Invoke(null, [model, visual])!;
            Matrix4x4 rotation = Matrix4x4.CreateRotationZ((visual.Direction + 64) * MathF.Tau / 256);
            var projected = pose.Select(vertex => camera.Project(Vector3.Transform(vertex, rotation) + new Vector3(visual.X, visual.Y, visual.Z))).ToArray();
            return new
            {
                visual.ObjectId,
                visual.ModelId,
                model.Name,
                model.TextureResourceName,
                Edition = model.TextureSourceDescriptor?.Edition.ToString(),
                model.TextureSourceVariant,
                EyeMode = pair.Actor.NativeEyes?.Mode,
                Visual = visual with { RegisteredTextures = null },
                pair.Sample.FirstFrame,
                pair.Sample.TerminalFrame,
                pair.Sample.AnimationStep,
                PoseHash = Digest(pose),
                ProjectedHash = Digest(projected),
                TopologyHash = Digest(model.Faces),
                UsesRegisteredTextures = visual.RegisteredTextures is not null && model.EditableOverride is null,
                SelectedTextures = Images(selected),
                BaseTextures = Images(model.Textures)
            };
        }).ToArray();
        var backgrounds = field.Background.Select((tile, index) => new { Index = index, tile.X, tile.Y, tile.Z, tile.Layer, tile.AnimationId, tile.AnimationState, tile.PackedPalette, tile.Blend, tile.Draw, Image = ImageHash(tile.ResolvedImage) }).ToArray();
        NativeBattleRetainedMemory? cached = world.Events.Scene.NativeEyeProcess?.Memory;
        NativeBattleRetainedMemory? current = session.CurrentNativeBattleMemory;
        return new
        {
            field.Id,
            world.Events.Frame,
            world.Events.Scene.CameraIndex,
            Camera = camera,
            Executable = field.NativeRegistration?.ExecutableHash,
            ActorCount = actors.Length,
            ActorsDigest = Digest(actors),
            Actors = actors.Take(32).ToArray(),
            BackgroundCount = backgrounds.Length,
            BackgroundDigest = Digest(backgrounds),
            BackgroundImageCount = backgrounds.Select(tile => tile.Image).Distinct().Count(),
            BackgroundImages = backgrounds.GroupBy(tile => tile.Image).Take(64).Select(group => new { Hash = group.Key, TileCount = group.Count(), Tiles = group.Take(16).ToArray() }).ToArray(),
            BackgroundStates = world.Events.BackgroundStates,
            BackgroundShades = world.Events.GetBackgroundModuleShades(),
            CachedEye = MemorySafely(cached),
            CurrentEye = MemorySafely(current)
        };
    }

    private static object? MemorySafely(NativeBattleRetainedMemory? memory)
    {
        try
        {
            return Memory(memory);
        }
        catch (Exception error)
        {
            return new { DiagnosticError = error.ToString() };
        }
    }

    private static object? Memory(NativeBattleRetainedMemory? memory)
    {
        if (memory is null)
        {
            return null;
        }
        NativeBattleEffectTextureState? textures = memory.EffectMemory?.Textures;
        using IncrementalHash vram = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int bytes = 0;
        int regions = 0;
        if (textures is not null)
        {
            foreach (NativeBattleEffectMemoryRegion region in NativeBattleStorageSnapshots.Capture(textures).VramRegions)
            {
                bytes = checked(bytes + region.Data.Length);
                if (bytes > 1024 * 1024)
                {
                    throw new InvalidDataException("Diagnostic VRAM receipt exceeds the physical 1 MiB bank.");
                }
                vram.AppendData(BitConverter.GetBytes(region.Offset));
                vram.AppendData(BitConverter.GetBytes(region.Data.Length));
                vram.AppendData(region.Data.AsSpan());
                regions++;
            }
        }
        return new
        {
            VramBytes = bytes,
            VramRegions = regions,
            VramHash = Convert.ToHexString(vram.GetHashAndReset()),
            memory.FieldTexturePlaneCacheFresh,
            PlanesHash = Digest(memory.FieldTextureAnimations?.Planes),
            AnimationHash = Digest(memory.FieldTextureAnimations)
        };
    }

    private static string Digest<T>(T value) => Hash(JsonSerializer.SerializeToUtf8Bytes(value, Fields));
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
