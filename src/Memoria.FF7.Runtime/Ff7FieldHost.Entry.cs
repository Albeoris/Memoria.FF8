using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    public void PlacePlayerAtEntry(FieldTransitionCommand transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        Ff7Actor? player = Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is null)
        {
            return;
        }
        SetActorSlot(player.Slot, actor =>
        {
            Ff7Actor directed = actor with { Direction = transition.Direction };
            return Process.PlayerLocked ? directed : Place(directed, new(transition.X, transition.Y, null, checked((short)transition.Triangle)));
        });
    }

    private void SelectPlayerActor(int entity)
    {
        int slot = GetActor(entity).Slot;
        Process = Process with { Actors = Process.Actors.Select(actor => actor with { PlayerControlled = actor.Slot == slot }).ToArray() };
    }

    public void RestorePlayerSelection()
    {
        if (Process.Actors.Length == 0 || Process.Actors.Any(actor => actor.PlayerControlled))
        {
            return;
        }
        if (!Process.Actors.Any(actor => actor.Slot == 0))
        {
            throw new InvalidDataException("FFVII actor selection requires its native initial slot zero.");
        }
        Process = Process with { Actors = Process.Actors.Select(actor => actor with { PlayerControlled = actor.Slot == 0 }).ToArray() };
        Console.Error.WriteLine("FFVII compatibility: missing legacy controlled-actor selection defaults to native slot zero; actor positions, script PCs and progress were preserved.");
    }

    /// <summary>Applies the controlled player's native movement defaults after field initializer scripts finish.</summary>
    public void InitializePlayerMovement(int fieldScale)
    {
        if (Process.PlayerLocked)
        {
            return;
        }
        short scale = unchecked((short)fieldScale);
        Process = Process with
        {
            Actors = Process.Actors.Select(actor => actor.PlayerControlled ? actor with
            {
                CollisionRadius = unchecked((ushort)((scale * 34) >> 9)),
                MoveSpeed = unchecked((ushort)((scale << 10) >> 9)),
                BaseAnimationSpeed = 16
            } : actor).ToArray()
        };
    }
}
