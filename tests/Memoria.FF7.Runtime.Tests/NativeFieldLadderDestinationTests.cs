using System.Numerics;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeFieldLadderDestinationTests
{
    internal static void Run()
    {
        PcWalkmeshTriangle[] mesh =
        [
            new(0, new(0, 0, 0), new(100, 0, 0), new(0, 100, 0), -1, -1, -1),
            new(1, new(300, 0, 100), new(400, 0, 100), new(300, 100, 100), -1, -1, -1)
        ];
        byte[] blocked = [0];
        Ff7Actor player = new() { Slot = 0, Entity = 1, Model = 0, Triangle = 0, X4096 = 10 << 12, Y4096 = 10 << 12, CollisionRadius = 1 };
        PcFieldGateway gate = new(new(320, 20, 100), new(330, 20, 100), 0, 0, 0, 200, 0, 0);
        FieldActorLadderLanding landing = new(1, 10, new(320, 20, 100, 1, 1, 1, 0, 1), true);
        string before = JsonSerializer.Serialize(new { mesh, blocked, player, gate, landing });
        bool Connects(IReadOnlyList<FieldActorLadderLanding> effects, byte[]? mask = null, Ff7Actor? actual = null) => Ff7AutoPlayPolicy.CanEstablishFieldGatewayLanding(mesh, mask ?? blocked, actual ?? player, gate, effects);
        Check(Connects([landing]), "A literal player ladder endpoint did not establish the disconnected gateway approach.");
        foreach (byte mode in new byte[] { 0, 2, 3 })
        {
            Check(Connects([landing with { Command = landing.Command with { Mode = mode } }]), "A supported native ladder axis/reversal lost its endpoint advice.");
        }
        foreach (FieldActorLadderLanding invalid in new[]
        {
            landing with { Entity = 2 },
            landing with { Literal = false },
            landing with { Command = landing.Command with { Mode = 4 } },
            landing with { Command = landing.Command with { Triangle = -1 } },
            landing with { Command = landing.Command with { Triangle = 2 } },
            landing with { Command = landing.Command with { X = 500 } },
            landing with { Command = landing.Command with { Z = 356 } }
        })
        {
            Check(!Connects([invalid]), "An NPC, variable, unsupported or invalid native endpoint authorized a destination prerequisite.");
        }
        Check(!Connects([]) && !Connects([landing, landing with { ProgramCounter = 20 }]), "Missing or ambiguous player ladder outcomes authorized a prerequisite.");
        Check(!Connects([landing], [2]), "A blocked landing triangle authorized a prerequisite.");
        Check(!Connects([landing], actual: player with { Triangle = 1, X4096 = 320 << 12, Y4096 = 20 << 12, Z4096 = 100 << 12 }), "An already reachable gateway manufactured a ladder prerequisite.");
        Check(before == JsonSerializer.Serialize(new { mesh, blocked, player, gate, landing }), "Endpoint topology preview changed actual geometry, blocks or player placement.");
        CheckResolution(mesh, blocked, player, gate, landing);
        CheckLiteralAnalysis();
        Console.WriteLine("Literal player ladder destination advice, endpoint guards and prerequisite-before-foreign-scan checks passed.");
    }

    private static void CheckResolution(PcWalkmeshTriangle[] mesh, byte[] blocked, Ff7Actor player, PcFieldGateway gate, FieldActorLadderLanding landing)
    {
        Ff7AutoPlayTarget gateway = new("gateway", -1, -1, Ff7AutoPlayTargetKind.Gateway, "gate", false, Vector3.Zero, GatewayIndex: 0, DestinationField: 200);
        Ff7AutoPlayTarget foreign = gateway with { Id = "foreign", GatewayIndex = 1, DestinationField = 400 };
        Ff7AutoPlayTarget producer = new("ladder", 2, 4, Ff7AutoPlayTargetKind.LineTouch, "ladder-condition", false, Vector3.Zero, MayChangeNavigation: true) { LadderLandings = [landing] };
        foreach (int variant in new[] { 0, 1, 2, 3 })
        {
            Ff7AutoPlayTarget candidate = variant == 1 ? producer with { AnalysisComplete = false } : variant == 2 ? producer with { LadderLandings = [landing with { Literal = false }] } : producer;
            Ff7AutoPlayObservation observation = new("ff7:100:program", "program", 1, [foreign, candidate, gateway], true, true) { NavigationKey = "lower" };
            Ff7AutoPlayState state = new() { DestinationField = 300 };
            var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
            AutoPlayEvent[] events = observation.Events.Select(target => new AutoPlayEvent(target.Id, AutoPlayEventKind.Interaction, ConditionSignature: target.ConditionFingerprint)).ToArray();
            planner.Observe(new(observation.VisitId, "program"), events);
            planner.ObserveNavigation(observation.VisitId, observation.NavigationKey, events);
            string coverage = JsonSerializer.Serialize(state.Coverage);
            List<ushort> reads = [];
            int[] Read(ushort field)
            {
                reads.Add(field);
                return field == 200 ? [300] : [];
            }
            bool Available(Ff7AutoPlayTarget target) => target.Id != gateway.Id && (target.Id != candidate.Id || variant != 3);
            bool Establishes(Ff7AutoPlayTarget target, Ff7AutoPlayTarget selected) => selected.Id == gateway.Id && Ff7AutoPlayPolicy.CanEstablishFieldGatewayLanding(mesh, blocked, player, gate, target.LadderLandings);
            Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation, Read, Available, Establishes);
            Check(variant == 0 ? state.NavigationGoal?.EventId == producer.Id && !reads.Contains(400) : state.NavigationGoal?.EventId != producer.Id, "Destination advice bypassed a real prerequisite or admitted an unknown/disabled/incomplete producer.");
            Check(coverage == JsonSerializer.Serialize(state.Coverage) && state.DestinationField == 300, "Destination advice credited native execution or changed explicit intent.");
        }
    }

    private static void CheckLiteralAnalysis()
    {
        foreach (byte selector in new byte[] { 0, 0x60 })
        {
            byte[] body = [0xC2, selector, 0, 20, 0, 20, 0, 100, 0, 1, 0, 1, 1, 0, 1, 0];
            ushort[][] entries = [new ushort[32]];
            entries[0][1] = 2;
            FieldProgram program = new([0, 0, .. body], entries);
            FieldMachine machine = new(program);
            machine.Initialize();
            FieldVmSnapshot snapshot = machine.Capture();
            snapshot.Entities[0] = snapshot.Entities[0] with { ModelBound = true };
            machine = FieldMachine.Restore(program, snapshot);
            string before = JsonSerializer.Serialize(machine.Capture());
            FieldScriptAnalysis analysis = machine.AnalyzeScript(0, 1);
            Check(analysis.Complete && analysis.LadderLandings.Single().Literal == (selector == 0), "Native ladder analysis confused a dynamic selector with literal endpoint operands.");
            Check(before == JsonSerializer.Serialize(machine.Capture()), "Ladder analysis changed native execution or variables.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
