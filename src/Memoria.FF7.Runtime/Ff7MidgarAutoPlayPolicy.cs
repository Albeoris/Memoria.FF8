using System.Numerics;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

/// <summary>Midgar story goals expressed through the shared native field controls.</summary>
public sealed partial class Ff7MidgarAutoPlayPolicy(Ff7Session session)
{
    public int? ChooseDialogueSelection(Ff7Dialogue dialogue)
    {
        if (Ff7FieldEventCoverageCatalog.RecognizesWallMarketInn(session) && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 188 && dialogue.Text == 1 && dialogue.FirstChoice == 2 && dialogue.LastChoice == 3)
        {
            return 3;
        }
        if (Ff7FieldEventCoverageCatalog.RecognizesWallMarketRestaurant(session) && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 188)
        {
            return (dialogue.Text, dialogue.FirstChoice, dialogue.LastChoice) switch
            {
                (14, 1, 2) => 1,
                (3, 2, 4) => 2,
                (16, 1, 3) => 2,
                _ => null
            };
        }
        if (Ff7FieldEventCoverageCatalog.RecognizesWallMarketItemShop(session) && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 188 && dialogue.Text == 3 && dialogue.FirstChoice == 0 && dialogue.LastChoice == 2)
        {
            return 2;
        }
        return (session.Field.Name, dialogue.Text, dialogue.FirstChoice, dialogue.LastChoice) switch
        {
            ("mds6_2", 2, 5, 6) => 5,
            ("mrkt3", 40, 1, 2) => 1,
            ("mkt_w", 8 or 11, int first, _) => first,
            ("eleout", 28 or 29 or 30, 0, _) when ReadFlags(0xE0) == 60 => 2,
            ("mktpb", 29 or 31, 2, 3) => 3,
            ("mkt_mens", 24, 1, 2) => 2,
            ("mkt_mens", 29, 2, 3) => 3,
            ("colne_4", 60, 1, 2) => 1,
            ("colne_b1", 146, 1, 2) => 1,
            ("colne_6", 39, 0, 2) => 1,
            ("mds5_5", 124, 0, 1) => 1,
            _ => null
        };
    }

