namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string NativeCharacterName(int entity)
    {
        string original = entity switch
        {
            0 => "Squall",
            1 => "Rinoa",
            2 => "Angelo",
            _ => throw new NotSupportedException("Native character naming only supports its original0/1/2 subjects.")
        };
        return Scene.Names.GetValueOrDefault(entity, original);
    }
}
