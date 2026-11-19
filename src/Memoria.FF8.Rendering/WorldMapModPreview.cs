using System.Numerics;
using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Rendering;

/// <summary>Location is centered native X/Z. A missing location uses an original field exit.</summary>
public sealed record WorldMapModPreviewView(Vector2? Location = null, WorldCamera? Camera = null, int Width = 480, int Height = 270, bool ShowCharacter = true);

public sealed record WorldMapModPreviewResult(Vector3 Position, WorldCamera Camera, PixelImage Original, PixelImage Modded);

/// <summary>Read-only, paired world inspection with independent original and single-package resource caches.</summary>
public sealed class WorldMapModPreview : IDisposable
{
    private const byte InitialStory = 0;
    private static readonly ushort TerrainMask = WorldTerrainSelection.ReplacementMask(0, InitialStory);
    private static readonly IReadOnlyDictionary<int, int> Replacements = WorldTerrainSelection.Replacements(TerrainMask);
    private readonly object gate = new();
    private readonly InspectionSide original;
    private readonly InspectionSide modded;
    private readonly WorldSpawn spawn;
    private bool disposed;

    private WorldMapModPreview(InspectionSide original, InspectionSide modded)
    {
        this.original = original;
        this.modded = modded;
        spawn = FindDefaultSpawn(original.Assets);
    }

