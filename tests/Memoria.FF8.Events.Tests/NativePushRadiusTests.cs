using System.Reflection;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePushRadiusContract()
    {
        foreach ((int operand, int radius) in new[] { (-1, 65535), (65536, 0), (65537, 1), (64, 64) })
        {
            EventEngine engine = CreateNativeRadiusEngine(operand, 0);
            List<ScriptCommand> commands = [];
            engine.CommandExecuted += commands.Add;
            engine.Step();
            ScriptCommand radiusCommand = commands.Single(command => command.ObjectId == 0 && command.Name == "PUSHRADIUS");
            Require(engine.Actors[0].PushRadius == radius && radiusCommand.Arguments.SequenceEqual(new[] { operand }), "PUSHRADIUS did not write lowWORD502 while preserving the original full DWORD command operand.");
            EventEngine restored = CreateNativeRadiusEngine(operand, 0);
            restored.Restore(engine.Capture());
            Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()) && restored.Actors[0].PushRadius == radius, "Unsigned WORD push radius did not restore independently.");
        }
        foreach ((int radius, int peerX, bool collision) in new[] { (64, 63, true), (64, 64, false), (0, 0, false), (46340, 0, true), (46341, 0, false), (65535, 0, false) })
        {
            EventEngine engine = CreateNativeRadiusEngine(radius, peerX);
            engine.Step();
            MethodInfo contact = typeof(EventEngine).GetMethod("EvaluateNativePlayerGroundContacts", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var result = ((bool Collision, int[] PushContacts))contact.Invoke(engine, new object[] { engine.Actors[0], new NativePlayerGroundProbe(0, 0, 0) })!;
            Require(result.Collision == collision, $"Native signed mean-radius IMUL comparison disagreed for radius{radius}, distance{peerX}; wrapped negative radius² must not create a false collision.");
        }
        Console.WriteLine("PUSHRADIUS99: full DWORD operand retained, low unsignedWORD502 (-1→65535,65536→0), independent restore, radius64 strict contact and signed mean-radius² overflow comparisons passed.");
    }

    private static EventEngine CreateNativeRadiusEngine(int radiusOperand, int peerX)
    {
        (Jsm.Opcode, int)[] Actor(int label, int model, int x) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.SETMODEL, model), (Jsm.Opcode.PSHN_L, x), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, radiusOperand), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.RET, 8)];
        var session = new GameSession(new GameState());
        var engine = new EventEngine(session);
        engine.LoadField(600, SyntheticObjects([0, 16 << 7], [Actor(0, 0, 0), Actor(16, 1, peerX)]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        return engine;
    }
}
