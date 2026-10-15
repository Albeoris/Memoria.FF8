using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSetPlacementContract()
    {
        byte[] Content(int x)
        {
            (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, -10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 37), (Jsm.Opcode.SET3, 5), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] placement = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, x), (Jsm.Opcode.PSHN_L, 6597), (Jsm.Opcode.SET, 65536 + 271), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.RET, 0)];
            return SyntheticObjects([1], [init, placement]);
        }
        (GameSession Session, EventEngine Engine) Create(int x = 1048576 + 82)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session);
            engine.LoadField(600, Content(x), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        var live = Create();
        live.Engine.Step();
        JsonElement before = live.Engine.Capture();
        var restored = Create();
        restored.Engine.Restore(before);
        live.Engine.Step();
        restored.Engine.Step();
        ActorState actor = live.Engine.Actors[0];
        Require(actor.X == 82 && actor.Y == 6597 && actor.Z == 37 && actor.WalkmeshTriangle == 271 && live.Session.State.Variables[5000] == 9, "SET lost DWORD shift/wrap, preserved Z, signed-WORD triangle or immediate return2 budget continuation.");
        Require(JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "SET placement diverged after independent restore.");
        var invalid = Create(65536);
        invalid.Engine.Step();
        try
        {
            invalid.Engine.Step();
            throw new InvalidOperationException("SET admitted an unproved coordinate outside its bounded signed anchor contract.");
        }
        catch (EventScriptException) when (invalid.Engine.LastFault?.Opcode == nameof(Jsm.Opcode.SET))
        {
        }
        JsonElement fiber = invalid.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(item => !item.GetProperty("Initialization").GetBoolean());
        Require(invalid.Engine.Actors[0].X == -10 && invalid.Engine.Actors[0].Y == 20 && invalid.Engine.Actors[0].Z == 37 && fiber.GetProperty("Stack").GetArrayLength() == 2 && fiber.GetProperty("Stack")[1].GetInt32() == 65536, "Unsupported SET changed placement or consumed its original operands.");
        Console.WriteLine("SET: fullDWORD fixed12 shift/wrap, retained Z, signed triangle, immediate budget continuation, independent restore and atomic unsupported anchor refusal passed.");
    }
}
