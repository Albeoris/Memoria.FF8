using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Mods;

public sealed class FieldSpringBoneMod
{
    private readonly int expectedBoneCount;
    private readonly int[]? expectedParents;
    private readonly IReadOnlyList<FieldSpringBoneJoint> joints;
    private readonly ConditionalWeakTable<FieldModel, FieldModel> replacements = new();

    internal FieldSpringBoneMod(ModFile file, string field, int? entry, string? mainCharacter, int packageOrder, int expectedBoneCount, int[]? expectedParents, IReadOnlyList<FieldSpringBoneJoint> joints)
    {
        PackageId = file.PackageId;
        RelativePath = file.RelativePath;
        ModelId = RelativePath["models/".Length..^".springs.json".Length];
        Field = field;
        Entry = entry;
        MainCharacter = mainCharacter;
        PackageOrder = packageOrder;
        this.expectedBoneCount = expectedBoneCount;
        this.expectedParents = expectedParents;
        this.joints = joints;
    }

    public string PackageId { get; }
    public string RelativePath { get; }
    public string Field { get; }
    public int? Entry { get; }
    public string? MainCharacter { get; }
    public string ModelId { get; }
    internal int PackageOrder { get; }

    public void ValidateRig(int[] parents)
    {
        if (parents.Length != expectedBoneCount)
        {
            throw new InvalidDataException($"{ModelId} requires {expectedBoneCount} bones, but the selected rig has {parents.Length}.");
        }
        if (expectedParents is not null && !parents.SequenceEqual(expectedParents))
        {
            throw new InvalidDataException($"{ModelId} does not match the declared ordered parent indices.");
        }
        if (joints.Any(joint => parents[joint.Bone] == -1))
        {
            throw new InvalidDataException($"{ModelId} cannot attach spring joints to root bones.");
        }
        _ = new FieldSpringBoneSimulation(parents, joints);
    }

    internal FieldModel Apply(FieldModel model) => replacements.GetValue(model, candidate =>
    {
        ValidateRig(candidate.GetSpringBoneParents());
        return candidate with { SpringBones = joints };
    });
}

