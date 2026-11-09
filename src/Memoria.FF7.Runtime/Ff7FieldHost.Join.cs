using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    private FieldHostReply JoinParty(long requestId, FieldPartyJoin command)
    {
        return ProcessParty(requestId, command.Duration, command.Leader, command.Follower1, command.Follower2, false, _ => (0, 0, 0));
    }

    private FieldHostReply SplitParty(long requestId, FieldPartySplit command)
    {
        return ProcessParty(requestId, command.Duration, command.Leader, command.Follower1, command.Follower2, true, actor => actor.PartyCharacter == command.Follower1 ? (command.X1, command.Y1, ReadPartyDirection(command.Direction1Source, command.Direction1)) : (command.X2, command.Y2, ReadPartyDirection(command.Direction2Source, command.Direction2)));
    }

    private FieldHostReply ProcessParty(long requestId, byte duration, int leaderCharacter, int follower1, int follower2, bool split, Func<Ff7Actor, (short X, short Y, byte Direction)> target)
    {
        if (ReadScriptParty is not null && !ReadScriptParty().SequenceEqual(new[] { leaderCharacter, follower1, follower2 }))
        {
            throw new NotSupportedException("FFVII party membership changed during an active party-motion request.");
        }
        Ff7Actor? leader = PartyActor(leaderCharacter);
        if (leader is null)
        {
            if (split && (PartyActor(follower1) is not null || PartyActor(follower2) is not null))
            {
                throw new NotSupportedException("FFVII SPLIT with a missing leader has no admitted completion contract.");
            }
            return new(true);
        }
        if (Process.Join is null)
        {
            Ff7JoinFollower[] followers = new[] { follower1, follower2 }.Select(PartyActor).Where(actor => actor is not null).Select(actor => new Ff7JoinFollower(actor!.Entity)).ToArray();
            Process = Process with { Join = new(requestId, leader.Entity, followers, split) };
        }
        Ff7PartyJoin join = Process.Join;
        if (join.RequestId != requestId || join.LeaderEntity != leader.Entity || join.Split != split)
        {
            throw new InvalidDataException("A native party motion changed its active owner, operation or leader.");
        }
        Ff7NativeMovement movement = Movement ?? throw new NotSupportedException("FFVII party motion requires native movement and turn tables.");
        List<Ff7JoinFollower> next = [];
        foreach (Ff7JoinFollower follower in join.Followers)
        {
            Ff7Actor actor = GetActor(follower.Entity);
            Ff7JoinFollower updated = follower;
            switch (follower.Stage)
            {
                case 0:
                    if (split)
                    {
                        var destination = target(actor);
                        Ff7Actor placed = actor with { X4096 = leader.X4096, Y4096 = leader.Y4096, Z4096 = leader.Z4096, Triangle = leader.Triangle };
                        SetActor(actor.Entity, _ => placed);
                        updated = StartPartyMovement(follower, placed, destination.X, destination.Y, duration);
                    }
                    else
                    {
                        int dx = (leader.X4096 >> 12) - (actor.X4096 >> 12);
                        int dy = (leader.Y4096 >> 12) - (actor.Y4096 >> 12);
                        int direction = movement.Direction(dx, dy);
                        Turn(actor.Entity, new FieldActorTurn((short)direction, 2, 16, 2));
                        updated = follower with { Stage = 2 };
                    }
                    break;
                case 2:
                    if (actor.TurnMode != 3)
                    {
                        break;
                    }
                    Turn(actor.Entity, new FieldActorTurn(0, 2, 16, 2));
                    updated = split ? follower with { Stage = 3 } : StartPartyMovement(follower, actor, unchecked((short)(leader.X4096 >> 12)), unchecked((short)(leader.Y4096 >> 12)), duration);
                    break;
                case 1:
                    if (follower.Arrived)
                    {
                        SetActor(actor.Entity, current => current with { MoveSpeed = follower.PreviousSpeed, AnimationStatus = 0, CollisionEnabled = split, TalkEnabled = split, Visible = split });
                        if (split)
                        {
                            Turn(actor.Entity, new FieldActorTurn(target(actor).Direction, 2, 16, 2));
                        }
                        updated = follower with { Stage = split ? 2 : 3 };
                    }
                    break;
                case 3:
                    break;
                default:
                    throw new InvalidDataException("FFVII party motion has an invalid native follower stage.");
            }
            next.Add(updated);
        }
        bool completed = next.All(follower => follower.Stage == 3);
        SetActor(leader.Entity, current => current with { CollisionEnabled = completed, AnimationStatus = completed ? current.AnimationStatus : 0 });
        Process = Process with { Join = completed ? null : join with { Followers = next.ToArray() }, Motions = completed ? Process.Motions : Process.Motions.Where(motion => motion.Entity != leader.Entity).ToArray() };
        return new(completed);
    }

    private Ff7JoinFollower StartPartyMovement(Ff7JoinFollower follower, Ff7Actor actor, short targetX, short targetY, byte duration)
    {
        if (duration == 0)
        {
            throw new NotSupportedException("FFVII party motion duration zero reaches a native divide-by-zero contract.");
        }
        int distance = Ff7NativeMovement.PlanarDistance(targetX - (actor.X4096 >> 12), targetY - (actor.Y4096 >> 12));
        if (distance == 0)
        {
            throw new NotSupportedException("FFVII party motion with coincident endpoints reaches a native divide-by-zero contract.");
        }
        int speed = unchecked((ushort)(unchecked(distance << 8) / duration));
        int animation = speed > 1536 ? 2 : 1;
        SetActor(actor.Entity, current => current with
        {
            MoveSpeed = speed,
            CollisionEnabled = false,
            TalkEnabled = false,
            Visible = true,
            AnimationStatus = 1,
            Animation = animation, RequestedAnimation = null,
            AnimationSpeed = current.Animation == animation ? current.AnimationSpeed : 16,
            AnimationFrame16 = current.Animation == animation ? current.AnimationFrame16 : 0,
            LastAnimationFrame = current.Animation == animation ? current.LastAnimationFrame : animationLength(current.Model, animation) - 1,
            AnimationLoop = true,
            AnimationStopped = false,
            AnimationFinished = false
        });
        return follower with { Stage = 1, PreviousSpeed = actor.MoveSpeed, TargetX = targetX, TargetY = targetY };
    }

    private byte ReadPartyDirection(FieldByteOperand? operand, byte fallback)
    {
        if (operand is null)
        {
            return fallback;
        }
        return operand.Bank == FieldBank.Immediate ? operand.Value : ReadFieldByte?.Invoke(operand) ?? throw new NotSupportedException("FFVII SPLIT direction requires the current native variable operand.");
    }

    private Ff7Actor? PartyActor(int character)
    {
        return character == 255 ? null : Process.Actors.LastOrDefault(actor => actor.EntityBound && actor.PartyCharacter == character);
    }

    private void AdvanceJoinedMovement()
    {
        if (Process.Join is not Ff7PartyJoin join)
        {
            return;
        }
        Ff7NativeMovement movement = Movement ?? throw new NotSupportedException("FFVII JOIN requires native movement tables.");
        List<Ff7JoinFollower> next = [];
        foreach (Ff7JoinFollower follower in join.Followers)
        {
            Ff7JoinFollower updated = follower;
            if (follower.Stage == 1 && !follower.Arrived)
            {
                Ff7NativeMoveResult result = movement.Step(GetActor(follower.Entity), follower.TargetX, follower.TargetY, Process.Actors, Process.BlockedTriangles, true);
                SetActor(follower.Entity, _ => result.Actor);
                updated = follower with { Arrived = result.Arrived };
            }
            next.Add(updated);
        }
        Process = Process with { Join = join with { Followers = next.ToArray() } };
    }
}
