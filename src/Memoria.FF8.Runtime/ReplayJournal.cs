using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum ReplayEntryKind
{
    FieldLoad,
    Step,
    Update,
    Action,
    Dialogue,
    WorldAction,
    MovieSample,
    MovieComplete,
    BattleInput,
    SemanticTrigger,
    ScriptGoal,
    InputSample,
    PersistentEffect,
    WorldAllocation
}

public enum ReplayReceiptKind
{
    Command,
    Branch,
    Dialogue,
    Progress,
    InputSample,
    PersistentEffect
}

public sealed record ReplayPlayerPose(int FieldId, float X, float Y, float Z, int Triangle, int Direction, int? FixedX = null, int? FixedY = null, int? FixedZ = null);
public sealed record ReplayNavigationStimulus(ReplayPlayerPose Start, ReplayPlayerPose End, bool NativeMovement, int? TriggerFixedX = null, int? TriggerFixedY = null, int? TriggerFixedZ = null, int[]? PushContacts = null);
public sealed record ReplayWorldNavigationStimulus(int X, int Z);
public sealed record ReplayReceipt(ReplayReceiptKind Kind, int FieldId, long Frame, int ObjectId, int ScriptId, int Operation, int Code = 0, int Value = 0, string? Text = null, int[]? Arguments = null, int Sequence = -1);

/// <summary>An engine input transaction with its ordered causal receipts; these are never applied as state writes.</summary>
public sealed record ReplayJournalEntry([property: JsonRequired] ReplayEntryKind Kind, [property: JsonRequired] SceneKind Scene, [property: JsonRequired] int FieldId, [property: JsonRequired] long Frame)
{
    public int Code { get; init; }
    public int Value { get; init; }
    public int? Quantity { get; init; }
    public string? Text { get; init; }
    public float Horizontal { get; init; }
    public float Vertical { get; init; }
    public float CameraHorizontal { get; init; }
    public int Frames { get; init; } = 1;
    public int Advanced { get; init; }
    public int Repeat { get; init; } = 1;
    public bool Accepted { get; init; } = true;
    public SceneKind? ResultScene { get; init; }
    public int? ResultFieldId { get; init; }
    public long? ResultFrame { get; init; }
    public int Trigger { get; init; }
    public int ResultTrigger { get; init; }
    public int? MessageId { get; init; }
    public int? ObjectId { get; init; }
    public int? ScriptId { get; init; }
    public int? Operation { get; init; }
    public int? ResultMessageId { get; init; }
    public int? ResultObjectId { get; init; }
    public int? ResultScriptId { get; init; }
    public int? ResultOperation { get; init; }
    public ReplayPlayerPose? StartPose { get; init; }
    public ReplayPlayerPose? EndPose { get; init; }
    public ReplayNavigationStimulus[] Navigation { get; init; } = [];
    public ReplayWorldNavigationStimulus[] WorldNavigation { get; init; } = [];
    public ReplayReceipt[] Receipts { get; init; } = [];
    public long CommandCount { get; init; }
    public ulong CommandDigest { get; init; }
    public double MovieSeconds { get; init; }
    public MoviePlaybackState? MoviePlayback { get; init; }
    public MovieSkipFrames? MovieSkipFrames { get; init; }
    public string? MovieSourceHash { get; init; }
    public bool Skipped { get; init; }
    public long ElapsedTicks { get; init; }
    public string? NativeSourceHash { get; init; }
    public BattleInput? BattleInput { get; init; }
    public ReplaySemanticTrigger? SemanticTrigger { get; init; }
    public ReplaySemanticTrigger[] SemanticAdmissions { get; init; } = [];
    public ReplayScriptGoal? ScriptGoal { get; init; }
    public int[]? EffectArguments { get; init; }
    public ReplayWorldAllocation? WorldAllocation { get; init; }
    public ReplayWorldEffectCause[] WorldEffects { get; init; } = [];

