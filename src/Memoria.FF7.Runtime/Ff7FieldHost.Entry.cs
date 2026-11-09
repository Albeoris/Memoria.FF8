namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
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
