namespace Memoria.FF7.Formats;

public sealed record BattleBone(int Parent, float Length, bool HasModel);
public sealed record BattlePackedClip(uint RotationCount, uint FrameCount, ReadOnlyMemory<byte> PackedData);
public sealed record BattleModelData(uint Type, IReadOnlyList<BattleBone> Bones, IReadOnlyList<BattlePackedClip> BodyClips, IReadOnlyList<BattlePackedClip> WeaponClips)
{
    public static BattleModelData Read(ReadOnlySpan<byte> skeleton, ReadOnlyMemory<byte> animationPack)
    {
        BinaryCursor model = new(skeleton);
        uint type = model.U32();
        model.Seek(12);
        uint boneCount = model.U32();
        model.Seek(28);
        uint bodyClips = model.U32();
        model.Seek(40);
        uint weaponClips = model.U32();
        model.Seek(52);
        if (boneCount == 0)
        {
            throw new NotSupportedException("FFVII battle stage geometry does not use the actor skeleton format.");
        }
        if (boneCount != model.Remaining / 12 || model.Remaining % 12 != 0)
        {
            throw new InvalidDataException("FFVII battle skeleton bone count does not match its record area.");
        }
        BattleBone[] bones = new BattleBone[(int)boneCount];
        for (int index = 0; index < bones.Length; index++)
        {
            int parent = model.I32();
            float length = model.F32();
            if (parent < -1 || parent >= index)
            {
                throw new InvalidDataException("FFVII battle bone parent must precede its child.");
            }
            bones[index] = new(parent, length, model.U32() != 0);
        }

        BinaryCursor animations = new(animationPack.Span);
        uint totalClips = animations.U32();
        if ((ulong)bodyClips + weaponClips != totalClips || totalClips > animations.Remaining / 12)
        {
            throw new InvalidDataException("FFVII battle animation counts do not match the actor's skeleton or the animation pack bounds.");
        }
        IReadOnlyList<BattlePackedClip> body = ReadClips(animationPack, ref animations, (int)bodyClips);
        IReadOnlyList<BattlePackedClip> weapon = ReadClips(animationPack, ref animations, (int)weaponClips);
        if (animations.Remaining != 0)
        {
            throw new InvalidDataException("FFVII battle animation pack has data beyond its declared clip records.");
        }
        return new(type, Array.AsReadOnly(bones), body, weapon);
    }

    internal static IReadOnlyList<BattlePackedClip> ReadClips(ReadOnlyMemory<byte> pack, ref BinaryCursor cursor, int count)
    {
        BattlePackedClip[] clips = new BattlePackedClip[count];
        for (int index = 0; index < clips.Length; index++)
        {
            uint rotationCount = cursor.U32();
            uint frameCount = cursor.U32();
            int packedSize = cursor.I32();
            int start = cursor.Position;
            cursor.Skip(packedSize);
            clips[index] = new(rotationCount, frameCount, pack.Slice(start, packedSize));
        }
        return Array.AsReadOnly(clips);
    }
}

public sealed record BattleActionBank(ushort CollisionRadius, ReadOnlyMemory<byte> JointSelectors, IReadOnlyList<ReadOnlyMemory<byte>> Scripts)
{
    private const int TableStart = 104;

    public static BattleActionBank Read(ReadOnlyMemory<byte> source)
    {
        if (source.Length == 8)
        {
            throw new NotSupportedException("FFVII battle action member contains no actor script table.");
        }
        BinaryCursor cursor = new(source.Span);
        cursor.Seek(4);
        ushort radius = cursor.U16();
        cursor.Seek(TableStart);
        ReadOnlyMemory<byte> joints = source.Slice(18, 16);
        uint dataStart = cursor.U32();
        if (dataStart == 0)
        {
            throw new NotSupportedException("FFVII battle action table without an initial script offset is unsupported.");
        }
        if (dataStart > source.Length || dataStart <= TableStart || (dataStart - TableStart) % 4 != 0)
        {
            throw new InvalidDataException("FFVII battle action table overlaps the header or exceeds the member.");
        }
        int count = ((int)dataStart - TableStart) / 4;
        cursor.Seek(TableStart);
        int[] offsets = new int[count];
        SortedSet<int> boundaries = [source.Length];
        for (int index = 0; index < count; index++)
        {
            uint offset = cursor.U32();
            if (offset == 0)
            {
                continue;
            }
            if (offset < dataStart || offset > source.Length)
            {
                throw new InvalidDataException("FFVII battle action script lies outside its member data area.");
            }
            offsets[index] = (int)offset;
            boundaries.Add((int)offset);
        }

        int[] ordered = boundaries.ToArray();
        ReadOnlyMemory<byte>[] scripts = new ReadOnlyMemory<byte>[count];
        for (int index = 0; index < offsets.Length; index++)
        {
            int offset = offsets[index];
            if (offset != 0 && offset != source.Length)
            {
                int boundaryIndex = Array.BinarySearch(ordered, offset);
                scripts[index] = source[offset..ordered[boundaryIndex + 1]];
            }
        }
        return new(radius, joints, Array.AsReadOnly(scripts));
    }
}
