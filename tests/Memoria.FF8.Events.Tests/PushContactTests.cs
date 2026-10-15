using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void GardenNativePushContactBoundary(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(137);
        JsmProgram program = JsmProgram.Read(field.Jsm);
        EventScript push = program.Objects[5].Scripts[3];
        Console.WriteLine($"Original137 actor5 push local3 label{push.Id}: {string.Join(", ", program.Operations.Skip(push.Start).Take(push.End - push.Start).Select(operation => $"{operation.Opcode}({operation.Parameter})"))}");
        EventEngine Create()
        {
            GameSession session = new(GameSession.Load(Path.Combine("../fixtures/external-fixture.json")));
            EventEngine result = new(session)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                ResolveWalkmesh = (x, y) => field.Walkmesh.TryPosition(x, y, out Vector3 point, out int triangle) ? new WalkmeshPosition(point, triangle) : null
            };
            result.LoadField(137, field.Jsm, field.Messages);
            result.Restore(session.State.EventState!.Value);
            return result;
        }
        EventEngine first = Create();
        Require(first.Frame == 265 && !first.Actors[5].CollisionEnabled && first.Actors[5].PushEnabled && !first.Actors[5].NativePushContactPending && push.Id == 40 && push.End - push.Start == 2 && program.Operations[push.Start].Opcode.ToString() == "LBL" && program.Operations[push.Start + 1].Opcode.ToString() == "RET", "Actual137 north contact source or original push script mapping changed.");
        List<ScriptCommand> commands = [];
        first.CommandExecuted += commands.Add;
        JsonElement? midPush = null;
        first.CommandExecuted += command =>
        {
            if (command.ObjectId == 5 && command.ScriptId == push.Id && command.Name == "LBL")
            {
                midPush = first.Capture();
                Require(midPush.Value.GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ObjectId").GetInt32() == 5 && fiber.GetProperty("ScriptId").GetInt32() == push.Id && fiber.GetProperty("Priority").GetInt32() == 6 && !fiber.GetProperty("Complete").GetBoolean()), "Native push request was dispatched at a different priority.");
            }
        };
        PlayerAction north = new(PlayerActionKind.Move, Vertical: 1);
        first.Apply(north);
        Require(first.Frame == 266 && first.LastFault is null && first.Actors[5].NativePushContactPending && !first.Actors[5].CollisionEnabled && first.Actors[5].PushEnabled && !commands.Any(command => command.ObjectId == 5 && command.ScriptId == push.Id), "THROUGHON/PUSHON did not preserve native delayed byte584 without a same-update push dispatch.");
        JsonElement contact = first.Capture();
        EventEngine resumed = Create();
        resumed.Restore(contact);
        Require(JsonElement.DeepEquals(contact, resumed.Capture()), "Persisted native push contact did not restore exactly.");
        List<ScriptCommand> restoredCommands = [];
        resumed.CommandExecuted += restoredCommands.Add;
        first.Apply(new PlayerAction(PlayerActionKind.Wait));
        resumed.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(JsonElement.DeepEquals(first.Capture(), resumed.Capture()) && !first.Actors[5].NativePushContactPending && commands.Any(command => command.ObjectId == 5 && command.ScriptId == push.Id) && restoredCommands.Any(command => command.ObjectId == 5 && command.ScriptId == push.Id), "Next ordinary update failed original push local3 dispatch or independent restored continuation.");
        Require(midPush is not null, "Original push local3/priority6 LBL was not observed.");
        first.Apply(north);
        Require(first.LastFault is null, "Repeated ordinary contact failed after the original idle push entry completed.");

        EventEngine refused = Create();
        EventEngine stationary = Create();
        stationary.Apply(new PlayerAction(PlayerActionKind.Move));
        Require(!stationary.Actors[5].NativePushContactPending, "Zero-axis idle movement invented a native actor contact.");
        JsonElement beforeGeometry = refused.Capture();
        ExpectRefusedMove(refused, north, beforeGeometry, "Geometry refusal committed a staged push flag or moved the native party.");
        EventEngine wall = Create();
        JsonNode wallVariant = JsonNode.Parse(wall.Capture().GetRawText())!;
        wallVariant["Actors"]![5]!["CollisionEnabled"] = true;
        using JsonDocument wallDocument = JsonDocument.Parse(wallVariant.ToJsonString());
        wall.Restore(wallDocument.RootElement);
        ExpectRefusedMove(wall, north, wall.Capture(), "Enabled actor wall partially committed push flag/movement before unsupported steering refusal.");
        EventEngine busy = Create();
        JsonNode busyVariant = JsonNode.Parse(busy.Capture().GetRawText())!;
        JsonArray fibers = busyVariant["Fibers"]!.AsArray();
        JsonNode template = fibers[0]!.DeepClone();
        template["ObjectId"] = 5;
        template["ScriptId"] = push.Id;
        template["Position"] = push.Start;
        template["Complete"] = false;
        template["Priority"] = 3;
        template["Sequence"] = busyVariant["Sequence"]!.GetValue<long>() + 1;
        template["Initialization"] = false;
        template["Started"] = false;
        template["WaitReason"] = 0;
        fibers.Add(template);
        busyVariant["Sequence"] = template["Sequence"]!.GetValue<long>();
        using JsonDocument busyDocument = JsonDocument.Parse(busyVariant.ToJsonString());
        busy.Restore(busyDocument.RootElement);
        busy.Apply(north);
        Require(busy.Actors[5].NativePushContactPending, "Competing-priority native push failed to retain its delayed contact.");
        busy.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(!busy.Actors[5].NativePushContactPending && busy.LastFault is null, "Competing-priority empty push failed native preemption and lower-slot continuation.");
        template["Priority"] = 6;
        busyVariant["Actors"]![5]!["NativePushContactPending"] = true;
        using JsonDocument occupiedDocument = JsonDocument.Parse(busyVariant.ToJsonString());
        busy.Restore(occupiedDocument.RootElement);
        long occupiedSequence = busy.Capture().GetProperty("Sequence").GetInt64();
        busy.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(!busy.Actors[5].NativePushContactPending && busy.Capture().GetProperty("Sequence").GetInt64() == occupiedSequence, "Occupied native push priority6 did not consume/drop the duplicate contact without another request.");
        Console.WriteLine("Original137 north PUSHON/THROUGHON contact: delayed local3/priority6 request, persisted byte584, independent next-update replay; geometry, collision and competing-priority refusals preserve full VM.");
    }

    private static void ExpectRefusedMove(EventEngine engine, PlayerAction action, JsonElement before, string message)
    {
        int actions = engine.ActionLog.Count;
        try
        {
            engine.Apply(action);
            throw new InvalidOperationException("Unsupported native contact context silently moved the player.");
        }
        catch (NotSupportedException)
        {
        }
        Require(JsonElement.DeepEquals(before, engine.Capture()) && engine.ActionLog.Count == actions, message);
    }
}
