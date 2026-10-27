using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Platform;

namespace Memoria.FF8.Bootstrap;

public static class GraphicsRestart
{
    public static string[]? ReadArguments(string requestPath, string expectedToken, int processId, string[] arguments)
    {
        if (!File.Exists(requestPath))
        {
            return null;
        }
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(requestPath));
        JsonElement root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("processId").GetInt32() != processId || root.GetProperty("token").GetString() != expectedToken)
        {
            throw new InvalidDataException("The graphics restart receipt does not belong to this game process.");
        }
        GraphicsBackend backend = GraphicsBackendSelection.Parse(root.GetProperty("backend").GetString()!);
        _ = GraphicsBackendSelection.InitializationOrder(backend, HostPlatforms.Current);
        string state = root.GetProperty("statePath").GetString() ?? throw new InvalidDataException("A graphics restart requires a standalone save.");
        if (!Path.IsPathFullyQualified(state) || Path.GetFullPath(Path.GetDirectoryName(state)!) != Path.GetFullPath(Path.GetDirectoryName(requestPath)!) || Path.GetExtension(state) != ".ff8state")
        {
            throw new InvalidDataException("The graphics restart save must be a standalone file beside the owned receipt.");
        }
        FileInfo file = new(state);
        if (file.LinkTarget is not null || file.Length == 0)
        {
            throw new InvalidDataException("The graphics restart save is missing, empty or a symbolic link.");
        }
        using FileStream stream = file.OpenRead();
        string hash = Convert.ToHexString(SHA256.HashData(stream));
        if (!string.Equals(hash, root.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The graphics restart save changed after handoff.");
        }
        List<string> result = [];
        for (int i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] is "--graphics-backend" or "--state")
            {
                if (++i == arguments.Length)
                {
                    throw new ArgumentException("Missing graphics restart argument value.");
                }
                continue;
            }
            result.Add(arguments[i]);
        }
        result.AddRange(["--graphics-backend", backend.ToString(), "--state", state]);
        return result.ToArray();
    }
}
