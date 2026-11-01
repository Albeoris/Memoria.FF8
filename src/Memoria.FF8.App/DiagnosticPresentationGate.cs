using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticPresentationDeclaration(string SignalPath, string ReceiptDirectory, string RunId)
{
    public double TailSeconds { get; init; } = 3;
    public bool FillSelectedActorAtb { get; init; } = true;
    public string? VisualSignalPath { get; init; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(SignalPath) || string.IsNullOrWhiteSpace(ReceiptDirectory) || string.IsNullOrWhiteSpace(RunId) || !double.IsFinite(TailSeconds) || TailSeconds < 0)
        {
            throw new InvalidDataException("Presentation requires a signal path, a fresh receipt directory, a run identity and a finite nonnegative tail.");
        }
        if (VisualSignalPath is not null && (string.IsNullOrWhiteSpace(VisualSignalPath) || Path.GetFullPath(VisualSignalPath) == Path.GetFullPath(SignalPath)))
        {
            throw new InvalidDataException("Visual presentation requires a distinct signal path.");
        }
    }
}

/// <summary>A coordinator releases a held, genuinely ready invocation with a shared UTC signal.</summary>
internal sealed class DiagnosticPresentationGate
{
    private static readonly JsonSerializerOptions ReceiptJson = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private readonly DiagnosticPresentationDeclaration declaration;
    private readonly ushort expectedEffect;
    private readonly byte? commandId;
    private readonly int? abilityId;
    private DateTimeOffset? readyUtc;
    private DateTimeOffset? startUtc;
    private DateTimeOffset? visualStartUtc;
    private bool visualReady;

    internal DiagnosticPresentationGate(DiagnosticPresentationDeclaration declaration, ushort expectedEffect, byte? commandId = null, int? abilityId = null)
    {
        declaration.Validate();
        this.declaration = declaration;
        this.expectedEffect = expectedEffect;
        this.commandId = commandId;
        this.abilityId = abilityId;
        Directory.CreateDirectory(declaration.ReceiptDirectory);
        if (new[] { "ready", "started", "visual-ready", "visual-started", "completed", "blocked" }.Any(state => File.Exists(Path.Combine(declaration.ReceiptDirectory, state + ".json"))))
        {
            throw new InvalidDataException("Presentation requires fresh lifecycle receipt paths to exclude stale receipts.");
        }
    }

    internal void Ready(long callback, long frame, DateTimeOffset now, bool? atbGating = null)
    {
        if (readyUtc is not null)
        {
            return;
        }
        readyUtc = now;
        Write("ready", callback, frame, now, atbGating: atbGating);
    }

    internal bool Released(DateTimeOffset now)
    {
        if (readyUtc is null)
        {
            return false;
        }
        return ReadRelease(declaration.SignalPath, ref startUtc, now);
    }

    internal void VisualReady(long callback, long frame, DateTimeOffset now, object nativeIdentity)
    {
        if (declaration.VisualSignalPath is null || visualReady)
        {
            throw new InvalidOperationException("Visual readiness requires its distinct unconsumed presentation gate.");
        }
        visualReady = true;
        Write("visual-ready", callback, frame, now, nativeIdentity: nativeIdentity);
    }

    internal bool VisualReleased(DateTimeOffset now) => visualReady && ReadRelease(declaration.VisualSignalPath!, ref visualStartUtc, now);

    internal void WaitForVisualRelease(TimeSpan timeout)
    {
        if (!visualReady || timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Visual entry requires a ready gate within its remaining overall deadline.");
        }
        using CancellationTokenSource cancellation = new(timeout);
        Task.Run(async () =>
        {
            while (!VisualReleased(DateTimeOffset.UtcNow))
            {
                await Task.Delay(1, cancellation.Token).ConfigureAwait(false);
            }
        }, cancellation.Token).GetAwaiter().GetResult();
    }

    private bool ReadRelease(string path, ref DateTimeOffset? targetUtc, DateTimeOffset now)
    {
        if (targetUtc is null && File.Exists(path))
        {
            using JsonDocument signal = JsonDocument.Parse(File.ReadAllText(path));
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
            targetUtc = target;
        }
        return targetUtc is { } start && now >= start;
    }

    internal void Write(string state, long callback, long frame, DateTimeOffset now, string? blocker = null, object? nativeIdentity = null, DateTimeOffset? dispatchUtc = null, bool? atbGating = null)
    {
        string path = Path.Combine(declaration.ReceiptDirectory, state + ".json");
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new { schema_version = 1, run_id = declaration.RunId, pid = Environment.ProcessId, expected_effect = expectedEffect, command_id = commandId, ability_id = abilityId, state, utc = now, start_at_utc = startUtc, visual_start_at_utc = visualStartUtc, dispatch_utc = dispatchUtc, native_identity = nativeIdentity, atb_gating = atbGating, callback, frame, blocker }, ReceiptJson));
        File.Move(temporary, path, false);
    }
}
