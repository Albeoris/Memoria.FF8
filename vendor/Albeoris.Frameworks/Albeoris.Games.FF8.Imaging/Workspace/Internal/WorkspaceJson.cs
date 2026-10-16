using System.Text.Json;
using System.Text.Json.Serialization;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal static class WorkspaceJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Write<T>(IWorkspaceWriter output, String path, T value) where T : notnull =>
        output.Write(path, stream => JsonSerializer.Serialize(stream, value, Options));

    public static T Read<T>(IWorkspaceReader input, String path) where T : class =>
        JsonSerializer.Deserialize<T>(input.Read(path), Options)
        ?? throw new InvalidDataException($"Workspace file '{path}' is empty.");
}