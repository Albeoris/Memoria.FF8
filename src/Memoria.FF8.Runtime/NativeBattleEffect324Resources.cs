using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect324Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Camera, NativeBattleEffectHeapAllocation Vibration, ImmutableDictionary<uint, NativeBattleModuleResource> Sprites, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    public static readonly ImmutableArray<uint> SpriteAddresses = [0xCD0B88, 0xCD0D00, 0xCD0B2C];
    public static readonly ImmutableArray<int> SpriteLengths = [376, 1028, 92];
    public static readonly ImmutableArray<int> SpriteFrameCounts = [14, 9, 3];
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(0xCD1210, 4), (0xCD1218, 96)];
    [JsonIgnore]
    public NativeVibrationBank VibrationBank => new(Convert.ToHexString(SHA256.HashData(Vibration.Data.AsSpan())), [checked((int)Dword(Vibration.Data, 0)), checked((int)Dword(Vibration.Data, 4))], Vibration.Data);
    [JsonIgnore]
    public ImmutableArray<uint> MaterialColors24 => Enumerable.Range(0, 24).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(0xCD1218u + (uint)(index * 4), 4))).ToImmutableArray();
    public NativeBattleModuleResource Sprite(uint address) => Sprites.TryGetValue(address, out NativeBattleModuleResource? source) ? source : throw new NotSupportedException($"Effect324 sprite 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);

    public byte[] ReadStatic(uint address, int length)
    {
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        throw new NotSupportedException($"Effect324 static range 0x{address:X8}/{length} is unsupported.");
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag323.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != 66656 || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != 316 || Vibration is null || Vibration.Data.IsDefault || Vibration.Data.Length != 268 || Sprites is null || Sprites.Count != 3 || StaticFronts is null)
        {
            throw new InvalidDataException("Effect324 requires its complete original resource inputs.");
        }
        if (Dword(MainTim.Data, 0) != 16 || Dword(MainTim.Data, 4) != 9)
        {
            throw new InvalidDataException("Effect324 texture input has an unexpected TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(MainTim.Data, cursor));
            if (length < 12 || length > MainTim.Data.Length - cursor || length != 12 + Word(MainTim.Data, cursor + 8) * Word(MainTim.Data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect324 TIM block leaves its input.");
            }
            cursor += length;
        }
        if (cursor != MainTim.Data.Length)
        {
            throw new InvalidDataException("Effect324 TIM input has trailing bytes.");
        }
        if (Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 24 || Word(Camera.Data, 6) != 316)
        {
            throw new InvalidDataException("Effect324 camera header is invalid.");
        }
        NativeBattleCameraBank bank = new(Camera.Data.AsSpan().Slice(24).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            _ = bank.TrackOffset(selector);
        }
        VibrationBank.Validate();
        for (int index = 0; index < SpriteAddresses.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprite(SpriteAddresses[index]);
            if (sprite.Address != SpriteAddresses[index] || sprite.Bytes.Length != SpriteLengths[index] || Word(sprite.Bytes, 8) != SpriteFrameCounts[index] || unchecked((short)Word(sprite.Bytes, 10 + SpriteFrameCounts[index] * 2)) != -1)
            {
                throw new InvalidDataException("Effect324 sprite extent or terminal directory is invalid.");
            }
            for (int frame = 0; frame < SpriteFrameCounts[index]; frame++)
            {
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
        foreach ((uint address, int length) in StaticRanges)
        {
            _ = ReadStatic(address, length);
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect324 DWORD read leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect324 WORD read leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
