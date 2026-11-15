using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeNamingLifecycleTests
{
    public static void Run()
    {
        CheckHostLifecycle();
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP native naming lifecycle: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        Ff7CharacterNamingResources resources = Ff7CharacterNamingResources.Load(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7SessionSave source = PrepareSource(original.Capture());
        foreach (byte identity in new byte[] { 6, 7 })
        {
            CheckSelectedNaming(installation, source, resources, kernel, identity, mapped: false);
            CheckSelectedNaming(installation, source, resources, kernel, identity, mapped: true);
        }
        CheckSelectedNaming(installation, source with { Progress = source.Progress with { BattleRng = null } }, resources, kernel, 6, mapped: true);
        CheckSkippedNaming(installation, source);
        CheckLegacyMenus(installation, source, resources);
        CheckDirtyStatistics(installation, source, resources);
        CheckConfiguredContinuation(installation);
    }

    private static void CheckHostLifecycle()
    {
        Ff7FieldResources field = NativeDialogueTests.Field();
        Ff7FieldHost host = new(field, (_, _) => 1);
        int starts = 0;
        int completions = 0;
        string? acceptedName = null;
        host.NamingStarted = menu =>
        {
            Check(host.Process.Menu is null && host.Process.GlobalCommand == Ff7GlobalCommand.None, "Naming start ran after publishing an editable native menu.");
            Check(menu.Parameter == 6, "Naming start lost its original character selector.");
            starts++;
        };
        host.NamingCompleted = (_, name) =>
        {
            acceptedName = name;
            completions++;
        };
        FieldHostRequest request = Request(6);
        Check(!host.Execute(request).Completed && host.Process.Menu is { NamingPhase: Ff7NamingPhase.Editing, Completed: false } && starts == 1, "A new native naming request omitted its before-editor initialization phase.");
        Check(!host.Execute(request).Completed && starts == 1, "Polling a pending naming request repeated its initialization.");
        string before = JsonSerializer.Serialize(host.Capture());
        Reject<InvalidDataException>(() => host.Execute(request with { Command = new FieldMenuCommand(6, 7) }));
        Reject<InvalidOperationException>(() => host.CompleteMenu(request.Id + 1));
        Check(before == JsonSerializer.Serialize(host.Capture()) && starts == 1 && completions == 0, "Invalid naming ownership changed its process or invoked lifecycle callbacks.");
        Ff7FieldHost cold = new(field, (_, _) => 1, Cold(host.Capture()));
        cold.NamingStarted = _ => throw new InvalidOperationException("Cold editing replayed naming start.");
        cold.NamingCompleted = (_, name) =>
        {
            acceptedName = name;
            completions++;
        };
        cold.CompleteMenu(request.Id, "Native");
        Check(completions == 1 && acceptedName == "Native" && cold.Process.Menu is { NamingPhase: Ff7NamingPhase.Completed, Completed: true }, "Common naming completion lost its native-name input or finalized phase.");
        cold.CompleteMenu(request.Id);
        Check(completions == 1 && cold.Execute(request).Completed && cold.Execute(request).Completed, "Completed naming polls repeated the native finalizer.");
        Ff7FieldHost completed = new(field, (_, _) => 1, Cold(cold.Capture()));
        Check(completed.Process.Menu?.NamingPhase == Ff7NamingPhase.Completed && completed.Execute(request).Completed, "Cold completed naming lost its request receipt or replayed the lifecycle.");
        Reject<InvalidDataException>(() => new Ff7FieldHost(field, (_, _) => 1, Cold(host.Capture() with { Menu = host.Process.Menu! with { NamingPhase = Ff7NamingPhase.Completed } })));
        Reject<InvalidDataException>(() => new Ff7FieldHost(field, (_, _) => 1, Cold(host.Capture() with { Menu = host.Process.Menu! with { Mode = 7 } })));
    }

    private static void CheckSelectedNaming(Ff7Installation installation, Ff7SessionSave source, Ff7CharacterNamingResources resources, IReadOnlyList<KernelSection> kernel, byte identity, bool mapped)
    {
        using Ff7Session live = Ff7Session.Restore(installation, source);
        FieldHostRequest request = Request(identity);
        string nativeBefore = JsonSerializer.Serialize(live.Machine.Capture());
        string rngBefore = JsonSerializer.Serialize(live.Progress.BattleRng);
        byte[] template = resources.CreateTemplate(identity);
        Check(!live.Host.Execute(request).Completed, "An original naming menu completed without editor acceptance.");
        Ff7CharacterState initialized = live.Progress.Characters.Single(character => character.Id == resources.RecordId(identity));
        Check(initialized.NativeRecord.SequenceEqual(template) && initialized.NativeRecord[0] == identity && live.Host.Process.Menu?.NamingPhase == Ff7NamingPhase.Editing, "Naming start did not replace the entire selected reused record with its original native template.");
        Check(nativeBefore == JsonSerializer.Serialize(live.Machine.Capture()) && rngBefore == JsonSerializer.Serialize(live.Progress.BattleRng), "Naming start replayed native scripts or consumed completion growth RNG.");
        string editing = JsonSerializer.Serialize(live.Capture());
        Check(!live.Host.Execute(request).Completed && editing == JsonSerializer.Serialize(live.Capture()), "Pending naming polling repeated a reset or changed its editing continuation.");
        Ff7SessionSave editingSave = Cold(live.Capture());
        using Ff7Session cold = Ff7Session.Restore(installation, editingSave);
        Check(editingSave.Progress.Characters.Single(character => character.Id == resources.RecordId(identity)).NativeRecord.SequenceEqual(cold.Progress.Characters.Single(character => character.Id == resources.RecordId(identity)).NativeRecord), "Cold editing repeated template initialization.");
        CheckUnrelatedProgress(editingSave.Progress, cold.Progress, resources.RecordId(identity));

        Ff7BattleRng expectedRandom = new(kernel[2].Data.Span, editingSave.Progress.BattleRng ?? new(new byte[8]));
        byte[] expected = new Ff7NativeGrowth(KernelGrowthTables.Read(kernel[2].Data.Span)).InitializeJoin(template, editingSave.Progress.Characters, editingSave.Progress.AvailableCharacters, expectedRandom);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(44), U16(expected, 46));
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(48), U16(expected, 50));
        string name = identity == 6 ? "JoinTest" : "Vince";
        if (!mapped)
        {
            WriteName(expected, name);
        }
        string beforeFailure = JsonSerializer.Serialize(cold.Capture());
        Reject<ArgumentException>(() => cold.CompleteNamingMenu(request.Id, ""));
        Reject<InvalidOperationException>(() => cold.CompleteNamingMenu(request.Id + 1, name));
        Check(beforeFailure == JsonSerializer.Serialize(cold.Capture()), "Invalid naming acceptance changed progress, RNG, process or request ownership.");
        if (mapped)
        {
            live.Host.CompleteMenu(request.Id);
            cold.Host.CompleteMenu(request.Id);
        }
        else
        {
            live.CompleteNamingMenu(request.Id, name);
            cold.CompleteNamingMenu(request.Id, name);
        }
        Equal(live, cold, "Cold naming completion diverged from the live native lifecycle.");
        Ff7CharacterState completed = live.Progress.Characters.Single(character => character.Id == resources.RecordId(identity));
        Check(completed.NativeRecord.SequenceEqual(expected) && completed.Level == expected[1] && completed.Experience == U32(expected, 60) && completed.HitPoints == U16(expected, 46) && completed.MagicPoints == U16(expected, 50), "Naming completion did not join-grow and refill native base vitals exactly once.");
        Check(completed.Name == (mapped ? initialized.Name : name), "Presentation-owned naming overwrote story identity or native naming lost its text.");
        Check(JsonSerializer.Serialize(live.Progress.BattleRng) == JsonSerializer.Serialize(expectedRandom.Capture()) && live.Host.Process.Menu?.NamingPhase == Ff7NamingPhase.Completed, "Naming completion lost the caller-owned growth RNG or finalized phase.");
        Check(U16(completed.NativeRecord, 56) == U16(template, 56) && U16(completed.NativeRecord, 58) == U16(template, 58), "Naming completion eagerly published party-dependent maximum vitals.");
        CheckUnrelatedProgress(editingSave.Progress, live.Progress, resources.RecordId(identity));
        Check(nativeBefore == JsonSerializer.Serialize(live.Machine.Capture()), "Naming lifecycle replayed completed field script PCs.");
        string finalized = JsonSerializer.Serialize(live.Capture());
        live.Host.CompleteMenu(request.Id);
        Check(finalized == JsonSerializer.Serialize(live.Capture()), "A repeated completed naming call reran growth or changed its native result.");
        using Ff7Session finishedCold = Ff7Session.Restore(installation, Cold(live.Capture()));
        Equal(live, finishedCold, "Cold completed naming replayed native initialization or finalization.");
        Check(live.Host.Execute(request).Completed && finishedCold.Host.Execute(request).Completed, "The completed naming request did not retire through its original owner.");
        Equal(live, finishedCold, "Completed request retirement changed cold naming facts.");
    }

    private static void CheckLegacyMenus(Ff7Installation installation, Ff7SessionSave source, Ff7CharacterNamingResources resources)
    {
        FieldHostRequest request = Request(6);
        using Ff7Session initialized = Ff7Session.Restore(installation, source);
        initialized.Host.Execute(request);
        Ff7SessionSave editing = initialized.Capture();
        Ff7SessionSave legacy = editing with { Process = editing.Process with { Menu = editing.Process.Menu! with { NamingPhase = null } } };
        using Ff7Session restored = Ff7Session.Restore(installation, Cold(legacy));
        Check(restored.Host.Process.Menu?.NamingPhase == Ff7NamingPhase.Editing && JsonSerializer.Serialize(legacy.Progress) == JsonSerializer.Serialize(restored.Progress) && JsonSerializer.Serialize(legacy.Vm) == JsonSerializer.Serialize(restored.Machine.Capture()), "A legacy active naming menu replayed reset/scripts or changed recorded progress while deriving its phase.");
        restored.Host.CompleteMenu(request.Id);
        Check(restored.Progress.Characters.Single(character => character.Id == resources.RecordId(6)).NativeRecord[0] == 6 && restored.Host.Process.Menu?.NamingPhase == Ff7NamingPhase.Completed, "A legacy initialized menu could not finish its pending native lifecycle.");

        Ff7SessionSave incompatible = source with { Process = editing.Process with { Menu = editing.Process.Menu! with { NamingPhase = null } } };
        using Ff7Session oldProfile = Ff7Session.Restore(installation, Cold(incompatible));
        string before = JsonSerializer.Serialize(oldProfile.Capture());
        Reject<NotSupportedException>(() => oldProfile.Host.CompleteMenu(request.Id));
        Check(before == JsonSerializer.Serialize(oldProfile.Capture()), "An old uninitialized active naming profile was silently reset during completion or restore.");
        Ff7SessionSave completed = incompatible with { Process = incompatible.Process with { Menu = incompatible.Process.Menu! with { Completed = true, NamingPhase = null } } };
        using Ff7Session oldCompleted = Ff7Session.Restore(installation, Cold(completed));
        Check(oldCompleted.Host.Process.Menu?.NamingPhase == Ff7NamingPhase.Completed && JsonSerializer.Serialize(completed.Progress) == JsonSerializer.Serialize(oldCompleted.Progress), "A legacy completed naming menu replayed its missing producer or reset progress.");
        oldCompleted.Host.CompleteMenu(request.Id);
        Check(JsonSerializer.Serialize(completed.Progress) == JsonSerializer.Serialize(oldCompleted.Progress), "A completed legacy menu reran native finalization for its old profile.");
    }

    private static void CheckSkippedNaming(Ff7Installation installation, Ff7SessionSave source)
    {
        foreach (int identity in new[] { 0, 9 })
        {
            using Ff7Session session = Ff7Session.Restore(installation, source);
            string progress = JsonSerializer.Serialize(session.Progress);
            string vm = JsonSerializer.Serialize(session.Machine.Capture());
            FieldHostRequest request = Request(identity);
            session.Host.Execute(request);
            session.Host.CompleteMenu(request.Id);
            Check(session.Host.Process.Menu?.NamingPhase == Ff7NamingPhase.Completed && progress == JsonSerializer.Serialize(session.Progress) && vm == JsonSerializer.Serialize(session.Machine.Capture()), "A native skip-join identity was reinitialized, grew or consumed RNG during naming.");
        }
    }

    private static void CheckDirtyStatistics(Ff7Installation installation, Ff7SessionSave source, Ff7CharacterNamingResources resources)
    {
        using Ff7Session live = Ff7Session.Restore(installation, source);
        FieldHostRequest naming = Request(6);
        live.Host.Execute(naming);
        live.Host.CompleteMenu(naming.Id);
        live.Host.Execute(naming);
        byte[] before = live.Progress.Characters.Single(character => character.Id == resources.RecordId(6)).NativeRecord.ToArray();
        FieldHostRequest c8 = new(naming.Id + 1, 0, 1, 0, new FieldPartyProjection(0, 6, 255, 0x40));
        Check(live.Host.Execute(c8).Completed && live.Progress.NativePartyStatisticsDirty, "Native party publication did not retain its deferred statistics dirty owner.");
        Check(live.Progress.Characters.Single(character => character.Id == resources.RecordId(6)).NativeRecord.SequenceEqual(before), "C8 eagerly recomputed naming maxima instead of marking native statistics dirty.");
        Ff7SessionSave dirty = Cold(live.Capture());
        using Ff7Session cold = Ff7Session.Restore(installation, dirty);
        Check(cold.Progress.NativePartyStatisticsDirty && cold.Progress.Characters.Single(character => character.Id == resources.RecordId(6)).NativeRecord.SequenceEqual(before), "Cold restoration flushed or lost the pending native party-statistics publication.");
        live.AdvanceTick(advanceClock: false);
        cold.AdvanceTick(advanceClock: false);
        Equal(live, cold, "Deferred party statistics publication changed after cold restoration.");
        Check(!live.Progress.NativePartyStatisticsDirty, "The next ordinary field callback did not flush its deferred native statistics owner.");
        Ff7CharacterState published = live.Progress.Characters.Single(character => character.Id == resources.RecordId(6));
        Check(U16(published.NativeRecord, 56) > 0 && U16(published.NativeRecord, 58) > 0 && published.NativeRecord[0] == 6, "Deferred party-statistics publication lost the initialized native persona or valid maximum vitals.");
    }

    private static void CheckConfiguredContinuation(Ff7Installation installation)
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NAMING_SAVE");
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP existing naming continuation: MEMORIA_FF7_NAMING_SAVE is unset.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The naming fixture requires an FFVII continuation.");
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        Check(JsonSerializer.Serialize(saved.Progress) == JsonSerializer.Serialize(session.Progress) && JsonSerializer.Serialize(saved.Vm) == JsonSerializer.Serialize(session.Machine.Capture()), "Loading the original naming continuation replayed lifecycle progress or native scripts.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Naming compatibility inspection changed its standalone source.");
    }

    private static Ff7SessionSave PrepareSource(Ff7SessionSave source)
    {
        Ff7CharacterState[] characters = source.Progress.Characters.Select(character =>
        {
            if (character.Id is not (0 or 1))
            {
                return character;
            }
            byte[] record = character.NativeRecord.ToArray();
            record[1] = character.Id == 0 ? (byte)16 : (byte)18;
            return character with { Level = record[1], NativeRecord = record };
        }).ToArray();
        return source with { Progress = source.Progress with { Characters = characters, AvailableCharacters = 3, BattleRng = new(new byte[8], Stream: 3, WordCounter: 7, CrtSeed: 1234), NativePartyStatisticsDirty = false } };
    }

    private static void CheckUnrelatedProgress(Ff7Progress before, Ff7Progress after, int selected)
    {
        Check(JsonSerializer.Serialize(before.Characters.Where(character => character.Id != selected)) == JsonSerializer.Serialize(after.Characters.Where(character => character.Id != selected)) && before.Inventory.SequenceEqual(after.Inventory) && before.Materia.SequenceEqual(after.Materia) && before.Gil == after.Gil && before.Party.SequenceEqual(after.Party) && before.AvailableCharacters == after.AvailableCharacters, "Naming changed another character, economy, party ownership or availability.");
    }

    private static FieldHostRequest Request(int identity)
    {
        return new(9000000 + identity, 0, 1, 0, new FieldMenuCommand(6, identity));
    }

    private static void WriteName(byte[] record, string name)
    {
        record.AsSpan(16, 12).Fill(byte.MaxValue);
        for (int index = 0; index < name.Length; index++)
        {
            record[16 + index] = (byte)(name[index] - 32);
        }
    }

    private static ushort U16(byte[] record, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(offset));
    }

    private static uint U32(byte[] record, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(offset));
    }

    private static T Cold<T>(T value)
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    }

    private static void Equal(Ff7Session live, Ff7Session cold, string reason)
    {
        Check(JsonSerializer.Serialize(live.Capture()) == JsonSerializer.Serialize(cold.Capture()), reason);
    }

    private static void Reject<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException("Invalid native naming input was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
