using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldMapTransferTests
{
    public static void Run(Ff7Installation installation, byte[] persistent, Ff7WorldProcess underwater)
    {
        byte[] party = [0, 1, 2];
        using Ff7WorldRuntime source = new(installation, persistent, underwater.EntrySelector, party, 2);
        source.Restore(underwater with { Entities = underwater.Entities.Select(entity => entity.Model == 13 ? entity with { Terrain = 3 } : entity).ToArray() });
        source.SetButtons(0x40, 0x40);
        source.Advance();
        Check(source.Process is { MainState: 5, MapTransfer.DestinationWorld: 0, MovementEnabled: false }, "Surfacing must begin the original fade and positive vertical acceleration.");
        using Ff7WorldRuntime cold = new(installation, source.Machine.CopyPersistent(), underwater.EntrySelector, party, 2);
        cold.Restore(Cold(source.Process));
        int callbacks = 0;
        while (source.ReadyMapTransfer is null && callbacks++ < 64)
        {
            source.Advance();
            cold.Advance();
            Check(JsonSerializer.Serialize(source.Process) == JsonSerializer.Serialize(cold.Process), "A cold submarine fade must preserve every subsequent map-transfer callback.");
        }
        Check(source.ReadyMapTransfer == 0 && source.Process.MainState == 7 && source.Process.Vm.Unloading, "Completed surfacing must hand off exactly once to the surface world dispatcher.");
        using (Ff7Session field = Ff7Session.NewGame(installation))
        {
            Ff7SessionSave template = field.Capture();
            Ff7SessionSave departure = template with { World = Cold(source.Process), Vm = template.Vm with { PersistentVariables = source.Machine.CopyPersistent() } };
            using Ff7Session session = Ff7Session.Restore(installation, departure);
            session.AdvanceTick(advanceClock: false);
            Check(session.World?.Process is { WorldId: 0, EntrySelector: 0, SubmarineMapEntry: true, Initialized: false }, "The session must atomically replace the source map at the completed native handoff.");
            Ff7SessionSave entrySave = session.Capture();
            using Ff7Session restored = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(entrySave))!);
            session.AdvanceTick(advanceClock: false);
            restored.AdvanceTick(advanceClock: false);
            Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(restored.Capture()), "Session cold restore must preserve the first destination initialization callback.");
        }
        byte[] saved = source.CaptureNativeWorldExit();
        using Ff7WorldRuntime surface = new(installation, saved, 0, party, 0);
        surface.PrepareSubmarineMapEntry();
        Ff7WorldProcess entry = Cold(surface.Process);
        using Ff7WorldRuntime resumedEntry = new(installation, saved.ToArray(), 0, party, 0);
        resumedEntry.Restore(entry);
        for (int callback = 0; callback < 64; callback++)
        {
            surface.Advance();
            resumedEntry.Advance();
            Check(JsonSerializer.Serialize(surface.Process) == JsonSerializer.Serialize(resumedEntry.Process), "The zero-selector surface initializer must survive cold restoration without losing its submarine entry owner.");
        }
        Check(surface.Process is { WorldId: 0, ControlledModel: 13, MainState: 1, MovementEnabled: true }, "The original surface EV must restore control of the serialized submarine.");
        surface.Restore(surface.Process with { Entities = surface.Process.Entities.Select(entity => entity.Model == 13 ? entity with { Terrain = 3 } : entity).ToArray() });
        surface.SetButtons(0x40, 0x40);
        surface.Advance();
        Check(surface.Process is { MainState: 4, MapTransfer.DestinationWorld: 2 }, "Cancel on surface sea terrain must start the native downward transfer.");
        callbacks = 0;
        while (surface.ReadyMapTransfer is null && callbacks++ < 64)
        {
            surface.Advance();
        }
        Check(surface.ReadyMapTransfer == 2 && surface.Process.MainState == 6, "Completed diving must select the underwater map dispatcher.");
        using Ff7WorldRuntime submerged = new(installation, surface.CaptureNativeWorldExit(), 0, party, 2);
        submerged.PrepareSubmarineMapEntry();
        for (int callback = 0; callback < 64; callback++)
        {
            submerged.Advance();
        }
        Check(submerged.Process is { WorldId: 2, ControlledModel: 13, MainState: 1, ViewMode: 2 } && submerged.Process.Entities.Single(entity => entity.Model == 13).Y == -3000, "The underwater initializer must restore the submarine at its original entry depth and camera mode.");
        Console.WriteLine("PASS original submarine surfacing fade, cold transfer and zero-selector surface initialization");
    }

    private static Ff7WorldProcess Cold(Ff7WorldProcess process)
    {
        return JsonSerializer.Deserialize<Ff7WorldProcess>(JsonSerializer.Serialize(process))!;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
