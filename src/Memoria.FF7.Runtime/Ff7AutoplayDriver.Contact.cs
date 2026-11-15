using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoplayDriver
{
    public Ff7AutoplayInput Contact(int entity, bool run = true)
    {
        return ChooseBoundaryInput(() => ChooseContactInput(entity, run));
    }

    private Ff7AutoplayInput ChooseContactInput(int entity, bool run)
    {
        Ff7FieldProcess process = session.Host.Process;
        Ff7Actor target = process.Actors.Single(actor => actor.Entity == entity);
        Ff7Actor? player = process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is null || process.PlayerLocked || process.Join is not null)
        {
            return default;
        }
        Vector3 position = new(target.X4096 / 4096f, target.Y4096 / 4096f, target.Z4096 / 4096f);
        Ff7AutoplayInput input = Steer(position, run);
        if (input.Horizontal != 0 || input.Vertical != 0)
        {
            var direction = Directions.Single(value => value.X == input.Horizontal && value.Y == input.Vertical);
            Ff7Actor controlled = player with { MoveSpeed = unchecked((ushort)session.GetPlayerMovementSpeed(run)) };
            Ff7NativeMoveResult probe = session.Host.Movement!.StepDirection(controlled, (byte)((direction.Direction + session.Field.Triggers.ControlDirection) & 255), process.Actors, process.BlockedTriangles, playerLocked: false, lineBlocked: process.LineBlocked);
            if (probe.PushEntities.Contains(entity) && AllowsPlannedMove(controlled, probe))
            {
                return input;
            }
        }
        return Approach(position, target.Triangle, run);
    }

    internal Ff7AutoplayInput ChooseTargetInput(Ff7AutoPlayTarget target, Func<Ff7AutoplayInput> choose)
    {
        return ChooseBoundaryInput(choose, ReadTargetGatewayIndices(target, session.Field.Triggers.Gateways));
    }

    private Ff7AutoplayInput ChooseBoundaryInput(Func<Ff7AutoplayInput> choose, IReadOnlySet<int>? gateways = null)
    {
        IReadOnlySet<int>? previous = admittedGateways;
        admittedGateways = gateways ?? previous;
        try
        {
            Ff7AutoplayInput input = choose();
            if ((input.Horizontal != 0 || input.Vertical != 0) && session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled) is Ff7Actor player)
            {
                var direction = Directions.Single(value => value.X == input.Horizontal && value.Y == input.Vertical);
                Ff7Actor controlled = player with { MoveSpeed = unchecked((ushort)session.GetPlayerMovementSpeed(input.Run)) };
                int rotation = (input.Held & 8) != 0 ? 224 : (input.Held & 4) != 0 ? 32 : 0;
                Ff7NativeMoveResult probe = session.Host.Movement!.StepDirection(controlled, unchecked((byte)(direction.Direction + session.Field.Triggers.ControlDirection + rotation)), session.Host.Process.Actors, session.Host.Process.BlockedTriangles, false, session.Host.Process.LineBlocked);
                if (!AllowsPlannedMove(controlled, probe))
                {
                    return default;
                }
            }
            return input;
        }
        finally
        {
            admittedGateways = previous;
        }
    }
}
