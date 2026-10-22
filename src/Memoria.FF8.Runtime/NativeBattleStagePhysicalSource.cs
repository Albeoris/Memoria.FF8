using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStagePhysicalSource(uint Address, int DirectoryOffset, ImmutableArray<byte> Prefix)
{
    public void Validate()
    {
        if (Address != NativeBattleModelCpuResources.EnemyAddress || Prefix.IsDefaultOrEmpty || DirectoryOffset < 0 || DirectoryOffset > Prefix.Length - 32 || Dword(DirectoryOffset) != 6)
        {
            throw new InvalidDataException("Native stage source requires its actual shared resource prefix and six-section directory.");
        }
        int previous = checked(DirectoryOffset + 32);
        for (int section = 0; section <= 5; section++)
        {
            int offset = Section(section);
            if (offset < previous || offset > Prefix.Length)
            {
                throw new InvalidDataException("Native stage sections leave their loaded prefix.");
            }
            previous = offset;
        }
        if (Section(5) != Prefix.Length || Section(6) < Prefix.Length)
        {
            throw new InvalidDataException("Native stage prefix must end at its actual TIM section.");
        }
        _ = checked(Address + (uint)Prefix.Length);
        for (int group = 0; group < 4; group++)
        {
            _ = Geometry(group);
        }
    }

    public uint? GroupGeometryAddress(int group)
    {
        Validate();
        return Geometry(group) is { } geometry ? checked(Address + (uint)geometry.Start) : null;
    }

    public int? GroupGeometryLength(int group)
    {
        Validate();
        return Geometry(group) is { } geometry ? geometry.End - geometry.Start : null;
    }

    public (NativeBattleFileStaging Staging, NativeBattleModelCpuResources Cpu) Publish500EA0(NativeBattleFileStaging staging, NativeBattleModelCpuResources cpu)
    {
        Validate();
        ushort maximum = cpu.ScratchMaximum ?? throw new NotSupportedException("Native stage load requires its retained projected vertex capacity.");
        for (int group = 0; group < 4; group++)
        {
            if (Geometry(group) is { } geometry)
            {
                int requested = Dword(geometry.End - 4);
                if (maximum < requested)
                {
                    maximum = unchecked((ushort)requested);
                }
            }
        }
        int cursor = checked((Prefix.Length + 3) & ~3);
        return (staging.Write(Address, Prefix.AsSpan()), cpu with { EnemyCursor = cursor, ScratchMaximum = maximum });
    }

    private (int Start, int End)? Geometry(int group)
    {
        if ((uint)group >= 4)
        {
            throw new ArgumentOutOfRangeException(nameof(group));
        }
        int start = Section(group);
        int end = Section(group + 1);
        if (start == end)
        {
            return null;
        }
        if (start > end - 16 || Dword(start) != 3)
        {
            throw new InvalidDataException("Native stage group requires its three-section directory.");
        }
        int skeleton = checked(start + Dword(start + 4));
        int geometry = checked(start + Dword(start + 8));
        int animations = checked(start + Dword(start + 12));
        if (skeleton < start + 16 || skeleton > geometry || geometry > animations - 4 || animations >= end)
        {
            throw new InvalidDataException("Native stage geometry leaves its original group.");
        }
        return (geometry, animations);
    }

    private int Section(int section) => checked(DirectoryOffset + Dword(DirectoryOffset + 4 + section * 4));

    private int Dword(int offset)
    {
        if (offset < 0 || offset > Prefix.Length - 4)
        {
            throw new InvalidDataException("Native stage source DWORD leaves its known prefix.");
        }
        return BinaryPrimitives.ReadInt32LittleEndian(Prefix.AsSpan().Slice(offset, 4));
    }
}
