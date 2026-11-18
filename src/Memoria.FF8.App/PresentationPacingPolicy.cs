namespace Memoria.FF8;

internal static class PresentationPacingPolicy
{
    internal static int SoftwareFrameLimit(int requestedLimit, bool synchronizedToDisplay, int? displayRefreshRate)
    {
        if (synchronizedToDisplay && displayRefreshRate is > 1 && requestedLimit >= displayRefreshRate.Value)
        {
            return 0;
        }
        return requestedLimit;
    }
}
