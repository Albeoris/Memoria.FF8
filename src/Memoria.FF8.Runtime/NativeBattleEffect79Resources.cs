using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect79Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect79NodeResourceBank(uint Address, ImmutableArray<byte> Data)
{
    public void Validate()
    {
        if (!NodeSourceLengths.TryGetValue(Address, out int length) || Data.IsDefault || Data.Length != length || Dword(0) != 0)
        {
            throw new InvalidDataException("Effect79 requires its selected original cold relative node bank.");
        }
        int nodes = 0;
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                uint pointer = Directory(table, index);
                if (pointer != 0 && (pointer >= Data.Length || table == 0 && pointer > Data.Length - 344))
                {
                    throw new InvalidDataException("Effect79 relative node directory leaves its exact source front.");
                }
                if (table != 0 || pointer == 0)
                {
                    continue;
                }
                nodes++;
                for (int field = 0xC8; field < 0x148; field += 4)
                {
                    if (Dword(checked((int)pointer + field)) >= Data.Length)
                    {
                        throw new InvalidDataException("Effect79 channel relocation leaves its own current node bank.");
                    }
                }
            }
        }
        int expected = Address == 0x1762E40 ? 13 : Address == 0x176A958 ? 2 : 1;
        if (nodes != expected)
        {
            throw new InvalidDataException("Effect79 source has different original node-record coverage.");
        }
    }

    public uint Directory(int table, int index)
    {
        if ((uint)table >= 4 || (uint)index >= 16)
        {
            throw new InvalidDataException("Effect79 directory selection leaves its four sixteen-pointer tables.");
        }
        return Dword(4 + table * 64 + index * 4);
    }

    private uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan().Slice(offset, 4));
}

public sealed record NativeBattleEffect79Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Camera, NativeBattleEffectHeapAllocation Vibration, ImmutableDictionary<uint, NativeBattleEffect79NodeResourceBank> NodeBanks, NativeBattleEffectHeapAllocation IoScript, ImmutableDictionary<short, NativeBattleEffectFileAsset> Files, ImmutableDictionary<uint, uint> Sounds, ImmutableDictionary<uint, ImmutableArray<byte>> Sprites, ImmutableArray<short> PointSelectors, uint TargetRecordAddress)
{
    public ImmutableArray<byte> ReadNodeBank(uint address) => NodeBanks.TryGetValue(address, out NativeBattleEffect79NodeResourceBank? bank) ? bank.Data : throw new NotSupportedException("Effect79 requires one of its three selected original node resources.");

    public NativeBattleEffectFileAsset ReadFile(int fileId) => fileId is >= 396 and <= 401 && Files.TryGetValue(checked((short)fileId), out NativeBattleEffectFileAsset? file) ? file : throw new NotSupportedException("Effect79 file selection leaves its six original named resources.");

