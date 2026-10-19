using System.Text.Json;

namespace Memoria.Launcher;

internal sealed record UpdateResume(string GameDirectory, string SavePath)
{
    internal static string IntentPath(string root) => Path.Combine(root, ".memoria-update-resume.json");

    internal static string? TryDelete(string root)
    {
        try
        {
            UpdateInstaller.RejectLinks(IntentPath(root));
            File.Delete(IntentPath(root));
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return "Could not remove the completed update resume information: " + exception.Message;
        }
    }

    internal void Write(string root)
    {
        string path = IntentPath(root);
        UpdateInstaller.RejectLinks(path);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, this);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    internal static UpdateResume Read(string root)
    {
        string path = IntentPath(root);
        UpdateInstaller.RejectLinks(path);
        UpdateResume result = JsonSerializer.Deserialize<UpdateResume>(File.ReadAllText(path)) ?? throw new InvalidDataException("The update resume information is empty.");
        if (!Path.IsPathFullyQualified(result.GameDirectory) || !Path.IsPathFullyQualified(result.SavePath) || !File.Exists(result.SavePath))
        {
            throw new InvalidDataException("The automatic update save is missing or its location is invalid.");
        }
        return result;
    }
}

internal sealed class GameUpdateSession
{
    internal string Directory { get; }
    internal string SavePath { get; private set; }

    internal GameUpdateSession(string? directory = null)
    {
        Directory = Path.GetFullPath(directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Memoria.FF8", "auto-update", Guid.NewGuid().ToString("N")));
        SavePath = Path.Combine(Directory, "auto-update-" + Guid.NewGuid().ToString("N") + ".json");
        System.IO.Directory.CreateDirectory(Path.Combine(Directory, "control", "requests"));
        System.IO.Directory.CreateDirectory(Path.Combine(Directory, "control", "responses"));
    }

    internal string ControlDirectory => Path.Combine(Directory, "control");

    internal async Task SaveAsync(Func<bool> hasExited, TimeSpan? timeout = null)
    {
        SavePath = Path.Combine(Directory, "auto-update-" + Guid.NewGuid().ToString("N") + ".json");
        JsonElement result = await RequestAsync("save-for-update", hasExited, timeout);
        if (!result.TryGetProperty("Saved", out JsonElement saved) || saved.ValueKind != JsonValueKind.True || !result.TryGetProperty("Path", out JsonElement path) || !string.Equals(path.GetString(), SavePath, StringComparison.OrdinalIgnoreCase) || !File.Exists(SavePath))
        {
            throw new InvalidDataException("The game did not confirm the automatic update save.");
        }
    }

    internal async Task<JsonElement> RequestAsync(string command, Func<bool> hasExited, TimeSpan? timeout = null)
    {
        string id = Guid.NewGuid().ToString("N");
        string request = Path.Combine(ControlDirectory, "requests", id + ".json");
        string response = Path.Combine(ControlDirectory, "responses", id + ".json");
        TimeSpan duration = timeout ?? TimeSpan.FromSeconds(30);
        using CancellationTokenSource deadline = new(duration);
        File.WriteAllText(request + ".tmp", JsonSerializer.Serialize(new { Id = id, Command = command, Path = SavePath, ExpiresAt = DateTimeOffset.UtcNow + duration }));
        File.Move(request + ".tmp", request);
        bool answered = false;
        try
        {
            while (true)
            {
                if (File.Exists(response))
                {
                    answered = true;
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(response));
                    JsonElement root = document.RootElement;
                    if (root.GetProperty("Id").GetString() != id)
                    {
                        throw new InvalidDataException("The game returned an unrelated update response.");
                    }
                    if (!root.GetProperty("Ok").GetBoolean())
                    {
                        throw new InvalidOperationException(root.GetProperty("Error").GetString() ?? "The game refused the update request.");
                    }
                    return root.GetProperty("Result").Clone();
                }
                if (hasExited())
                {
                    throw new IOException("The game closed before answering the update request.");
                }
                await Task.Delay(100, deadline.Token);
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            throw new TimeoutException("The game did not answer the update request within the time limit.");
        }
        finally
        {
            if (!answered)
            {
                File.Delete(request);
            }
        }
    }
}