    public static WorldMapModPreview Open(ModPreviewResourcesProfile profile, ModCatalogSnapshot catalog, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Packages.Count != 1 || catalog.Files.Any(file => !file.PackageId.Equals(catalog.Packages[0].Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("World comparison requires one package and its own resources.", nameof(catalog));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!NativeTextureMods.TryPrepare(catalog, out NativeTextureMods textures, out IReadOnlyList<ModDiagnostic> textureDiagnostics, cancellationToken))
        {
            throw InvalidPackage(textureDiagnostics);
        }
        if (!ModelMods.TryPrepare(catalog, out ModelMods models, out IReadOnlyList<ModDiagnostic> modelDiagnostics))
        {
            throw InvalidPackage(modelDiagnostics);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!JunctionEncodedResources.TryPrepare(catalog, out IReadOnlyList<EncodedResource> encoded, out IReadOnlyList<ModDiagnostic> encodedDiagnostics))
        {
            throw InvalidPackage(encodedDiagnostics);
        }

        InspectionSide baseline = InspectionSide.Open(profile);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            InspectionSide applied = InspectionSide.Open(profile, textures, models, encoded, catalog);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new(baseline, applied);
            }
            catch
            {
                applied.Dispose();
                throw;
            }
        }
        catch
        {
            baseline.Dispose();
            throw;
        }
    }

    public WorldMapModPreviewResult Render(WorldMapModPreviewView view, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(view);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            Vector2 location = view.Location ?? new(spawn.X, spawn.Z);
            if (!float.IsFinite(location.X) || !float.IsFinite(location.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(view), "World coordinates must be finite.");
            }
            float x = WorldAssets.Wrap(location.X, WorldAssets.Columns * WorldAssets.SegmentUnits);
            float z = WorldAssets.Wrap(location.Y, WorldAssets.Rows * WorldAssets.SegmentUnits);
            if (!float.IsFinite(x) || !float.IsFinite(z))
            {
                throw new ArgumentOutOfRangeException(nameof(view), "World coordinates exceed the supported range.");
            }
            float height = original.Assets.FindGround(x, z, Replacements, view.Location is null ? -spawn.NativeHeight : null)?.Height ?? (view.Location is null ? -spawn.NativeHeight : 0);
            Vector3 position = new(x, height, z);
            WorldCamera camera = view.Camera ?? WorldCamera.Foot((spawn.Unknown & 0xff) * 16, 2048);
            ValidateCamera(camera);
            float heading = (spawn.Unknown >> 8) * MathF.Tau / 256;
            PixelImage before = original.Render(position, heading, camera, view, cancellationToken);
            PixelImage after = modded.Render(position, heading, camera, view, cancellationToken);
            return new(position, camera, before, after);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            try
            {
                original.Dispose();
            }
            finally
            {
                modded.Dispose();
            }
        }
    }

    private static void ValidateCamera(WorldCamera camera)
    {
        if (!float.IsFinite(camera.Heading) || !float.IsFinite(camera.Distance) || !float.IsFinite(camera.Height) || !float.IsFinite(camera.TargetHeight) || !float.IsFinite(camera.FieldOfView) || !float.IsFinite(camera.FarDistance) || camera.TargetPosition is Vector3 target && (!float.IsFinite(target.X) || !float.IsFinite(target.Y) || !float.IsFinite(target.Z)))
        {
            throw new ArgumentOutOfRangeException(nameof(camera), "World camera coordinates must be finite.");
        }
    }

    private static WorldSpawn FindDefaultSpawn(WorldAssets assets)
    {
        foreach (WorldSpawn entry in assets.Spawns)
        {
            if (entry != default && assets.FindGround(entry.X, entry.Z, Replacements, -entry.NativeHeight) is { Triangle.Walkable: true })
            {
                return entry;
            }
        }
        throw new InvalidDataException("The installed world has no usable field exit for the comparison.");
    }

    private static InvalidDataException InvalidPackage(IReadOnlyList<ModDiagnostic> diagnostics) => new(string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.Message)));

    private sealed class InspectionSide(IResourceSource source, WorldAssets assets) : IDisposable
    {
        private WorldTerrainRenderer? renderer;
        public WorldAssets Assets { get; } = assets;

        public static InspectionSide Open(ModPreviewResourcesProfile profile, NativeTextureMods? textures = null, ModelMods? models = null, IReadOnlyList<EncodedResource>? encoded = null, ModCatalogSnapshot? catalog = null)
        {
            IResourceSource source = GameDataSourceFactory.Open(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language);
            try
            {
                ResourceService resources = new(source, profile.Language);
                if (catalog is not null && textures is not null)
                {
                    List<ITextureReplacementResolver> resolvers = [textures];
                    if (catalog.Files.Any(file => file.Format == ModFormat.Demaster))
                    {
                        resolvers.Add(new DemasterTextureResolver(catalog));
                    }
                    if (catalog.Files.Any(file => file.Format == ModFormat.Junction))
                    {
                        resolvers.Add(new FfnxTextureResolver(catalog));
                    }
                    TonberryTextureResolver? tonberry = TonberryTextureResolver.Create(source.Descriptor, catalog, out IReadOnlyList<ModDiagnostic> diagnostics);
                    if (diagnostics.Any(diagnostic => diagnostic.Code == "Tonberry.MalformedRow"))
                    {
                        throw InvalidPackage(diagnostics);
                    }
                    if (tonberry is not null)
                    {
                        resolvers.Add(tonberry);
                    }
                    resources.Prepare(new OrderedTextureResolver(resolvers.ToArray()), encodedResources: encoded).Commit();
                }
                return new(source, new WorldAssets(profile.GameDirectory, resources, models));
            }
            catch
            {
                source.Dispose();
                throw;
            }
        }

        public PixelImage Render(Vector3 position, float heading, WorldCamera camera, WorldMapModPreviewView view, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (renderer is null || renderer.Width != view.Width || renderer.Height != view.Height)
            {
                renderer = new(Assets, view.Width, view.Height);
            }
            WorldSky sky = view.Camera is null ? WorldTerrainSelection.SelectSky(Assets.Skies, (int)position.X, (int)position.Z, InitialStory) : Assets.Skies[camera.SkyIndex];
            WorldRenderFrame frame = new(position, heading, 0, 0, camera, sky, view.ShowCharacter)
            {
                NativeMainLoopCounter = 0,
                TerrainReplacementMask = TerrainMask
            };
            byte[] pixels = renderer.Render(frame, Replacements);
            cancellationToken.ThrowIfCancellationRequested();
            return new(view.Width, view.Height, MemoryMarshal.Cast<byte, Rgba32>(pixels.AsSpan()).ToArray());
        }

        public void Dispose()
        {
            try
            {
                Assets.Dispose();
            }
            finally
            {
                source.Dispose();
            }
        }
    }
}
