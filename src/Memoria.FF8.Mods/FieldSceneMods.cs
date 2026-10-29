using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Mods;

public sealed record FieldBackgroundRegion
{
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }

    public bool Contains(int x, int y) => x >= X && x < (long)X + Width && y >= Y && y < (long)Y + Height;
}

public sealed record FieldBackgroundPatch
{
    public required int Layer { get; init; }
    public required int ModuleId { get; init; }
    public required int State { get; init; }
    public required bool Visible { get; init; }
    public FieldBackgroundRegion? Region { get; init; }
}

public sealed record FieldModelReference
{
    public required int FieldId { get; init; }
    public required int ModelId { get; init; }
}

public sealed record FieldBackgroundCondition
{
    public required int ModuleId { get; init; }
    public required int State { get; init; }
}

public sealed record FieldActorPatch
{
    public required int ObjectId { get; init; }
    public required FieldModelReference Model { get; init; }
    public int AnimationId { get; init; }
    public float? Direction { get; init; }
    public FieldBackgroundCondition? When { get; init; }
}

/// <summary>Player input rotation kept separate from the original INF/AXIS angle.</summary>
public sealed record FieldMovementPatch
{
    public required float RotationDegrees { get; init; }
}

public sealed record FieldScenePatch
{
    public required int SchemaVersion { get; init; }
    public required int FieldId { get; init; }
    public required string FieldName { get; init; }
    public IReadOnlyList<FieldBackgroundPatch> Backgrounds { get; init; } = [];
    public IReadOnlyList<FieldActorPatch> Actors { get; init; } = [];
    public FieldMovementPatch? Movement { get; init; }
}

public sealed record OwnedFieldScenePatch(string PackageId, string RelativePath, FieldScenePatch Patch);

/// <summary>Immutable declarative scene changes in catalog precedence order. Original game resources remain external.</summary>
public sealed class FieldSceneMods
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static FieldSceneMods Empty { get; } = new([]);

    private FieldSceneMods(IReadOnlyList<OwnedFieldScenePatch> patches)
    {
        Patches = patches;
    }

    public IReadOnlyList<OwnedFieldScenePatch> Patches { get; }

    public static bool TryPrepare(ModCatalogSnapshot catalog, out FieldSceneMods result, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        List<OwnedFieldScenePatch> patches = [];
        List<ModDiagnostic> errors = [];
        foreach (ModFile file in catalog.Files.Where(file => file.Format == ModFormat.Memoria && file.RelativePath.StartsWith("fields/", StringComparison.OrdinalIgnoreCase) && file.RelativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                FieldScenePatch patch = JsonSerializer.Deserialize<FieldScenePatch>(file.ReadBytes(), JsonOptions) ?? throw new InvalidDataException("A field patch must be a JSON object.");
                Validate(patch, file.RelativePath);
                patch = patch with { Backgrounds = Array.AsReadOnly(patch.Backgrounds.ToArray()), Actors = Array.AsReadOnly(patch.Actors.ToArray()) };
                patches.Add(new(file.PackageId, file.RelativePath, patch));
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
                errors.Add(new("Memoria.InvalidFieldPatch", exception.Message, file.PackageId, file.RelativePath));
            }
        }
        result = errors.Count == 0 ? new(Array.AsReadOnly(patches.ToArray())) : Empty;
        diagnostics = Array.AsReadOnly(errors.ToArray());
        return errors.Count == 0;
    }

    private static void Validate(FieldScenePatch patch, string relativePath)
    {
        if (patch.SchemaVersion != 1)
        {
            throw new InvalidDataException("Unsupported field patch schemaVersion; expected 1.");
        }
        if (patch.FieldId is < 0 or > ushort.MaxValue || string.IsNullOrWhiteSpace(patch.FieldName) || patch.FieldName.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_') || !relativePath.Equals($"fields/{patch.FieldName}.json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A field patch requires a valid fieldId and fieldName matching fields/<fieldName>.json.");
        }
        if (patch.Backgrounds is null || patch.Actors is null || patch.Backgrounds.Count + patch.Actors.Count == 0 && patch.Movement is null || patch.Backgrounds.Count + patch.Actors.Count > 4096)
        {
            throw new InvalidDataException("A field patch requires movement or background/actor changes, with at most 4096 background or actor changes.");
        }
        if (patch.Movement is { } movement && (!float.IsFinite(movement.RotationDegrees) || movement.RotationDegrees is < -180 or > 180))
        {
            throw new InvalidDataException("Movement rotationDegrees must be finite and within -180..180.");
        }
        HashSet<(int Layer, int Module, int State, FieldBackgroundRegion? Region)> backgrounds = [];
        foreach (FieldBackgroundPatch background in patch.Backgrounds)
        {
            if (background is null || background.Layer is < 0 or > 3 || background.ModuleId is < 0 or > 255 || background.State is < 0 or > 255 || !backgrounds.Add((background.Layer, background.ModuleId, background.State, background.Region)) || background.Region is { } region && (region.X is < short.MinValue or > short.MaxValue || region.Y is < short.MinValue or > short.MaxValue || region.Width is < 1 or > 65536 || region.Height is < 1 or > 65536))
            {
                throw new InvalidDataException("Background selectors require unique layer (0..3), moduleId (0..255), state (0..255), and optional region addresses with signed 16-bit origins and dimensions in 1..65536.");
            }
        }
        HashSet<int> actors = [];
        foreach (FieldActorPatch actor in patch.Actors)
        {
            if (actor is null || actor.ObjectId is < 0 or > ushort.MaxValue || !actors.Add(actor.ObjectId) || actor.Model is null || actor.Model.FieldId is < 0 or > ushort.MaxValue || actor.Model.ModelId is < 0 or > ushort.MaxValue || actor.AnimationId is < 0 or > ushort.MaxValue || actor.Direction is float direction && (!float.IsFinite(direction) || direction is < 0 or >= 256) || actor.When is { } condition && (condition.ModuleId is < 0 or >= 255 || condition.State is < 0 or > 255))
            {
                throw new InvalidDataException("Actor changes require unique object IDs, valid model/animation IDs, direction in 0..<256, and an optional animated module/state condition.");
            }
        }
    }
}
