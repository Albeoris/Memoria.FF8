using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF7.Events;

internal static class NativeGaeaCaveOriginalTests
{
    internal static void Inspect()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        using FileStream input = File.OpenRead(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAEA_CAVE_SAVE")!);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        foreach (string name in new[] { "gaiin_1", "gaiin_2" })
        {
            Console.WriteLine($"Native cave profile {name}: {FieldProgram.FromPcScripts(Ff7FieldResources.Load(installation, name).Scripts).Identity}");
        }
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Console.WriteLine($"Native cave source field={session.MapId}, triangle={player.Triangle}, mask={Convert.ToBase64String(session.Host.Process.BlockedTriangles)}, connections690={string.Join(',', session.ReadAutoPlayFieldConnections(690))}, connections691={string.Join(',', session.ReadAutoPlayFieldConnections(691))}, flags83={session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x83)}");
        Ff7AutoPlayState state = new() { DestinationField = 692 };
        var advice = Ff7AutoPlayPolicy.FindFieldEntryDestinationAdvice(state, observation, 692, player, session.Host.Process.BlockedTriangles, session.ReadAutoPlayFieldTopology, target => new Ff7AutoplayDriver(session).HasNativeGeometricContact(target));
        Console.WriteLine($"Native entry-qualified advice={advice.Target?.Id}, limited={advice.Limited}, lines690={session.ReadAutoPlayFieldTopology(690).Lines.Count}, lines691={session.ReadAutoPlayFieldTopology(691).Lines.Count}");
        foreach (Ff7AutoPlayTarget target in observation.Events.Where(target => target.Kind == Ff7AutoPlayTargetKind.Gateway || target.ChangesField))
        {
            Console.WriteLine($"Native cave target {target.Id}, destination={target.DestinationField}, alternatives={string.Join(',', target.PossibleDestinationFields)}, complete={target.AnalysisComplete}, contact={new Ff7AutoplayDriver(session).HasNativeGeometricContact(target)}");
        }
    }

    internal static void Run()
    {
        NativeGaeaRockOriginalTests.Run();
    }
}
