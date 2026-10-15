using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDrawPointAllocatedByteContract()
    {
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, -1494), (Jsm.Opcode.PSHN_L, 670), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 3), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETDRAWPOINT, 0), (Jsm.Opcode.RET, 0));
        GameSession session = new(new GameState { WorldConfiguration = NativeWorldConfiguration.FromFieldPacket(new WorldTransition(63, 33, -1), 262) });
        EventEngine engine = new(session, session.State.Variables);
        engine.LoadField(134, content, []);
        engine.Step();
        Require(engine.Scene.PreviousWorldEntityByte86 == 33 && engine.Scene.DrawPointReset is not null, "Known allocated BYTE86 was misclassified as an overlapping DWORD at B88.");
        GameSession unprovedSession = new(new GameState { Variables = new Dictionary<int, int> { [86] = 33 } });
        EventEngine unproved = new(unprovedSession, unprovedSession.State.Variables);
        unproved.LoadField(134, content, []);
        ExpectDrawPointFault(unproved, "SETDRAWPOINT", 7, 1);
    }

    private static void NativeDrawPointSetupContract(string gameDirectory)
    {
        EventEngine Create(byte[] content, int fieldId = 148, Dictionary<int, int>? variables = null)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, variables ?? session.State.Variables);
            engine.LoadField(fieldId, content, []);
            return engine;
        }
        byte[] Content(int mask = 1, int index = 4, bool clear = false)
        {
            List<(Jsm.Opcode Opcode, int Parameter)> operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, -1494), (Jsm.Opcode.PSHN_L, 670), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 3), (Jsm.Opcode.PSHN_L, mask), (Jsm.Opcode.SETDRAWPOINT, 0), (Jsm.Opcode.PSHN_L, index), (Jsm.Opcode.Unknown16, 0)];
            if (clear)
            {
                operations.Add((Jsm.Opcode.CLEAR, 0));
            }
            operations.AddRange([(Jsm.Opcode.PSHM_B, 240), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHM_B, 241), (Jsm.Opcode.POPI_L, 4), (Jsm.Opcode.PSHM_B, 242), (Jsm.Opcode.POPI_L, 5), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
            return SyntheticProgram(operations.ToArray());
        }
        foreach ((int mask, int index, byte expected) in new[] { (1, 4, (byte)3), (257, 260, (byte)3), (-255, -252, (byte)3), (1, 0, (byte)255) })
        {
            byte[] content = Content(mask, index);
            EventEngine engine = Create(content);
            JsonElement before = engine.Capture();
            List<ScriptCommand> commands = [];
            engine.CommandExecuted += commands.Add;
            engine.Step();
            Require(engine.Scene.DrawPointFieldStateVersion == 1 && engine.Scene.DrawPointEnabled == 1 && engine.Scene.DrawPointDisplayParameter == 1 && engine.Scene.DrawPointIndex == expected && engine.Scene.DrawPointReset is { SampledDisplayMask: 1, ObjectId: 0, ScriptId: 0, Operation: 7 } reset && reset.RawPosition == new FieldFixedPosition(-6119424, 2744320, 0) && reset.Sparks.Count == 8 && reset.Sparks.All(anchor => anchor == new NativeDrawPointSparkAnchor(-1494, 670, 0, 1)), "Draw-point setup lost its low bytes, native anchor reset or immediate index continuation.");
            JsonElement saved = engine.Capture();
            Require(saved.GetProperty("Fibers")[0].GetProperty("Results").GetProperty("3").GetInt32() == 1 && saved.GetProperty("Fibers")[0].GetProperty("Results").GetProperty("4").GetInt32() == 1 && saved.GetProperty("Fibers")[0].GetProperty("Results").GetProperty("5").GetInt32() == expected, "Exact native byte reads did not use the known typed setup/index.");
            Require(JsonElement.DeepEquals(saved.GetProperty("EventRandom"), before.GetProperty("EventRandom")) && saved.GetProperty("Variables").EnumerateObject().All(pair => !int.TryParse(pair.Name, out int address) || address is < 116 or > 242) && engine.AcquiredGuardianForces is null, "Setup fabricated depletion data, GF state, B88 or EventRND draws.");
            Require(commands.Single(command => command.Name == "SETDRAWPOINT").Arguments.SequenceEqual([mask]) && commands.Single(command => command.Name == "Unknown16").Arguments.SequenceEqual([index]), "Draw-point setup discarded its original full operands.");
            EventEngine independent = Create(content);
            independent.Restore(saved);
            Require(JsonElement.DeepEquals(saved, independent.Capture()), "Known draw-point bytes and reset provenance did not restore independently.");
            engine.Step();
            independent.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), independent.Capture()), "Restored draw-point setup changed ordinary wait continuation.");
            independent.LoadField(149, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), []);
            Require(independent.Scene.DrawPointFieldStateVersion == 1 && independent.Scene.DrawPointEnabled == 0 && independent.Scene.DrawPointDisplayParameter == 0 && independent.Scene.DrawPointReset is null && independent.Scene.DrawPointIndex == expected, "Normal field load did not reset field display bytes while preserving known global B242.");
        }
        EventEngine cleared = Create(Content(clear: true));
        cleared.Step();
        cleared.Step();
        Require(cleared.Scene.DrawPointEnabled == 1 && cleared.Scene.DrawPointDisplayParameter == 1 && cleared.Scene.DrawPointIndex == 3 && cleared.Scene.DrawPointReset is not null, "CLEAR changed the native below256 draw-point bytes.");
        NativeDrawPointAllocatedByteContract();
        foreach (int mask in new[] { 0, 2 })
        {
            EventEngine blocked = Create(Content(mask));
            ExpectDrawPointFault(blocked, "SETDRAWPOINT", 7, mask);
            Require(blocked.Scene.DrawPointReset is null && blocked.Scene.DrawPointEnabled == 0 && blocked.Scene.DrawPointIndex is null, "Unsupported display mask changed native setup/index.");
        }
        foreach (int alias in new[] { 85, 88, 237, 239, 240, 241, 242 })
        {
            Dictionary<int, int> variables = new() { [alias] = 99 };
            EventEngine blocked = Create(Content(), variables: variables);
            ExpectDrawPointFault(blocked, "SETDRAWPOINT", 7, 1);
            Require(variables[alias] == 99 && blocked.Scene.DrawPointReset is null, "Ambiguous native byte alias changed before setup refusal.");
        }
        foreach ((Jsm.Opcode opcode, int address) in new[] { (Jsm.Opcode.PSHM_B, 88), (Jsm.Opcode.PSHM_B, 242), (Jsm.Opcode.PSHM_W, 239), (Jsm.Opcode.PSHSM_W, 241), (Jsm.Opcode.PSHM_L, 237) })
        {
            EventEngine blocked = Create(SyntheticProgram((Jsm.Opcode.LBL, 0), (opcode, address), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.RET, 0)));
            ExpectDrawPointFault(blocked, opcode.ToString(), 1);
        }
        foreach ((Jsm.Opcode opcode, int address) in new[] { (Jsm.Opcode.POPM_B, 240), (Jsm.Opcode.POPM_W, 241), (Jsm.Opcode.POPM_L, 239) })
        {
            EventEngine blocked = Create(SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 99), (opcode, address), (Jsm.Opcode.RET, 0)));
            ExpectDrawPointFault(blocked, opcode.ToString(), 2, 99);
        }
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.SETDRAWPOINT, Jsm.Opcode.Unknown16 })
        {
            EventEngine empty = Create(SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (opcode, 0), (Jsm.Opcode.RET, 0)));
            JsonNode missingOperand = JsonNode.Parse(empty.Capture().GetRawText())!;
            missingOperand["Fibers"]![0]!["Position"] = 2;
            empty.Restore(JsonSerializer.SerializeToElement(missingOperand));
            ExpectDrawPointFault(empty, opcode.ToString(), 2);
        }
        EventEngine special = Create(Content(), 1);
        ExpectDrawPointFault(special, "SETDRAWPOINT", 7, 1);
        EventEngine retainedAliases = Create(SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.CLEAR, 0), (Jsm.Opcode.RET, 0)), variables: new Dictionary<int, int> { [238] = 5 });
        retainedAliases.Step();
        Require(retainedAliases.LastFault is null && retainedAliases.Capture().GetProperty("Variables").GetProperty("238").GetInt32() == 5, "Native CLEAR changed an alias outside its original256..1279 range.");

        byte[] knownContent = Content();
        EventEngine known = Create(knownContent);
        known.Step();
        JsonElement valid = known.Capture();
        foreach (string invalid in new[] { "status", "mask", "frame", "count", "authority", "width" })
        {
            JsonNode malformed = JsonNode.Parse(valid.GetRawText())!;
            JsonNode scene = malformed["Scene"]!;
            switch (invalid)
            {
                case "status":
                    scene["DrawPointReset"]!["Sparks"]![0]!["Status"] = 2;
                    break;
                case "mask":
                    scene["DrawPointReset"]!["SampledDisplayMask"] = 2;
                    break;
                case "frame":
                    scene["DrawPointReset"]!["Frame"] = known.Frame + 1;
                    break;
                case "count":
                    scene["DrawPointReset"]!["Sparks"]!.AsArray().RemoveAt(0);
                    break;
                case "authority":
                    scene["DrawPointFieldStateVersion"] = 0;
                    break;
                case "width":
                    scene["DrawPointIndex"] = 256;
                    break;
            }
            try
            {
                known.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Invalid native draw-point snapshot was accepted.");
            }
            catch (Exception exception) when (exception is InvalidDataException or JsonException)
            {
            }
            Require(JsonElement.DeepEquals(valid, known.Capture()), "Invalid draw-point snapshot mutated the VM before validation.");
        }
        EventEngine legacy = Create(SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHM_B, 240), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.RET, 0)));
        JsonNode missing = JsonNode.Parse(legacy.Capture().GetRawText())!;
        foreach (string property in new[] { "DrawPointFieldStateVersion", "DrawPointEnabled", "DrawPointDisplayParameter", "DrawPointIndex", "DrawPointReset" })
        {
            missing["Scene"]!.AsObject().Remove(property);
        }
        legacy.Restore(JsonSerializer.SerializeToElement(missing));
        Require(legacy.Scene.DrawPointFieldStateVersion == 0 && legacy.Scene.DrawPointIndex is null, "Legacy missing draw-point data invented authority/index0.");
        ExpectDrawPointFault(legacy, "PSHM_B", 1);

        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(148);
        string source = Path.Combine("../fixtures/external-fixture.json");
        EventEngine Actual()
        {
            GameSession session = new(GameSession.Load(source));
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                ResolveNativeTriangleHeight = (triangle, x, y) => FieldPartyTraceGeometry.ResolveTriangleHeight(field.Walkmesh, triangle, x, y),
                ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z)
            };
            engine.LoadField(148, field.Jsm, field.Messages, session.State.FieldArrival);
            engine.Restore(session.State.EventState!.Value);
            return engine;
        }
        EventEngine actual = Actual();
        Require(actual.Frame == 0 && actual.Scene.NativeFieldLoadVersion == 0 && actual.Scene.DrawPointIndex is null, "Original148 preinit source or unknown prior draw index changed.");
        actual.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(actual.Frame == 1 && actual.LastFault is null && actual.Scene.DrawPointReset is { ObjectId: 17, ScriptId: 105, Operation: 1961, SampledDisplayMask: 1 } actualReset && actualReset.RawPosition == new FieldFixedPosition(-6119424, 2744320, 0) && actualReset.Sparks.All(anchor => anchor == new NativeDrawPointSparkAnchor(-1494, 670, 0, 1)) && actual.Scene.DrawPointIndex == 3, "Actual148 ordinary Wait1 did not preserve original setup and following373.");
        EventEngine resumed = Actual();
        resumed.Restore(actual.Capture());
        for (int tick = 0; tick < 4; tick++)
        {
            Require(JsonElement.DeepEquals(actual.Capture(), resumed.Capture()), "Actual148 draw-point reset/bytes and complete VM failed independent restore.");
            actual.Apply(new PlayerAction(PlayerActionKind.Wait));
            resumed.Apply(new PlayerAction(PlayerActionKind.Wait));
        }
        Require(JsonElement.DeepEquals(actual.Capture(), resumed.Capture()), "Actual148 draw-point setup changed restored four-tick continuation.");
        Console.WriteLine("Draw-point setup: exact low-byte stores/mask1/eight anchors, persistent known index, load reset/CLEAR retention, atomic alias/refusal and checkpoint validation; original148 Wait1 frame0→1 plus four restored ordinary ticks.");
    }

    private static void ExpectDrawPointFault(EventEngine engine, string opcode, int position, int? operand = null)
    {
        try
        {
            engine.Step();
            throw new InvalidOperationException("Unsupported draw-point context was silently accepted.");
        }
        catch (EventScriptException)
        {
        }
        JsonElement owner = engine.Capture().GetProperty("Fibers")[0];
        Require(engine.LastFault?.Opcode == opcode && owner.GetProperty("Position").GetInt32() == position && (operand is null ? owner.GetProperty("Stack").GetArrayLength() == 0 : owner.GetProperty("Stack")[0].GetInt32() == operand), "Draw-point refusal changed original PC, operand or fault opcode.");
    }
}
