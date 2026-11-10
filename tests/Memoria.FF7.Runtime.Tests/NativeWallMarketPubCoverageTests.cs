using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWallMarketPubCoverageTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WALL_MARKET_PUB_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native pub coverage: select original resources and a genuine market medicine continuation.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(stream);
        Ff7SessionSave saved = envelope.Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoplayDriver coldDriver = new(cold);
        (int Entity, int Pc)[] residents = [(12, 0x09A4), (13, 0x09FF), (14, 0x0C19), (15, 0x0C9D), (16, 0x0D1E)];
        HashSet<(int Entity, int Pc)> returns = [];
        int leaves = 0;
        bool acceptedClosedLeave = false;
        bool acceptedOpenLeave = false;
        int knocks = 0;
        int rewardReturns = 0;
        int perfumeTransitions = 0;
        bool hadPerfume = (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x41) & 8) != 0;
        bool rewardText = false;
        bool rewardDisabledLine = false;
        bool postRewardChecked = false;
        bool declineSelected = false;
        bool declineReturned = false;
        session.InteractionScriptRequested += (entity, script, accepted) =>
        {
            if (entity == 4 && script == 6)
            {
                if (accepted)
                {
                    bool wasOpen = session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x0C) == 1;
                    acceptedClosedLeave |= !wasOpen;
                    acceptedOpenLeave |= wasOpen;
                }
                FieldEntitySnapshot owner = session.Machine.Capture().Entities[4];
                Console.WriteLine($"PUB_LEAVE_ADMISSION tick={session.Machine.Tick} accepted={accepted} priority={owner.Priority} pc={owner.ProgramCounter:X4}");
            }
        };
        session.NativeTickCompleted += observation =>
        {
            if (observation.Field != "mktpb")
            {
                return;
            }
            foreach (var returned in observation.ScriptReturns.Where(value => value.Priority == 1))
            {
                returns.Add((returned.Entity, returned.ProgramCounter));
                if (returned.Entity == 4 && returned.ProgramCounter == 0x06BB)
                {
                    Check(session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x0C) == 0, "The native leave callback did not finish with its door closed.");
                    leaves++;
                    Console.WriteLine($"PUB_LEAVE_RETURN {observation.Tick} count={leaves}");
                }
                if (returned.Entity == 4 && returned.ProgramCounter == 0x069A)
                {
                    Check(session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x0C) == 1, "The native knock callback did not finish with its door open.");
                    knocks++;
                    Console.WriteLine($"PUB_KNOCK_RETURN {observation.Tick} count={knocks}");
                }
                if (returned.Entity == 11 && returned.ProgramCounter == 0x091F)
                {
                    int reward = session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA0) & 7;
                    declineReturned |= declineSelected && reward == 0;
                    if (reward == 4)
                    {
                        rewardReturns++;
                    }
                }
            }
        };
        bool continuation = saved.FieldName == "mktpb";
        int requiredLeaves = continuation ? 1 : 2;
        int phase = continuation ? 2 : 0;
        bool entered = continuation;
        bool previewPure = false;
        int callbacks = 0;
        int callbackLimit = continuation ? 600 : 2000;
        for (; callbacks < callbackLimit; callbacks++)
        {
            if (entered && session.Field.Name == "mrkt1")
            {
                break;
            }
            Ff7AutoplayInput command = default;
            if (session.Field.Name == "mrkt2")
            {
                command = driver.Gateway(205);
            }
            else if (session.Field.Name == "mrkt1")
            {
                command = driver.Gateway(204);
            }
            else
            {
                Check(session.Field.Name == "mktpb", "The native pub route entered an unexpected field.");
                entered = true;
                if (session.Machine.Tick >= 12)
                {
                    Ff7Line line = session.Host.Process.Lines.Single(value => value.Entity == 4);
                    Vector3 midpoint = new((line.X1 + line.X2) / 2f, (line.Y1 + line.Y2) / 2f, 0);
                    bool idle = session.Machine.Capture().Entities[4].Priority == 7 && !session.Host.Process.PlayerLocked;
                    if (!previewPure)
                    {
                        string before = JsonSerializer.Serialize(session.Capture());
                        driver.Approach(midpoint);
                        driver.Approach(midpoint);
                        Check(before == JsonSerializer.Serialize(session.Capture()), "Pub door navigation preview changed native state.");
                        CheckCatalog(session, rewarded: false);
                        previewPure = true;
                    }
                    if (phase == 0 && line.Touch && idle)
                    {
                        phase = 1;
                    }
                    if (phase == 1 && leaves >= 1)
                    {
                        phase = 2;
                    }
                    if (phase == 2 && knocks >= 1 && idle)
                    {
                        phase = 3;
                    }
                    if (phase == 3 && line.Touch && idle)
                    {
                        phase = 4;
                    }
                    if (phase == 4 && leaves >= requiredLeaves)
                    {
                        phase = 5;
                    }
                    int target = residents.Where(value => session.Host.Process.Actors.Any(actor => actor.Entity == value.Entity && actor.Visible && actor.TalkEnabled) && !returns.Contains(value)).Select(value => value.Entity).FirstOrDefault(-1);
                    if (phase == 5 && target < 0)
                    {
                        phase = 6;
                    }
                    if (phase == 6 && knocks >= 2 && idle)
                    {
                        phase = 7;
                    }
                    if (phase == 7 && declineReturned)
                    {
                        phase = 8;
                    }
                    if (phase == 8 && rewardReturns > 0 && !session.Host.Process.PlayerLocked)
                    {
                        Check(session.Host.Process.Lines.Single(value => value.Entity == 4).Enabled == 1, "The native reward did not restore the pub door line.");
                        Check(session.Host.Process.Actors.Single(value => value.Entity == 11) is { Visible: false, TalkEnabled: false }, "The rewarded woman remained available in the pub.");
                        Check(session.Host.Process.Actors.Single(value => value.Entity == 12) is { Visible: true, TalkEnabled: true, X4096: -640 * 4096, Y4096: 409 * 4096, Triangle: 148 }, "The original reward did not finish the man's movement.");
                        CheckCatalog(session, rewarded: true);
                        postRewardChecked = true;
                        phase = 9;
                    }
                    command = phase switch
                    {
                        0 or 3 => driver.Approach(midpoint),
                        1 or 4 => idle ? driver.LeaveLine(4) : default,
                        2 or 6 => driver.TalkToLine(4, run: false),
                        5 => driver.TalkTo(target),
                        7 or 8 => driver.TalkTo(11),
                        9 => driver.Gateway(205),
                        _ => throw new InvalidOperationException("Unknown pub proof phase.")
                    };
                }
            }
            foreach (Ff7Session current in new[] { session, cold })
            {
                rewardText |= current.Field.Name == "mktpb" && current.Host.Process.Dialogues.Any(value => !value.Completed && value.Text == 12);
                foreach (var dialogue in current.Host.Process.Dialogues.Where(value => !value.Completed && value.AwaitingInput && current.Host.CanConfirmDialogue(value.RequestId)).ToArray())
                {
                    Ff7Dialogue chosen = dialogue;
                    if (dialogue.FirstChoice is not null)
                    {
                        Check(dialogue.Text == 4 && dialogue.FirstChoice == 0 && dialogue.LastChoice == 1, "An unreviewed pub question was reached.");
                        int selection = phase >= 8 ? 0 : 1;
                        chosen = Ff7DialogueCursor.Select(dialogue, selection);
                        if (ReferenceEquals(current, session))
                        {
                            declineSelected |= selection == 1;
                            Console.WriteLine($"PUB_ASK tick={session.Machine.Tick} text=4 selected={selection}");
                        }
                    }
                    current.Host.UpdateDialogue(Ff7DialogueCursor.Confirm(chosen, FieldText.Read(current.Field.Scripts.Texts[dialogue.Text].Span)));
                }
            }
            driver.Apply(command);
            coldDriver.Apply(command);
            session.AdvanceTick();
            cold.AdvanceTick();
            Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The native pub continuation diverged after cold restore.");
            bool hasPerfume = (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x41) & 8) != 0;
            if (hasPerfume && !hadPerfume)
            {
                perfumeTransitions++;
            }
            hadPerfume = hasPerfume;
            rewardDisabledLine |= hasPerfume && session.Field.Name == "mktpb" && session.Host.Process.Lines.Single(value => value.Entity == 4).Enabled == 0;
        }
        Ff7Actor finalPlayer = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Console.WriteLine($"PUB_FINAL callbacks={callbacks} phase={phase} field={session.Field.Name} tick={session.Machine.Tick} player={finalPlayer.X4096 / 4096f},{finalPlayer.Y4096 / 4096f}/triangle{finalPlayer.Triangle} leaves={leaves} knocks={knocks} declineSelected={declineSelected} declineReturned={declineReturned} returns={string.Join(';', returns.OrderBy(value => value.Entity).ThenBy(value => value.Pc).Select(value => $"{value.Entity}:{value.Pc:X4}"))}");
        if (session.Field.Name == "mktpb")
        {
            Console.WriteLine("PUB_DOOR_OWNER " + JsonSerializer.Serialize(session.Machine.Capture().Entities[4]));
            Console.WriteLine("PUB_DOOR_LINE " + JsonSerializer.Serialize(session.Host.Process.Lines.Single(value => value.Entity == 4)));
        }
        string? diagnostic = Environment.GetEnvironmentVariable("MEMORIA_FF7_WALL_MARKET_PUB_DIAGNOSTIC");
        if (session.Field.Name == "mktpb" && !string.IsNullOrWhiteSpace(diagnostic))
        {
            Check(!string.Equals(Path.GetFullPath(diagnostic), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase), "The pub diagnostic must not replace its source.");
            using FileStream output = File.Create(diagnostic);
            CombinedSessionCodec.Write(output, envelope with { Ff7 = session.Capture() });
        }
        Check(entered && session.Field.Name == "mrkt1" && leaves >= requiredLeaves && previewPure && declineReturned, "The native pub sequence did not complete within its selected callback bound.");
        Check(acceptedOpenLeave && (continuation || acceptedClosedLeave), "The native pub sequence skipped a required door leave branch.");
        Check(residents.All(returns.Contains), "The native pub sequence skipped a required current resident.");
        Check((session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA0) & 7) == 4 && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA2) & 4) == 0, "The legitimate Digestive outcome is missing.");
        Check((session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x41) & 8) != 0 && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x42) & 8) == 0, "The original perfume/medicine key-item bits are inconsistent.");
        Check(postRewardChecked && rewardText && rewardDisabledLine && perfumeTransitions == 1 && rewardReturns == 1, "The pub did not complete exactly one native Sexy Cologne reward.");
        Console.WriteLine($"PASS native pub finite door sequence, residents, medicine and return: {callbacks} cold-equal callbacks.");
        int revisitKnock = knocks + 1;
        int revisitLeave = leaves + 1;
        bool revisited = false;
        bool departure = false;
        bool revisitDoorText = false;
        int revisitCallbacks = 0;
        for (; revisitCallbacks < 1200; revisitCallbacks++)
        {
            if (departure && session.Field.Name == "mrkt1")
            {
                break;
            }
            Ff7AutoplayInput command = session.Field.Name == "mrkt1" ? driver.Gateway(204) : default;
            if (session.Field.Name == "mktpb" && session.Machine.Tick >= 12)
            {
                Ff7Line line = session.Host.Process.Lines.Single(value => value.Entity == 4);
                if (!revisited)
                {
                    Check(line.Enabled == 1 && session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x0C) == 0, "The original pub revisit must initialize an enabled, closed door.");
                    Check(session.Host.Process.Actors.Single(value => value.Entity == 11) is { Visible: false, TalkEnabled: false }, "The native revisit exposed a claimed medicine reward.");
                    Check(session.Host.Process.Actors.Single(value => value.Entity == 12) is { Visible: true, TalkEnabled: true, X4096: -625 * 4096, Y4096: 405 * 4096, Triangle: 148 }, "The native revisit did not select the rewarded man's placement.");
                    CheckCatalog(session, rewarded: true);
                    revisited = true;
                }
                if (knocks < revisitKnock)
                {
                    command = driver.TalkToLine(4, run: false);
                }
                else if (leaves < revisitLeave)
                {
                    command = driver.LeaveLine(4, run: false);
                }
                else
                {
                    departure = true;
                    command = driver.Gateway(205);
                }
            }
            foreach (Ff7Session current in new[] { session, cold })
            {
                foreach (Ff7Dialogue dialogue in current.Host.Process.Dialogues.Where(value => !value.Completed).ToArray())
                {
                    Check(dialogue.FirstChoice is null && dialogue.Text != 12, "The rewarded pub revisit repeated a medicine offer or reward.");
                    revisitDoorText |= dialogue.Text == 14;
                    if (dialogue.AwaitingInput && current.Host.CanConfirmDialogue(dialogue.RequestId))
                    {
                        current.Host.UpdateDialogue(Ff7DialogueCursor.Confirm(dialogue, FieldText.Read(current.Field.Scripts.Texts[dialogue.Text].Span)));
                    }
                }
            }
            driver.Apply(command);
            coldDriver.Apply(command);
            session.AdvanceTick();
            cold.AdvanceTick();
            Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The native pub revisit diverged after cold restore.");
        }
        Check(revisited && session.Field.Name == "mrkt1" && revisitDoorText && knocks == revisitKnock && leaves == revisitLeave && rewardReturns == 1, "The native pub revisit did not finish its current door callbacks and return.");
        Check((session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA0) & 7) == 4 && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA2) & 4) == 0 && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x41) & 8) != 0 && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x42) & 8) == 0, "The original pub revisit changed the claimed medicine outcome.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The pub proof changed its source.");
        Console.WriteLine($"PASS native post-reward pub revisit and return: {revisitCallbacks} cold-equal callbacks.");
    }

    public static void RunOutcome()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WALL_MARKET_PUB_SAVE");
        Check(!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(path), "Select the genuine pending pub outcome continuation.");
        byte[] source = File.ReadAllBytes(path!);
        using MemoryStream stream = new(source);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(stream);
        Ff7SessionSave saved = envelope.Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Check(session.Field.Name == "mktpb", "The outcome source must already be inside the pub.");
        Ff7Dialogue offer = session.Host.Process.Dialogues.Last(value => value.Text == 4);
        Check(offer.Selected == 0 && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA2) & 4) != 0 && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA0) & 7) == 0, "The source is not the original pending legitimate medicine offer.");
        Check((session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x41) & 8) == 0 && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x42) & 8) != 0, "The pending source must own the medicine and not already own its perfume reward.");
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoplayDriver coldDriver = new(cold);
        int womanReturns = 0;
        int perfumeTransitions = 0;
        bool priorPerfume = false;
        bool rewardText = false;
        session.NativeTickCompleted += observation => womanReturns += observation.ScriptReturns.Count(value => observation.Field == "mktpb" && value.Entity == 11 && value.Priority == 1 && value.ProgramCounter == 0x091F);
        int callbacks = 0;
        for (; callbacks < 1200 && session.Field.Name == "mktpb"; callbacks++)
        {
            foreach (Ff7Session current in new[] { session, cold })
            {
                foreach (var dialogue in current.Host.Process.Dialogues.Where(value => !value.Completed).ToArray())
                {
                    rewardText |= dialogue.Text == 12;
                    Check(dialogue.FirstChoice is null || dialogue.Text == 4 && dialogue.Selected == 0, "An unexpected question replaced the pending native offer.");
                    if (dialogue.AwaitingInput && current.Host.CanConfirmDialogue(dialogue.RequestId))
                    {
                        current.Host.UpdateDialogue(Ff7DialogueCursor.Confirm(dialogue, FieldText.Read(current.Field.Scripts.Texts[dialogue.Text].Span)));
                    }
                }
            }
            Ff7AutoplayInput command = womanReturns > 0 && !session.Host.Process.PlayerLocked ? driver.Gateway(205) : default;
            driver.Apply(command);
            coldDriver.Apply(command);
            session.AdvanceTick();
            cold.AdvanceTick();
            Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The original pub outcome diverged after cold restore.");
            bool perfume = (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x41) & 8) != 0;
            if (perfume && !priorPerfume)
            {
                perfumeTransitions++;
            }
            priorPerfume = perfume;
        }
        Console.WriteLine($"PUB_OUTCOME callbacks={callbacks} field={session.Field.Name} generation={session.FieldGeneration} tick={session.Machine.Tick} womanReturns={womanReturns} perfumeTransitions={perfumeTransitions} rewardText={rewardText}");
        if (session.Field.Name == "mktpb" && Environment.GetEnvironmentVariable("MEMORIA_FF7_WALL_MARKET_PUB_DIAGNOSTIC") is string diagnostic)
        {
            Check(!string.Equals(Path.GetFullPath(diagnostic), Path.GetFullPath(path!), StringComparison.OrdinalIgnoreCase), "The pub outcome diagnostic must not replace its source.");
            using FileStream output = File.Create(diagnostic);
            CombinedSessionCodec.Write(output, envelope with { Ff7 = session.Capture() });
        }
        Check(session.Field.Name == "mrkt1" && womanReturns == 1 && perfumeTransitions == 1 && rewardText, "The original pub outcome or205 return did not complete within the bound.");
        Check((session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA0) & 7) == 4 && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA2) & 4) == 0 && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x41) & 8) != 0 && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x42) & 8) == 0, "The native exclusive perfume gain or medicine removal is wrong.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path!))), "The pub outcome wrote its genuine source.");
        Console.WriteLine("PASS original pub medicine outcome, once-only reward, native return and every-callback cold equality.");
    }

    private static void CheckCatalog(Ff7Session session, bool rewarded)
    {
        Ff7FieldEventCoveragePlan plan = Ff7FieldEventCoverageCatalog.Inspect(session);
        Check(!plan.Complete, "The current pub catalog must not admit a nested visit before outcome-aware observers exist.");
        Check(plan.Events.Single(value => value.Id == "actor:11:script:1").Kind == (rewarded ? Ff7FieldEventCoverageKind.Inactive : Ff7FieldEventCoverageKind.Conditional), "The pub catalog exposed the wrong medicine availability.");
        Check(plan.Events.Single(value => value.Id == "actor:12:script:1").Kind == (rewarded ? Ff7FieldEventCoverageKind.Empty : Ff7FieldEventCoverageKind.Interaction), "The pub catalog exposed the wrong conversation for the man.");
        Check(plan.Events.Single(value => value.Id == "line:4:script:1").Kind == Ff7FieldEventCoverageKind.Conditional && plan.Events.Single(value => value.Id == "line:4:script:6").Kind == Ff7FieldEventCoverageKind.Conditional, "The enabled pub door must expose both native callback owners.");
        Check(plan.Events.Single(value => value.Id == "alternative:11:other-medicines").Kind == Ff7FieldEventCoverageKind.Inactive, "The legitimate Digestive visit exposed a mutually exclusive lower medicine outcome.");
        Check(plan.Events.Single(value => value.Kind == Ff7FieldEventCoverageKind.Exit).Id == "exit:map:205", "The pub catalog did not identify its sole native return.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
