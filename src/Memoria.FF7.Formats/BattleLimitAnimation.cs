namespace Memoria.FF7.Formats;

public sealed record BattleLimitAnimation(IReadOnlyList<BattlePackedClip> Body, IReadOnlyList<BattlePackedClip> Weapon)
{
    public const int FirstClip = 0x2C;
    private const int ClipsPerBank = 8;

    public static BattleLimitAnimation Read(ReadOnlyMemory<byte> source)
    {
        BinaryCursor cursor = new(source.Span);
        if (cursor.U32() != 2 * ClipsPerBank)
        {
            throw new InvalidDataException("FFVII Limit animation requires eight original body and eight weapon records.");
        }

        IReadOnlyList<BattlePackedClip> body = BattleModelData.ReadClips(source, ref cursor, ClipsPerBank);
        IReadOnlyList<BattlePackedClip> weapon = BattleModelData.ReadClips(source, ref cursor, ClipsPerBank);
        if (cursor.Remaining != 0)
        {
            throw new InvalidDataException("FFVII Limit animation has trailing data beyond its clip records.");
        }
        return new(body, weapon);
    }

    public BattleModelData ApplyTo(BattleModelData model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.BodyClips.Count < FirstClip + ClipsPerBank || Body.Any(clip => clip.RotationCount != model.Bones.Count + 1) || Weapon.Any(clip => clip.RotationCount != 1))
        {
            throw new InvalidDataException("FFVII Limit animation does not match its original actor body and weapon banks.");
        }

        BattlePackedClip[] body = model.BodyClips.ToArray();
        BattlePackedClip[] weapon = new BattlePackedClip[Math.Max(model.WeaponClips.Count, FirstClip + ClipsPerBank)];
        for (int index = 0; index < weapon.Length; index++)
        {
            weapon[index] = index < model.WeaponClips.Count ? model.WeaponClips[index] : new(1, 0, ReadOnlyMemory<byte>.Empty);
        }
        for (int index = 0; index < ClipsPerBank; index++)
        {
            body[FirstClip + index] = Body[index];
            weapon[FirstClip + index] = Weapon[index];
        }
        return model with { BodyClips = Array.AsReadOnly(body), WeaponClips = Array.AsReadOnly(weapon) };
    }
}
