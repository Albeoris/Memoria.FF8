namespace Memoria.FF7.Runtime;

public sealed partial class Ff7MidgarAutoPlayPolicy
{
    private bool TryChooseShinraEntrance(Ff7AutoplayDriver driver, string field, out Ff7AutoplayInput input)
    {
        input = default;
        if (field is not ("wcrimb_2" or "md0" or "sinbil_1" or "sinbil_2" or "blinst_1" or "blinst_2" or "blinst_3" or "blin59" or "eleout"))
        {
            return false;
        }
        Ff7FieldProcess process = session.Host.Process;
        Ff7Actor? player = process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is null || process.Tick == 0 || process.FadeState is { EntryCurve: true, Completed: false })
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
            "wcrimb_2" => player.Triangle switch
            {
                <= 22 => driver.Gateway(225),
                23 or 24 or >= 25 and <= 27 or >= 29 and <= 47 or >= 51 and <= 60 or >= 101 and <= 103 => ApproachLine(driver, 10, 24),
                >= 63 and <= 74 or >= 97 and <= 100 => ApproachLine(driver, 7, 63),
                _ => ApproachLine(driver, 5, 95)
            },
            "md0" => driver.Gateway(227),
            "sinbil_1" => driver.Gateway(228),
            "sinbil_2" => driver.Gateway(229),
            "blinst_1" => ApproachLine(driver, 6, 103),
            "blinst_2" => ApproachLine(driver, 10),
            "blinst_3" => ApproachLine(driver, 9),
            "blin59" => (ReadFlags(0x43) & 64) == 0 ? ApproachLine(driver, 24) : driver.TalkToLine(17),
            "eleout" => ApproachLineWithConfirm(driver, 7),
            _ => default
        };
        return true;
    }
}
