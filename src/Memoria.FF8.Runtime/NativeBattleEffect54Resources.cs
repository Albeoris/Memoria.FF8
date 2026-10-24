using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect54Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect54Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Camera, NativeBattleEffectHeapAllocation OriginalSourceBank, ImmutableDictionary<uint, NativeBattleModuleResource> Sprites, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    public const int SourceLength = 3556;
    public const int DirectoryLength = 260;
    public const int DescriptorLength = 344;
    public static readonly ImmutableArray<uint> DescriptorAddresses = [0x17AAE38, 0x17AAF90];
    public static readonly ImmutableArray<uint> SpriteAddresses = [FirstSpriteAddress, SecondSpriteAddress];
    public static readonly ImmutableArray<int> SpriteLengths = [524, 248];
    public static readonly ImmutableArray<int> SpriteFrameCounts = [15, 9];
    public static readonly ImmutableArray<uint> MutableDwordAddresses = new uint[] { SourceAddress, SourceAddress + 4, SourceAddress + 8, SourceAddress + 0x44, SourceAddress + 0x48 }.Concat(DescriptorAddresses.SelectMany(address => Enumerable.Range(0, 32).Select(index => address + 0xC8 + (uint)(index * 4)))).Order().ToImmutableArray();
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(SoundAddress, 4), (0x17AA70C, 27), (0x17AA734, 54), (0x17AA76C, 54), (0x17AA7A4, 54), (0x17AAB94, 14), (0x17AABBC, 28), (0x17AABD8, 28), (0x17AABF4, 28), (0x17AA7DC, 30), (0x17AA7FC, 30), (0x17AA81C, 30), (0x17AAC10, 18), (0x17AAC24, 18), (0x17AAC38, 18)];

    public NativeBattleModuleResource Sprite(uint address) => Sprites.TryGetValue(address, out NativeBattleModuleResource? sprite) ? sprite : throw new NotSupportedException($"Effect54 sprite 0x{address:X8} is unsupported.");

    public byte[] OriginalMutableDword(uint address)
    {
        if (!MutableDwordAddresses.Contains(address))
        {
            throw new InvalidDataException("Effect54 mutable DWORD address leaves its selected source cells.");
        }
        return OriginalBytes(address, 4);
    }

    public byte[] ReadStatic(uint address, int length)
    {
        if (OverlapsMutable(address, length))
        {
            throw new NotSupportedException("Effect54 mutable source reads require their current staging owner.");
        }
        foreach ((uint start, ImmutableArray<byte> data) in StaticFronts)
        {
            if (Contains(start, data.Length, address, length))
            {
                return data.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        if (Contains(SourceAddress, SourceLength, address, length))
        {
            return OriginalBytes(address, length);
        }
        throw new NotSupportedException($"Effect54 immutable range 0x{address:X8}/{length} is unsupported.");
    }

    public byte[] ReadResource(NativeBattleFileStaging staging, uint address, int length)
    {
        byte[] result = OriginalBytes(address, length);
        for (int index = 0; index < length; index++)
        {
            uint current = checked(address + (uint)index);
            if (MutableDwordAddresses.Any(cell => current >= cell && current < cell + 4))
            {
                result[index] = staging.Read(current, 1)[0];
            }
        }
        return result;
    }

    public ImmutableArray<byte> ReadResource(NativeBattleFileStaging staging, uint address)
    {
        int length;
        if (address == SourceAddress)
        {
            length = DirectoryLength;
        }
        else if (DescriptorAddresses.Contains(address))
        {
            length = DescriptorLength;
        }
        else
        {
            int index = SpriteAddresses.IndexOf(address);
            if (index < 0)
            {
                throw new NotSupportedException($"Effect54 resource 0x{address:X8} has no selected finite extent.");
            }
            length = SpriteLengths[index];
        }
        return ReadResource(staging, address, length).ToImmutableArray();
    }

    public ImmutableArray<byte> ReadBank(NativeBattleFileStaging staging) => ReadResource(staging, SourceAddress, SourceLength).ToImmutableArray();

    public NativeBattleFileStaging PublishBank(NativeBattleFileStaging staging, ImmutableArray<byte> current)
    {
        if (current.IsDefault || current.Length != SourceLength)
        {
            throw new InvalidDataException("Effect54 current bank leaves its exact source extent.");
        }
        for (int offset = 0; offset < SourceLength; offset++)
        {
            uint address = SourceAddress + (uint)offset;
            if (!OverlapsMutable(address, 1) && current[offset] != OriginalSourceBank.Data[offset])
            {
                throw new InvalidDataException("Effect54 attempted to publish an unowned source byte.");
            }
        }
        foreach (uint address in MutableDwordAddresses)
        {
            _ = staging.Read(address, 4);
            staging = staging.Write(address, current.AsSpan(checked((int)(address - SourceAddress)), 4));
        }
        return staging;
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag053.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != 165408 || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != 176 || OriginalSourceBank is null || OriginalSourceBank.Data.IsDefault || OriginalSourceBank.Data.Length != SourceLength || Sprites is null || Sprites.Count != 2 || StaticFronts is null)
        {
            throw new InvalidDataException("Effect54 requires its exact original resource inputs.");
        }
        if (Dword(MainTim.Data, 0) != 16 || Dword(MainTim.Data, 4) != 9 || Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 40 || Word(Camera.Data, 6) != 176 || Dword(OriginalSourceBank.Data, 0) != 0)
        {
            throw new InvalidDataException("Effect54 texture, camera or unrelocated directory header is invalid.");
        }
        int timCursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(MainTim.Data, timCursor));
            if (length < 12 || length > MainTim.Data.Length - timCursor || length != 12 + Word(MainTim.Data, timCursor + 8) * Word(MainTim.Data, timCursor + 10) * 2)
            {
                throw new InvalidDataException("Effect54 TIM block leaves its original input.");
            }
            timCursor += length;
        }
        if (timCursor != MainTim.Data.Length)
        {
            throw new InvalidDataException("Effect54 TIM input has trailing bytes.");
        }
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                uint relative = Dword(OriginalSourceBank.Data, 4 + table * 64 + index * 4);
                uint expected = table == 0 && index < 2 ? DescriptorAddresses[index] - SourceAddress : table == 1 && index < 2 ? SpriteAddresses[index] - SourceAddress : 0;
                if (relative != expected)
                {
                    throw new InvalidDataException("Effect54 directory leaves its selected descriptor and sprite domains.");
                }
            }
        }
        for (int descriptorIndex = 0; descriptorIndex < DescriptorAddresses.Length; descriptorIndex++)
        {
            uint descriptor = DescriptorAddresses[descriptorIndex];
            int offset = checked((int)(descriptor - SourceAddress));
            ImmutableArray<byte> data = OriginalSourceBank.Data;
            if (data[offset + 0x11] != (descriptorIndex == 0 ? 0 : 3) || data[offset + 0x12] != (descriptorIndex == 0 ? 27 : 14) || data[offset + 0x16] != 0 || data[offset + 0x36] != (descriptorIndex == 0 ? 1 : 4) || data[offset + 0x37] != 0 || data[offset + 0x22] != descriptorIndex || data[offset + 0x27] != 0 || data[offset + 0x1E] != 0 || data[offset + 0x30] != 0)
            {
                throw new InvalidDataException("Effect54 descriptor leaves its selected emitter and sprite lifetime domain.");
            }
            for (int field = 0xC8; field <= 0x144; field += 4)
            {
                uint relative = Dword(OriginalSourceBank.Data, offset + field);
                if (relative >= SourceLength)
                {
                    throw new InvalidDataException("Effect54 descriptor channel leaves its source bank.");
                }
            }
        }
        for (int index = 0; index < 2; index++)
        {
            NativeBattleModuleResource sprite = Sprite(SpriteAddresses[index]);
            if (sprite.Address != SpriteAddresses[index] || sprite.Bytes.IsDefault || sprite.Bytes.Length != SpriteLengths[index] || Word(sprite.Bytes, 8) != SpriteFrameCounts[index] || !sprite.Bytes.AsSpan().SequenceEqual(OriginalBytes(sprite.Address, sprite.Bytes.Length)))
            {
                throw new InvalidDataException("Effect54 sprite leaves its original finite source.");
            }
            for (int frame = 0; frame < SpriteFrameCounts[index]; frame++)
            {
                NativeBattleModuleSpriteFrame selected = sprite.ProjectedFrame571C80(frame);
                if (selected.ExtendedPacket || selected.Quads.Length == 0 || selected.Quads.Length > (index == 0 ? 40 : 20))
                {
                    throw new InvalidDataException("Effect54 sprite frame leaves its selected positive-prefix quad domain.");
                }
                for (int quad = 0; quad < selected.Quads.Length; quad += 20)
                {
                    ushort flags = Word(selected.Quads, quad + 14);
                    if ((flags >> 12) > 1 || (flags & 0xC00) != 0x400)
                    {
                        throw new InvalidDataException("Effect54 sprite palette or transform mode is unsupported.");
                    }
                }
            }
            if (BinaryPrimitives.ReadInt16LittleEndian(sprite.Bytes.AsSpan(10 + SpriteFrameCounts[index] * 2, 2)) >= 0)
            {
                throw new InvalidDataException("Effect54 sprite terminal directory entry is not negative.");
            }
        }
        foreach ((uint address, int length) in StaticRanges)
        {
            _ = ReadStatic(address, length);
        }
    }

    private byte[] OriginalBytes(uint address, int length)
    {
        if (!Contains(SourceAddress, SourceLength, address, length))
        {
            throw new NotSupportedException($"Effect54 source range 0x{address:X8}/{length} is unsupported.");
        }
        return OriginalSourceBank.Data.AsSpan(checked((int)(address - SourceAddress)), length).ToArray();
    }

    private static bool OverlapsMutable(uint address, int length) => length >= 0 && MutableDwordAddresses.Any(cell => (ulong)address < (ulong)cell + 4 && (ulong)cell < (ulong)address + (uint)length);
    private static bool Contains(uint start, int size, uint address, int length) => length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)size;
    private static uint Dword(ImmutableArray<byte> data, int offset) => offset >= 0 && offset <= data.Length - 4 ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4)) : throw new InvalidDataException("Effect54 DWORD read leaves its input.");
    private static ushort Word(ImmutableArray<byte> data, int offset) => offset >= 0 && offset <= data.Length - 2 ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2)) : throw new InvalidDataException("Effect54 WORD read leaves its input.");
}
