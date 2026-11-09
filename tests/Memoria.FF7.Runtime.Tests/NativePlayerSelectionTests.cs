using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePlayerSelectionTests
{
    public static void Run()
    {
        Ff7FieldResources field = Field();
        var host = new Ff7FieldHost(field, (_, _) => 4) { ReadScriptParty = () => [0, 1, 2] };
        byte[] code = [0xA1, 0, 0, 0];
        var vm = new FieldMachine(new(code, [Enumerable.Repeat((ushort)0, 32).ToArray()]), host: host);
        vm.Initialize();
        Equal(true, host.GetActor(0).PlayerControlled);
        Equal(false, host.Process.PlayerLocked);
        host.InitializePlayerMovement(512);
        Equal(34, host.GetActor(0).CollisionRadius);
        Equal(1024, host.GetActor(0).MoveSpeed);
        host.Execute(new(10, 9, 7, 0, new FieldActorBind(1)));
        Equal(false, host.GetActor(9).PlayerControlled);
        host.Execute(new(11, 0, 7, 0, new FieldPartyCharacter(1, 1)));
        Equal(true, host.GetActor(0).PlayerControlled);
        Equal(false, host.GetActor(0).Visible);
        host.Execute(new(12, 9, 7, 0, new FieldPartyCharacter(0, 0)));
        Equal(false, host.GetActor(0).PlayerControlled);
        Equal(true, host.GetActor(9).PlayerControlled);
        Equal(1, host.Process.Actors.Count(actor => actor.PlayerControlled));

        host.Execute(new(13, 0, 7, 0, new FieldActorProperty(FieldActorPropertyKind.PlayerCharacter, 0)));
        host.Execute(new(14, 0, 7, 0, new FieldActorBind(1)));
        Equal(true, host.Process.Actors.Single(actor => actor.Slot == 0).PlayerControlled);
        Equal(false, host.Process.Actors.Single(actor => actor.Slot == 0).EntityBound);
        Equal(false, host.GetActor(0).PlayerControlled);
        Ff7Actor rebound = host.GetActor(0);
        host.PlacePlayerAtEntry(new(0, 12, 34, 0, 180));
        Ff7Actor physical = host.Process.Actors.Single(actor => actor.Slot == 0);
        Equal(12 * 4096, physical.X4096);
        Equal(34 * 4096, physical.Y4096);
        Equal(7 * 4096, physical.Z4096);
        Equal(180, physical.Direction);
        Equal(rebound, host.GetActor(0));
        host.Execute(new(40, 0, 7, 0, new FieldActorMove(30, 40)));
        Equal(false, host.HasScriptedMotion(physical));
        Equal(true, host.HasScriptedMotion(host.GetActor(0)));
        rebound = host.GetActor(0);
        Ff7Actor moved = physical with { X4096 = 13 * 4096, Direction = 90 };
        host.ApplyPlayerMovement(new(moved, false, true, []));
        Equal(moved, host.Process.Actors.Single(actor => actor.Slot == 0));
        Equal(rebound, host.GetActor(0));
        try
        {
            host.ApplyPlayerMovement(new(moved with { Entity = 99 }, false, true, []));
            throw new Exception("Manual movement must retain its physical actor identity.");
        }
        catch (InvalidDataException)
        {
            Equal(moved, host.Process.Actors.Single(actor => actor.Slot == 0));
        }
        var lockedState = host.Capture() with { PlayerLocked = true, Actors = host.Process.Actors.Select(actor => actor.Slot == 0 ? actor with { TurnMode = 2, TurnStep = 3, TurnSteps = 8 } : actor).ToArray() };
        var locked = new Ff7FieldHost(field, (_, _) => 4, lockedState);
        locked.PlacePlayerAtEntry(new(0, 70, 80, 0, 17));
        Ff7FieldProcess directed = lockedState with { Actors = lockedState.Actors.Select(actor => actor.Slot == 0 ? actor with { Direction = 17 } : actor).ToArray() };
        Equal(JsonSerializer.Serialize(directed), JsonSerializer.Serialize(locked.Capture()));
        host.Execute(new(15, 0, 7, 0, new FieldActorProperty(FieldActorPropertyKind.PlayerCharacter, 0)));
        Equal(true, host.GetActor(0).PlayerControlled);
        Equal(1, host.Process.Actors.Count(actor => actor.PlayerControlled));
        Ff7FieldProcess selected = JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!;
        try
        {
            Ff7FieldProcess ambiguous = selected with { Actors = selected.Actors.Select(actor => actor with { PlayerControlled = true }).ToArray() };
            _ = new Ff7FieldHost(field, (_, _) => 4, ambiguous);
            throw new Exception("Multiple recorded selected slots must fail clearly.");
        }
        catch (InvalidDataException error)
        {
            Equal(true, error.Message.Contains("unique physical slot", StringComparison.Ordinal));
        }
        var cold = new Ff7FieldHost(field, (_, _) => 4, selected);
        cold.RestorePlayerSelection();
        Equal(JsonSerializer.Serialize(selected), JsonSerializer.Serialize(cold.Capture()));
        Ff7FieldProcess missing = selected with { Actors = selected.Actors.Select(actor => actor with { PlayerControlled = false }).ToArray() };
        cold = new Ff7FieldHost(field, (_, _) => 4, missing);
        cold.RestorePlayerSelection();
        Ff7FieldProcess expected = missing with { Actors = missing.Actors.Select(actor => actor with { PlayerControlled = actor.Slot == 0 }).ToArray() };
        Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(cold.Capture()));
        InstalledContinuation();
    }

    private static Ff7FieldResources Field()
    {
        byte[] script = new byte[34];
        BinaryPrimitives.WriteUInt16LittleEndian(script, 0x0502);
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(4), 32);
        BinaryPrimitives.WriteInt16LittleEndian(script.AsSpan(8), 512);
        PcFieldModels models = new(0, 512, Enumerable.Range(0, 2).Select(index => new PcFieldModelDescriptor($"actor{index}", 0, "actor", "512", [], 0, ["idle"])).ToArray());
        PcWalkmeshTriangle triangle = new(0, new Vector3(-100, -100, 7), new(1000, -100, 7), new(-100, 1000, 7), -1, -1, -1);
        return new("selection-fixture", null!, PcFieldScripts.Read(script), models, [], [triangle], null!, null!, null!);
    }

    private static void InstalledContinuation()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original-resource actor selection: optional fixture inputs are unset.");
            return;
        }
        CombinedSessionSave envelope;
        using (FileStream input = File.OpenRead(path))
        {
            envelope = CombinedSessionCodec.Read(input);
        }
        Ff7SessionSave saved = envelope.Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(directory), saved);
        Ff7Actor[] expected = saved.Process.Actors.Any(actor => actor.PlayerControlled) ? saved.Process.Actors : saved.Process.Actors.Select(actor => actor with { PlayerControlled = actor.Slot == 0 }).ToArray();
        Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(session.Host.Process.Actors));
        Equal(JsonSerializer.Serialize(saved.Vm), JsonSerializer.Serialize(session.Capture().Vm));
        Equal(JsonSerializer.Serialize(saved.Progress), JsonSerializer.Serialize(session.Progress));
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
