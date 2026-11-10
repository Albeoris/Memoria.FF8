using System.Numerics;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

/// <summary>Midgar story goals expressed through the shared native field controls.</summary>
public sealed class Ff7MidgarAutoPlayPolicy(Ff7Session session)
{
    public int? ChooseDialogueSelection(Ff7Dialogue dialogue)
    {
        return (session.Field.Name, dialogue.Text, dialogue.FirstChoice, dialogue.LastChoice) switch
        {
            ("mds6_2", 2, 5, 6) => 5,
            ("mrkt3", 40, 1, 2) => 1,
            ("mktpb", 29 or 31, 2, 3) => 3,
            ("mkt_mens", 24, 1, 2) => 2,
            ("mkt_mens", 29, 2, 3) => 3,
            _ => null
        };
    }

    public bool TryChooseInput(Ff7AutoplayDriver driver, out Ff7AutoplayInput input)
    {
        input = default;
        string field = session.Field.Name;
        if (field is not ("ealin_1" or "ealin_2" or "niv_cl" or "eals_1" or "mds5_1" or "mds5_3" or "mds6_1" or "mds6_2" or "mds6_3" or "mrkt2" or "mrkt1" or "mrkt3" or "mkt_s1" or "mktpb" or "mkt_mens" or "colne_1" or "colne_2" or "colne_4"))
        {
            return false;
        }
        int story = session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);
        if (story == 158 && field is ("eals_1" or "mds5_1" or "mds5_3"))
        {
            return false;
        }
        Ff7FieldProcess process = session.Host.Process;
        Ff7Actor? player = process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is null || process.Tick == 0 || process.PlayerLocked || process.Join is not null || process.Dialogues.Any(dialogue => !dialogue.Completed && session.Host.CanConfirmDialogue(dialogue.RequestId)))
        {
            return true;
        }
        if (player.Ladder is { } ladder)
        {
            input = ladder.Completed ? default : driver.Steer(new(player.X4096 >> 12, player.Y4096 >> 12, player.Z4096 >> 12));
            return true;
        }
        if (player.Jump is not null || session.Host.HasScriptedMotion(player))
        {
            return true;
        }
        input = field switch
        {
            "ealin_1" when story == 164 => driver.Gateway(190),
            "ealin_1" when story == 170 => driver.Gateway(187),
            "ealin_2" when story is 164 or 167 => default,
            "ealin_2" when story == 170 => driver.Gateway(188, run: false),
            "niv_cl" when story is 167 or 170 => default,
            "eals_1" when story == 164 => ApproachLine(driver, 9),
            "eals_1" when story == 173 => driver.Gateway(177),
            "mds5_1" when story == 173 => driver.Gateway(172),
            "mds5_3" when story is 173 or 176 => driver.Gateway(191),
            "mds6_1" when story == 176 => ApproachLine(driver, 8),
            "mds6_2" when story is 176 or 179 or 183 => default,
            "mds6_2" when story == 185 => driver.Gateway(194),
            "mds6_3" when story == 185 => driver.Gateway(195),
            "mrkt2" when story is 185 or 188 => driver.Gateway(214),
            "mrkt3" when story is 185 or 188 => driver.TalkTo(16),
            "mrkt3" when story == 190 => driver.Gateway(195),
            "mrkt2" when story == 190 => driver.Gateway(205),
            "mrkt1" when story is 190 or 192 => driver.Gateway(206),
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
            "colne_4" when story == 197 => driver.Gateway(207),
            "colne_2" when story == 197 => driver.Gateway(210),
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
        Ff7Line counter = session.Host.Process.Lines.Single(line => line.Entity == 2);
        ushort confirm = session.Host.Process.Tick % 16 == 0 ? (ushort)32 : (ushort)0;
        return ApproachLine(driver, 2) with { Held = counter.Push || counter.Touch || counter.OnLine ? confirm : (ushort)0 };
    }

    private Ff7AutoplayInput ApproachLine(Ff7AutoplayDriver driver, int entity)
    {
        Ff7Line line = session.Host.Process.Lines.Single(line => line.Entity == entity);
        return driver.Approach(new Vector3((line.X1 + line.X2) / 2f, (line.Y1 + line.Y2) / 2f, line.Z1));
    }
}
