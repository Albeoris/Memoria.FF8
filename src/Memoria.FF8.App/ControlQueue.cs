using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record ControlRequest(string Id, string Command, PlayerAction? Action = null, int Frames = 900, string? Path = null, string? Name = null, int? Selection = null, DebugSceneCommand? Debug = null, bool Compact = false, UiControlCommand? Ui = null, BattleControlCommand? Battle = null, DateTimeOffset? ExpiresAt = null);

/// <summary>A local opt-in queue. Producers atomically rename each unique request from .tmp to .json.</summary>
internal sealed class ControlQueue
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, IncludeFields = true, Converters = { new JsonStringEnumConverter() } };
    private readonly string root;
    private readonly Func<ControlRequest, object> dispatch;

    public ControlQueue(string directory, Func<ControlRequest, object> dispatch)
    {
        root = Path.GetFullPath(directory);
        this.dispatch = dispatch;
        foreach (string child in new[] { "requests", "responses", "processed", "captures" })
        {
            Directory.CreateDirectory(Path.Combine(root, child));
        }
        Console.WriteLine($"control-queue={root}");
    }

    public void Poll()
    {
        string? path = Directory.EnumerateFiles(Path.Combine(root, "requests"), "*.json").Order(StringComparer.Ordinal).FirstOrDefault();
        if (path is null)
        {
            return;
        }
        string name = Path.GetFileName(path);
        string output = Path.Combine(root, "responses", name);
        if (!File.Exists(output))
        {
            string payload;
            try
            {
                payload = File.ReadAllText(path);
            }
            catch (FileNotFoundException)
            {
                // A producer may withdraw a timed-out request after enumeration.
                return;
            }
            catch (IOException exception) when ((exception.HResult & 0xFFFF) is 32 or 33)
            {
                // The published request remains pending; nothing has been dispatched.
                return;
            }
            string id = Path.GetFileNameWithoutExtension(path);
            object response;
            try
            {
                ControlRequest request = JsonSerializer.Deserialize<ControlRequest>(payload, Options) ?? throw new InvalidDataException("Empty command request.");
                id = request.Id;
                if (request.ExpiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow)
                {
                    throw new TimeoutException("The control request expired before it could be dispatched.");
                }
                response = new { Id = id, Ok = true, Result = dispatch(request) };
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                response = new { Id = id, Ok = false, Error = exception.Message, Type = exception.GetType().Name };
            }
            string temporary = output + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(response, Options));
            File.Move(temporary, output);
        }
        string archive = Path.Combine(root, "processed", name);
        if (File.Exists(archive))
        {
            archive += ".duplicate-" + Guid.NewGuid().ToString("N");
        }
        try
        {
            File.Move(path, archive);
        }
        catch (FileNotFoundException)
        {
            // A producer may withdraw a timed-out request while its response is being published.
        }
    }
}
