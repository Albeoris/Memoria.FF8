using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeGatewayComponentObservationTests
{
    internal static void Run()
    {
        PcWalkmeshTriangle[] mesh = Enumerable.Range(0, 3).Select(index => new PcWalkmeshTriangle(index, new(index * 300, 0, 0), new(index * 300 + 100, 0, 0), new(index * 300, 100, 0), -1, -1, -1)).ToArray();
        byte[] blocked = [0];
        Ff7Actor player = new() { Slot = 0, Entity = 1, Model = 0, Triangle = 0, X4096 = 20 << 12, Y4096 = 20 << 12, CollisionRadius = 10 };
        PcFieldGateway gateway = new(new(620, 20, 0), new(630, 20, 0), 0, 0, 0, 600, 0, 0);
        Ff7Line[] lines = [new(0, 4, 20, 20, 0, 30, 20, 0), new(1, 7, 320, 20, 0, 330, 20, 0)];
        FieldActorLadderLanding firstLanding = new(1, 10, new(320, 20, 0, 1, 1, 1, 0, 1), true);
        FieldActorLadderLanding secondLanding = new(1, 20, new(620, 20, 0, 2, 1, 1, 0, 1), true);
        Ff7AutoPlayTarget first = new("first", 4, 2, Ff7AutoPlayTargetKind.LinePush, "first-condition", false, default, MayChangeNavigation: true) { LadderLandings = [firstLanding] };
        Ff7AutoPlayTarget second = new("second", 7, 2, Ff7AutoPlayTargetKind.LinePush, "second-condition", false, default, MayChangeNavigation: true) { LadderLandings = [secondLanding] };
        string before = JsonSerializer.Serialize(new { mesh, blocked, player, gateway, lines, first, second });
        bool Offer(IReadOnlyList<Ff7AutoPlayTarget> targets, IReadOnlyList<Ff7Line>? observedLines = null, byte[]? mask = null, bool? direct = false) => Ff7AutoPlayDiscovery.CanOfferGatewayWithComponentRoute(direct, mesh, mask ?? blocked, player, gateway, targets, observedLines ?? lines);

        Check(Ff7AutoPlayDiscovery.ReadGatewayCrossingAvailability(mesh, blocked, player, gateway, 16) == false, "The authored current component unexpectedly already admits the remote gateway crossing.");
        Check(Offer([first, second]), "A complete two-stage native ladder route failed to retain its gateway in observation.");
        Check(!Ff7AutoplayDriver.HasGatewayTopologyPath(mesh, blocked, player, gateway, out _), "Offering a future component route changed current native gateway reachability.");
        Check(Offer([], direct: true) && Offer([], direct: null), "Existing direct or unknown gateway availability was narrowed by component advice.");
        Check(!Offer([]) && !Offer([first]), "Missing or disconnected native endpoint metadata retained a definitely unreachable gateway.");
        Check(!Offer([first, second], [lines[0] with { Enabled = 0 }, lines[1]]), "A disabled source LINE retained a future gateway.");
        Check(!Offer([first, second], [lines[0] with { Entity = 9 }, lines[1]]), "An unrelated native LINE authenticated the first component step.");
        Check(!Offer([first, second], mask: [2]) && !Offer([first, second], mask: [4]), "A blocked intermediate or final landing retained a future gateway.");
        Check(!Offer([first, second with { LadderLandings = [secondLanding with { Command = secondLanding.Command with { Triangle = 1, X = 320 } }] }]), "A closed component cycle was treated as a destination route.");

        foreach (Ff7AutoPlayTarget invalid in new[]
        {
            first with { AnalysisComplete = false },
            first with { MayChangeNavigation = false },
            first with { ChangesStory = true },
            first with { ChangesField = true },
            first with { Kind = Ff7AutoPlayTargetKind.ActorTalk },
            first with { LadderLandings = [] },
            first with { LadderLandings = [firstLanding with { Entity = 2 }] },
            first with { LadderLandings = [firstLanding with { Literal = false }] },
            first with { LadderLandings = [firstLanding with { Command = firstLanding.Command with { Mode = 4 } }] },
            first with { LadderLandings = [firstLanding with { Command = firstLanding.Command with { X = 500 } }] },
            first with { LadderLandings = [firstLanding with { Command = firstLanding.Command with { Z = 256 } }] }
        })
        {
            Check(!Offer([invalid, second]), "Incomplete, effectful, wrong-owner, variable or unsupported endpoint advice retained a gateway.");
        }
        Check(before == JsonSerializer.Serialize(new { mesh, blocked, player, gateway, lines, first, second }), "Gateway observation mutated native geometry, masks, endpoint metadata or current player state.");
        Console.WriteLine("Gateway observation preserves proved component routes without changing current native crossing eligibility.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
