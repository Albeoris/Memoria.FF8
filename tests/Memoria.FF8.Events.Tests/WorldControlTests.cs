using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyPowerAndFlagAliases(string gameDirectory)
    {
        NativeWorldControlState initial = NativeWorldControlState.CreateNewGame() with { Menu209 = 0xFF };
        foreach (int argument in new[] { 0, 1 })
        {
            GameSession session = new(new GameState { WorldControls = initial });
            EventEngine engine = new(session, session.State.Variables);
            byte[] powerProgram = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, argument), (Jsm.Opcode.PHSPOWER, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
            engine.LoadField(100, powerProgram, []);
            engine.Step();
            Require(session.WorldControls == initial.WithPartyPower(argument) && engine.Scene.PartyPower == argument, "PHSPOWER did not update typed source bit and native209 mask atomically.");
            NativeWorldControlState afterPower = session.WorldControls!;
            JsonElement saved = engine.Capture();
            GameSession restoredSession = new(new GameState { WorldControls = NativeWorldControlState.CreateNewGame() });
            EventEngine restored = new(restoredSession, restoredSession.State.Variables);
            restored.LoadField(100, powerProgram, []);
            restored.Restore(saved);
            Require(JsonElement.DeepEquals(saved, restored.Capture()) && restoredSession.WorldControls == afterPower, "VM-only PHSPOWER restore left canonical flags at initial native values.");
            engine.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()) && restoredSession.WorldControls == session.WorldControls, "Independent post-PHSPOWER VM restore changed native flags or subsequent wait.");
            session.RequestField(new FieldTransition(101, 10, 20, 0, 0));
            Require(session.WorldControls == afterPower.WithNormalFieldLoad(), "Normal field load lost source105 mask or unrelated209 bits.");
        }

        GameSession legacy = new(new GameState());
        EventEngine legacyEngine = new(legacy, legacy.State.Variables);
        legacyEngine.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PHSPOWER, 0), (Jsm.Opcode.RET, 0)), []);
        legacyEngine.Step();
        Require(legacy.WorldControls is null && legacyEngine.Scene.PartyPower == 1, "Legacy PHSPOWER fabricated native world-control provenance.");
        JsonElement legacyCapture = legacyEngine.Capture();
        JsonObject legacyJson = JsonNode.Parse(legacyCapture.GetRawText())!.AsObject();
        legacyJson.Remove("WorldControls");
        legacy.SetWorldControls(initial);
        legacyEngine.Restore(JsonSerializer.SerializeToElement(legacyJson));
        Require(legacy.WorldControls == initial, "Missing legacy VM controls erased explicitly proved service metadata.");
        legacyEngine.Restore(legacyCapture);
        Require(legacy.WorldControls == initial, "Null legacy VM controls erased explicitly proved service metadata.");

        GameSession byteSession = new(new GameState { WorldControls = initial });
        EventEngine bytes = new(byteSession, byteSession.State.Variables);
        bytes.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHM_B, 209), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.PSHN_L, 0xA3), (Jsm.Opcode.POPM_B, 209), (Jsm.Opcode.PSHM_B, 209), (Jsm.Opcode.POPM_W, 514), (Jsm.Opcode.RET, 0)), []);
        bytes.Step();
        Require(byteSession.State.Variables[512] == 0xFF && byteSession.State.Variables[514] == 0xA3 && !byteSession.State.Variables.ContainsKey(209) && byteSession.WorldControls == initial with { Menu209 = 0xA3 }, "B209 used a stale duplicate variable or lost its complete canonical byte.");
        GameSession byteRestoredSession = new(new GameState { WorldControls = NativeWorldControlState.CreateNewGame() });
        EventEngine byteRestored = new(byteRestoredSession, byteRestoredSession.State.Variables);
        byteRestored.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHM_B, 209), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.PSHN_L, 0xA3), (Jsm.Opcode.POPM_B, 209), (Jsm.Opcode.PSHM_B, 209), (Jsm.Opcode.POPM_W, 514), (Jsm.Opcode.RET, 0)), []);
        byteRestored.Restore(bytes.Capture());
        Require(JsonElement.DeepEquals(bytes.Capture(), byteRestored.Capture()) && byteRestoredSession.WorldControls == byteSession.WorldControls, "VM-only B209 restore left its canonical byte stale.");

        foreach ((Jsm.Opcode opcode, int address) in new[] { (Jsm.Opcode.PSHM_B, 105), (Jsm.Opcode.POPM_B, 105), (Jsm.Opcode.PSHM_W, 104), (Jsm.Opcode.PSHSM_W, 104), (Jsm.Opcode.POPM_W, 104), (Jsm.Opcode.PSHM_L, 102), (Jsm.Opcode.POPM_L, 102), (Jsm.Opcode.PSHM_W, 208), (Jsm.Opcode.POPM_W, 208), (Jsm.Opcode.PSHM_L, 206), (Jsm.Opcode.POPM_L, 206) })
        {
            GameSession session = new(new GameState { WorldControls = initial });
            EventEngine blocked = new(session, session.State.Variables);
            bool read = opcode is Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHM_L;
            byte[] commands = read ? SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 123), (opcode, address), (Jsm.Opcode.POPM_L, 512), (Jsm.Opcode.POPM_L, 514), (Jsm.Opcode.RET, 0)) : SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 123), (opcode, address), (Jsm.Opcode.RET, 0));
            blocked.LoadField(100, commands, []);
            ExpectWorldControlFault(blocked, opcode, 2, [123]);
            Require(session.WorldControls == initial && session.State.Variables.Count == 0, "Refused alias access mutated typed controls or raw variables.");
        }
        foreach (int alias in new[] { 102, 103, 104, 105, 206, 207, 208, 209 })
        {
            foreach (bool transition in new[] { false, true })
            {
                GameSession session = new(new GameState { FieldId = 100, WorldControls = initial, Variables = new Dictionary<int, int> { [alias] = 77 } });
                EventEngine blocked = new(session, session.State.Variables);
                byte[] commands = transition ? SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 101), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MAPJUMPO, 0), (Jsm.Opcode.RET, 0)) : SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PHSPOWER, 0), (Jsm.Opcode.RET, 0));
                blocked.LoadField(100, commands, []);
                ExpectWorldControlFault(blocked, transition ? Jsm.Opcode.MAPJUMPO : Jsm.Opcode.PHSPOWER, transition ? 3 : 2, transition ? [0, 101] : [0]);
                Require(session.State.FieldId == 100 && session.WorldControls == initial && session.State.Variables[alias] == 77 && blocked.Scene.PartyPower == 0, "Ambiguous legacy aliases allowed a field packet or world-control mutation.");
            }
        }

        using GameAssets assets = new(gameDirectory);
        GameSession fresh = new(new GameState());
        fresh.StartNewGame();
        FieldAssets start = assets.LoadField(74);
        EventEngine original = new(fresh, fresh.State.Variables);
        original.LoadField(74, start.Jsm, start.Messages);
        bool reached = false;
        original.CommandExecuted += command =>
        {
            if (command.Name == "PHSPOWER")
            {
                Require(command.Operation == 80 && command.Arguments.SequenceEqual([0]) && fresh.WorldControls?.SourceDisabled == true && fresh.WorldControls.Menu209 == 0 && !fresh.IsMoviePlaying, "Original start0 PHSPOWER80 failed to establish source105 before movie30.");
                reached = true;
            }
        };
        for (int tick = 0; tick < 30 && !fresh.IsMoviePlaying; tick++)
        {
            original.Step();
        }
        Require(reached && fresh.IsMoviePlaying && fresh.WorldControls?.SourceDisabled == true, "Ordinary native New Game did not preserve PHSPOWER through original movie30.");
        Console.WriteLine("Native PHSPOWER: original start0/op80 sets typed source105 before movie30; zero/nonzero preserve209 other bits, B209 canonical, legacy unknown retained and partial aliases freeze before PC/stack.");
    }

    private static void ExpectWorldControlFault(EventEngine engine, Jsm.Opcode opcode, int operation, int[] stack)
    {
        try
        {
            engine.Step();
            throw new InvalidOperationException("Unsupported world-control alias completed silently.");
        }
        catch (EventScriptException) when (engine.LastFault?.Opcode == opcode.ToString())
        {
        }
        JsonElement checkpoint = engine.Capture();
        JsonElement fiber = checkpoint.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("Position").GetInt32() == operation && !item.GetProperty("Complete").GetBoolean());
        Require(fiber.GetProperty("Stack").EnumerateArray().Select(item => item.GetInt32()).SequenceEqual(stack), "World-control alias refusal consumed original operands.");
        engine.Restore(checkpoint);
        Require(JsonElement.DeepEquals(checkpoint, engine.Capture()), "World-control alias refusal failed exact frozen restore.");
    }
}
