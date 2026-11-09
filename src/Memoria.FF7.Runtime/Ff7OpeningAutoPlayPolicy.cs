using System.Numerics;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

/// <summary>Original opening story goals expressed solely through physical field controls.</summary>
public sealed class Ff7OpeningAutoPlayPolicy(Ff7Session session)
{
    private long generation = -1;
    private int stage;

    private bool EscapingFirstReactor => session.Progress.CompletedBattles.Any(receipt => receipt.Formation == 324);

    private ushort ConfirmPulse => session.Host.Process.Tick % 16 == 0 ? (ushort)32 : (ushort)0;

    private ushort ConfirmOnContact(Ff7Line line) => line.Push || line.Touch || line.OnLine ? ConfirmPulse : (ushort)0;

    public string Goal => "Reach the church through the original story sequence";

    public int? ChooseDialogueSelection(Ff7Dialogue dialogue)
    {
        if (session.Field.Name == "mds7pb_1" && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 105 && dialogue.Text == 112 && dialogue.FirstChoice == 2 && dialogue.LastChoice == 3)
        {
            return 3;
        }
        if (session.Field.Name == "tunnel_2" && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 108 && dialogue.Text == 14 && dialogue.FirstChoice == 1 && dialogue.LastChoice == 3)
        {
            return 2;
        }
        return null;
    }