/// <summary>Prepared field presentation data in package precedence order.</summary>
public sealed class FieldSpringBoneMods
{
    private const string Prefix = "models/field/";
    private const string Suffix = ".springs.json";
    private const string MainCharacterDirectory = "main_chr";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false
    };

    private readonly Dictionary<string, FieldSpringBoneMod> exactProfiles;
    private readonly Dictionary<string, FieldSpringBoneMod> sharedProfiles;

    private FieldSpringBoneMods(Dictionary<string, FieldSpringBoneMod> profiles, IReadOnlyList<string> packageIds)
    {
        Definitions = Array.AsReadOnly(profiles.Values.ToArray());
        exactProfiles = Definitions.Where(definition => definition.Entry is not null).ToDictionary(definition => definition.ModelId, StringComparer.OrdinalIgnoreCase);
        sharedProfiles = Definitions.Where(definition => definition.MainCharacter is not null).ToDictionary(definition => definition.MainCharacter!, StringComparer.OrdinalIgnoreCase);
        PackageIds = packageIds;
    }

    public static FieldSpringBoneMods Empty { get; } = new(new(StringComparer.OrdinalIgnoreCase), []);
    public IReadOnlyList<FieldSpringBoneMod> Definitions { get; }
    public IReadOnlyList<string> PackageIds { get; }

    public static bool IsSpringDocumentPath(string relativePath) => TryParseAddress(relativePath, out _, out _, out _);

    public static bool TryPrepare(ModCatalogSnapshot catalog, out FieldSpringBoneMods result, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        Dictionary<string, FieldSpringBoneMod> profiles = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> packageOrder = new(StringComparer.OrdinalIgnoreCase);
        HashSet<(int PackageOrder, string ModelId)> packageSelectors = [];
        List<ModDiagnostic> errors = [];
        foreach (ModFile file in catalog.Files.Where(file => file.Format == ModFormat.Memoria && HasSpringDocumentShape(file.RelativePath)))
        {
            try
            {
                if (!TryParseAddress(file.RelativePath, out string field, out int? entry, out string? mainCharacter))
                {
                    throw new InvalidDataException("Spring definitions require models/field/<field>/<entry>.springs.json or models/field/main_chr/<dNNN>.springs.json.");
                }
                if (!packageOrder.TryGetValue(file.PackageId, out int order))
                {
                    order = packageOrder.Count;
                    packageOrder.Add(file.PackageId, order);
                }
                FieldSpringBoneMod profile = ReadProfile(file, field, entry, mainCharacter, order);
                if (!packageSelectors.Add((order, profile.ModelId.ToLowerInvariant())))
                {
                    throw new InvalidDataException($"The package defines spring bones for {profile.ModelId} more than once.");
                }
                profiles.TryAdd(profile.ModelId, profile);
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                errors.Add(new("Memoria.InvalidFieldSpringBones", exception.Message, file.PackageId, file.RelativePath));
            }
        }
        result = errors.Count == 0 ? new(profiles, Array.AsReadOnly(packageOrder.Keys.ToArray())) : Empty;
        diagnostics = Array.AsReadOnly(errors.ToArray());
        return errors.Count == 0;
    }

    public FieldSpringBoneMod? Resolve(string modelId, FieldModel original)
    {
        if (Definitions.Count == 0 || !modelId.StartsWith("field/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        FieldSpringBoneMod? specific = exactProfiles.GetValueOrDefault(modelId);
        FieldSpringBoneMod? shared = original.MainCharacterSourceId is { } mainCharacter ? sharedProfiles.GetValueOrDefault(mainCharacter) : null;
        return shared is null || specific is not null && specific.PackageOrder <= shared.PackageOrder ? specific : shared;
    }

    public FieldModel Apply(string modelId, FieldModel original, FieldModel model) => Resolve(modelId, original)?.Apply(model) ?? model;

    private static bool HasSpringDocumentShape(string path) => path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) && path.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase);

    private static bool TryParseAddress(string path, out string field, out int? entry, out string? mainCharacter)
    {
        field = string.Empty;
        entry = null;
        mainCharacter = null;
        if (!HasSpringDocumentShape(path))
        {
            return false;
        }
        string address = path["models/".Length..^Suffix.Length].ToLowerInvariant();
        string[] parts = address.Split('/');
        if (parts.Length != 3)
        {
            return false;
        }
        field = parts[1];
        string selector = parts[2];
        if (field == MainCharacterDirectory)
        {
            if (selector.Length != 4 || selector[0] != 'd' || !selector.Skip(1).All(char.IsAsciiDigit))
            {
                return false;
            }
            mainCharacter = selector;
            return true;
        }
        if (!ModelImportTarget.IsModelId(address) || !int.TryParse(selector, NumberStyles.None, CultureInfo.InvariantCulture, out int index) || selector != index.ToString(CultureInfo.InvariantCulture))
        {
            return false;
        }
        entry = index;
        return true;
    }

    private static FieldSpringBoneMod ReadProfile(ModFile file, string field, int? entry, string? mainCharacter, int order)
    {
        SpringDocument document = JsonSerializer.Deserialize<SpringDocument>(file.ReadBytes(), JsonOptions) ?? throw new InvalidDataException("A spring definition must be a JSON object.");
        if (document.Version != 1)
        {
            throw new InvalidDataException($"Unsupported spring definition version {document.Version}. Expected version 1.");
        }
        if (document.ExpectedBoneCount < 2 || document.Joints is null || document.Joints.Length == 0 || document.Joints.Length >= document.ExpectedBoneCount)
        {
            throw new InvalidDataException("expectedBoneCount must be at least two and joints must contain one or more non-root bones.");
        }
        if (document.ExpectedParents is { } parents && parents.Length != document.ExpectedBoneCount)
        {
            throw new InvalidDataException("expectedParents must contain one parent index per bone, using -1 for roots.");
        }
        if (mainCharacter is not null && document.ExpectedParents is null)
        {
            throw new InvalidDataException("A main character spring definition requires expectedParents.");
        }
        FieldSpringBoneJoint[] joints = new FieldSpringBoneJoint[document.Joints.Length];
        HashSet<int> seen = [];
        for (int i = 0; i < joints.Length; i++)
        {
            SpringJoint? joint = document.Joints[i];
            if (joint is null || (uint)joint.Bone >= document.ExpectedBoneCount || !seen.Add(joint.Bone))
            {
                throw new InvalidDataException($"joints[{i}] must identify a unique bone in the expected rig.");
            }
            Vector3 tail = ReadVector(joint.LocalTail, $"joints[{i}].localTail");
            float length = tail.Length();
            if (!float.IsFinite(length) || length <= 0 || !float.IsFinite(joint.Stiffness) || joint.Stiffness < 0 || !float.IsFinite(joint.Damping) || joint.Damping < 0)
            {
                throw new InvalidDataException($"joints[{i}] requires a finite, nonzero localTail and finite, nonnegative stiffness and damping.");
            }
            joints[i] = new(joint.Bone, tail, joint.Stiffness, joint.Damping, ReadVector(joint.Gravity, $"joints[{i}].gravity"));
        }
        FieldSpringBoneMod profile = new(file, field, entry, mainCharacter, order, document.ExpectedBoneCount, document.ExpectedParents, Array.AsReadOnly(joints));
        if (document.ExpectedParents is { } expectedParents)
        {
            profile.ValidateRig(expectedParents);
        }
        return profile;
    }

    private static Vector3 ReadVector(float[]? values, string name)
    {
        if (values is not { Length: 3 } || !values.All(float.IsFinite))
        {
            throw new InvalidDataException($"{name} must contain three finite coordinates.");
        }
        return new(values[0], values[1], values[2]);
    }

    private sealed class SpringDocument
    {
        public required int Version { get; init; }
        public required int ExpectedBoneCount { get; init; }
        public int[]? ExpectedParents { get; init; }
        public required SpringJoint?[]? Joints { get; init; }
    }

    private sealed class SpringJoint
    {
        public required int Bone { get; init; }
        public required float[]? LocalTail { get; init; }
        public required float Stiffness { get; init; }
        public required float Damping { get; init; }
        public required float[]? Gravity { get; init; }
    }
}
