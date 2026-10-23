using Memoria.FF8.Assets;

namespace Memoria.FF8.Mods;

public sealed record ModelModClipReplacement(string Bank, int Slot, EditableAnimation Animation);
public sealed record ModelModReplacement(string ModelId, EditableModel Model, bool ReplaceGeometry, IReadOnlyList<ModelModClipReplacement> Clips)
{
    public IReadOnlyList<int> PreserveNativeParts { get; init; } = [];
}
public sealed record ModelModOutput(string RelativePath, byte[] Content);
public sealed record ModelModInstallPlan(IReadOnlyList<ModelModOutput> Files)
{
    public IReadOnlyList<string> GetConflicts(string allowedRoot, string destination) => ModelModInstaller.GetConflicts(allowedRoot, destination, this);
}

/// <summary>Plans independent geometry/clip outputs and installs only selected replacements.</summary>
public static class ModelModInstaller
{
    public static ModelModInstallPlan Plan(IEnumerable<ModelModReplacement> replacements)
    {
        List<ModelModOutput> outputs = [];
        HashSet<string> addresses = new(StringComparer.OrdinalIgnoreCase);
        foreach (ModelModReplacement replacement in replacements)
        {
            string id = replacement.ModelId.ToLowerInvariant();
            if (!ModelMods.IsModelId(id))
            {
                throw new InvalidDataException("The replacement has an unsupported logical model address.");
            }
            if (replacement.ReplaceGeometry)
            {
                if (replacement.Model.Kind == "stage")
                {
                    throw new NotSupportedException("Battle stage replacement requires an unsupported native stage geometry binding. Stage export remains available.");
                }
                System.Text.Json.Nodes.JsonObject metadata = System.Text.Json.Nodes.JsonNode.Parse(replacement.Model.NativeMetadata.GetRawText()) as System.Text.Json.Nodes.JsonObject ?? new();
                metadata["memoria_preserve_native_parts"] = System.Text.Json.JsonSerializer.SerializeToNode(replacement.PreserveNativeParts);
                Add($"models/{id}.geometry.glb", replacement.Model with { Animations = [], NativeMetadata = System.Text.Json.JsonSerializer.SerializeToElement(metadata) });
            }
            foreach (ModelModClipReplacement clip in replacement.Clips)
            {
                if (!ModelMods.IsBank(clip.Bank) || clip.Slot is < 0 or > 65535)
                {
                    throw new InvalidDataException("The replacement has an unsupported clip bank or slot.");
                }
                Add($"models/{id}.clips/{clip.Bank.ToLowerInvariant()}/{clip.Slot}.glb", replacement.Model with { Meshes = [], Textures = [], Animations = [clip.Animation] });
            }
        }
        return new(Array.AsReadOnly(outputs.ToArray()));

        void Add(string path, EditableModel model)
        {
            if (!addresses.Add(path))
            {
                throw new InvalidDataException($"Multiple selected items replace the same target: {path}.");
            }
            model = model with { NativeMetadata = ModelExchange.RetainedNativeMetadata(model.NativeMetadata, model.Meshes.Count != 0), ExchangeManifest = null };
            byte[] bytes = ModelExchange.ExportGlb(model);
            _ = ModelExchange.ImportGlb(bytes, allowBoneCountChange: true);
            outputs.Add(new(path, bytes));
        }
    }

    public static IReadOnlyList<string> GetConflicts(string allowedRoot, string destination, ModelModInstallPlan plan)
    {
        string root = ValidateDestination(allowedRoot, destination);
        return Array.AsReadOnly(plan.Files.Select(file => OutputPath(root, file.RelativePath)).Where(File.Exists).ToArray());
    }

    public static void Install(string allowedRoot, string destination, ModelModInstallPlan plan)
    {
        if (plan.Files.Count == 0)
        {
            return;
        }
        string root = ValidateDestination(allowedRoot, destination);
        if (plan.Files.Select(file => file.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != plan.Files.Count)
        {
            throw new InvalidDataException("An installation cannot write duplicate replacement paths.");
        }
        List<(string Target, byte[]? Original)> written = [];
        List<string> createdDirectories = [];
        string stage = Path.Combine(Path.GetFullPath(allowedRoot), ".model-stage-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (ModelModOutput file in plan.Files)
            {
                _ = OutputPath(root, file.RelativePath);
                _ = ModelExchange.ImportGlb(file.Content, allowBoneCountChange: true);
            }
            CreateDirectories(stage, createdDirectories);
            for (int index = 0; index < plan.Files.Count; index++)
            {
                File.WriteAllBytes(Path.Combine(stage, index.ToString()), plan.Files[index].Content);
            }
            for (int index = 0; index < plan.Files.Count; index++)
            {
                string target = OutputPath(root, plan.Files[index].RelativePath);
                CreateDirectories(Path.GetDirectoryName(target)!, createdDirectories);
                ValidateDestination(allowedRoot, destination);
                RejectReparsePoints(target);
                byte[]? previous = File.Exists(target) ? File.ReadAllBytes(target) : null;
                File.Move(Path.Combine(stage, index.ToString()), target, true);
                written.Add((target, previous));
            }
        }
        catch
        {
            foreach ((string target, byte[]? previous) in written.AsEnumerable().Reverse())
            {
                RejectReparsePoints(target);
                if (previous is null)
                {
                    File.Delete(target);
                }
                else
                {
                    File.WriteAllBytes(target, previous);
                }
            }
            throw;
        }
        finally
        {
            if (Directory.Exists(stage))
            {
                Directory.Delete(stage, true);
            }
            foreach (string directory in createdDirectories.AsEnumerable().Reverse())
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
        }
    }

    private static string ValidateDestination(string allowedRoot, string destination)
    {
        string allowed = Path.TrimEndingDirectorySeparator(Path.GetFullPath(allowedRoot));
        string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        if (!target.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Select a package directory inside Mods/Memoria.");
        }
        RejectReparsePoints(allowed);
        RejectReparsePoints(target);
        return target;
    }

    private static string OutputPath(string root, string relative)
    {
        if (!relative.StartsWith("models/", StringComparison.Ordinal) || !relative.EndsWith(".glb", StringComparison.Ordinal) || relative.Contains('\\') || relative.Split('/').Any(part => part is "" or "." or "..") || relative.Contains(':'))
        {
            throw new InvalidDataException("The install plan contains an invalid replacement path.");
        }
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A replacement escapes the selected package.");
        }
        RejectReparsePoints(path);
        return path;
    }

    private static void RejectReparsePoints(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(current);
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                continue;
            }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Mod installation cannot traverse a symbolic link or junction.");
            }
        }
    }

    private static void CreateDirectories(string path, List<string> created)
    {
        if (Directory.Exists(path))
        {
            return;
        }
        string? parent = Path.GetDirectoryName(path);
        if (parent is not null)
        {
            CreateDirectories(parent, created);
        }
        RejectReparsePoints(path);
        Directory.CreateDirectory(path);
        created.Add(path);
    }
}
