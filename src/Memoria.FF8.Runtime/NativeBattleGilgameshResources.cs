using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The complete external files selected by Excalipoor, independent of their live stream destinations.</summary>
public sealed record NativeBattleGilgameshResources([property: JsonRequired] ImmutableArray<NativeBattleArchiveFile> Files)
{
    public ushort Effect { get; init; } = 330;

    public static ImmutableArray<ushort> FileIds { get; } = [745, 746, 747, 748, 749, 750, 751, 752, 753, 755, 757];

    public static ImmutableArray<ushort> FileIdsForEffect(ushort effect) => FileIds.SetItem(10, NativeBattleGilgameshProfile.ForEffect(effect).AttackFile);

    public void Validate()
    {
        ImmutableArray<ushort> ids = FileIdsForEffect(Effect);
        string[] names = ["mag326_a.dat", "mag326_b.dat", "mag326_c.dat", "mag326_d.dat", "mag326_e.dat", "mag326_f.dat", "mag326_g.dat", "mag326_h.dat", "mag326_k.dat", "mag328_i.dat", NativeBattleGilgameshProfile.ForEffect(Effect).AttackFileName];
        int[] lengths = [103968, 30688, 132640, 33824, 31004, 4, 50592, 4, 4, 34336, NativeBattleGilgameshProfile.ForEffect(Effect).AttackFileLength];
        if (Files.IsDefault || Files.Length != FileIds.Length)
        {
            throw new InvalidDataException("Native Excalipoor external reads require their eleven complete original files.");
        }
        for (int index = 0; index < Files.Length; index++)
        {
            NativeBattleArchiveFile file = Files[index];
            if (file is null || file.FileId != ids[index] || !string.Equals(file.Name, names[index], StringComparison.OrdinalIgnoreCase) || file.Bytes.IsDefault || file.Bytes.Length != lengths[index])
            {
                throw new InvalidDataException("Native Excalipoor external file identity or complete extent contradicts its selected profile.");
            }
        }
    }
}

public sealed record NativeBattleGilgameshDirectorResources([property: JsonRequired] NativeBattleEffectHeapAllocation CameraProgram, [property: JsonRequired] NativeVibrationBank Vibration, [property: JsonRequired] uint SoundInitial, [property: JsonRequired] uint SoundMiddle, [property: JsonRequired] uint SoundLate, [property: JsonRequired] uint SoundClosing)
{
    public ushort Effect { get; init; } = 330;

    public void Validate()
    {
        NativeBattleGilgameshProfile profile = NativeBattleGilgameshProfile.ForEffect(Effect);
        if (CameraProgram is null || CameraProgram.Identity != $"FF8_EN:{profile.CameraAddress:X}" || CameraProgram.Data.IsDefault || CameraProgram.Data.Length != 0xEA || BinaryPrimitives.ReadUInt16LittleEndian(CameraProgram.Data.AsSpan().Slice(0xE8)) != 0x8000)
        {
            throw new InvalidDataException("Native Excalipoor camera requires its complete selected program and terminal WORD.");
        }
        if (Vibration is null || Vibration.SourceHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || Vibration.Offsets.IsDefault || !Vibration.Offsets.SequenceEqual(new[] { 0, 0x100 }) || Vibration.Bytes.IsDefault || Vibration.Bytes.Length != profile.VibrationLength)
        {
            throw new InvalidDataException("Native Excalipoor vibration requires its authenticated selected bank and original lookahead bytes.");
        }
        Vibration.Validate();
        ReadOnlySpan<byte> bytes = Vibration.Bytes.AsSpan();
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0 || BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4)) != 0x100 || Vibration.ReadWord(0x100) != profile.VibrationChannelCount || Vibration.ReadWord(0x102) != 4 || BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(profile.VibrationLength - 2)) != (Effect == 330 ? 0xAA93 : 0) || SoundInitial != 0x3AA93 || SoundMiddle != 0x3AA96 || SoundLate != 0x3AA99 || SoundClosing != profile.ClosingSound)
        {
            throw new InvalidDataException("Native Excalipoor vibration or sounds contradict their selected original profile.");
        }
    }
}
