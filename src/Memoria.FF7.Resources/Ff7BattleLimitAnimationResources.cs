using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public static class Ff7BattleLimitAnimationResources
{
    public static BattleLimitAnimation Load(LgpArchive archive, ushort attackId)
    {
        ArgumentNullException.ThrowIfNull(archive);
        string name = attackId switch
        {
            0x80 => "blaver.a00",
            0x87 => "hvshot.a00",
            0x95 => "limfast.a00",
            _ => throw new NotSupportedException($"FFVII Limit {attackId:X2} requires its original animation overlay.")
        };
        IReadOnlyList<LgpEntry> members = archive.Find(name);
        if (members.Count != 1)
        {
            throw new InvalidDataException($"FFVII Limit animation '{name}' resolves to {members.Count} original members.");
        }
        return BattleLimitAnimation.Read(archive.ReadEntry(members[0]));
    }
}
