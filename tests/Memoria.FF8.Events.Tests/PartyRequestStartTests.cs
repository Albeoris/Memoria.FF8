using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyRequestStartAndGuard()
    {
        foreach (bool busy in new[] { false, true })
        {
            EventEngine accepted = CreatePartyRequestFixture(0, 1, busy, Jsm.Opcode.PREQSW);
            for (int callback = 0; callback < 3; callback++)
            {
                accepted.Step();
            }
            JsonElement pending = accepted.Capture();
            JsonElement caller = pending.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
            Require(!CapturedVariables(pending).TryGetProperty("512", out _) && !CapturedVariables(pending).TryGetProperty("516", out _) && caller.GetProperty("WaitReason").GetString() == nameof(WaitReason.None) && caller.GetProperty("RequestSequence").ValueKind == JsonValueKind.Null, "Started PREQSW invented a completion wait or failed its native return3 yield.");
            JsonElement suspended = pending.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 3);
            EventEngine restored = CreatePartyRequestFixture(0, 1, busy, Jsm.Opcode.PREQSW);
            restored.Restore(pending);
            accepted.Step();
            restored.Step();
            JsonElement started = accepted.Capture();
            Require(CapturedVariables(started).GetProperty("512").GetInt32() == 11 && !CapturedVariables(started).TryGetProperty("516", out _) && JsonElement.DeepEquals(started, restored.Capture()), "PREQSW waited for target completion instead of continuing on the next source callback after start.");
            if (busy)
            {
                Require(JsonElement.DeepEquals(suspended, started.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 3)), "Started higher party request advanced the suspended default's retained WAIT or changed its owner.");
            }
            for (int callback = 0; callback < 4; callback++)
            {
                accepted.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(accepted.Capture(), restored.Capture()), "PREQSW target/default/caller continuation diverged after independent cold restore.");
            }
            Require(CapturedVariables(accepted.Capture()).GetProperty("516").GetInt32() == 7, "PREQSW target failed to finish independently of its caller.");
        }
        foreach ((int slot, int priority) in new[] { (1, 1), (0, 0) })
        {
            EventEngine ignored = CreatePartyRequestFixture(slot, priority, true, Jsm.Opcode.PREQSW);
            for (int callback = 0; callback < 5; callback++)
            {
                ignored.Step();
            }
            Require(CapturedVariables(ignored.Capture()).GetProperty("512").GetInt32() == 11 && !ignored.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 4), "PREQSW absent party slot or occupied same-priority slot was not consumed and dropped.");
        }
        foreach ((int slot, int priority) in new[] { (3, 1), (0, 8) })
        {
            EventEngine invalid = CreatePartyRequestFixture(slot, priority, false, Jsm.Opcode.PREQSW);
            invalid.Step();
            invalid.Step();
            try
            {
                invalid.Step();
                throw new InvalidOperationException("Out-of-range PREQSW slot or priority was accepted.");
            }
            catch (EventScriptException) when (invalid.LastFault?.Opcode == nameof(Jsm.Opcode.PREQSW))
            {
            }
            JsonElement frozen = invalid.Capture();
            JsonElement caller = frozen.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
            Require(caller.GetProperty("Position").GetInt32() == invalid.LastFault!.Operation && caller.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([2, priority]) && !CapturedVariables(frozen).TryGetProperty("512", out _), "Invalid PREQSW bounds changed the original PC, operands or successor.");
            ExpectInterruptedRestoreRefusal(invalid, frozen);
            try
            {
                invalid.Step();
                throw new InvalidOperationException("Rejected interrupted restore cleared the PREQSW fault guard.");
            }
            catch (EventScriptException) when (invalid.LastFault?.Opcode == nameof(Jsm.Opcode.PREQSW))
            {
            }
            Require(JsonElement.DeepEquals(frozen, invalid.Capture()), "PREQSW fault guard changed the frozen execution state.");
        }
    }

    private static void GardenPartyRequestStartBoundary(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(137);
        JsmProgram program = JsmProgram.Read(field.Jsm);
        EventScript targetScript = program.Objects[5].Scripts[5];
        const string leaf = "Events.Tests.PartyRequestStartTests.1";
        EventEngine Create(string source = leaf)
        {
            GameState state = GameSession.Load(TestResources.FixturePath(source));
            GameSession session = new(state);
            EventEngine engine = new(session) { AnimationFrameCount = field.GetAnimationFrameCount, LoadSoundResource = _ => SoundResourceStatus.Muted, ResolveGlobalSoundResource = MutedGlobalSample };
            engine.LoadField(137, field.Jsm, field.Messages);
            engine.Restore(state.EventState!.Value);
            return engine;
        }
        EventEngine first = Create();
        EventEngine resumed = Create();
        Require(first.Frame == 65 && first.Scene.FieldPartyNativeActorIds.SequenceEqual([0, 1, 255]) && first.Scene.NativeActorObjectIds[1] == 5, "Actual PREQSW source lost its compact party mapping or safe preceding frame.");
        Require(!first.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ObjectId").GetInt32() == 5 && !fiber.GetProperty("Complete").GetBoolean()), "Actual PREQSW target ceased to be idle.");
        List<ScriptCommand> commands = [];
        first.CommandExecuted += commands.Add;
        first.Apply(new PlayerAction(PlayerActionKind.Wait));
        resumed.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(first.Frame == 66 && first.LastFault is null && first.Capture().GetRawText() == resumed.Capture().GetRawText(), "Actual ordinary PREQSW Wait1 changed native arguments or diverged on replay.");
        ScriptCommand request = commands.Single(command => command.ObjectId == 0 && command.ScriptId == 6 && command.Operation == 57);
        Require(request.Name == nameof(Jsm.Opcode.PREQSW) && request.Arguments.SequenceEqual([3, 5]) && commands.Any(command => command.ObjectId == 5 && command.ScriptId == targetScript.Id), "Actual PREQSW did not start retained Selphie object5/local5 at priority3.");
        JsonElement pending = first.Capture();
        JsonElement caller = pending.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 6);
        Require(caller.GetProperty("Position").GetInt32() > 57 && caller.GetProperty("WaitReason").GetString() != nameof(WaitReason.RequestEnd), "Actual PREQSW caller did not continue after target start.");
        EventEngine restored = Create();
        restored.Restore(pending);
        Require(pending.GetRawText() == restored.Capture().GetRawText(), "Actual PREQSW pending target failed exact restore.");
        first.Apply(new PlayerAction(PlayerActionKind.Wait));
        restored.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(first.Capture().GetRawText() == restored.Capture().GetRawText(), "Actual PREQSW independent target progression diverged after restore.");
        EventEngine retry = Create("Events.Tests.PartyRequestStartTests.2");
        caller = retry.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 6);
        Require(retry.LastFault is null && caller.GetProperty("Position").GetInt32() == 57 && caller.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 5, 3 }), "PREQSW upgrade changed the exact saved fault PC or original operands.");
        commands.Clear();
        retry.CommandExecuted += commands.Add;
        retry.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(commands.Any(command => command.ObjectId == 5 && command.ScriptId == targetScript.Id) && retry.LastFault is null, "Exact PREQSW saved fault failed to retry its original party request.");
        Console.WriteLine($"Original137 PREQSW: ordinary Wait1/frame65→66, party1/compact1→Selphie5 local5/label{targetScript.Id} priority3; caller continues, independent target restore passes; next {first.NextTrigger.Kind}/frame{first.Frame}.");
    }
}
