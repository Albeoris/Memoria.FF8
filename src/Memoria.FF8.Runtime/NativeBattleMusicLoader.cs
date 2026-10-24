using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Immutable original resource sections; live501A20 retains pointers into the shared staging owner.</summary>
public sealed record NativeBattleMusicLoadSource([property: JsonRequired] ImmutableArray<byte> SoundBank, [property: JsonRequired] ImmutableArray<byte> Music)
{
    public static NativeBattleMusicLoadSource Read501A20(ImmutableArray<byte> directory)
    {
        int first = NativeBattleDatDirectory.Section(directory.AsSpan(), 0);
        int second = NativeBattleDatDirectory.Section(directory.AsSpan(), 1);
        int end = NativeBattleDatDirectory.Section(directory.AsSpan(), 2);
        return new(directory.Slice(first, second - first), directory.Slice(second, end - second));
    }

    public void Validate()
    {
        if (SoundBank.IsDefault || Music.IsDefault)
        {
            throw new InvalidDataException("Native501A20 requires both retained source allocations, including their actual lengths.");
        }
    }

    public NativeMusicBufferIdentity ReadIdentity()
    {
        Validate();
        return ReadIdentity(Music.AsSpan());
    }

    public static NativeMusicBufferIdentity ReadIdentity(ReadOnlySpan<byte> music)
    {
        if (music.Length < 8 || !music[..4].SequenceEqual("AKAO"u8))
        {
            throw new NotSupportedException("Native501A70 music buffer identity requires the reached original AKAO header.");
        }
        return new(music[4], NativeMusicBufferProducer.Battle501A70);
    }
}

public sealed record NativeBattleMusicLoadPointers([property: JsonRequired] uint SoundBankAddress, [property: JsonRequired] uint MusicAddress, [property: JsonRequired] int MusicLength)
{
    public void Validate()
    {
        if (SoundBankAddress < NativeBattleFileStaging.Address || MusicAddress < SoundBankAddress || MusicLength < 0 || (ulong)MusicAddress + (uint)MusicLength > uint.MaxValue)
        {
            throw new InvalidDataException("Native501A20 requires its actual retained source pointers and bounded copy extent.");
        }
    }
}

public sealed record NativeBattleMusicLoadStep(NativeBattleMusicProcessState State, NativeGlobalSoundProcess Sound, byte Phase, bool Completed);

public sealed partial record NativeBattleMusicProcessState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMusicLoadPointers? LoadSource { get; init; }

    public NativeBattleMusicProcessState StageLoad501A20(NativeBattleFileStaging staging, uint directoryAddress)
    {
        Validate();
        ImmutableArray<byte> directory = staging.ReadDat(directoryAddress);
        int first = NativeBattleDatDirectory.Section(directory.AsSpan(), 0);
        int second = NativeBattleDatDirectory.Section(directory.AsSpan(), 1);
        int end = NativeBattleDatDirectory.Section(directory.AsSpan(), 2);
        return this with { LoadSource = new(checked(directoryAddress + (uint)first), checked(directoryAddress + (uint)second), end - second) };
    }

    public NativeBattleMusicProcessState StageLoad501A20(uint directoryAddress, Func<uint, int, byte[]> readSource)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(readSource);
        byte[] directory = readSource(checked(directoryAddress + 4), 12);
        int first = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(directory);
        int second = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(directory.AsSpan(4));
        int end = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(directory.AsSpan(8));
        if (first < 0 || second < first || end < second)
        {
            throw new InvalidDataException("Native501A20 requires ordered current section offsets.");
        }
        NativeBattleMusicLoadPointers source = new(checked(directoryAddress + (uint)first), checked(directoryAddress + (uint)second), end - second);
        source.Validate();
        return this with { LoadSource = source };
    }

    public NativeBattleMusicLoadStep AdvanceLoad501A70(NativeGlobalSoundProcess sound, NativeBattleFileStaging? staging, byte phase, uint completionAddress, Action<uint, byte>? writeCompletion = null, Func<uint, int, byte[]>? readSource = null)
    {
        Validate();
        sound.Validate();
        if (phase == 0)
        {
            return new(QueueStop501A70(), sound, 1, false);
        }
        if (phase == 1)
        {
            //46C040 is an unconditional zero-return function in authenticated FF8_EN; no artificial wait is introduced.
            NativeMusicPlaybackState playback = (sound.MusicPlayback ?? NativeMusicPlaybackState.Unknown()).ConsumeBattleFirstLoad501A70(out bool first);
            byte? bank = ReturnBank;
            if (first)
            {
                bank = unchecked((byte)((bank ?? throw new NotSupportedException("Native501A70 first load requires its actual return-bank BYTE.")) ^ 1));
            }
            uint address = Commands?.TargetBufferAddress ?? throw new NotSupportedException("Native501A70 requires the actual501C60 destination pointer, independently of the return bank.");
            byte target = address switch
            {
                0x1CE075C => 0,
                0x1CDC750 => 1,
                _ => throw new NotSupportedException("Native501A70 cannot copy music into an uninitialized destination.")
            };
            NativeBattleMusicLoadPointers source = LoadSource ?? throw new NotSupportedException("Native501A70 requires501A20's retained original source pointers.");
            byte[] music = readSource is { } currentSource ? currentSource(source.MusicAddress, source.MusicLength) : (staging ?? throw new NotSupportedException("Native501A70 requires the actual shared199B018 bytes at its retained music pointer.")).Read(source.MusicAddress, source.MusicLength);
            NativeMusicProcessState buffers = Buffers.LoadBuffer(target, NativeBattleMusicLoadSource.ReadIdentity(music));
            NativeBattleMusicProcessState next = (this with { Buffers = buffers, ReturnBank = bank }).QueuePlay501A70();
            return new(next, sound with { MusicPlayback = playback }, 2, false);
        }
        if (phase == 2)
        {
            (writeCompletion ?? throw new NotSupportedException("Native501A70 requires its actual caller-owned completion BYTE address."))(completionAddress, 1);
            return new(this, sound, phase, true);
        }
        return new(this, sound, phase, false);
    }
}
