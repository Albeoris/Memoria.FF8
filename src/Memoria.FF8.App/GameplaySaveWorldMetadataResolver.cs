using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Resolves normal-load presentation preferences and the current position's installed map location without allocating a world scene.</summary>
internal static class GameplaySaveWorldMetadataResolver
{
    internal static GameplaySaveWorldResumeMetadata Resolve(GameplaySaveComponentDocument document, string gameDirectory, ResourceService? resources = null, IModelOverrideSource? modelOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();
        if (document.Version != GameplaySaveComponentDocument.CurrentVersion || document.Progression?.World is not { } saved)
        {
            throw new NotSupportedException("World resume metadata requires complete current gameplay components.");
        }
        WorldAssets? assets = null;
        try
        {
            return ResolvePolicy(document.Resume, saved, (x, rawZ) => (assets ??= new WorldAssets(gameDirectory, resources, modelOverrides)).GetNativeMapLocationId(x, rawZ));
        }
        finally
        {
            assets?.Dispose();
        }
    }

    internal static GameplaySaveWorldResumeMetadata ResolvePolicy(GameplaySaveResumeDescriptor resume, GameplaySaveWorldData saved, Func<int, int, short> resolveMapLocation)
    {
        ArgumentNullException.ThrowIfNull(resume);
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(resolveMapLocation);
        resume.Validate();
        saved.Validate();
        if (resume is not { Kind: GameplaySaveResumeKind.World, World: { } current })
        {
            throw new InvalidOperationException("World resume metadata requires a current world position.");
        }
        if (current.EntityType is not (0 or 1 or 2 or 32 or 33 or 35 or 48 or 50) || saved.UnresolvedEntry is not null)
        {
            throw new NotSupportedException("World resume metadata requires a supported normal-load transport and resolved saved values.");
        }
        // Older component documents may retain a matching transport's animation choice and a global map preference.
        byte animation = current.AnimationVariant ?? (saved.Return?.EntityType == current.EntityType ? saved.Return.AnimationVariant : null) ?? throw new NotSupportedException("World resume requires its current animation choice or a known matching saved transport preference.");
        byte mapDisplay = current.MapDisplayMode ?? saved.Return?.MapDisplayMode ?? saved.InitialMapDisplayMode ?? throw new NotSupportedException("World resume requires a known map-display preference.");
        if (animation > 1 || mapDisplay > 2)
        {
            throw new InvalidDataException("World resume presentation preferences exceed the supported normal-load widths.");
        }
        short location = resolveMapLocation(current.X, current.Z);
        if (location is < sbyte.MinValue or > sbyte.MaxValue)
        {
            throw new InvalidDataException("World resume map location must preserve its native signed byte.");
        }
        // Normal load restarts the presentation camera at body heading; no old camera callback is claimed.
        return new(current.BodyYaw, animation, mapDisplay, location);
    }
}