    public void Validate()
    {
        WorldAllocation?.Validate();
        if ((Kind == ReplayEntryKind.WorldAllocation) != (WorldAllocation is not null) || EffectArguments is not null && (Kind != ReplayEntryKind.PersistentEffect || EffectArguments.Length > 256) || WorldEffects is null || WorldEffects.Length > 30000)
        {
            throw new InvalidDataException("Invalid semantic allocation/effect input payload.");
        }
        foreach (ReplayWorldEffectCause cause in WorldEffects)
        {
            ReplayWorldEffectCause.FromArguments(cause.ToArguments());
        }
        SemanticTrigger?.Validate();
        if ((Kind == ReplayEntryKind.SemanticTrigger) != (SemanticTrigger is not null) || (Kind is ReplayEntryKind.ScriptGoal or ReplayEntryKind.InputSample) != (ScriptGoal is not null) || SemanticAdmissions is null || SemanticAdmissions.Length > 262144)
        {
            throw new InvalidDataException("Semantic replay inputs require their exact typed payload.");
        }
        foreach (ReplaySemanticTrigger admission in SemanticAdmissions)
        {
            if (admission is null)
            {
                throw new InvalidDataException("Missing semantic admission provenance.");
            }
            admission.Validate();
        }
        if (ScriptGoal is { } goal && (goal.FieldId != FieldId || goal.ProgramHash is not { Length: 64 } || !goal.ProgramHash.All(Uri.IsHexDigit) || goal.ExecutableHash is not null && (goal.ExecutableHash.Length != 64 || !goal.ExecutableHash.All(Uri.IsHexDigit)) || goal.ObjectId < 0 || goal.ScriptId < 0 || goal.Operation < 0))
        {
            throw new InvalidDataException("Invalid inferred script producer goal.");
        }
        MovieSkipFrames?.Validate();
        if (MovieSkipFrames is not null && (Kind != ReplayEntryKind.Action || Code != 6 || MovieSourceHash is null || MoviePlayback is null))
        {
            throw new InvalidDataException("Accelerated movie samples require their skip action, completion clock and source identity.");
        }
        BattleInput?.Validate();
        if (!float.IsFinite(CameraHorizontal) || Math.Abs(CameraHorizontal) > 1 || CameraHorizontal != 0 && (Kind != ReplayEntryKind.WorldAction || Code is not (4 or 5)))
        {
            throw new InvalidDataException("Manual camera input requires a finite world Move/Wait axis between -1 and 1.");
        }
        if (Kind == ReplayEntryKind.BattleInput ? BattleInput is null : BattleInput is not null)
        {
            throw new InvalidDataException("Battle replay input must belong to a battle transaction.");
        }
        if (!Enum.IsDefined(Kind) || !Enum.IsDefined(Scene) || FieldId is < 0 or > ushort.MaxValue || Frame < 0 || Frames is < 1 or > 30000 || Advanced < 0 || Advanced > 30000 || Repeat is < 1 or > 30000 || Text?.Length > 1024 || !float.IsFinite(Horizontal) || !float.IsFinite(Vertical) || Math.Abs(Horizontal) > 1 || Math.Abs(Vertical) > 1 || !double.IsFinite(MovieSeconds) || MovieSeconds < 0 || ResultScene is SceneKind scene && !Enum.IsDefined(scene) || ResultFieldId is < 0 or > ushort.MaxValue || ResultFrame < 0 || CommandCount < 0 || ElapsedTicks < 0 || Navigation is null || Receipts is null || Navigation.Length > 30000 || Receipts.Length > 262144)
        {
            throw new InvalidDataException("Invalid engine replay transaction.");
        }
        if (Kind == ReplayEntryKind.Action && (Code is < 0 or > 11 || Code == 8 && (Value < 0 || Value > ushort.MaxValue) || Code == 11 && (uint)Value >= 3 || Code == 9 && (uint)Value >= 8 && Value != 255 || Code == 10 && (Value is < 0 or > byte.MaxValue || Quantity is null or < 1 or > byte.MaxValue)) || Kind == ReplayEntryKind.WorldAction && Code is < 0 or > 6 || NativeSourceHash is not null && (NativeSourceHash.Length != 64 || NativeSourceHash.Any(character => !Uri.IsHexDigit(character))))
        {
            throw new InvalidDataException("Unsupported replay action or native source hash.");
        }
        if (Trigger is < 0 or > 9 || ResultTrigger is < 0 or > 9 || MessageId < -1 || ObjectId < -1 || ScriptId < -1 || Operation < -1 || ResultMessageId < -1 || ResultObjectId < -1 || ResultScriptId < -1 || ResultOperation < -1)
        {
            throw new InvalidDataException("Invalid replay causal trigger or dialogue site.");
        }
        ValidatePose(StartPose);
        ValidatePose(EndPose);
        if (MovieSourceHash is not null && (MovieSourceHash.Length != 64 || MovieSourceHash.Any(character => !Uri.IsHexDigit(character))))
        {
            throw new InvalidDataException("Invalid replay movie source identity.");
        }
        if (WorldNavigation is null || WorldNavigation.Length > 30000 || WorldNavigation.Any(point => point is null || point.X is < -131072 or >= 131072 || point.Z is < -98304 or >= 98304))
        {
            throw new InvalidDataException("Invalid replay world navigation proposal.");
        }
        foreach (ReplayNavigationStimulus stimulus in Navigation)
        {
            if (stimulus is null || stimulus.Start is null || stimulus.End is null)
            {
                throw new InvalidDataException("Missing replay navigation stimulus.");
            }
            ValidatePose(stimulus.Start);
            ValidatePose(stimulus.End);
            if (stimulus.Start.FieldId != FieldId || stimulus.End.FieldId != FieldId || stimulus.PushContacts?.Length > 256 || stimulus.PushContacts?.Any(id => id < 0) == true || stimulus.TriggerFixedX.HasValue != stimulus.TriggerFixedY.HasValue || stimulus.TriggerFixedX.HasValue != stimulus.TriggerFixedZ.HasValue)
            {
                throw new InvalidDataException("Invalid replay navigation contact provenance.");
            }
        }
        foreach (ReplayReceipt receipt in Receipts)
        {
            if (receipt is null || !Enum.IsDefined(receipt.Kind) || receipt.FieldId is < 0 or > ushort.MaxValue || receipt.Frame < 0 || receipt.Text?.Length > 1024 || receipt.Arguments?.Length > 256 || receipt.Sequence < -1)
            {
                throw new InvalidDataException("Invalid engine causal receipt.");
            }
        }
        if (MoviePlayback is MoviePlaybackState playback && (playback.TerminalFrame < 0 || playback.FrameIndex < 0 || playback.FrameIndex > playback.TerminalFrame || playback.Completed && playback.IsPlaying))
        {
            throw new InvalidDataException("Invalid replay movie clock.");
        }
    }

