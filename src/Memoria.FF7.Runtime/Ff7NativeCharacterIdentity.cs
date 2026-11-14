namespace Memoria.FF7.Runtime;

/// <summary>Maps main-party identities to the nine physical native character records.</summary>
public static class Ff7NativeCharacterIdentity
{
    public static int RecordId(int identity)
    {
        return identity switch
        {
            >= 0 and <= 8 => identity,
            9 => 6,
            10 => 7,
            _ => throw new InvalidDataException("FFVII party identity has no supported native character record.")
        };
    }

    public static bool IsPartyMember(IReadOnlyList<int> party, int recordId)
    {
        return party.Any(identity => identity != 255 && RecordId(identity) == recordId);
    }
}
