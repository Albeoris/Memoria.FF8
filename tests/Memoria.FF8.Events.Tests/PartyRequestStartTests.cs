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
        EventEngine accepted = CreatePartyRequestFixture(0, 1, false, Jsm.Opcode.PREQSW);
        accepted.Step();
        accepted.Step();
        JsonElement pending = accepted.Capture();
        JsonElement caller = pending.GetProperty("Fibers")[0];
        Require(CapturedVariables(pending).GetProperty("512").GetInt32() == 11 && !CapturedVariables(pending).TryGetProperty("513", out _), "PREQSW waited for target completion instead of continuing when its idle target started.");
        Require(caller.GetProperty("WaitReason").GetString() == nameof(WaitReason.None) && caller.GetProperty("RequestSequence").ValueKind == JsonValueKind.Null, "An immediately started PREQSW installed an invented caller completion wait.");
        accepted.Step();
        accepted.Step();
        string completed = accepted.Capture().GetRawText();
        Require(CapturedVariables(accepted.Capture()).GetProperty("513").GetInt32() == 7, "PREQSW target failed to finish independently.");
        accepted.Restore(pending);
        accepted.Step();
        accepted.Step();
        Require(completed == accepted.Capture().GetRawText(), "Restoring an independent PREQSW target changed its completion.");

        foreach ((int slot, int priority) in new[] { (1, 1), (0, 0) })
        {
            EventEngine ignored = CreatePartyRequestFixture(slot, priority, true, Jsm.Opcode.PREQSW);
            ignored.Step();
            ignored.Step();
            Require(CapturedVariables(ignored.Capture()).GetProperty("512").GetInt32() == 11 && ignored.Capture().GetProperty("Fibers").GetArrayLength() == 2, "PREQSW absent party slot or occupied same-priority slot was not consumed and dropped.");
        }

        EventEngine conflicting = CreatePartyRequestFixture(0, 1, true, Jsm.Opcode.PREQSW);
        conflicting.Step();
        try
        {
            conflicting.Step();
            throw new InvalidOperationException("Unsupported PREQSW competing target was preempted.");
        }
        catch (EventScriptException) when (conflicting.LastFault?.Opcode == nameof(Jsm.Opcode.PREQSW))
        {
        }
        JsonElement frozen = conflicting.Capture();
        caller = frozen.GetProperty("Fibers")[0];
        Require(caller.GetProperty("Position").GetInt32() == 7 && caller.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 1, 1 }), "Unsupported PREQSW preemption consumed original operands or changed its PC.");
        conflicting.Restore(frozen);
        try
        {
            conflicting.Step();
            throw new InvalidOperationException("A restored PREQSW competing-target boundary resumed.");
        }
        catch (EventScriptException) when (conflicting.LastFault?.Opcode == nameof(Jsm.Opcode.PREQSW))
        {
        }
        Require(frozen.GetRawText() == conflicting.Capture().GetRawText(), "PREQSW preemption boundary failed exact frozen restore.");

        foreach ((int slot, int priority) in new[] { (3, 1), (0, 8), (0, 0) })
        {
            EventEngine invalid = CreatePartyRequestFixture(slot, priority, false, Jsm.Opcode.PREQSW);
            invalid.Step();
            try
            {
                invalid.Step();
                throw new InvalidOperationException("Unsupported PREQSW slot/priority executed.");
            }
            catch (EventScriptException) when (invalid.LastFault?.Opcode == nameof(Jsm.Opcode.PREQSW))
            {
            }
            caller = invalid.Capture().GetProperty("Fibers")[0];
            Require(caller.GetProperty("Position").GetInt32() == 7 && caller.GetProperty("Stack").GetArrayLength() == 2, "Invalid PREQSW bounds consumed original operands or changed its PC.");
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