    private static void ValidatePose(ReplayPlayerPose? pose)
    {
        if (pose is not null && (pose.FieldId is < 0 or > ushort.MaxValue || !float.IsFinite(pose.X) || !float.IsFinite(pose.Y) || !float.IsFinite(pose.Z) || pose.Triangle < 0 || pose.Direction is < 0 or > 255 || pose.FixedX.HasValue != pose.FixedY.HasValue || pose.FixedX.HasValue != pose.FixedZ.HasValue))
        {
            throw new InvalidDataException("Invalid replay player pose.");
        }
    }
}

/// <summary>A persistent save-bound causal journal. Only an actual New Game creates its root.</summary>
[JsonConverter(typeof(ReplayJournalJsonConverter))]
public sealed partial class ReplayJournal
{
    public const int CurrentVersion = 2;
    public const int MaximumEntries = 262144;
    public const int MaximumDecodedBytes = 64 * 1024 * 1024;
    // Authentic capsules written before storage compaction may exceed the
    // compact journal budget. Reading their inputs must preserve reconstruction.
    public const int MaximumInputDecodedBytes = 256 * 1024 * 1024;
    public const string NewGameRoot = "Memoria.FF8.NewGame.causal.v1";
    private readonly Node? tail;
    private readonly Node? completedVisits;
    private readonly ReplayCompletedVisit[] visits;
    private readonly int version;

    private ReplayJournal(string sessionId, Node? tail, int count, int bytes, bool complete, string? reason, Node? completedVisits = null, ReplayCompletedVisit[]? visits = null, int version = CurrentVersion)
    {
        SessionId = sessionId;
        this.tail = tail;
        this.completedVisits = completedVisits;
        this.visits = visits ?? [];
        this.version = version;
        EntryCount = count;
        EncodedByteCount = bytes;
        IsComplete = complete;
        UnavailableReason = reason;
    }