    public Ff7AutoplayInput ChooseInput(Ff7AutoplayDriver driver)
    {
        Ff7Actor? player = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is null)
        {
            return default;
        }
        if (generation != session.FieldGeneration)
        {
            generation = session.FieldGeneration;
            stage = 0;
        }
        if (player.Ladder is { } ladder)
        {
            return ladder.Completed ? default : driver.Steer(new(player.X4096 >> 12, player.Y4096 >> 12, player.Z4096 >> 12));
        }
        if (player.Jump is not null)
        {
            return default;
        }
        if (session.Host.Process.PlayerLocked || session.Host.Process.Join is not null || session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed && session.Host.CanConfirmDialogue(dialogue.RequestId)))
        {
            return default;
        }
        if (session.Host.Process.Tick == 0)
        {
            return default;
        }
        int x = player.X4096 >> 12;
        int y = player.Y4096 >> 12;
        int z = player.Z4096 >> 12;
        switch (session.Field.Name)
        {
            case "md1stin":
                return driver.Gateway(117);
            case "md1_1":
            case "md8_4":
            case "rootmap":
                return default;
            case "tin_3":
                return session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x15) >= 1 ? driver.TalkTo(15) : default;
            case "tunnel_1":
                if (session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0x7F) != 6)
                {
                    throw new NotSupportedException("FFVII autoplay has reached an unproved tunnel entry handoff.");
                }
                Ff7Line forwardTunnelExit = session.Host.Process.Lines.Single(line => line.Entity == 2);
                return driver.Approach(new((forwardTunnelExit.X1 + forwardTunnelExit.X2) / 2f, (forwardTunnelExit.Y1 + forwardTunnelExit.Y2) / 2f, forwardTunnelExit.Z1));
            case "tunnel_2":
                Ff7Line serviceCover = session.Host.Process.Lines.Single(line => line.Entity == 5);
                return driver.Approach(new((serviceCover.X1 + serviceCover.X2) / 2f, (serviceCover.Y1 + serviceCover.Y2) / 2f, serviceCover.Z1));
            case "sbwy4_1":
                Ff7Line serviceExit = session.Host.Process.Lines.Single(line => line.Entity == 2);
                return driver.Approach(new((serviceExit.X1 + serviceExit.X2) / 2f, (serviceExit.Y1 + serviceExit.Y2) / 2f, serviceExit.Z1));
            case "sbwy4_4":
                Ff7Line passageExit = session.Host.Process.Lines.Single(line => line.Entity == 5);
                return driver.Approach(new((passageExit.X1 + passageExit.X2) / 2f, (passageExit.Y1 + passageExit.Y2) / 2f, passageExit.Z1));
            case "sbwy4_5":
                FieldEntitySnapshot passageEntry = session.Machine.Capture().Entities[player.Entity];
                if (passageEntry.Priority == 7 && passageEntry.ProgramCounter >= session.Field.Scripts.Entities[player.Entity].ScriptOffsets[0] && passageEntry.ProgramCounter < session.Field.Scripts.Entities[player.Entity].ScriptOffsets[1] && session.Field.Scripts.Data.Span[passageEntry.ProgramCounter] != 0)
                {
                    return default;
                }
                Ff7Line reactorLadder = session.Host.Process.Lines.Single(line => line.Entity == 4);
                return driver.Approach(new((reactorLadder.X1 + reactorLadder.X2) / 2f, (reactorLadder.Y1 + reactorLadder.Y2) / 2f, reactorLadder.Z1)) with { Held = ConfirmPulse };
            case "sbwy4_3":
                Ff7Line forwardServiceLadder = session.Host.Process.Lines.Single(line => line.Entity == 2);
                return driver.Approach(new((forwardServiceLadder.X1 + forwardServiceLadder.X2) / 2f, (forwardServiceLadder.Y1 + forwardServiceLadder.Y2) / 2f, forwardServiceLadder.Z1)) with { Held = ConfirmPulse };
            case "sbwy4_6":
                FieldEntitySnapshot reactorEntry = session.Machine.Capture().Entities[player.Entity];
                if (reactorEntry.Priority == 7 && reactorEntry.ProgramCounter >= session.Field.Scripts.Entities[player.Entity].ScriptOffsets[0] && reactorEntry.ProgramCounter < session.Field.Scripts.Entities[player.Entity].ScriptOffsets[1] && session.Field.Scripts.Data.Span[reactorEntry.ProgramCounter] != 0)
                {
                    return default;
                }
                Ff7Line reactorEntryLadder = session.Host.Process.Lines.Single(line => line.Entity == 3);
                return driver.Approach(new((reactorEntryLadder.X1 + reactorEntryLadder.X2) / 2f, (reactorEntryLadder.Y1 + reactorEntryLadder.Y2) / 2f, reactorEntryLadder.Z1)) with { Held = ConfirmPulse };
            case "smkin_2":
                if (session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) != 117)
                {
                    throw new NotSupportedException("FFVII autoplay has reached the next second-reactor story handoff.");
                }
                if (z > 1700)
                {
                    Ff7Line reactorPipe = session.Host.Process.Lines.Single(line => line.Entity == 4);
                    return driver.Approach(new((reactorPipe.X1 + reactorPipe.X2) / 2f, (reactorPipe.Y1 + reactorPipe.Y2) / 2f, reactorPipe.Z1));
                }
                return driver.Gateway(130);
            case "smkin_3":
                if (session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) != 117)
                {
                    throw new NotSupportedException("FFVII autoplay has reached the next second-reactor ladder handoff.");
                }
                if (z > 2000)
                {
                    Ff7Line upperRoute = session.Host.Process.Lines.Single(line => line.Entity == (x < 0 ? 2 : 4));
                    return driver.Approach(new((upperRoute.X1 + upperRoute.X2) / 2f, (upperRoute.Y1 + upperRoute.Y2) / 2f, upperRoute.Z1)) with { Held = ConfirmOnContact(upperRoute) };
                }
                if (z > 1000)
                {
                    Ff7Line lowerRoute = session.Host.Process.Lines.Single(line => line.Entity == 6);
                    return driver.Approach(new((lowerRoute.X1 + lowerRoute.X2) / 2f, (lowerRoute.Y1 + lowerRoute.Y2) / 2f, lowerRoute.Z1)) with { Held = ConfirmOnContact(lowerRoute) };
                }
                return driver.Gateway(131);
            case "smkin_4":
                if (session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) != 117)
                {
                    throw new NotSupportedException("FFVII autoplay has reached the next second-reactor save-area handoff.");
                }
                if (z > 0)
                {
                    Ff7Line saveAreaDescent = session.Host.Process.Lines.Single(line => line.Entity == 6);
                    return driver.Approach(new((saveAreaDescent.X1 + saveAreaDescent.X2) / 2f, (saveAreaDescent.Y1 + saveAreaDescent.Y2) / 2f, saveAreaDescent.Z1));
                }
                return driver.Gateway(132);
            case "smkin_5":
                int coreStory = session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);
                if (coreStory == 123)
                {
                    int plantTriangle = session.Machine.Variables.ReadWord(FieldBank.MapWord, 7);
                    if ((uint)plantTriangle >= session.Field.Walkmesh.Count)
                    {
                        throw new InvalidDataException("FFVII reactor plant trigger requires its original walkmesh triangle.");
                    }
                    var plantFace = session.Field.Walkmesh[plantTriangle];
                    return driver.Approach((plantFace.A + plantFace.B + plantFace.C) / 3, plantTriangle);
                }
                if (coreStory != 117)
                {
                    throw new NotSupportedException("FFVII autoplay has reached the next second-reactor core handoff.");
                }
                Ff7Line coreEvent = session.Host.Process.Lines.Single(line => line.Entity == 6);
                return driver.Approach(new((coreEvent.X1 + coreEvent.X2) / 2f, (coreEvent.Y1 + coreEvent.Y2) / 2f, coreEvent.Z1));
            case "tin_1":
                if (session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 108)
                {
                    Ff7Line monitor = session.Host.Process.Lines.Single(line => line.Entity == 27);
                    if (monitor.Enabled == 1)
                    {
                        return driver.Approach(new((monitor.X1 + monitor.X2) / 2f, (monitor.Y1 + monitor.Y2) / 2f, monitor.Z1));
                    }
                    Ff7Line carriageDoor = session.Host.Process.Lines.Single(line => line.Entity == 26);
                    return driver.Approach(new((carriageDoor.X1 + carriageDoor.X2) / 2f, (carriageDoor.Y1 + carriageDoor.Y2) / 2f, carriageDoor.Z1));
                }
                if (session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) >= 108)
                {
                    throw new NotSupportedException("FFVII autoplay has reached the second-mission carriage handoff.");
                }
                if (session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) < 51)
                {
                    return default;
                }
                return driver.TalkTo((session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0xDF) & 4) != 0 ? 31 : 34);
            case "cargoin":
                return session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) >= 48 ? driver.Steer(new(20, -140, 0)) : default;
            case "tin_2":
                Ff7Line secondCarDoor = session.Host.Process.Lines.Single(line => line.Entity == 18);
                return driver.Approach(new((secondCarDoor.X1 + secondCarDoor.X2) / 2f, (secondCarDoor.Y1 + secondCarDoor.Y2) / 2f, secondCarDoor.Z1));
            case "tin_4":
                Ff7Line repeatedCarDoor = session.Host.Process.Lines.Single(line => line.Entity == 15);
                return driver.Approach(new((repeatedCarDoor.X1 + repeatedCarDoor.X2) / 2f, (repeatedCarDoor.Y1 + repeatedCarDoor.Y2) / 2f, repeatedCarDoor.Z1));
            case "mds7st3":
                if (session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 108)
                {
                    return driver.Approach(new(-2880, 3370, 101));
                }
                if (session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) >= 108)
                {
                    throw new NotSupportedException("FFVII autoplay has reached the second-mission train boarding handoff.");
                }
                return driver.Gateway(156);
            case "mds7plr1":
                return driver.Gateway(session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 108 ? 146 : 151);
            case "mds7":
                if (session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 108)
                {
                    return driver.Gateway(156);
                }
                return driver.Approach(new(300, 430, 76));
            case "mds7pb_1":
                int pubStory = session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);
                if (pubStory is 69 or 78 or 108)
                {
                    return driver.Gateway(151);
                }
                if (pubStory == 72)
                {
                    return driver.Approach(new(-30, 180, 0)) with { Held = ConfirmPulse };
                }
                if (pubStory == 105)
                {
                    return driver.TalkTo((session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0xD6) & 0x80) == 0 ? 20 : 19);
                }
                if (pubStory < 69)
                {
                    return default;
                }
                throw new NotSupportedException($"FFVII autoplay has reached the next pub story handoff at native story {pubStory}.");
            case "mds7pb_2":
                if (session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 72)
                {
                    return driver.Approach(new(12, 168, 0));
                }
                if (session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) is 78 or 105)
                {
                    return driver.Approach(new(142, -40, 0)) with { Held = ConfirmPulse };
                }
                throw new NotSupportedException("FFVII autoplay has reached the next basement story handoff.");
            case "md1_2":
                return driver.Gateway(119);
            case "md8_1":
                return driver.Gateway(134);
            case "md8_2":
                return driver.Gateway(135);
            case "md8_3":
                return driver.Gateway(137);
            case "md8brdg":
                return driver.Approach(new(-430, 0, 518));
            case "nrthmk":
                return driver.Gateway(EscapingFirstReactor ? 136 : 120);
            case "nmkin_1":
                if (EscapingFirstReactor)
                {
                    var escapeVm = session.Machine.Capture();
                    if (session.Host.Process.Motions.Length != 0 || escapeVm.Entities[9].Priority < 7 || escapeVm.Entities[10].Priority < 7)
                    {
                        return default;
                    }
                    if (session.Host.GetActor(10).TalkEnabled && session.Host.GetActor(10).Y4096 >> 12 > 3300)
                    {
                        return driver.TalkTo(10);
                    }
                    if (session.Host.GetActor(9).TalkEnabled && session.Host.GetActor(9).Y4096 >> 12 > 2800)
                    {
                        return driver.TalkTo(9);
                    }
                    return driver.Gateway(119);
                }
                Ff7Actor biggs = session.Host.Process.Actors.Single(actor => actor.Entity == 9);
                Ff7Actor jessie = session.Host.Process.Actors.Single(actor => actor.Entity == 10);
                if (y < 2500)
                {
                    return driver.TalkTo(biggs.Entity);
                }
                if (stage == 0 && y < 2900)
                {
                    if (x > -1740)
                    {
                        return driver.Steer(new(-1770, 2740, z));
                    }
                    stage = 1;
                }
                if (stage == 1 && y < 2940)
                {
                    return driver.Steer(new(-1770, 2950, z));
                }
                if (jessie.TalkEnabled)
                {
                    return driver.TalkTo(jessie.Entity);
                }
                return driver.Gateway(121);
            case "elevtr1":
                if (session.Host.Process.Actors.Single(actor => actor.Entity == 6).TalkEnabled || EscapingFirstReactor && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xE1) & 1) == 0)
                {
                    return driver.Approach(new(99, 51, 5), 8) with { Held = ConfirmPulse };
                }
                return driver.Approach(new(-250, 0, 5), 0);
            case "nmkin_2":
                return driver.Gateway(EscapingFirstReactor ? 121 : 123);
            case "nmkin_3":
                if (EscapingFirstReactor)
                {
                    if (z < 1000)
                    {
                        return driver.Steer(new(265, 1450, 860)) with { Held = ConfirmPulse, Talk = ConfirmPulse != 0 };
                    }
                    if (z < 1500)
                    {
                        return player.Triangle is 12 or 13 ? default : driver.Approach(new(-190, 1410, 1643), 6);
                    }
                    if (z < 2000)
                    {
                        Ff7Actor walkwayPickup = session.Host.Process.Actors.Single(actor => actor.Entity == 5);
                        if (walkwayPickup.TalkEnabled)
                        {
                            return driver.TalkTo(walkwayPickup.Entity);
                        }
                        Ff7Actor trappedGuide = session.Host.Process.Actors.Single(actor => actor.Entity == 4);
                        if (trappedGuide.TalkEnabled && (trappedGuide.Y4096 >> 12) < 1500)
                        {
                            return driver.TalkTo(trappedGuide.Entity);
                        }
                        return driver.Steer(new(-200, 1780, 1641)) with { Held = ConfirmPulse, Talk = ConfirmPulse != 0 };
                    }
                    return x > -200 ? driver.Steer(new(-250, 1920, 2213)) : driver.Gateway(122);
                }
                if (z > 2000)
                {
                    if (x < 100)
                    {
                        return driver.Steer(new(175, 1920, 2213));
                    }
                    Ff7Actor ladderGuide = session.Host.Process.Actors.Single(actor => actor.Entity == 4);
                    if (ladderGuide.Z4096 >> 12 > 2200)
                    {
                        return driver.TalkTo(ladderGuide.Entity);
                    }
                    return driver.Steer(new(210, 1830, 1642)) with { Held = ConfirmPulse, Talk = true };
                }
                if (z >= 1500)
                {
                    return driver.Approach(new(265, 1510, 1321));
                }
                if (z > 1000)
                {
                    return driver.Steer(new(265, 1450, 1321)) with { Held = ConfirmPulse, Talk = true };
                }
                return driver.Gateway(124);
            case "nmkin_4":
                if (EscapingFirstReactor)
                {
                    return z < 0 ? driver.Approach(new(-245, 1950, -185)) with { Held = ConfirmPulse, Talk = ConfirmPulse != 0 } : driver.Gateway(123);
                }
                if (z > 0)
                {
                    return driver.Approach(new(-380, 1990, 640));
                }
                return driver.Gateway(125);
            case "nmkin_5":
                if (EscapingFirstReactor)
                {
                    return driver.Gateway(124);
                }
                if (session.Host.Process.Actors.Single(actor => actor.Entity == 6).TalkEnabled || EscapingFirstReactor && stage == 0)
                {
                    return driver.TalkTo(6);
                }
                return driver.Approach(new(-90, -1600, -184)) with { Held = ConfirmPulse, Talk = ConfirmPulse != 0 };
            default:
                throw new NotSupportedException($"FFVII autoplay has reached the next unsupported native story goal in field '{session.Field.Name}'.");
        }
    }
}
