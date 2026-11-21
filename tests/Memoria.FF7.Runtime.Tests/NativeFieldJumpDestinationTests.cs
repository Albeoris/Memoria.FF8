using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeFieldJumpDestinationTests
{
    internal static void Run()
    {
        CheckEndpointAdvice();
        CheckTerminalAnalysis();
        Console.WriteLine("Native terminal jump destination checks passed.");
    }

    private static void CheckEndpointAdvice()
    {
        PcWalkmeshTriangle[] mesh =
        [
            new(0, new(0, 0, 0), new(100, 0, 0), new(0, 100, 0), -1, -1, -1),
            new(1, new(300, 0, 100), new(400, 0, 100), new(300, 100, 100), -1, -1, -1)
        ];
        byte[] blocked = [0];
        Ff7Actor player = new() { Slot = 0, Entity = 1, Model = 0, Triangle = 0, X4096 = 10 << 12, Y4096 = 10 << 12, CollisionRadius = 1 };
        PcFieldGateway gate = new(new(320, 20, 100), new(330, 20, 100), 0, 0, 0, 200, 0, 0);
        FieldActorJumpLanding landing = new(1, 10, new(320, 20, 1, 17), true);
        string before = JsonSerializer.Serialize(new { mesh, blocked, player, gate, landing });
        bool Connects(IReadOnlyList<FieldActorJumpLanding> effects, byte[]? mask = null, Ff7Actor? actual = null) => Ff7AutoPlayPolicy.CanEstablishFieldGatewayJumpLanding(mesh, mask ?? blocked, actual ?? player, gate, effects);
        Check(Connects([landing]), "A literal terminal player jump did not establish the disconnected gateway approach.");
        foreach (FieldActorJumpLanding invalid in new[]
        {
            landing with { Entity = 2 },
            landing with { Literal = false },
            landing with { Command = landing.Command with { Duration = 0 } },
            landing with { Command = landing.Command with { Duration = -1 } },
            landing with { Command = landing.Command with { Triangle = -1 } },
            landing with { Command = landing.Command with { Triangle = 2 } },
            landing with { Command = landing.Command with { X = 500 } }
        })
        {
            Check(!Connects([invalid]), "An NPC, variable, invalid or nonfinite jump endpoint authorized a prerequisite.");
        }
        Check(!Connects([]) && !Connects([landing, landing with { ProgramCounter = 20 }]), "Absent or ambiguous player jump outcomes authorized a prerequisite.");
        Check(!Connects([landing], [2]), "A blocked landing authorized a prerequisite.");
        Check(!Connects([landing], actual: player with { Triangle = 1, X4096 = 320 << 12, Y4096 = 20 << 12, Z4096 = 100 << 12 }), "An already reachable gateway manufactured a prerequisite.");
        Check(before == JsonSerializer.Serialize(new { mesh, blocked, player, gate, landing }), "Jump endpoint advice mutated the actual source or geometry.");
    }

    private static void CheckTerminalAnalysis()
    {
        byte[] Jump(byte selector = 0) => [0xC0, selector, 0, 20, 0, 20, 0, 1, 0, 17, 0];
        FieldScriptAnalysis Analyze(byte[] body)
        {
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
            Check(before == JsonSerializer.Serialize(machine.Capture()), "Terminal jump analysis mutated native execution or variables.");
            return analysis;
        }

        FieldScriptAnalysis plain = Analyze([.. Jump(), 0]);
        Check(plain.Complete && plain.TerminalJumpLandings is [{ Literal: true, ProgramCounter: 2 }], "A literal jump with a RET footer was not exposed.");
        FieldScriptAnalysis chain = Analyze([.. Jump(), .. Jump(), 0x33, 0, 0x4A, 0, 0]);
        Check(chain.Complete && chain.TerminalJumpLandings is [{ ProgramCounter: 13, Literal: true }], "An earlier jump in a chain was mistaken for its terminal landing.");
        Check(Analyze([.. Jump(0x50), 0]).TerminalJumpLandings.All(landing => !landing.Literal), "A variable endpoint selector acquired literal authority.");
        foreach (byte[] footer in new byte[][]
        {
            [0x74, 0, 0, 0, 0],
            [0x80, 0x50, 0, 1, 0],
            [0x10, 2, 0],
            [0x01, 0, 0x21, 0]
        })
        {
            Check(Analyze([.. Jump(), .. footer]).TerminalJumpLandings.Length == 0, "A jump followed by a query, write, branch or request was treated as a terminal endpoint.");
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
