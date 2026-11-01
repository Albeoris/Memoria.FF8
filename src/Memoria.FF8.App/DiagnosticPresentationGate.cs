using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticPresentationDeclaration(string SignalPath, string ReceiptDirectory, string RunId)
{
    public double TailSeconds { get; init; } = 3;

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(SignalPath) || string.IsNullOrWhiteSpace(ReceiptDirectory) || string.IsNullOrWhiteSpace(RunId) || !double.IsFinite(TailSeconds) || TailSeconds < 0)
        {
            throw new InvalidDataException("Presentation requires a signal path, a fresh receipt directory, a run identity and a finite nonnegative tail.");
        }
    }
}

/// <summary>A coordinator releases a held, genuinely ready invocation with a shared UTC signal.</summary>
internal sealed class DiagnosticPresentationGate
{
    private readonly DiagnosticPresentationDeclaration declaration;
    private readonly ushort expectedEffect;
    private DateTimeOffset? readyUtc;
    private DateTimeOffset? startUtc;

    internal DiagnosticPresentationGate(DiagnosticPresentationDeclaration declaration, ushort expectedEffect)
    {
        declaration.Validate();
        this.declaration = declaration;
        this.expectedEffect = expectedEffect;
        Directory.CreateDirectory(declaration.ReceiptDirectory);
        if (new[] { "ready", "started", "completed", "blocked" }.Any(state => File.Exists(Path.Combine(declaration.ReceiptDirectory, state + ".json"))))
        {
            throw new InvalidDataException("Presentation requires fresh lifecycle receipt paths to exclude stale receipts.");
        }
    }

    internal void Ready(long callback, long frame, DateTimeOffset now)
    {
        if (readyUtc is not null)
        {
            return;
        }
        readyUtc = now;
        Write("ready", callback, frame, now);
    }

    internal bool Released(DateTimeOffset now)
    {
        if (readyUtc is null)
        {
            return false;
        }
        if (startUtc is null && File.Exists(declaration.SignalPath))
        {
            using JsonDocument signal = JsonDocument.Parse(File.ReadAllText(declaration.SignalPath));
            JsonElement root = signal.RootElement;
            if (root.GetProperty("run_id").GetString() != declaration.RunId)
            {
                throw new InvalidDataException("Presentation signal run identity does not match.");
            }
            DateTimeOffset target = root.GetProperty("start_at_utc").GetDateTimeOffset();
            if (target.Offset != TimeSpan.Zero || target <= now)
            {
                throw new InvalidDataException("Presentation signal must declare a future UTC start time.");
            }
            startUtc = target;
        }
        return startUtc is { } start && now >= start;
    }

    internal void Write(string state, long callback, long frame, DateTimeOffset now, string? blocker = null)
    {
        string path = Path.Combine(declaration.ReceiptDirectory, state + ".json");
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new { schema_version = 1, run_id = declaration.RunId, pid = Environment.ProcessId, expected_effect = expectedEffect, state, utc = now, start_at_utc = startUtc, callback, frame, blocker }));
        File.Move(temporary, path, false);
    }
}