    public int Version => version;
    public string Root => NewGameRoot;
    public int RandomSeed => 0;
    public string SessionId { get; }
    public int EntryCount { get; }
    public int EncodedByteCount { get; }
    public bool IsComplete { get; }
    public string? UnavailableReason { get; }
    public ReplayJournalEntry? Endpoint => tail is null ? null : Snapshot(tail.Entry);
    public IReadOnlyList<ReplayJournalEntry> Entries
    {
        get
        {
            ReplayJournalEntry[] entries = new ReplayJournalEntry[EntryCount];
            Node? node = tail;
            for (int index = entries.Length - 1; index >= 0; index--)
            {
                entries[index] = Snapshot(node!.Entry);
                node = node.Previous;
            }
            return entries;
        }
    }

    internal static ReplayJournal StartNewGame(string? sessionId = null)
    {
        string id = sessionId ?? Guid.NewGuid().ToString("N");
        if (!Guid.TryParseExact(id, "N", out _))
        {
            throw new InvalidDataException("Invalid causal journal session identity.");
        }
        return new ReplayJournal(id, null, 0, 2, true, null);
    }

    public ReplayJournal Append(ReplayJournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        entry.Validate();
        if (!IsComplete)
        {
            return this;
        }
        if (Version == 1)
        {
            return MarkUnavailable("A literal version1 capsule must be recreated before recording current semantic history.");
        }
        ReplayJournal current = tail is not null && (entry.Kind is ReplayEntryKind.FieldLoad or ReplayEntryKind.WorldAllocation || entry.Scene == SceneKind.WorldMap && tail.Entry.Scene != SceneKind.WorldMap) ? CompleteVisit(entry) : this;
        return current.AppendCore(entry);
    }

    private ReplayJournal AppendCore(ReplayJournalEntry entry)
    {
        if (tail is Node lastBattle && TryCombineBattleTicks(lastBattle.Entry, entry) is ReplayJournalEntry combinedBattle)
        {
            int oldBytes = ReplayJournalEntryStorage.Encode(lastBattle.Entry).Length;
            int newBytes = ReplayJournalEntryStorage.Encode(combinedBattle).Length;
            if (EncodedByteCount - oldBytes > MaximumDecodedBytes - newBytes)
            {
                return MarkUnavailable("The causal journal capacity was reached. Its authentic prefix remains saved, but reconstruction is unavailable.");
            }
            return new ReplayJournal(SessionId, new Node(combinedBattle, lastBattle.Previous), EntryCount, EncodedByteCount - oldBytes + newBytes, true, null, completedVisits, visits, version);
        }
        if (tail is Node previous && CanRepeat(previous.Entry, entry))
        {
            ReplayJournalEntry combined = previous.Entry with { Repeat = previous.Entry.Repeat + 1, ResultFrame = entry.ResultFrame };
            int oldBytes = ReplayJournalEntryStorage.Encode(previous.Entry).Length;
            int newBytes = ReplayJournalEntryStorage.Encode(combined).Length;
            if (EncodedByteCount - oldBytes > MaximumDecodedBytes - newBytes)
            {
                return MarkUnavailable("The causal journal capacity was reached. Its authentic prefix remains saved, but reconstruction is unavailable.");
            }
            return new ReplayJournal(SessionId, new Node(combined, previous.Previous), EntryCount, EncodedByteCount - oldBytes + newBytes, true, null, completedVisits, visits, version);
        }
        int bytes = ReplayJournalEntryStorage.Encode(entry).Length + (EntryCount == 0 ? 0 : 1);
        if (EntryCount == MaximumEntries || EncodedByteCount > MaximumDecodedBytes - bytes)
        {
            return MarkUnavailable("The causal journal capacity was reached. Its authentic prefix remains saved, but reconstruction is unavailable.");
        }
        return new ReplayJournal(SessionId, new Node(Snapshot(entry), tail), EntryCount + 1, EncodedByteCount + bytes, true, null, completedVisits, visits, version);
    }