    public bool TryChooseInput(Ff7AutoplayDriver driver, out Ff7AutoplayInput input)
    {
        input = default;
        string field = session.Field.Name;
        int story = session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);
        if (TryChooseShinraDeparture(driver, field, story, out input))
        {
            return true;
        }
        if (field == "blackbgc" && story is 239 or 245)
        {
            return true;
        }
        if (field == "blackbg9" && story is 326 or 332 || field == "roadend" && story >= 332)
        {
            return true;
        }
        if (field == "blin1" && story == 326)
        {
            if (session.Host.Process.Tick != 0 && !session.Host.Process.PlayerLocked && session.Host.Process.Lines.Any(line => line.Entity == 19 && line.Enabled == 1))
            {
                input = ApproachLine(driver, 19, 8);
            }
            return true;
        }
        if (field == "blackbgb" && story >= 326)
        {
            if ((session.Machine.Input.Held & 0x0800) == 0)
            {
                input = new(Held: 0x0800);
            }
            return true;
        }
        if (field == "mds5_5" && story >= 332)
        {
            Ff7Actor? exiting = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
            if (session.Host.Process.Tick != 0 && exiting is { Ladder: null, Jump: null } && !session.Host.Process.PlayerLocked && !session.Host.HasScriptedMotion(exiting))
            {
                if (story < 341)
                {
                    // The east approach to LINE8 leaves Cloud room to reach the scripted dialogue position.
                    input = exiting.Triangle == 14 ? driver.Steer(new(663, -2860, 0), run: false) : driver.Approach(new(775, -2880, 0), 14, run: false);
                }
                else
                {
                    input = exiting.Triangle == 14 ? driver.Steer(new(472, -2920, 0)) : driver.Gateway(1);
                }
            }
            return true;
        }
        if (field == "ealin_12" && story is 248 or 251 or 253)
        {
            return true;
        }
        if (field == "mds7st32" && story is 251 or 253)
        {
            return true;
        }
        if (field == "wcrimb_1" && story == 257)
        {
            byte swingFrame = session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x22);
            if (session.Host.Process.PlayerLocked && session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x1D) == 1 && swingFrame == 60 && !session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed))
            {
                input = new(Held: 32);
                return true;
            }
            Ff7Actor? climber = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
            if (climber?.Ladder is { Completed: false })
            {
                input = driver.Steer(new(climber.X4096 >> 12, climber.Y4096 >> 12, climber.Z4096 >> 12));
            }
            else if (climber is not null && climber.Triangle is not (111 or 112) && !session.Host.Process.PlayerLocked && !session.Host.HasScriptedMotion(climber))
            {
                input = climber.Triangle switch
                {
                    0 => default,
                    >= 4 and <= 13 or >= 24 and <= 27 => ApproachLine(driver, 7, 7),
                    30 or 70 or 71 or 166 => ApproachLine(driver, 12, 166),
                    >= 36 and <= 50 or 176 or 177 => ApproachLine(driver, 10, 176),
                    161 or >= 171 and <= 175 or >= 182 and <= 184 => ApproachLine(driver, 21, 161),
                    34 or 35 or >= 60 and <= 67 => new(0, -1, Run: true),
                    >= 51 and <= 59 or 157 or 160 => ApproachLine(driver, 14, 160),
                    72 or 73 or >= 76 and <= 89 => ApproachLine(driver, 15, 72),
                    >= 90 and <= 104 or 158 or 159 or 178 or 179 => (ReadFlags(0xA5) & 4) == 0 ? ApproachLineWithConfirm(driver, 25, 103) : ApproachLine(driver, 17, 178),
                    _ when (ReadFlags(0xA5) & 0xE0) == 0 => ApproachLine(driver, 8, 155),
                    _ when (ReadFlags(0xA5) & 2) == 0 => ApproachLineWithConfirm(driver, 32),
                    32 or 33 => driver.Steer(new(climber.X4096 >> 12, 770, 1610)),
                    _ => ApproachLine(driver, 19, 167)
                };
            }
            return true;
        }
        if (story is 257 or 260 && TryChooseShinraEntrance(driver, field, out input))
        {
            return true;
        }
        if (field is not ("ealin_1" or "ealin_2" or "niv_cl" or "eals_1" or "mds5_1" or "mds5_3" or "mds6_1" or "mds6_2" or "mds6_22" or "mds6_3" or "mrkt2" or "mrkt1" or "mrkt3" or "mrkt4" or "mkt_w" or "mkt_s1" or "mktpb" or "mkt_mens" or "colne_1" or "colne_2" or "colne_3" or "colne_4" or "colne_5" or "colne_6" or "blin70_1" or "blin70_2" or "colne_b1" or "colne_b3" or "mds7st1" or "mds7st2" or "mds7st3" or "mds7plr1" or "mds7plr2" or "pillar_1" or "pillar_2" or "pillar_3"))
        {
            return false;
        }
        if (story == 158 && field is ("eals_1" or "mds5_1" or "mds5_3"))
        {
            return false;
        }
        Ff7FieldProcess process = session.Host.Process;
        if (field == "pillar_2" && story == 221 && process.Dialogues.Any(dialogue => dialogue is { Window: 3, Text: 29, Completed: false }))
        {
            // Barret's equipment prompt is a persistent window. Its script reads Confirm separately.
            input = new(Held: (session.Machine.Input.Held & 32) == 0 ? (ushort)32 : (ushort)0);
            return true;
        }
        Ff7Actor? player = process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is null || process.Tick == 0 || process.FadeState is { EntryCurve: true, Completed: false } || process.Join is not null || process.Dialogues.Any(dialogue => !dialogue.Completed && session.Host.CanConfirmDialogue(dialogue.RequestId)))
        {
            return true;
        }
        if (player.Ladder is { } ladder)
        {
            input = ladder.Completed ? default : driver.Steer(new(player.X4096 >> 12, player.Y4096 >> 12, player.Z4096 >> 12));
            return true;
        }
        if (process.PlayerLocked || player.Jump is not null || session.Host.HasScriptedMotion(player))
        {
            return true;
        }
        input = field switch
        {
            "ealin_1" when story == 164 => driver.Gateway(190),
            "ealin_1" when story == 170 => driver.Gateway(187),
            "ealin_1" when story == 255 => driver.Gateway(187),
            "ealin_2" when story is 164 or 167 => default,
            "ealin_2" when story == 170 => ChooseSneakInput(driver),
            "niv_cl" when story is 167 or 170 => default,
            "eals_1" when story == 164 => ApproachLine(driver, 9),
            "eals_1" when story == 173 => driver.Gateway(177),
            "eals_1" when story is 255 or 257 => driver.Gateway(177),
            "mds5_1" when story == 173 => driver.Gateway(172),
            "mds5_1" when story == 257 => driver.Gateway(172, run: false),
            "mds5_3" when story is 173 or 176 => driver.Gateway(191),
            "mds5_3" when story == 257 => driver.Gateway(191, run: false),
            "mds6_1" when story == 176 => ApproachLine(driver, 7),
            "mds6_2" when story is 176 or 179 or 183 => default,
            "mds6_2" when story == 185 => driver.Gateway(194),
            "mds6_3" when story == 185 => driver.Gateway(195),
            "mds6_3" when story == 257 => driver.Gateway(195),
            "mrkt2" when story is 185 or 188 => driver.Gateway(214),
            "mrkt3" when story is 185 or 188 => driver.TalkTo(16),
            "mrkt3" when story == 190 => driver.Gateway(195),
            "mrkt2" when story == 190 => driver.Gateway(205),
            "mrkt2" when story == 257 => driver.Gateway(205),
            "mrkt1" when story is 190 or 192 => driver.Gateway(206),
            "mrkt1" when story == 257 => driver.Gateway((ReadFlags(0xA5) & 0xE0) == 0 ? 196 : 222),
            "mkt_w" when story == 257 => (ReadFlags(0xA5) & 0xE0) == 0 ? driver.TalkTo(9, run: false) : driver.Gateway(205),
            "mrkt4" when story == 257 => (ReadFlags(0xA5) & 0xE0) == 0 ? driver.Gateway(205) : (ReadFlags(0xB7) & 8) != 0 ? ApproachLineWithConfirm(driver, 3) : driver.TalkTo(10, run: false),
            "colne_1" when story is 190 or 192 => driver.TalkTo(14),
            "colne_1" when story == 191 => driver.Gateway(205),
            "mrkt1" when story == 191 => driver.Gateway(WardrobeDestination()),
            "mrkt2" when story == 191 => driver.Gateway(WardrobeDestination() == 195 ? 201 : 205),
            "mkt_s1" when story == 191 => ChooseBoutiqueInput(driver),
            "mkt_s1" when story == 192 => driver.Gateway(195),
            "mrkt2" when story == 192 => driver.Gateway(205),
            "mktpb" when story == 191 => (ReadFlags(0xA1) & 0xE0) == 0 ? driver.TalkTo(14) : driver.Gateway(205),
            "mkt_mens" when story == 191 => HasWig ? driver.Gateway(205) : driver.TalkTo(12),
            "colne_2" when story == 192 => driver.Gateway(209),
            "colne_4" when story == 192 => driver.TalkTo(12),
            "colne_4" when story == 197 => ChooseCorneoBasementInput(driver),
            "colne_2" when story == 197 => ChooseCorneoHallInput(driver),
            "colne_5" when story == 197 => driver.Gateway(211),
            "colne_3" when story == 197 => ChooseCorneoLoungeInput(driver),
            "colne_6" when story == 197 => ApproachLineWithConfirm(driver, 16),
            "colne_6" when story == 203 => default,
            "blin70_1" when story == 203 => default,
            "blin70_2" when story is 203 or 206 => default,
            "colne_b1" when story == 206 => session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x0A) == 0 ? driver.TalkTo(2) : session.Machine.Variables.ReadByte(FieldBank.MapByte, 9) == 0 ? driver.TalkTo(3) : default,
            "colne_b1" when story == 209 => ChooseSewerExitInput(driver, player),
            "colne_b3" when story == 209 => ApproachLineWithConfirm(driver, 2),
            "mds7st1" when story is 209 or 212 => ChooseTrainGraveyardEntryInput(driver, player),
            "mds7st2" when story == 212 => ChooseTrainGraveyardSwitchInput(driver, player),
            "mds7st3" when story == 212 => driver.Gateway(156),
            "mds7st3" when story == 251 => default,
            "mds7plr1" when story is 212 or 218 => default,
            "mds7plr2" when story == 215 => default,
            "mds7plr1" when story == 221 => ChoosePillarApproach(driver, player),
            "pillar_1" when story == 221 => driver.Gateway(159),
            "pillar_2" when story == 221 => driver.TalkTo(4),
            "pillar_3" when story == 221 => ChoosePillarTopInput(driver),
            "mds6_22" when story is 236 or 239 => default,
            "mds6_1" when story is 239 or 245 => default,
            "mds6_1" when story == 248 => driver.Gateway(172, run: false),
            "mds6_1" when story == 257 => ApproachLine(driver, 7),
            "mds6_22" when story == 257 => driver.Gateway(194),
            "mds5_3" when story == 248 => driver.Gateway(177),
            "mds5_1" when story == 248 => driver.Gateway(187),
            "eals_1" when story == 248 => ApproachLine(driver, 9),
            _ => throw new NotSupportedException($"FFVII Midgar autoplay has reached an unproved story goal in {field} at story {story}.")
        };
        return true;
    }



    private byte ReadFlags(byte address) => session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, address);

    private bool HasDress => (ReadFlags(0x40) & 7) != 0;

    private bool HasWig => (ReadFlags(0x40) & 0x38) != 0;

    private int WardrobeDestination()
    {
        if ((ReadFlags(0xA2) & 0x80) != 0 && (ReadFlags(0xA1) & 0xE0) == 0)
        {
            return 204;
        }
        return HasDress && !HasWig ? 197 : 195;
    }

    private Ff7AutoplayInput ChooseBoutiqueInput(Ff7AutoplayDriver driver)
    {
        if ((ReadFlags(0xA2) & 0x80) != 0 && (ReadFlags(0xA1) & 0xE0) == 0 || HasDress && !HasWig)
        {
            return driver.Gateway(195);
        }
        if (session.Machine.Variables.ReadByte(FieldBank.MapByte, 4) == 4)
        {
            return ApproachLine(driver, 3);
        }
        return ApproachLineWithConfirm(driver, 2);
    }

    private Ff7AutoplayInput ChooseCorneoHallInput(Ff7AutoplayDriver driver)
    {
        bool thresholdBlocked = (session.Host.Process.BlockedTriangles[78 >> 3] & (1 << (78 & 7))) != 0;
        bool leftRoomPending = (session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0xA2) & 0x80) == 0;
        if (thresholdBlocked && leftRoomPending && (session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0xA3) & 8) != 0)
        {
            return driver.Gateway(209);
        }
        return thresholdBlocked ? driver.TalkToLine(13) : driver.Gateway(210);
    }

    private Ff7AutoplayInput ChooseCorneoBasementInput(Ff7AutoplayDriver driver)
    {
        Ff7Line reunion = session.Host.Process.Lines.Single(line => line.Entity == 20);
        return reunion.Enabled == 1 ? ApproachLine(driver, reunion.Entity) : driver.Gateway(207);
    }

    private Ff7AutoplayInput ChooseCorneoLoungeInput(Ff7AutoplayDriver driver)
    {
        if ((session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0xA3) & 8) != 0)
        {
            return driver.Gateway(207);
        }
        int approaches = session.Machine.Variables.ReadByte(FieldBank.MapByte, 2);
        if (session.Machine.Variables.ReadByte(FieldBank.MapByte, 0) != 0 && approaches < 3)
        {
            return driver.TalkTo(13 + approaches);
        }
        return driver.TalkTo(11);
    }

    private Ff7AutoplayInput ChooseSewerExitInput(Ff7AutoplayDriver driver, Ff7Actor player)
    {
        int ladder = player.Triangle switch
        {
            >= 11 and <= 23 or >= 38 and <= 45 or 65 or 66 or >= 76 and <= 79 => 5,
            >= 46 and <= 62 => 8,
            _ => -1
        };
        if (player.Triangle is 30 or 31)
        {
            return driver.Steer(new Vector3(1060, -555, 128));
        }
        return ladder >= 0 ? ApproachLineWithConfirm(driver, ladder) : ApproachLine(driver, 9, 26);
    }

    private Ff7AutoplayInput ChoosePillarTopInput(Ff7AutoplayDriver driver)
    {
        if (session.Machine.Variables.ReadByte(FieldBank.MapByte, 7) != 0)
        {
            if (session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x0F) == 1)
            {
                var landing = session.Field.Walkmesh[18];
                return driver.Approach((landing.A + landing.B + landing.C) / 3, landing.Index);
            }
            return default;
        }
        if (session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x13) != 0 && session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x17) == 0)
        {
            return driver.TalkTo(4);
        }
        if (session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x10) != 0)
        {
            return ApproachLine(driver, 1);
        }
        return session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x12) == 0
            && session.Progress.CompletedBattles.Any(battle => battle.FieldGeneration == session.FieldGeneration && battle.Formation is >= 412 and <= 414) ? driver.TalkTo(5) : default;
    }

    private Ff7AutoplayInput ApproachLineWithConfirm(Ff7AutoplayDriver driver, int entity, int? triangle = null)
    {
        Ff7Line counter = session.Host.Process.Lines.Single(line => line.Entity == entity);
        ushort confirm = session.Host.Process.Tick % 16 == 0 ? (ushort)32 : (ushort)0;
        return ApproachLine(driver, entity, triangle) with { Held = counter.Push || counter.Touch || counter.OnLine ? confirm : (ushort)0 };
    }

    private Ff7AutoplayInput ApproachLine(Ff7AutoplayDriver driver, int entity, int? triangle = null)
    {
        Ff7Line line = session.Host.Process.Lines.Single(line => line.Entity == entity);
        return driver.Approach(new Vector3((line.X1 + line.X2) / 2f, (line.Y1 + line.Y2) / 2f, line.Z1), triangle);
    }
}
