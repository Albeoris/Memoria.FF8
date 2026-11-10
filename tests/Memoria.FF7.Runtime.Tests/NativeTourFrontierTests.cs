using System.Security.Cryptography;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeTourFrontierTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_TOUR_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native optional-area frontier: configure original resources and a market arrival fixture.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        Check(session.Field.Name == "mrkt2" && session.Host.Process.Tick == 0, "The frontier fixture must be before native market initialization.");
        string before = JsonSerializer.Serialize(session.Capture() with { EventCoverage = null });
        Ff7OpeningAutoPlayPolicy policy = new(session, Ff7AutoPlayGoal.MidgarWorldMap);
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoplayInput input = policy.ChooseInput(driver);
        Check(input == default, "The first native callback must not physically steer uninitialized market actors.");
        Check(before == JsonSerializer.Serialize(session.Capture() with { EventCoverage = null }), "Cataloguing optional rooms must not change native progress, inputs, inventory or script PCs.");
        AutoPlayTourState tour = session.EventCoverage!.Tour!;
        Check(tour.Frontiers.Length == 9 && tour.Frontiers.Count(edge => edge.Kind == AutoPlayTourKind.OptionalGameplay && edge.Status == AutoPlayTourStatus.Pending) == 6, "All six optional rooms must remain explicitly unvisited in the native nine-door directory.");
        Check(!new AutoPlayTourLedger(tour).Complete, "An incomplete native room catalog must block regional completion.");
        driver.Apply(input);
        session.AdvanceTick();
        Check(session.Field.Name == "mrkt2" && session.Host.Process.PlayerLocked, "The first callback must execute the native automatic arrival instead of prematurely rejecting its uninitialized catalog.");
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
        Ff7OpeningAutoPlayPolicy coldPolicy = new(cold, Ff7AutoPlayGoal.MidgarWorldMap);
        new Ff7AutoplayDriver(cold).Apply(coldPolicy.ChooseInput(new(cold)));
        cold.AdvanceTick();
        driver.Apply(policy.ChooseInput(driver));
        session.AdvanceTick();
        Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold native arrival must retain the optional frontier and initialized owner without replay.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The immutable arrival fixture was modified.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