    private static ReplayJournalEntry? TryCombineBattleTicks(ReplayJournalEntry previous, ReplayJournalEntry next)
    {
        if (previous.Kind != ReplayEntryKind.BattleInput || next.Kind != ReplayEntryKind.BattleInput || previous.BattleInput is not { Kind: BattleInputKind.Tick } oldInput || next.BattleInput is not { Kind: BattleInputKind.Tick } newInput || previous.Repeat != 1 || next.Repeat != 1 || oldInput.Frames > 30000 - newInput.Frames || previous.Advanced > 30000 - next.Advanced || previous.Receipts.Length != 0 || next.Receipts.Length != 0 || previous.Navigation.Length != 0 || next.Navigation.Length != 0 || previous.WorldNavigation.Length != 0 || next.WorldNavigation.Length != 0)
        {
            return null;
        }
        ReplayJournalEntry oldBoundary = previous with { BattleInput = oldInput with { Frames = 1 }, Advanced = 0 };
        ReplayJournalEntry newBoundary = next with { BattleInput = newInput with { Frames = 1 }, Advanced = 0 };
        // The suspended VM boundary is identical. Every represented battle dispatch still executes; player commands and mode changes fence this timing run.
        return oldBoundary == newBoundary ? previous with { BattleInput = oldInput with { Frames = oldInput.Frames + newInput.Frames }, Advanced = previous.Advanced + next.Advanced } : null;
    }

    private ReplayJournal CompleteVisit(ReplayJournalEntry boundary, bool inferred = false)
    {
        ReplayJournalEntry[] active = ActiveEntries.ToArray();
        ReplayJournalEntry[] intents = ProjectVisit(active, inferred);
        int earlierCount = EntryCount - active.Length;
        int earlierBytes = earlierCount == 0 ? 2 : 1 + visits.SelectMany(value => VisitEntries(value)).Sum(value => ReplayJournalEntryStorage.Encode(value).Length + 1);
        ReplayJournal compact = new(SessionId, completedVisits, earlierCount, earlierBytes, true, null, completedVisits, visits, version);
        ReplayJournalEntry first = active[0];
        if (first.Kind is ReplayEntryKind.FieldLoad or ReplayEntryKind.WorldAllocation)
        {
            compact = compact.AppendCore(WithoutPhysicalDiagnostics(first));
        }
        foreach (ReplayJournalEntry entry in intents)
        {
            compact = compact.AppendCore(entry);
        }
        ReplayCompletedVisit visit = new(visits.Length, first.Scene, first.FieldId, first.Kind is ReplayEntryKind.FieldLoad or ReplayEntryKind.WorldAllocation ? WithoutPhysicalDiagnostics(first) : null, new ReplayVisitExit(boundary.Scene, boundary.FieldId, active.SelectMany(value => value.SemanticAdmissions).LastOrDefault(value => value.Kind is ReplaySemanticTriggerKind.FieldGateway or ReplaySemanticTriggerKind.WorldFieldEntry)), intents.Select(Snapshot).ToArray(), inferred);
        return new ReplayJournal(SessionId, compact.tail, compact.EntryCount, compact.EncodedByteCount, true, null, compact.tail, [.. visits, visit], version);
    }

