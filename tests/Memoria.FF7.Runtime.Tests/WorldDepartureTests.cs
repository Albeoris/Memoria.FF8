using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldDepartureTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed world departure: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.NewGame(installation);
        session.Machine.Variables.WriteWord(FieldBank.Persistent0Word, 0, 152);
        session.Machine.Variables.WriteByte(FieldBank.Persistent4Byte, 250, 83);
        Check(session.Host.RequestGateway(new(1, 0, 0, 0, 0)), "Field must accept the bounded world entry request.");
        session.AdvanceTick();
        Ff7WorldRuntime world = session.World ?? throw new InvalidOperationException("Field did not enter World.");
        for (int tick = 0; tick < 700; tick++)
        {
            if (world.Process.Dialogue is { Completed: false, AwaitingInput: true } dialogue)
            {
                world.CompleteDialogue(dialogue.RequestId, dialogue.Selected);
            }
            session.AdvanceTick();
            if (world.Process.MainState == 1 && world.Process.FadeRate == 0 && world.Process.FadeDisplayedAlpha == 0 && world.MovementEnabled)
            {
                break;
            }
        }
        Ff7WorldProcess ready = world.Process;
        Check(ready is { MainState: 1, FadeRate: 0, FadeDisplayedAlpha: 0, MovementEnabled: true }, "The original world must finish its field-entry tutorial and fade.");
        PcWorldEvents events = world.Resources.ReadEvents();
        PcWorldFunction gateway = events.Functions.Single(function => function.Header == 0xA0E4);
        PcWorldFieldEntry entry = world.Resources.ReadFieldTable().Select(0x100);
        Check(entry.Field > 64, "The first original field-table row must describe a field destination.");
        world.Machine.StartMesh(gateway.Mesh % 36, gateway.Mesh / 36, gateway.Event);
        session.AdvanceTick();
        Check(world.Process.FieldDeparture is { Selector: 0x100, Operation: WorldOperation.FieldTransition } && world.Process is { MainState: 2, FadeRate: 16, MovementEnabled: false }, "World opcode 318 must own the table selector and start the original fade.");
        Ff7SessionSave departing = Cold(session.Capture());
        using Ff7Session restored = Ff7Session.Restore(installation, departing);
        using Ff7Session retry = Ff7Session.Restore(installation, departing);
        int oldField = session.MapId;
        long generation = session.FieldGeneration;
        int failures = 0;
        List<FieldAudioCommand> audio = [];
        retry.Host.AudioRequested = audio.Add;
        retry.ValidateWorldFieldDestination = _ =>
        {
            failures++;
            throw new InvalidDataException("Injected destination preparation failure.");
        };
        for (int tick = 0; tick < 64 && session.World is not null; tick++)
        {
            session.AdvanceTick();
            restored.AdvanceTick();
            try
            {
                retry.AdvanceTick();
            }
            catch (InvalidDataException error) when (error.Message == "Injected destination preparation failure.")
            {
                Check(retry.MapId == oldField && retry.FieldGeneration == generation && retry.World is { ReadyFieldDeparture: not null } && !audio.Any(command => command.Operation == 0xF1), "A failed destination must leave the source owner usable and avoid publishing deferred audio.");
                string retained = JsonSerializer.Serialize(retry.Capture());
                try
                {
                    retry.AdvanceTick();
                }
                catch (InvalidDataException repeat) when (repeat.Message == error.Message)
                {
                    Check(JsonSerializer.Serialize(retry.Capture()) == retained, "Retry failure must leave the source tick, request and progress unchanged.");
                }
                retry.ValidateWorldFieldDestination = null;
                retry.AdvanceTick();
            }
        }
        Check(failures == 2 && session.World is null && restored.World is null && retry.World is null && session.FieldGeneration == generation + 1, "Departure must adopt the field exactly once after retry.");
        CheckThrows<ObjectDisposedException>(() => world.Resources.ReadEvents(), "The adopted field must retire the source world archive.");
        Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(restored.Capture()) && JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(retry.Capture()), "Cold and retried world handoffs must publish the same field continuation.");
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Check(session.MapId == entry.Field && session.Progress.Party.SequenceEqual(departing.Progress.Party) && session.Machine.Variables.ReadByte(FieldBank.Persistent4Byte, 250) == 83 && player.Direction == entry.Direction, "The native table arrival must preserve party, persistent variables and direction.");
        if (!session.Host.Process.PlayerLocked)
        {
            Check(player.X4096 == entry.X * 4096 && player.Y4096 == entry.Y * 4096 && player.Triangle == entry.Triangle, "The unlocked arrival must place the actor at the native X/Y and triangle.");
        }
        Check(audio.Count(command => command.Operation == 0xF1) == 1, "The departure must retire world sound exactly once.");

        using Ff7Session direct = Ff7Session.Restore(installation, departing);
        Ff7WorldRuntime directWorld = direct.World!;
        int directPc = Enumerable.Range(1, events.Code.Count - 1).First(index => events.Code[index] == (ushort)WorldOperation.FieldTransitionSelector);
        directWorld.Execute(new(ready.Vm.NextRequestId - 1, -1, -1, directPc, new(WorldOperation.FieldTransitionSelector, [0x2D])));
        Check(directWorld.Process is { MainState: 8, FieldDeparture: { Selector: 0x2D, Operation: WorldOperation.FieldTransitionSelector } }, "World opcode 33D must retain the unmodified operand and select the immediate path.");
        CheckThrows<InvalidDataException>(() => direct.AdvanceTick(), "The shipped 33D low-only selector resolves outside the original table.");
        Check(direct.World == directWorld && direct.MapId == oldField, "Invalid 33D destinations must preserve the original world owner.");
    }

    private static Ff7SessionSave Cold(Ff7SessionSave save) => JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(save))!;

    private static void CheckThrows<T>(Action action, string message) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
