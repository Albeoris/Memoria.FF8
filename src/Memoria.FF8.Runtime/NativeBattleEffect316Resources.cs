using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect316Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect316Resources(NativeBattleEffectInitializerAsset MainTim, ImmutableArray<byte> EmbeddedTim, NativeBattleEffectHeapAllocation Camera, NativeBattleEffectHeapAllocation Vibration, uint Sound, NativeBattleModuleResource ProjectedQuads)
{
    public const int ProjectedQuadsLength = 364;
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(EmbeddedTimAddress, 66080), (CameraAddress, 256), (VibrationAddress, 270), (SoundAddress, 4), (ProjectedQuadsAddress, ProjectedQuadsLength)];

    public NativeBattleModuleResource ReadProjectedQuads(uint address) => address == ProjectedQuadsAddress ? ProjectedQuads : throw new NotSupportedException("Effect316 projected source address is unsupported.");

    public byte[] ReadStatic(uint address, int length)
    {
        foreach ((uint start, ImmutableArray<byte> bytes) in Fronts())
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        throw new NotSupportedException("Effect316 static read leaves its original source fronts.");
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag315.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != 67616 || EmbeddedTim.IsDefault || EmbeddedTim.Length != 66080 || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != 256 || Vibration is null || Vibration.Data.IsDefault || Vibration.Data.Length != 270 || ProjectedQuads is null || ProjectedQuads.Address != ProjectedQuadsAddress || ProjectedQuads.Bytes.IsDefault || ProjectedQuads.Bytes.Length != ProjectedQuadsLength)
        {
            throw new InvalidDataException("Effect316 requires its complete original resource inputs.");
        }
        ValidateTim(MainTim.Data);
        ValidateTim(EmbeddedTim);
        if (Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 24 || Word(Camera.Data, 6) != 256)
        {
            throw new InvalidDataException("Effect316 camera header leaves its original track domains.");
        }
        if (Dword(Vibration.Data, 0) != 0 || Dword(Vibration.Data, 4) != 256 || Word(Vibration.Data, 256) != 6 || Word(Vibration.Data, 258) != 4)
        {
            throw new InvalidDataException("Effect316 vibration channels leave their original input.");
        }
        if (Word(ProjectedQuads.Bytes, 8) != 12 || unchecked((short)Word(ProjectedQuads.Bytes, 34)) != -1)
        {
            throw new InvalidDataException("Effect316 projected source has an invalid frame directory.");
        }
        for (int life = 11; life >= 1; life--)
        {
            int offset = Word(ProjectedQuads.Bytes, 8 + 2 * (12 - life));
            int count = checked((int)Dword(ProjectedQuads.Bytes, offset));
            if (count < 0 || count > 2 || offset > ProjectedQuads.Bytes.Length - 4 - 20 * count)
            {
                throw new InvalidDataException("Effect316 projected frame leaves its original records.");
            }
        }
    }

    private IEnumerable<(uint Address, ImmutableArray<byte> Bytes)> Fronts()
    {
        yield return (EmbeddedTimAddress, EmbeddedTim);
        yield return (CameraAddress, Camera.Data);
        yield return (VibrationAddress, Vibration.Data);
        yield return (SoundAddress, BitConverter.GetBytes(Sound).ToImmutableArray());
        yield return (ProjectedQuadsAddress, ProjectedQuads.Bytes);
    }

    private static void ValidateTim(ImmutableArray<byte> data)
    {
        if (Dword(data, 0) != 16 || Dword(data, 4) != 9)
        {
            throw new InvalidDataException("Effect316 texture has an invalid TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            if (length < 12 || length > data.Length - cursor || length != 12 + Word(data, cursor + 8) * Word(data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect316 TIM block leaves its input.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect316 TIM input has trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect316 DWORD leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect316 WORD leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
