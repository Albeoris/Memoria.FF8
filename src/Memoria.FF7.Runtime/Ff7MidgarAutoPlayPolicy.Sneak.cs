using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7MidgarAutoPlayPolicy
{
    private Ff7AutoplayInput ChooseSneakInput(Ff7AutoplayDriver driver)
    {
        driver.AvoidLinePush(9, 11, 13, 14, 16);
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        int x = player.X4096 >> 12;
        int y = player.Y4096 >> 12;
        if (player.Triangle is >= 8 and <= 13 && y >= 306)
        {
            return driver.Gateway(188, run: false);
        }
        if (x >= 210)
        {
            return driver.Steer(new Vector3(260, 350, 288), run: false);
        }
        if (x >= 31)
        {
            return driver.Steer(new Vector3(230, 260, 288), run: false);
        }
        if (y >= 315)
        {
            return driver.Steer(new Vector3(40, 324, 288), run: false);
        }
        return driver.Steer(new Vector3(-69, 356, 288), run: false);
    }
}
