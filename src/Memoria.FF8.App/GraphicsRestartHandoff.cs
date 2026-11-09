using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Platform;

namespace Memoria.FF8;

/// <summary>Publishes the shared graphics dispatcher receipt after a host prepares its own continuation.</summary>
internal static class GraphicsRestartHandoff
{
    internal static object Request(GraphicsBackend backend, DateTimeOffset? expiresAt, bool paused, Func<string, DateTimeOffset, UpdateSaveResult> save, Action cancel, Action exit, string scene, int fieldId)
    {
        if (GraphicsBackendSelection.UnavailableReason(backend, HostPlatforms.Current) is string reason)
        {
            throw new PlatformNotSupportedException(reason);
        }
        string? requestPath = Environment.GetEnvironmentVariable("MEMORIA_GRAPHICS_RESTART_REQUEST");
        string? token = Environment.GetEnvironmentVariable("MEMORIA_GRAPHICS_RESTART_TOKEN");
        if (string.IsNullOrWhiteSpace(requestPath) || !Path.IsPathFullyQualified(requestPath) || string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Start the game through the Memoria launcher or graphics dispatcher to enable automatic restart.");
        }
        if (expiresAt is null || expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new TimeoutException("The graphics restart request expired. Try again.");
        }
        if (paused)
        {
            throw new InvalidOperationException("Another save handoff is already prepared. Finish or cancel it before restarting graphics.");
        }
        string directory = Path.GetDirectoryName(requestPath)!;
        string statePath = Path.Combine(directory, "graphics-restart-" + Guid.NewGuid().ToString("N") + ".ff8state");
        string temporary = requestPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            UpdateSaveResult saved = save(statePath, expiresAt.Value);
            string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(saved.Path))).ToLowerInvariant();
            var receipt = new { schemaVersion = 1, processId = Environment.ProcessId, token, backend = backend.ToString(), statePath = saved.Path, sha256 = hash, scene = scene, fieldId = fieldId };
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, receipt);
                stream.Flush(true);
            }
            if (expiresAt <= DateTimeOffset.UtcNow)
            {
                throw new TimeoutException("The graphics restart request expired before handoff. Try again.");
            }
            File.Move(temporary, requestPath);
            Console.WriteLine($"Graphics restart: requested={backend} state={saved.Path} scene={scene} field={fieldId}");
            exit();
            return new { Restarting = true, Path = saved.Path, Backend = backend.ToString() };
        }
        catch
        {
            cancel();
            throw;
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
