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
        if (field == "blin67_2" && story is >= 293 and <= 302
            || field == "blin70_2" && story is >= 284 and <= 305
            || field == "blin70_3" && story is >= 305 and < 323)
        {
            return true;
        }
        if (story is < 302 or > 320 || field is not ("blin67_1" or "blin68_1" or "blin69_1" or "blin70_1" or "eleout"))
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
                ("blin68_1", 302) => driver.Gateway(264),
                ("blin69_1", 302) => driver.Gateway(266, 2),
                ("blin70_1", 305) => driver.Gateway(268),
                ("blin69_1", 311) => ApproachLineWithConfirm(driver, 16),
                ("eleout", 311) => ApproachLineWithConfirm(driver, 7),
                ("blin69_1", 320) when process.Actors.Any(actor => actor is { Entity: 5, TalkEnabled: true }) => driver.TalkTo(5, run: false),
                _ => default
            };
        }
        return true;
    }
}
