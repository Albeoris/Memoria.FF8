namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    internal string ReadAutoPlayFieldProgramHash(int fieldId)
    {
        return assets.ReadFieldProgramHash(fieldId);
    }
}
