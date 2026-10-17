using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The two original relative sections retained by501A20 at1D96EA4/1D96E9C, with the latter's actual copy extent.</summary>
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
        if (Music.Length < 8 || !Music.AsSpan()[..4].SequenceEqual("AKAO"u8))
        {
            throw new NotSupportedException("Native501A70 music buffer identity requires the reached original AKAO header.");
        }
        return new(Music[4], NativeMusicBufferProducer.Battle501A70);
    }
}

public sealed record NativeBattleMusicLoadStep(NativeBattleMusicProcessState State, NativeGlobalSoundProcess Sound, byte Phase, bool Completed);

public sealed partial record NativeBattleMusicProcessState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMusicLoadSource? LoadSource { get; init; }

    public NativeBattleMusicProcessState StageLoad501A20(ImmutableArray<byte> directory)
    {
        Validate();
        return this with { LoadSource = NativeBattleMusicLoadSource.Read501A20(directory) };
    }

    public NativeBattleMusicLoadStep AdvanceLoad501A70(NativeGlobalSoundProcess sound, byte phase, uint completionAddress, Action<uint, byte>? writeCompletion = null)
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
            NativeBattleMusicLoadSource source = LoadSource ?? throw new NotSupportedException("Native501A70 requires501A20's retained original sections.");
            NativeMusicProcessState buffers = Buffers.LoadBuffer(target, source.ReadIdentity());
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
