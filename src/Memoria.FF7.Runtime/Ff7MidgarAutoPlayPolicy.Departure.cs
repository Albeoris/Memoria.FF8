using System.Numerics;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7MidgarAutoPlayPolicy
{
    private int? ChooseShinraCustodyDialogue(Ff7Dialogue dialogue)
    {
        return (session.Field.Name, dialogue.Text, dialogue.FirstChoice) switch
        {
            ("blin67_2", >= 183 and <= 190, int first) => first,
            ("blin69_1", 104, 2) => 2,
            _ => null
        };
    }

    private bool TryChooseShinraDeparture(Ff7AutoplayDriver driver, string field, int story, out Ff7AutoplayInput input)
    {
        input = default;
        if (field == "blin68_2" && story == 278 && session.Host.Process.Dialogues.Any(dialogue => dialogue is { Text: 30, Completed: false, AwaitingInput: true }))
        {
            if ((session.Machine.Input.Held & 0x20) == 0)
            {
                input = new(Held: 0x20);
            }
            return true;
        }
        if (field == "blin67_2" && story is >= 293 and <= 302)
        {
            if (story == 293 && !session.Host.Process.PlayerLocked && session.Host.Process.Lines.Any(line => line is { Entity: 7, Enabled: 1 }))
            {
                input = session.Host.Process.Tick % 128 < 64
                    ? driver.Steer(new Vector3(883, 430, 0), run: false)
                    : ApproachLine(driver, 7);
            }
            else if (story == 296 && !session.Host.Process.PlayerLocked)
            {
                bool inspectedGuard = session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x15) != 0;
                int tifaConversation = session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x10);
                bool reachedBarretCell = session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x16) != 0;
                int prisonerConversation = session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x14);
                input = !inspectedGuard ? driver.TalkTo(6, run: false)
                    : tifaConversation < 2 ? driver.TalkTo(3, run: false)
                    : !reachedBarretCell && (session.Host.Process.BlockedTriangles[166 >> 3] & 1 << (166 & 7)) != 0 ? default
                    : !reachedBarretCell ? ApproachLineWithConfirm(driver, 8, 167)
                    : prisonerConversation == 0 ? driver.TalkTo(4, run: false)
                    : prisonerConversation == 1 ? ApproachLine(driver, 9, 11)
                    : driver.TalkTo(3, run: false);
            }
            return true;
        }
        if (field == "blin70_2" && story is >= 284 and <= 305
            || field == "blin70_3" && story is >= 305 and < 323)
        {
            return true;
        }
        if (story is < 302 or > 320 || field is not ("blin67_1" or "blin671b" or "blin673b" or "blin68_1" or "blin69_1" or "blin70_1" or "eleout"))
        {
            return false;
        }
        Ff7FieldProcess process = session.Host.Process;
        Ff7Actor? player = process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is { Ladder: null, Jump: null } && process.Tick != 0 && !process.PlayerLocked && !session.Host.HasScriptedMotion(player))
        {
            input = (field, story) switch
            {
                ("blin67_1", 302) => driver.Gateway(262),
                ("blin671b", 302) => ApproachLine(driver, 17, 276),
                ("blin673b", 302) when (ReadFlags(0xE2) & 0x80) == 0 => driver.TalkTo(4, run: false),
                ("blin673b", 302) => driver.Gateway(257, 271, run: false),
                ("blin68_1", 302) => driver.Gateway(264),
                ("blin69_1", 302) => ApproachShinraRoofStairs(driver, player),
                ("blin70_1", 305) => driver.Gateway(268),
                ("blin69_1", 311) => ApproachLineWithConfirm(driver, 16),
                ("eleout", 311) => ApproachLineWithConfirm(driver, 7),
                ("blin69_1", 320) when process.Actors.Any(actor => actor is { Entity: 5, TalkEnabled: true }) => driver.TalkTo(5, run: false),
                _ => default
            };
        }
        return true;
    }

    private static Ff7AutoplayInput ApproachShinraRoofStairs(Ff7AutoplayDriver driver, Ff7Actor player)
    {
        return player.Triangle is 328 or 329
            ? driver.Steer(new Vector3(-400, 800, 320), run: false)
            : driver.Approach(new Vector3(-338, 800, 323), 328, run: false);
    }
}
