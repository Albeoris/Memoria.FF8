using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect344Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect344Resources(NativeBattleEffectHeapAllocation Music, NativeBattleEffectHeapAllocation Camera)
{
    public void Validate()
    {
        if (Music is null || Music.Data.IsDefault || Music.Data.Length != MusicLength || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != CameraLength)
        {
            throw new InvalidDataException("Effect344 requires its two exact original embedded resources.");
        }
        if (BinaryPrimitives.ReadInt32LittleEndian(Music.Data.AsSpan()) != 2 || BinaryPrimitives.ReadInt32LittleEndian(Music.Data.AsSpan().Slice(4)) != 16 || BinaryPrimitives.ReadInt32LittleEndian(Music.Data.AsSpan().Slice(8)) != 129584)
        {
            throw new InvalidDataException("Effect344 music requires its original ordered section directory.");
        }
        NativeBattleMusicLoadSource source = NativeBattleMusicLoadSource.Read501A20(Music.Data);
        if (source.SoundBank.Length != 129568 || source.Music.Length != 10528 || BinaryPrimitives.ReadUInt16LittleEndian(source.Music.AsSpan().Slice(6)) != source.Music.Length)
        {
            throw new InvalidDataException("Effect344 music sections leave their original bounded front.");
        }
        _ = source.ReadIdentity();
        ReadOnlySpan<byte> camera = Camera.Data.AsSpan();
        if (BinaryPrimitives.ReadUInt16LittleEndian(camera) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(camera.Slice(2)) != 8 || BinaryPrimitives.ReadUInt16LittleEndian(camera.Slice(4)) != 12 || BinaryPrimitives.ReadUInt16LittleEndian(camera.Slice(6)) != CameraLength || !camera.Slice(8, 4).SequenceEqual(new byte[] { 6, 0, 0, 2 }))
        {
            throw new InvalidDataException("Effect344 camera requires its original control and track directories.");
        }
        if (BinaryPrimitives.ReadUInt16LittleEndian(camera.Slice(12)) != 1 || BinaryPrimitives.ReadUInt16LittleEndian(camera.Slice(14)) != 8 || BinaryPrimitives.ReadUInt16LittleEndian(camera.Slice(16)) != 508 || BinaryPrimitives.ReadUInt16LittleEndian(camera.Slice(18)) != 0)
        {
            throw new InvalidDataException("Effect344 camera bank requires its original one-group bounds.");
        }
        NativeBattleCameraBank bank = new(Camera.Data.AsSpan().Slice(12).ToArray().ToImmutableArray());
        if (bank.TrackOffset(0) != 24)
        {
            throw new InvalidDataException("Effect344 camera selected track changed its original source offset.");
        }
    }

    public byte[] ReadStatic(uint address, int length)
    {
        if (length >= 0 && address >= MusicAddress && (ulong)address + (uint)length <= (ulong)MusicAddress + MusicLength)
        {
            return Music.Data.AsSpan().Slice(checked((int)(address - MusicAddress)), length).ToArray();
        }
        if (length >= 0 && address >= CameraAddress && (ulong)address + (uint)length <= (ulong)CameraAddress + CameraLength)
        {
            return Camera.Data.AsSpan().Slice(checked((int)(address - CameraAddress)), length).ToArray();
        }
        throw new NotSupportedException("Effect344 static read leaves its two original resource fronts.");
    }
}
