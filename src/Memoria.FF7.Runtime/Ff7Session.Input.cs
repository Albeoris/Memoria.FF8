using System.Numerics;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private int movementSpeedPercent = 100;
    private int worldMovementSpeedPercent = 100;

    public void ConfigureWorldMovementSpeed(int percent)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(percent, 50);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 300);
        worldMovementSpeedPercent = percent;
        World?.ConfigureMovementSpeed(percent);
    }

    public void ConfigureMovementSpeed(int percent)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(percent, 50);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 300);
        movementSpeedPercent = percent;
    }

    public int GetPlayerMovementSpeed(bool run)
    {
        int scale = Field.Scripts.Scale;
        int speed = Host.Process.MovieBackgroundEnabled ? run ? (6144 * scale) >> 9 : (3072 * scale) >> 10 : run ? (scale << 12) >> 9 : (scale << 10) >> 9;
        return checked(speed * movementSpeedPercent / 100);
    }
    public void SetInput(ushort held, ushort pressed)
    {
        if (World is not null)
        {
            World.SetButtons(held, pressed);
            return;
        }
        if (HasPendingTick)
        {
            deferredButtons = (held, pressed);
            return;
        }
        Machine.SetInput(held, pressed);
        UpdatePlayerHelp(pressed);
    }

    private void UpdatePlayerHelp(ushort pressed)
    {
        if ((pressed & 0x100) != 0)
        {
            byte mode = Machine.Variables.ReadByte(FieldBank.Persistent3Byte, 30);
            Machine.Variables.WriteByte(FieldBank.Persistent3Byte, 30, (byte)(mode ^ 1));
        }
    }

    public void MovePlayer(int horizontal, int vertical, bool run)
    {
        if (World is not null)
        {
            World.SetMovement(horizontal, vertical, run);
            return;
        }
        if (HasPendingTick)
        {
            deferredMovement = (horizontal, vertical, run);
            return;
        }
        Machine.SetMovementInput(horizontal, vertical, run);
    }

    public bool TryTalk()
    {
        if (World is not null)
        {
            return false;
        }
        if (HasPendingTick || Host.Process.PlayerLocked || Host.Process.Join is not null)
        {
            return false;
        }
        Ff7Actor? player = Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is null)
        {
            return false;
        }
        bool lineTalk = Host.RequestLineTalk(player);
        Ff7Actor? selected = GetTalkTarget();
        bool actorTalk = selected is not null && RequestInteractionScript(selected.Entity, 1);
        return actorTalk || lineTalk;
    }

    public Ff7Actor? GetTalkTarget()
    {
        if (Host.Process.PlayerLocked || Host.Process.Join is not null)
        {
            return null;
        }
        Ff7Actor? player = Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is null)
        {
            return null;
        }
        return PreviewTalkTarget(player);
    }

    internal Ff7Actor? PreviewTalkTarget(Ff7Actor player)
    {
        if (Host.Process.PlayerLocked || Host.Process.Join is not null)
        {
            return null;
        }
        Ff7NativeMovement movement = Host.Movement ?? throw new NotSupportedException("FFVII interaction requires native movement direction tables.");
        int best = 64;
        Ff7Actor? selected = null;
        foreach (Ff7Actor actor in Host.Process.Actors.OrderBy(actor => actor.Slot))
        {
            if (actor.Slot == player.Slot || !actor.TalkEnabled)
            {
                continue;
            }
            int dx = (actor.X4096 >> 12) - (player.X4096 >> 12);
            int dy = (actor.Y4096 >> 12) - (player.Y4096 >> 12);
            int dz = (player.Z4096 >> 12) - (actor.Z4096 >> 12);
            if ((dx == 0 && dy == 0) || dz <= -256 || dz >= 256)
            {
                continue;
            }
            int squared = unchecked(dx * dx + dy * dy);
            if (squared < 0)
            {
                throw new NotSupportedException("FFVII interaction distance exceeds native signed arithmetic.");
            }
            int distance = (int)Math.Sqrt(squared);
            if (distance >= actor.TalkRadius + player.CollisionRadius)
            {
                continue;
            }
            int difference = (player.Direction - movement.Direction(dx, dy)) & 255;
            if (difference > 128)
            {
                difference = 256 - difference;
            }
            if (difference < best)
            {
                best = difference;
                selected = actor;
            }
        }
        return selected;
    }

    private void AdvancePlayerInput()
    {
        Ff7FieldProcess process = Host.Process;
        if (process.PlayerLocked || process.Join is not null || process.Transition is not null || process.GlobalCommand is Ff7GlobalCommand.Battle or Ff7GlobalCommand.Menu)
        {
            Host.ClearLineOnLine();
            return;
        }
        Ff7Actor? player = process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is null || player.Ladder is not null || player.Jump is not null || Host.HasScriptedMotion(player))
        {
            return;
        }
        int horizontal = Machine.Input.Horizontal;
        int vertical = Machine.Input.Vertical;
        if (horizontal == 0 && vertical == 0)
        {
            Host.UpdateLineContacts(player, player, moving: false);
            Host.ApplyPlayerMovement(new Ff7NativeMoveResult(player with { RequestedAnimation = null, Animation = 0 }, false, false, []));
            return;
        }
        int direction;
        if (vertical < 0)
        {
            direction = horizontal < 0 ? 32 : horizontal > 0 ? 224 : 0;
        }
        else if (vertical > 0)
        {
            direction = horizontal < 0 ? 96 : horizontal > 0 ? 160 : 128;
        }
        else
        {
            direction = horizontal < 0 ? 64 : 192;
        }
        int rotation = (Machine.Input.Held & 8) != 0 ? 224 : (Machine.Input.Held & 4) != 0 ? 32 : 0;
        direction = (direction + Field.Triggers.ControlDirection + rotation) & 255;
        bool run = Machine.Input.Run;
        int speed = GetPlayerMovementSpeed(run);
        Ff7NativeMovement movement = Host.Movement ?? throw new NotSupportedException("FFVII control requires its native movement contract.");
        var controlled = player with { MoveSpeed = unchecked((ushort)speed), RequestedAnimation = null, Animation = 0, AnimationSpeed = checked((ushort)(16 * movementSpeedPercent / 100)) };
        Ff7NativeMoveResult result = movement.StepDirection(controlled, (byte)direction, process.Actors, process.BlockedTriangles, playerLocked: false, lineBlocked: process.LineBlocked);
        int animation = run ? 2 : 1;
        if (Field.Models.Models[player.Model].Animations.Count <= animation)
        {
            animation = 0;
        }
        result = result with { Actor = result.Actor with { RequestedAnimation = null, Animation = result.Moved ? animation : 0 } };
        Host.UpdateLineContacts(controlled, result.ProbeActor ?? result.Actor, moving: true);
        Host.ApplyPlayerMovement(result);
        foreach (int entity in result.PushEntities)
        {
            RequestInteractionScript(entity, 2);
        }
        if (!process.MapJumpDisabled)
        {
            CheckGateways(player, result.Actor);
        }
        Host.UpdateBackgroundTriggers(controlled);
        if (result.Moved && Host.Process.Transition is null)
        {
            AdvanceEncounter(result.Actor);
        }
    }

    private void CheckGateways(Ff7Actor from, Ff7Actor to)
    {
        Vector3 start = new(from.X4096 >> 12, from.Y4096 >> 12, from.Z4096 >> 12);
        Vector3 end = new(to.X4096 >> 12, to.Y4096 >> 12, from.Z4096 >> 12);
        foreach (PcFieldGateway gateway in Field.Triggers.Gateways)
        {
            if (gateway.DestinationField == 0x7FFF)
            {
                continue;
            }
            int distance = DistanceToSegment(gateway.A, gateway.B, start);
            if (distance < 0 || distance >= unchecked(from.CollisionRadius * from.CollisionRadius))
            {
                continue;
            }
            int fromSide = Side(gateway.A, gateway.B, start);
            int toSide = Side(gateway.A, gateway.B, end);
            bool crossed = fromSide >= 0 && toSide < 0 || toSide >= 0 && fromSide < 0 || fromSide > 0 && toSide <= 0 || toSide > 0 && fromSide <= 0;
            if (crossed)
            {
                Host.RequestGateway(new FieldTransitionCommand(checked((ushort)gateway.DestinationField), gateway.DestinationX, gateway.DestinationY, gateway.DestinationTriangle, checked((byte)gateway.Direction)));
            }
        }
    }

    internal static int Side(Vector3 a, Vector3 b, Vector3 point)
    {
        return unchecked(((int)b.X - (int)a.X) * ((int)point.Y - (int)a.Y) - ((int)point.X - (int)a.X) * ((int)b.Y - (int)a.Y));
    }

    private static int DistanceToSegment(Vector3 a, Vector3 b, Vector3 point)
    {
        return DistanceToSegment(a, b, point, out _);
    }

    internal static int DistanceToSegment(Vector3 a, Vector3 b, Vector3 point, out Vector3 nearest)
    {
        nearest = default;
        int dx = (int)b.X - (int)a.X;
        int dy = (int)b.Y - (int)a.Y;
        int dz = (int)b.Z - (int)a.Z;
        int length = unchecked(dx * dx + dy * dy + dz * dz);
        if (length <= 0)
        {
            throw new InvalidDataException("FFVII active gateway has a degenerate or overflowing native segment.");
        }
        int fraction = unchecked(-256 * (((int)a.X - (int)point.X) * dx + ((int)a.Y - (int)point.Y) * dy + ((int)a.Z - (int)point.Z) * dz)) / length;
        int x = unchecked(fraction * dx >> 8) + (int)a.X;
        int y = unchecked(fraction * dy >> 8) + (int)a.Y;
        int z = unchecked(fraction * dz >> 8) + (int)a.Z;
        if (x < Math.Min(a.X, b.X) || x > Math.Max(a.X, b.X) || y < Math.Min(a.Y, b.Y) || y > Math.Max(a.Y, b.Y))
        {
            return -1;
        }
        nearest = new Vector3(x, y, z);
        dx = x - (int)point.X;
        dy = y - (int)point.Y;
        dz = z - (int)point.Z;
        return unchecked(dx * dx + dy * dy + dz * dz);
    }
}
