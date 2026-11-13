namespace Memoria.FF7.Runtime;

public sealed partial class Ff7MidgarAutoPlayPolicy
{
    private Ff7AutoplayInput ChooseTrainGraveyardEntryInput(Ff7AutoplayDriver driver, Ff7Actor player)
    {
        return player.Triangle switch
        {
            // The sewer ladder starts on its own two-triangle landing.
            4 or 68 => ApproachLine(driver, 2),
            // The first train roof reaches the next carriage by a scripted jump.
            3 or 11 or 13 or 23 or 47 or 53 or 54 or 55 or 77 or 79 or >= 192 and <= 197 => ApproachLine(driver, 10),
            // Cross the isolated second roof between its two ladders.
            >= 134 and <= 139 => driver.TalkToLine(9),
            >= 105 and <= 114 or >= 116 and <= 133 or >= 159 and <= 173 => driver.Gateway(145),
            _ when player.Z4096 < 32 * 4096 && player.Y4096 < -2700 * 4096 => driver.TalkToLine(4),
            _ => driver.TalkToLine(6)
        };
    }

    private Ff7AutoplayInput ChooseTrainGraveyardSwitchInput(Ff7AutoplayDriver driver, Ff7Actor player)
    {
        // The lower ladder and the two roofs occupy separate walkmesh components.
        switch (player.Triangle)
        {
            case 24 or 27 or 68 or 69 or 144 or 168 or 183 or 184 or 193 or 194:
                return ApproachLine(driver, 11);
            case 13 or 14 or 15 or 47 or 48 or 49 or 50 or 51 or 52 or 59 or 64 or 65 or 98 or 99 or 125 or 148 or 171 or 173 or 174 or 175 or 180 or 181 or 188 or 189 or 202 or 230:
                return driver.TalkToLine(6);
        }

        if (player.Z4096 < 40 * 4096 && player.X4096 < -1000 * 4096)
        {
            return driver.Gateway(146, 37);
        }

        // The forward switch disables its own line and enables the return switch.
        if (session.Host.Process.Lines.Single(line => line.Entity == 7).Enabled != 0)
        {
            return driver.TalkToLine(7);
        }
        if (session.Host.Process.Lines.Single(line => line.Entity == 9).Enabled != 0)
        {
            return driver.TalkToLine(9);
        }
        return driver.TalkToLine(3);
    }

    private static Ff7AutoplayInput ChoosePillarApproach(Ff7AutoplayDriver driver, Ff7Actor player)
    {
        // Reach the lower part of the ramp before targeting its upper gateway.
        if (player.Y4096 < 1070 * 4096)
        {
            return driver.Approach(new(256, 1129, 0), 245);
        }
        return driver.Gateway(158);
    }
}
