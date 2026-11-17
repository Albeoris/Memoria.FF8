namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    internal bool IsKnownAutoPlayDestination(ushort field)
    {
        return field > 64 && field < mapList.Count;
    }
}
