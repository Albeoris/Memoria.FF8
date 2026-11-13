using System.Numerics;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class AutoPlayController
{
    private static readonly (int First, int Second)[] PropagatorPairs = [(16, 64), (4, 8), (2, 32), (1, 128)];

    private int RagnarokGoal(GameWorld world)
    {
        int field = world.Events.FieldId;
        if (session.State.WorldEntryMemory?.StoryWord != 3007 || !world.HasCurrentFieldAllocation || !IsOriginalRagnarokRoom(world, field))
        {
            return 0;
        }

        // NG-075: Finish the native pending pair before provoking another Propagator.
        int defeated = world.Events.EventMemory.Read(446, 1).RequireExact();
        if (defeated == 255)
        {
            return 840;
        }
        int mode = world.Events.EventMemory.Read(445, 1).RequireExact();
        int pending = world.Events.EventMemory.Read(447, 1).RequireExact();
        foreach ((int first, int second) in PropagatorPairs)
        {
            if (((first | second) & defeated) == (first | second))
            {
                continue;
            }
            if ((mode & 4) == 0 && (pending == first || pending == second))
            {
                return PropagatorRoom(pending == first ? second : first);
            }
        }
        foreach ((int first, int second) in PropagatorPairs)
        {
            if (((first | second) & defeated) != (first | second))
            {
                return PropagatorRoom((defeated & first) == 0 ? first : second);
            }
        }
        return 0;
    }

    private static int PropagatorRoom(int bit) => bit switch
    {
        1 => 823,
        2 => 840,
        4 => 842,
        8 => 844,
        16 => 836,
        32 => 838,
        64 => 829,
        128 => 832,
        _ => throw new ArgumentOutOfRangeException(nameof(bit))
    };

    private static bool IsOriginalRagnarokRoom(GameWorld world, int field)
    {
        string? hash = field switch
        {
            823 => "A62F53AD6D971CC3FC53997C9473C8D1359F21BFF32B812D1F0817FF6581E06A",
            825 => "AF2050D39F84812CFC8D7E1FA4D1083D82D652EB93AAE1D668E95E4052A4959A",
            829 => "D78A2C8D8DD0030F783D1568BDCF413AC3A370A73FD8854A6074E0740F28705C",
            832 => "70A7E46C4524B3BE238A7B4C1A026C534871AFA2AE149DB0C032B5F8CFDD2F53",
            836 => "B48B896D7B1DD681BCE0B788C69204F6D454393B853B1BFAD92CA4EFEAFF19FC",
            838 => "58BA19E2950CB807C250B1AC6FB486FE4652950EE2AEA325D0A171B8E7B95D4A",
            840 => "BF7959B0462A0573393227B031BA81677E5CC10425BE78186853098DBE66979C",
            842 => "784A8387FDF9EEC62516D47F6417E34A168BA83526B06FF9F660528AC8ACD419",
            844 => "55EAB573F0679C3C37D84B9802F0907635875890539E06297E8E206B210CD403",
            _ => null
        };
        return hash is not null && world.ReadAutoPlayFieldProgramHash(field) == hash;
    }

    private string? RagnarokTravel(GameWorld world, int goal)
    {
        int field = world.Events.FieldId;
        int mode = world.Events.EventMemory.Read(445, 1).RequireExact();
        return field switch
        {
            825 => "825:gateway:0",
            823 when goal != 823 => "823:gateway:1",
            829 when goal is 840 or 832 => "829:gateway:0",
            829 when goal != 829 => "829:gateway:1",
            836 when goal == 838 => "836:gateway:1",
            836 when goal is 842 or 829 || goal is 840 or 832 && (mode & 1) == 0 => "836:gateway:0",
            836 when goal != 836 => "836:gateway:2",
            838 when goal != 838 => "838:line:11",
            840 when goal == 832 => "840:gateway:1",
            840 when goal == 829 => "840:gateway:2",
            840 when goal != 840 && (mode & 1) == 0 => "840:line:5",
            840 when goal != 840 => "840:gateway:0",
            840 when world.Events.EventMemory.Read(446, 1).RequireExact() == 255 => "840:talk:2",
            842 when goal == 829 || goal is 840 or 832 && (mode & 1) == 0 => "842:gateway:1",
            842 when goal != 842 => "842:gateway:0",
            844 when goal == 823 => "844:gateway:0",
            844 when goal is 840 or 832 && (mode & 1) != 0 => "844:gateway:2",
            844 when goal != 844 => "844:gateway:1",
            _ => null
        };
    }

    private bool TryApproachPropagator(GameWorld world, int field)
    {
        EventEngine events = world.Events;
        if (events.PlayerActor is not { } player)
        {
            return false;
        }
        bool inEncounterRange = field switch
        {
            823 => Math.Abs(player.X) < 30 && player.Y is > 630 and < 705,
            829 => player.X is >= -300 and <= 300,
            832 => true,
            836 => player.Z == 0 && player.Y <= 1600,
            838 => player.Y > 560,
            840 => Math.Abs(player.X) < 30 && Math.Abs(player.Y - 37) is > 100 and < 180,
            842 => player.X > -300 && player.Y > -150,
            844 => player.X is > -200 and < 1000 && player.Y <= 2100,
            _ => false
        };
        if (inEncounterRange)
        {
            Apply(new(PlayerActionKind.Wait, Frames: 120, WaitWhilePlayable: true));
            return true;
        }
        if (field == 842 && player.X is > -130 and < 14 && player.Y is > -190 and <= -150)
        {
            Vector2 axes = events.AutoPlayMovementAxes(new(0, 1));
            Apply(new(PlayerActionKind.Move, Horizontal: axes.X, Vertical: axes.Y));
            return true;
        }
        Vector2? destination = field switch
        {
            823 => new(0, player.Y > 513 ? 670 : 355),
            836 => new(1032, 1559),
            838 => new(-250, 800),
            840 => new(0, player.Y > 37 ? 180 : -106),
            842 => new(0, -125),
            844 => new(0, 1650),
            _ => null
        };
        if (destination is not { } point || events.ResolveWalkmesh?.Invoke(point.X, point.Y) is not { } waypoint)
        {
            return false;
        }
        AutoPlayTarget target = new($"{field}:propagator", AutoPlayTargetKind.Talk, waypoint.Position.X, waypoint.Position.Y, waypoint.Position.Z, waypoint.Triangle);
        if (!world.TryFindAutoPlayWalkingRoute(target, out AutoPlayWalkingRoute route, idleDeadline))
        {
            return false;
        }
        RecordRecentAction($"Walk toward the native Propagator encounter in field {field}");
        AutoPlayWalkResult result = world.WalkAutoPlayRoute(route, ResetIdleDeadline);
        if (result.Arrived)
        {
            Apply(new(PlayerActionKind.Wait, Frames: 120, WaitWhilePlayable: true));
        }
        return result.Arrived || result.Interrupted;
    }
}
