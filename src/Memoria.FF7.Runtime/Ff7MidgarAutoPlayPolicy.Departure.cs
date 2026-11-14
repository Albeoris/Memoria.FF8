using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7MidgarAutoPlayPolicy
{
    private bool TryChooseShinraDeparture(Ff7AutoplayDriver driver, string field, int story, out Ff7AutoplayInput input)
    {
        input = default;
        if (field == "blin70_3" && story is >= 305 and < 323)
        {
            return true;
        }
        if (field != "blin69_1" || story != 320)
        {
            return false;
        }
        Ff7FieldProcess process = session.Host.Process;
        Ff7Actor? player = process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is { Ladder: null, Jump: null } && process.Tick != 0 && !process.PlayerLocked && !session.Host.HasScriptedMotion(player) && process.Actors.Any(actor => actor is { Entity: 5, TalkEnabled: true }))
        {
            input = driver.TalkTo(5, run: false);
        }
        return true;
    }
}
