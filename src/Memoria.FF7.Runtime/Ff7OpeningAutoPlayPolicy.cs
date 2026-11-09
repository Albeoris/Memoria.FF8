using System.Numerics;

namespace Memoria.FF7.Runtime;

/// <summary>Original opening story goals expressed solely through physical field controls.</summary>
public sealed class Ff7OpeningAutoPlayPolicy(Ff7Session session)
{
    private long generation = -1;
    private int stage;

    private ushort ConfirmPulse => session.Host.Process.Tick % 16 == 0 ? (ushort)32 : (ushort)0;

    public string Goal => "Reach the church through the original story sequence";

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
        if (player.Ladder is { Completed: false })
        {
            return driver.Steer(new(player.X4096 >> 12, player.Y4096 >> 12, player.Z4096 >> 12));
        }
        if (session.Host.Process.PlayerLocked || session.Host.Process.Join is not null || session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed))
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
                return default;
            case "md1_2":
                return driver.Gateway(119);
            case "nrthmk":
                return driver.Gateway(120);
            case "nmkin_1":
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
                if (session.Host.Process.Actors.Single(actor => actor.Entity == 6).TalkEnabled)
                {
                    return driver.Approach(new(99, 51, 5), 8) with { Held = ConfirmPulse };
                }
                return driver.Approach(new(-250, 0, 5), 0);
            case "nmkin_2":
                return driver.Gateway(123);
            case "nmkin_3":
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
                if (z > 0)
                {
                    return driver.Approach(new(-380, 1990, 640));
                }
                return driver.Gateway(125);
            case "nmkin_5":
                if (session.Host.Process.Actors.Single(actor => actor.Entity == 6).TalkEnabled)
                {
                    return driver.TalkTo(6);
                }
                return driver.Approach(new(-90, -1600, -184)) with { Held = ConfirmPulse, Talk = ConfirmPulse != 0 };
            default:
                throw new NotSupportedException($"FFVII autoplay has reached the next unsupported native story goal in field '{session.Field.Name}'.");
        }
    }
}
