using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleQuezacotlResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        ImmutableArray<NativeBattleEffectFileAsset>.Builder files = ImmutableArray.CreateBuilder<NativeBattleEffectFileAsset>(23);
        //6C3940's explicit5341D0 cases request original IDs1F9..20F, not effect IDs.
        for (short fileId = 0x1F9; fileId <= 0x20F; fileId++)
        {
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, checked(0xB84CCCu + 4u * (uint)fileId), 4).AsSpan());
            ImmutableArray<byte> name = NativeBattleEffectStaticResourcesReader.Read(executable, address, 128);
            int length = name.AsSpan().IndexOf((byte)0);
            if (length < 1)
            {
                throw new InvalidDataException("Native Quezacotl file directory lacks its original terminated name.");
            }
            files.Add(new(fileId, Encoding.ASCII.GetString(name.AsSpan().Slice(0, length)), []));
        }
        return files.MoveToImmutable();
    }

    public static NativeBattleQuezacotlResources Read(byte[] executable, NativeBattleEffectInitializerAsset initializer)
    {
        if (initializer.Name != "mag115.tim")
        {
            throw new InvalidDataException("Native6C3560 requires its actual mag115.tim source.");
        }
        NativeBattleEffectHeapAllocation first = new("FF8_EN:1298C68", NativeBattleEffectStaticResourcesReader.Read(executable, 0x1298C68, 0x109FC));
        NativeBattleEffectHeapAllocation second = new("FF8_EN:12A9664", NativeBattleEffectStaticResourcesReader.Read(executable, 0x12A9664, 0x4D9C));
        //6C3803 addresses pattern1 directly; DWORD0 is NULL, not a pointer-table length.
        ImmutableArray<byte> table = NativeBattleEffectStaticResourcesReader.Read(executable, 0x12AE500, 8);
        ImmutableArray<int>.Builder offsets = ImmutableArray.CreateBuilder<int>(2);
        int end = 8;
        for (int index = 0; index < 2; index++)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan().Slice(4 * index));
            offsets.Add(offset);
            if (offset == 0)
            {
                continue;
            }
            ImmutableArray<byte> header = NativeBattleEffectStaticResourcesReader.Read(executable, checked(0x12AE500u + (uint)offset), 4);
            //4A2B80's interpolation can read the following original intensity/duration bytes.
            end = Math.Max(end, checked(offset + 4 + BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan()) + BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan().Slice(2)) + 2));
        }
        NativeVibrationBank vibration = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, offsets.MoveToImmutable(), NativeBattleEffectStaticResourcesReader.Read(executable, 0x12AE500, end));
        vibration.Validate();
        ImmutableArray<byte> anchorWords = NativeBattleEffectStaticResourcesReader.Read(executable, 0x12AFE40, 22);
        return new(initializer, first, second, vibration)
        {
            AnchorVertexIndices11 = Enumerable.Range(0, 11).Select(index => BinaryPrimitives.ReadInt16LittleEndian(anchorWords.AsSpan().Slice(2 * index))).ToImmutableArray(),
            InitialSound = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0x12AE4F0, 4).AsSpan()),
            Frame65Sound = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0x12AE4F4, 4).AsSpan()),
            Frame209Sound = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0x12AE4F8, 4).AsSpan()),
            Frame229FlashColor = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0x12AE7EC, 4).AsSpan()),
            FlashColors = Enumerable.Range(0, 3).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, checked(0x12AE7E0u + 4u * (uint)index), 4).AsSpan())).ToImmutableArray(),
            Ribbon = NativeBattleQuezacotlRibbonResourcesReader.Read(executable)
        };
    }
}

public sealed partial class GameAssets
{
    private NativeBattleQuezacotlResources? nativeQuezacotlResources;

    public NativeBattleQuezacotlResources ReadNativeQuezacotlResources()
    {
        if (nativeQuezacotlResources is null)
        {
            using IFlArchive magic = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "magic.fs"), FlArchiveRepresentation.Files);
            using IFlArchive battle = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "battle.fs"), FlArchiveRepresentation.Files);
            NativeBattleEffectInitializerAsset initializer = new("mag115.tim", Read(magic, "mag115.tim").ToImmutableArray());
            byte[] executable = File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe"));
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleQuezacotlResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeQuezacotlResources = NativeBattleQuezacotlResourcesReader.Read(executable, initializer) with { Files = files };
        }
        return nativeQuezacotlResources;
    }
}
