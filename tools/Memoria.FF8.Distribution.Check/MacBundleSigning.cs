using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Memoria.FF8.Platform;

static class MacBundleSigning
{
    public static void SignBundle(string root)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("A complete macOS distribution must be finalized on macOS.");
        }
        string manifestPath = Path.Combine(root, MacCodeSigning.Manifest);
        string[] files = MacCodeSigning.BundleFiles(root);
        Dictionary<string, string> inputFiles = files.Where(path => path != manifestPath).ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'), StringComparer.Ordinal);
        if (inputFiles.Keys.Any(path => path.StartsWith("Contents/MacOS/", StringComparison.Ordinal) && path != MacCodeSigning.Launcher))
        {
            throw new InvalidDataException("Contents/MacOS may contain only the native launcher; managed assemblies and runtime resources belong in Contents/Resources.");
        }
        JsonObject manifest = JsonNode.Parse(File.ReadAllBytes(manifestPath))!.AsObject();
        if (manifest["SchemaVersion"]?.GetValue<int>() != 2 || !HostPlatforms.IsMacRuntime(manifest["RuntimeIdentifier"]?.GetValue<string>() ?? ""))
        {
            throw new InvalidDataException("Signing requires a freshly assembled macOS distribution.");
        }
        MacBundleVersion.Validate(File.ReadAllBytes(Path.Combine(root, "Contents/Info.plist")), manifest["Version"]!.GetValue<string>());
        // Reject stale input inventory before any signature changes its bytes.
        HashSet<string> inventoried = new(StringComparer.Ordinal);
        foreach (JsonObject file in manifest["Files"]!.AsArray().OfType<JsonObject>())
        {
            string relative = file["Path"]!.GetValue<string>();
            if (!inventoried.Add(relative) || !inputFiles.TryGetValue(relative, out string? path))
            {
                throw new InvalidDataException("Invalid pre-signing inventory path: " + relative);
            }
            using FileStream input = File.OpenRead(path);
            if (input.Length != file["Size"]!.GetValue<long>() || Convert.ToHexStringLower(SHA256.HashData(input)) != file["Sha256"]!.GetValue<string>())
            {
                throw new InvalidDataException("Pre-signing inventory mismatch: " + path);
            }
        }
        if (!inventoried.SetEquals(inputFiles.Keys) || inputFiles.ContainsKey(MacCodeSigning.ResourceEnvelope))
        {
            throw new InvalidDataException("Signing requires an exact, freshly assembled unsigned bundle inventory.");
        }
        foreach (string path in files)
        {
            // SDK/NuGet payloads can retain executable bits on managed DLLs.
            // Normalize the complete bundle before recording modes or signing.
            bool executable = Path.GetFileName(path) is "Memoria.Launcher" or "Memoria.FF8" or "Memoria.FF8.Music" or "Memoria.PsfDecoder";
            File.SetUnixFileMode(path, (UnixFileMode)(executable ? 493 : 420));
        }
        foreach (string path in files.OrderByDescending(path => path.Length))
        {
            if (MacCodeSigning.IsMachO(path))
            {
                MacCodeSigning.Sign(path);
            }
        }
        JsonArray inventory = new();
        foreach (string path in files.Order(StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (path == manifestPath || relative == MacCodeSigning.ResourceEnvelope)
            {
                continue;
            }
            bool launcher = relative == MacCodeSigning.Launcher;
            using FileStream input = File.OpenRead(path);
            (long size, string hash) = launcher ? MacCodeSigning.UnsignedHash(path) : (input.Length, Convert.ToHexStringLower(SHA256.HashData(input)));
            JsonObject file = new() { ["Path"] = relative, ["Size"] = size, ["Sha256"] = hash, ["UnixMode"] = (int)File.GetUnixFileMode(path) };
            if (launcher)
            {
                file["HashKind"] = MacCodeSigning.UnsignedHashKind;
            }
            inventory.Add(file);
        }
        manifest["SchemaVersion"] = 3;
        manifest["Files"] = inventory;
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        // The outer signature seals the final manifest, Info.plist and nested code.
        MacCodeSigning.Sign(root);
        PackagedDistribution.Validate(root, manifest["RuntimeIdentifier"]!.GetValue<string>());
    }
}
