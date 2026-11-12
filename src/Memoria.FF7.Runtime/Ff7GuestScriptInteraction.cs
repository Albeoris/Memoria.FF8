namespace Memoria.FF7.Runtime;

/// <summary>Describes an imported command's interaction with host scripts without reusing foreign action indices.</summary>
public sealed record Ff7GuestScriptInteraction(byte CommandId, ushort ActionId, int? DrawSourceSlot = null, bool InspectionOnly = false, bool HasRecipientEffect = false);

public interface IFf7BattleGuestScriptActions
{
    Ff7GuestScriptInteraction DescribeScriptInteraction(byte command, ushort ability, int targetSlot);
}
