using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDoorLineToggleContract()
    {
        (Jsm.Opcode, int)[] actor = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] areaInit = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.DOORLINEOFF, 0), (Jsm.Opcode.POPM_W, 5000), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] areaMain = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 124), (Jsm.Opcode.DOORLINEON, 0), (Jsm.Opcode.DOORLINEOFF, 0), (Jsm.Opcode.DOORLINEON, 0), (Jsm.Opcode.POPM_W, 5001), (Jsm.Opcode.RET, 0)];
        byte[] content = SyntheticObjects([(16 << 7) | 1, 0], [actor, areaInit, areaMain]);
        content[0] = 1;
        content[3] = 1;
        (GameSession Session, EventEngine Engine) Create()
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session);
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        var live = Create();
        List<(bool Enabled, long Frame)> toggles = [];
        live.Engine.CommandExecuted += command =>
        {
            if (command.Name is nameof(Jsm.Opcode.DOORLINEON) or nameof(Jsm.Opcode.DOORLINEOFF))
            {
                ActorState area = live.Engine.Actors[1];
                Require(area.LineEnabled && area.LineStart is null && area.LineEnd is null && !area.LineContact && command.Arguments.Count == 0, "Area toggle changed SETLINE geometry, contact state or consumed an operand.");
                toggles.Add((area.DoorLineEnabled, live.Engine.Frame));
            }
        };
        live.Engine.Step();
        Require(!live.Engine.Actors[1].DoorLineEnabled && live.Session.State.Variables[5000] == 123 && live.Engine.Actors[0].DoorLineEnabled, "DOORLINEOFF selected a model actor or failed immediate no-pop continuation.");
        var restored = Create();
        restored.Engine.Restore(live.Engine.Capture());
        live.Engine.Step();
        restored.Engine.Step();
        Require(live.Engine.Actors[1].DoorLineEnabled && live.Session.State.Variables[5001] == 124 && toggles.SequenceEqual(new[] { (false, 1L), (true, 2L), (false, 2L), (true, 2L) }) && JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "DOORLINEON/OFF lost current-area selection, no-yield budget or independent gate persistence.");
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.DOORLINEON, Jsm.Opcode.DOORLINEOFF })
        {
            GameSession session = new(new GameState());
            EventEngine invalid = new(session);
            invalid.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 77), (opcode, 0), (Jsm.Opcode.POPM_W, 5000), (Jsm.Opcode.RET, 0)), []);
            try
            {
                invalid.Step();
                throw new InvalidOperationException("Door-line toggle admitted a model body as an original area.");
            }
            catch (EventScriptException) when (invalid.LastFault?.Opcode == opcode.ToString())
            {
            }
            JsonElement fiber = invalid.Capture().GetProperty("Fibers")[0];
            Require(invalid.Actors[0].DoorLineEnabled && fiber.GetProperty("Position").GetInt32() == 2 && fiber.GetProperty("Stack")[0].GetInt32() == 77, "Wrong-body toggle refusal changed its flag, PC or unconsumed operand.");
        }
        Console.WriteLine("Door-line toggles: native area selection, enabled-bit writes, no operands/yield, SETLINE separation, independent restore and atomic wrong-body refusal passed.");
    }
}
