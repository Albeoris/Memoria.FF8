namespace Memoria.FF8;

internal static class ModalRecoveryControlPolicy
{
    internal static bool Allows(ControlRequest request)
    {
        return request.Command.ToLowerInvariant() is "status" or "world-status" or "world-observe" or "world-snapshot" or "quit" or "cancel-update" || request.Command.Equals("world-autoplay", StringComparison.OrdinalIgnoreCase) && string.Equals(request.Name, "status", StringComparison.OrdinalIgnoreCase);
    }
}
