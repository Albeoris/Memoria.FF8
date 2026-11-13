using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMinigameTests
{
    public static void Run()
    {
        TypedOpcode();
        InstalledStoryDeparture();
    }

    private static void TypedOpcode()
    {
        byte[] code = [0, 0x20, 0x65, 0, 0x2E, 0xFB, 0xDE, 0xFF, 0x32, 0, 0x81, 0xFE, 0x80, 0x50, 9, 87, 0x12, 0];
        FieldProgram program = new(code, [new ushort[32]]);
        RecordingHost host = new();
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        machine.AdvanceTick();
        FieldHostRequest request = machine.Capture().PendingRequests!.Single();
        Check(request.ProgramCounter == 1 && request.Command == new FieldMinigameCommand(new(101, -1234, -34, 50, 0x81), 0xFE), "MINIGAME changed the raw destination packet.");
        RecordingHost coldHost = new();
        FieldMachine cold = FieldMachine.Restore(program, RoundTrip(machine.Capture()), coldHost);
        cold.AdvanceTick();
        Check(coldHost.Request == request && cold.Capture().Entities[0].ProgramCounter == 1 && cold.Variables.ReadByte(FieldBank.MapByte, 9) == 0, "A waiting MINIGAME must retain its PC and request across cold restore.");
        coldHost.Complete = true;
        cold.AdvanceTick();
        Check(cold.Capture().PendingRequests!.Length == 0 && cold.Variables.ReadByte(FieldBank.MapByte, 9) == 87, "MINIGAME completion must advance 11 bytes and continue in the same pass.");
        Console.WriteLine("PASS MINIGAME raw operands, pending owner, cold continuation and completion length");
    }

    private static void InstalledStoryDeparture()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP original MINIGAME handoff. Configure MEMORIA_FF7_GAME_DIR.");
            return;
        }

        Ff7Installation installation = Ff7Installation.Discover(directory);
        PcHighwayCourse storyCourse = Ff7HighwayResources.Load(installation, arcade: false);
        PcHighwayCourse arcadeCourse = Ff7HighwayResources.Load(installation, arcade: true);
        Check(storyCourse.Track.Count == 67 && storyCourse.Track.Sum(command => command.Length) == 4950 && storyCourse.Events.Count == 17 && storyCourse.Events[^1] == new PcHighwayEvent(1795, 20), "The installed highway story course lost its native track commands or finish event.");
        Check(arcadeCourse.Track.Count == 65 && arcadeCourse.Events.Count == 16 && arcadeCourse.Events[^1] == new PcHighwayEvent(1767, 21), "The installed highway arcade course lost its distinct track or event table.");

        Check(storyCourse.Paths.Count == 10 && storyCourse.Paths[2].PointCount == 443 && storyCourse.Paths[2].Sample(0x8000, true) == new Vector3(767, 376, 8218) && storyCourse.Paths[8].Sample(0x8000, true) == new Vector3(8, 0, -1) && storyCourse.Paths[8].Sample(0x8000, false) == new Vector3(8, 0, 1), "The installed highway paths lost their signed fixed-point interpolation.");

        using Ff7Session initial = Ff7Session.NewGame(installation);
        FieldVariables variables = new(initial.Machine.Variables.CopyPersistent(), new byte[256]);
        variables.WriteWord(FieldBank.Persistent0Word, 0, 326);
        Ff7NativeSaveSlot slot = Ff7NativeSaveCodec.ExportField(initial.Progress, variables.CopyPersistent(), 101, 0, 0, 3, 0);
        Ff7SessionSave entry = Ff7Session.ImportNativeField(slot, installation);
        using Ff7Session session = Ff7Session.Restore(installation, entry);
        session.AdvanceTick();

        Ff7SessionSave pending = session.Capture();
        FieldHostRequest request = pending.Vm.PendingRequests!.Single(request => request.Command is FieldMinigameCommand);
        Check(request.Entity == 0 && request.ProgramCounter == 0x0143 && request.Command == new FieldMinigameCommand(new(101, 0, 0, 3, 0), 0), "The original director did not produce its native story highway request.");
        Check(session.Field.Name == "blackbg9" && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 332 && session.Host.PendingMinigame == request && !session.CanSave && !session.CanSaveMemoria && session.Battle is null, "The original field must yield to the highway owner after writing its story moment.");
        using Ff7Session cold = Ff7Session.Restore(installation, RoundTrip(pending));
        RequireHighwayBoundary(session, pending, request);
        RequireHighwayBoundary(cold, pending, request);
        Ff7HighwayStart prepared = cold.HighwayStart!;
        Check(prepared.Request == request && !prepared.Arcade && prepared.HighScore == 0 && prepared.InitialSpeed == 122 && prepared.InitialTrackSegment == 80 && prepared.FirstDueEvent == new PcHighwayEvent(1, 10), "Highway preparation lost its original owner, story mode, race registers or first event.");
        Check(prepared.Road[0] == new Ff7HighwayRoadSegment(256, 700, 0, 0, false) && prepared.Road[7] == new Ff7HighwayRoadSegment(2048, 700, 1, 7, false) && prepared.Road[8] == new Ff7HighwayRoadSegment(2304, 700, 3, 0, false) && prepared.Road[^1] == new Ff7HighwayRoadSegment(20480, 700, 1, 7, false), "The highway road ring changed from native initialization.");
        string surfaceHash = Convert.ToHexString(SHA256.HashData(prepared.Road.Select(segment => segment.Surface).ToArray()));
        Check(surfaceHash == "0135059F251685B15D5EB16D8B55DCC56C17CF2381CA1EFBF18BE9D786431AA2", "The original highway startup surface sequence changed.");
        Check(prepared.Gauges.Count == 5 && prepared.Gauges.All(gauge => gauge == new Ff7HighwayGauge(65535, 65535)), "The initial highway gauges lost their unsigned maximum.");
        RequireHighwayBoundary(cold, pending, request);
        Check(ReferenceEquals(prepared, cold.HighwayStart) && session.HighwayStart is { } original && original.Request == prepared.Request && original.Road.SequenceEqual(prepared.Road), "A repeated or cold highway boundary changed the prepared startup.");
        RejectCorruptMinigameOwner(installation, pending, request);

        FieldVariables arcadeVariables = new(entry.Vm.PersistentVariables, entry.Vm.MapVariables);
        arcadeVariables.WriteByte(FieldBank.Persistent1Byte, 0x73, 1);
        arcadeVariables.WriteWord(FieldBank.Persistent2Word, 0x6D, unchecked((short)0xFEDC));
        using Ff7Session arcade = Ff7Session.Restore(installation, entry with { Vm = entry.Vm with { PersistentVariables = arcadeVariables.CopyPersistent() } });
        arcade.AdvanceTick();
        Ff7SessionSave arcadePending = arcade.Capture();
        FieldHostRequest arcadeRequest = arcadePending.Vm.PendingRequests!.Single(value => value.Command is FieldMinigameCommand);
        RequireHighwayBoundary(arcade, arcadePending, arcadeRequest);
        Check(arcade.HighwayStart is { Arcade: true, HighScore: 0xFEDC, FirstDueEvent: { Segment: 1, Code: 12 } }, "The original highway mode and unsigned high score did not select the arcade course.");
        Console.WriteLine("PASS original blackbg9 request and installed story/arcade highway course data");
    }

    private static void RequireHighwayBoundary(Ff7Session session, Ff7SessionSave pending, FieldHostRequest request)
    {
        try
        {
            session.AdvanceTick();
            throw new InvalidOperationException("An unsupported highway module ran or returned to the field.");
        }
        catch (FieldExecutionException error) when (error.InnerException is NotSupportedException)
        {
            Check(error.InnerException.Message.Contains("prop and rider initialization", StringComparison.Ordinal) && error.Context.Opcode == 0x20 && error.Context.Entity == request.Entity && error.Context.ProgramCounter == request.ProgramCounter, "The highway boundary lost its native opcode owner.");
            Ff7SessionSave after = session.Capture();
            Check(after.Vm.Tick == pending.Vm.Tick && after.FieldGeneration == pending.FieldGeneration && after.Vm.PersistentVariables.SequenceEqual(pending.Vm.PersistentVariables) && session.Host.PendingMinigame == request, "The refused minigame consumed field progress or returned a synthetic result.");
        }
    }

    private static void RejectCorruptMinigameOwner(Ff7Installation installation, Ff7SessionSave pending, FieldHostRequest request)
    {
        try
        {
            (pending with { Vm = pending.Vm with { PendingRequests = [] } }).Validate();
            throw new InvalidOperationException("The snapshot accepted a minigame without its VM request.");
        }
        catch (InvalidDataException)
        {
        }

        FieldMinigameCommand command = (FieldMinigameCommand)request.Command;
        FieldHostRequest changed = request with { Command = command with { Game = 2 } };
        Ff7SessionSave mismatched = pending with
        {
            Vm = pending.Vm with { PendingRequests = [changed] },
            Process = pending.Process with { Receipts = pending.Process.Receipts.Select(receipt => receipt.Request == request ? receipt with { Request = changed } : receipt).ToArray() }
        };
        try
        {
            using Ff7Session _ = Ff7Session.Restore(installation, mismatched);
            throw new InvalidOperationException("The snapshot accepted a minigame type changed from its native instruction.");
        }
        catch (InvalidDataException)
        {
        }
    }

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class RecordingHost : IFieldHost
    {
        public FieldHostRequest? Request { get; private set; }
        public bool Complete { get; set; }

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Request = request;
            return new(Complete);
        }
    }
}