    public NativeBattleGfAnimationSource ReadProgramme(uint address)
    {
        int index = ProgrammeAddresses.IndexOf(address);
        if (index < 0)
        {
            throw new NotSupportedException("Effect79 programme address leaves its original staged source identities.");
        }
        return new(address, ReadFile(396 + index).Data);
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag078.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != TextureLength || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Vibration is null || Vibration.Data.IsDefault || Vibration.Data.Length != VibrationLength || NodeBanks is null || NodeBanks.Count != 3 || IoScript is null || IoScript.Data.IsDefault || IoScript.Data.Length != ScriptLength || Files is null || Files.Count != 6 || Sounds is null || Sounds.Count != 3 || Sprites is null || Sprites.Count != 3 || PointSelectors.IsDefault || PointSelectors.Length != 18 || TargetRecordAddress != TargetAddress)
        {
            throw new InvalidDataException("Effect79 resources have different original identities or exact front dimensions.");
        }
        foreach ((uint address, int length) in NodeSourceLengths)
        {
            if (!NodeBanks.TryGetValue(address, out NativeBattleEffect79NodeResourceBank? bank) || bank.Address != address || bank.Data.Length != length)
            {
                throw new InvalidDataException("Effect79 is missing a selected addressed node bank.");
            }
            bank.Validate();
        }
        for (int index = 0; index < 4; index++)
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(index * 2, 2)) != new ushort[] { 2, 8, 24, 804 }[index])
            {
                throw new InvalidDataException("Effect79 requires its original two-track camera directory.");
            }
        }
        short[] points = [48, 35, 25, 44, 45, 46, 47, 34, 43, 56, 40, 28, 52, 53, 54, 55, 39, 51];
        if (!PointSelectors.SequenceEqual(points) || Dword(Vibration.Data, 0) != 0 || Dword(Vibration.Data, 4) != 256 || Word(Vibration.Data, 256) != 44 || Word(Vibration.Data, 258) != 0)
        {
            throw new InvalidDataException("Effect79 point selectors or reached vibration channel differ from their original source.");
        }
        string[] names = ["MAG078_B.1S0", "MAG078_B.1S1", "MAG078_B.1S2", "MAG078_B.1S3", "MAG078_B.1T0", "MAG078_B.9M0"];
        int[] lengths = [5292, 4604, 4544, 6092, 33312, 136136];
        for (int index = 0; index < 6; index++)
        {
            NativeBattleEffectFileAsset file = ReadFile(396 + index);
            if (file.FileId != 396 + index || file.Name != names[index] || file.Data.IsDefault || file.Data.Length != lengths[index])
            {
                throw new InvalidDataException("Effect79 file identity disagrees with its original selected directory and declared extent.");
            }
        }
        int[] opcodes = [0, 3, 3, 3, 3, 1, 255];
        for (int index = 0; index < 7; index++)
        {
            if (Word(IoScript.Data, index * 16) != opcodes[index])
            {
                throw new InvalidDataException("Effect79 requires its complete original wait/programmes/TIM/end IO script.");
            }
            if (index is >= 1 and <= 5 && (Word(IoScript.Data, index * 16 + 2) != 395 + index || Dword(IoScript.Data, index * 16 + 8) != lengths[index - 1] || Word(IoScript.Data, index * 16 + 4) != (index == 5 ? 0 : index - 1)))
            {
                throw new InvalidDataException("Effect79 IO record does not match its selected original file front.");
            }
        }
        ValidateTim(MainTim.Data, [320, 240, 256, 10, 384, 256, 320, 256]);
        ValidateTim(ReadFile(400).Data, [320, 251, 256, 1, 704, 256, 64, 256]);
        ImmutableArray<byte> music = ReadFile(401).Data;
        if (Dword(music, 0) != 2 || Dword(music, 4) != 16 || Dword(music, 8) != 131520 || Dword(music, 12) != 136136 || NativeBattleMusicLoadSource.Read501A20(music).ReadIdentity().EncodedAkaoId != 77)
        {
            throw new InvalidDataException("Effect79 music requires its original two sections and reached AKAO identity.");
        }
        uint[] soundAddresses = [0x1580B24, 0x1580B28, 0x1580B2C];
        for (int index = 0; index < soundAddresses.Length; index++)
        {
            if (!Sounds.TryGetValue(soundAddresses[index], out uint sound) || sound != 0x668B9u + (uint)(index * 3))
            {
                throw new InvalidDataException("Effect79 sound directory differs from the original packed events.");
            }
        }
        foreach (uint address in new uint[] { 0x1580B30, 0x1580D78, 0x1580E54 })
        {
            if (!Sprites.TryGetValue(address, out ImmutableArray<byte> bytes) || bytes.IsDefaultOrEmpty)
            {
                throw new InvalidDataException("Effect79 is missing an original direct sprite resource.");
            }
        }
    }

    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));

    private static void ValidateTim(ImmutableArray<byte> data, ushort[] rectangles)
    {
        if (Dword(data, 0) != 16 || Dword(data, 4) != 9)
        {
            throw new InvalidDataException("Effect79 requires its original indexed TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            for (int index = 0; index < 4; index++)
            {
                if (Word(data, cursor + 4 + index * 2) != rectangles[block * 4 + index])
                {
                    throw new InvalidDataException("Effect79 TIM rectangle differs from its actual VRAM placement.");
                }
            }
            if (length != 12 + 2 * rectangles[block * 4 + 2] * rectangles[block * 4 + 3] || length > data.Length - cursor)
            {
                throw new InvalidDataException("Effect79 texture block leaves its selected original bytes.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect79 texture has bytes outside its two original blocks.");
        }
    }
}
