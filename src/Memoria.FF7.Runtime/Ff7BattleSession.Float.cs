using System.Numerics;
using Memoria.Battles;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleFloatPresentation(byte Phase, long Tick, int Remainder, int Offset);

public sealed partial class Ff7BattleSession
{
    public const float OriginActorToHostScale = 0.81593376f;

    public int RenderActorFloatOffset(int slot)
    {
        Ff7BattleActorModelState? model = state.ActorModels.SingleOrDefault(model => model.Slot == slot);
        return model?.Render?.FloatOffset ?? model?.Float?.Offset ?? 0;
    }

    private Ff7BattlePlacement PosedActorPlacement(int slot)
    {
        Ff7BattlePlacement placement = VisualPlacement(slot);
        int offset = state.ActorModels.SingleOrDefault(model => model.Slot == slot)?.Float?.Offset ?? 0;
        return placement with { Position = placement.Position + new Vector3(0, offset, 0) };
    }

    private Ff7BattleActorModelState AdvanceOriginFloatPresentation(Ff7BattleActorModelState model)
    {
        if ((GetOriginBattleStatuses(model.Slot) & Ff7GuestBattleStatuses.OriginFloat) == 0)
        {
            return model with { Float = null };
        }
        bool admitted = IsAlive(Actor(model.Slot)) && model.Ready && !(state.Action is { } action && action.Command.ActorSlot == model.Slot);
        return model with { Float = AdvanceOriginFloat(model.Float, Tick, admitted) };
    }

    internal static Ff7BattleFloatPresentation AdvanceOriginFloat(Ff7BattleFloatPresentation? previous, long tick, bool admitted)
    {
        Ff7BattleFloatPresentation current = previous ?? new(0, tick, 0, admitted ? HostFloatOffset(0) : 0);
        long elapsed = checked(tick - current.Tick);
        if (elapsed < 0 || current.Remainder is < 0 or >= 120)
        {
            throw new InvalidDataException("The Float presentation clock contradicts its host callback.");
        }
        //47CE10's 31/2 callbacks per second, against the host's60 logic updates.
        long units = checked(elapsed * 31 + current.Remainder);
        long callbacks = units / 120;
        byte phase = current.Phase;
        int offset = admitted ? current.Offset == 0 ? HostFloatOffset(phase) : current.Offset : 0;
        if (admitted && callbacks > 0)
        {
            offset = HostFloatOffset(unchecked((byte)(phase + callbacks - 1)));
            phase = unchecked((byte)(phase + callbacks));
        }
        return new(phase, tick, (int)(units % 120), offset);
    }

    internal static int HostFloatOffset(byte phase) => (int)MathF.Round(NativeBattleFloatPresentation.Vertical50A5F0(phase) * OriginActorToHostScale);
}