    public ReplayJournal MarkUnavailable(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1024)
        {
            throw new ArgumentException("A bounded replay-unavailable reason is required.", nameof(reason));
        }
        return IsComplete ? new ReplayJournal(SessionId, tail, EntryCount, EncodedByteCount, false, reason, completedVisits, visits, version) : this;
    }

    /// <summary>Explicitly selects only recorded committed transactions. This does not recover any failed or unrecorded suffix of the source save.</summary>
    public ReplayJournal GetCommittedPrefix()
    {
        Validate();
        return IsComplete ? this : new ReplayJournal(SessionId, tail, EntryCount, EncodedByteCount, true, null, completedVisits, visits, version);
    }

    public void Validate()
    {
        if (!Guid.TryParseExact(SessionId, "N", out _) || EntryCount is < 0 or > MaximumEntries || EncodedByteCount < 2 || EncodedByteCount > (Version == 1 ? MaximumInputDecodedBytes : MaximumDecodedBytes) || IsComplete && UnavailableReason is not null || !IsComplete && string.IsNullOrWhiteSpace(UnavailableReason))
        {
            throw new InvalidDataException("Invalid causal journal completeness or capacity.");
        }
    }

    private static ReplayJournalEntry Snapshot(ReplayJournalEntry entry)
    {
        return entry with
        {
            EffectArguments = entry.EffectArguments is null ? null : (int[])entry.EffectArguments.Clone(),
            WorldEffects = (ReplayWorldEffectCause[])entry.WorldEffects.Clone(),
            SemanticAdmissions = (ReplaySemanticTrigger[])entry.SemanticAdmissions.Clone(),
            WorldNavigation = entry.WorldNavigation.Length == 0 ? [] : (ReplayWorldNavigationStimulus[])entry.WorldNavigation.Clone(),
            Receipts = entry.Receipts.Length == 0 ? [] : entry.Receipts.Select(receipt => receipt with { Arguments = receipt.Arguments is null ? null : (int[])receipt.Arguments.Clone() }).ToArray(),
            Navigation = entry.Navigation.Length == 0 ? [] : entry.Navigation.Select(stimulus => stimulus with { PushContacts = stimulus.PushContacts is null ? null : (int[])stimulus.PushContacts.Clone() }).ToArray()
        };
    }

    private static bool CanRepeat(ReplayJournalEntry previous, ReplayJournalEntry next)
    {
        // Codes4/5 are ordinary Move/Wait inputs. Each repetition still executes one complete callback.
        if (previous.SemanticAdmissions.Length != 0 || next.SemanticAdmissions.Length != 0)
        {
            return false;
        }
        bool cadence = previous.Kind == ReplayEntryKind.Step || (previous.Kind is ReplayEntryKind.Action or ReplayEntryKind.WorldAction && previous.Code is 4 or 5 && previous.Frames == 1);
        if (!cadence || next.Kind != previous.Kind || next.Repeat != 1 || previous.Repeat >= 30000 || previous.Advanced != 1 || next.Advanced != 1 || previous.Frame > long.MaxValue - previous.Repeat || previous.Frame + previous.Repeat != next.Frame || previous.Navigation.Length != 0 || next.Navigation.Length != 0 || previous.WorldNavigation.Length != 0 || next.WorldNavigation.Length != 0 || previous.Receipts.Length != next.Receipts.Length || previous.ResultFrame.HasValue != next.ResultFrame.HasValue || next.ResultFrame.HasValue && (previous.ResultFrame != next.Frame || next.Frame == long.MaxValue || next.ResultFrame != next.Frame + 1))
        {
            return false;
        }
        ReplayJournalEntry first = previous with { Frame = 0, ResultFrame = null, Repeat = 1, Receipts = [], Navigation = [] };
        ReplayJournalEntry second = next with { Frame = 0, ResultFrame = null, Repeat = 1, Receipts = [], Navigation = [] };
        if (first != second)
        {
            return false;
        }
        for (int index = 0; index < previous.Receipts.Length; index++)
        {
            ReplayReceipt oldReceipt = previous.Receipts[index];
            ReplayReceipt newReceipt = next.Receipts[index];
            if (oldReceipt.Frame - previous.Frame != newReceipt.Frame - next.Frame || oldReceipt with { Frame = 0, Arguments = null } != newReceipt with { Frame = 0, Arguments = null } || (oldReceipt.Arguments is null) != (newReceipt.Arguments is null) || oldReceipt.Arguments is not null && !oldReceipt.Arguments.SequenceEqual(newReceipt.Arguments!))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Expands compact scheduler cadence into the original ordered semantic receipt timeline.</summary>
    public IEnumerable<ReplayReceipt> EnumerateReceipts()
    {
        foreach (ReplayJournalEntry entry in Entries)
        {
            for (int repeat = 0; repeat < entry.Repeat; repeat++)
            {
                foreach (ReplayReceipt receipt in entry.Receipts)
                {
                    yield return receipt with { Frame = checked(receipt.Frame + repeat) };
                }
            }
        }
    }

    private sealed record Node(ReplayJournalEntry Entry, Node? Previous);
}

public sealed class ReplayJournalJsonConverter : JsonConverter<ReplayJournal>
{
    public override ReplayJournal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;
            int version = root.GetProperty("Version").GetInt32();
            if (version is not (1 or ReplayJournal.CurrentVersion) || root.GetProperty("Root").GetString() != ReplayJournal.NewGameRoot || root.GetProperty("RandomSeed").GetInt32() != 0)
            {
                throw new InvalidDataException("Unsupported causal journal version or authentic NewGame root.");
            }
            int count = root.GetProperty("EntryCount").GetInt32();
            string data = root.GetProperty("Data").GetString() ?? throw new InvalidDataException("Missing causal journal data.");
            if (count is < 0 or > ReplayJournal.MaximumEntries || data.Length > ReplayJournal.MaximumInputDecodedBytes * 2)
            {
                throw new InvalidDataException("Causal journal exceeds its declared capacity.");
            }
            using MemoryStream compressed = new(Convert.FromBase64String(data), false);
            using BrotliStream decompressor = new(compressed, CompressionMode.Decompress);
            using MemoryStream decoded = new();
            byte[] buffer = new byte[8192];
            int bytes;
            while ((bytes = decompressor.Read(buffer)) != 0)
            {
                if (decoded.Length + bytes > (version == 1 ? ReplayJournal.MaximumInputDecodedBytes : ReplayJournal.MaximumDecodedBytes))
                {
                    throw new InvalidDataException("Causal journal decompression exceeds its capacity.");
                }
                decoded.Write(buffer, 0, bytes);
            }
            ReplayCompletedVisit[] visits = [];
            ReplayJournalEntry[] active;
            if (version == 1)
            {
                active = JsonSerializer.Deserialize<ReplayJournalEntry[]>(decoded.ToArray()) ?? throw new InvalidDataException("Missing causal transaction array.");
            }
            else
            {
                using JsonDocument payload = JsonDocument.Parse(decoded.ToArray());
                visits = payload.RootElement.GetProperty("CompletedVisits").Deserialize<ReplayCompletedVisit[]>() ?? throw new InvalidDataException("Missing semantic completed visits.");
                active = payload.RootElement.GetProperty("ActiveEntries").Deserialize<ReplayJournalEntry[]>() ?? throw new InvalidDataException("Missing active physical suffix.");
            }
            if (active.Length + visits.Sum(visit => visit.Intents.Length + (visit.EntryFieldLoad is null ? 0 : 1)) != count)
            {
                throw new InvalidDataException("Causal journal count differs from its payload.");
            }
            ReplayJournal journal = ReplayJournal.Restore(version, root.GetProperty("SessionId").GetString() ?? throw new InvalidDataException("Missing causal session identity."), visits, active, root.GetProperty("IsComplete").GetBoolean(), root.TryGetProperty("UnavailableReason", out JsonElement reason) ? reason.GetString() : null);
            journal.Validate();
            return journal;
        }
        catch (Exception exception) when (exception is KeyNotFoundException or FormatException or InvalidOperationException or OverflowException or EndOfStreamException or ArgumentException or JsonException)
        {
            throw new InvalidDataException("Malformed causal journal: " + exception.Message, exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, ReplayJournal value, JsonSerializerOptions options)
    {
        value.Validate();
        using MemoryStream compressed = new();
        using (BrotliStream compressor = new(compressed, CompressionLevel.Optimal, true))
        {
            if (value.Version == 1)
            {
                JsonSerializer.Serialize(compressor, value.Entries, ReplayJournalEntryStorage.Options);
            }
            else
            {
                JsonSerializer.Serialize(compressor, new { value.CompletedVisits, value.ActiveEntries }, ReplayJournalEntryStorage.Options);
            }
        }
        writer.WriteStartObject();
        writer.WriteNumber("Version", value.Version);
        writer.WriteString("Root", value.Root);
        writer.WriteNumber("RandomSeed", value.RandomSeed);
        writer.WriteString("SessionId", value.SessionId);
        writer.WriteNumber("EntryCount", value.EntryCount);
        writer.WriteBoolean("IsComplete", value.IsComplete);
        if (value.UnavailableReason is not null)
        {
            writer.WriteString("UnavailableReason", value.UnavailableReason);
        }
        writer.WriteBase64String("Data", compressed.ToArray());
        writer.WriteEndObject();
    }
}
