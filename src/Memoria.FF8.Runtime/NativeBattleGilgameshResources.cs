using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The complete external files selected by Excalipoor, independent of their live stream destinations.</summary>
public sealed record NativeBattleGilgameshResources([property: JsonRequired] ImmutableArray<NativeBattleArchiveFile> Files)
{
    public static ImmutableArray<ushort> FileIds { get; } = [745, 746, 747, 748, 749, 750, 751, 752, 753, 755, 757];

    public void Validate()
    {
        string[] names = ["mag326_a.dat", "mag326_b.dat", "mag326_c.dat", "mag326_d.dat", "mag326_e.dat", "mag326_f.dat", "mag326_g.dat", "mag326_h.dat", "mag326_k.dat", "mag328_i.dat", "mag329_j.dat"];
        int[] lengths = [103968, 30688, 132640, 33824, 31004, 4, 50592, 4, 4, 34336, 99360];
        if (Files.IsDefault || Files.Length != FileIds.Length)
        {
            throw new InvalidDataException("Native Excalipoor external reads require their eleven complete original files.");
        }
        for (int index = 0; index < Files.Length; index++)
        {
            NativeBattleArchiveFile file = Files[index];
            if (file is null || file.FileId != FileIds[index] || !string.Equals(file.Name, names[index], StringComparison.OrdinalIgnoreCase) || file.Bytes.IsDefault || file.Bytes.Length != lengths[index])
            {
                throw new InvalidDataException("Native Excalipoor external file identity or complete extent contradicts its selected profile.");
            }
        }
    }
}

public sealed record NativeBattleGilgameshDirectorResources([property: JsonRequired] NativeBattleEffectHeapAllocation CameraProgram, [property: JsonRequired] NativeVibrationBank Vibration, [property: JsonRequired] uint SoundInitial, [property: JsonRequired] uint SoundMiddle, [property: JsonRequired] uint SoundLate, [property: JsonRequired] uint SoundClosing)
{
    public void Validate()
    {
        if (CameraProgram is null || CameraProgram.Identity != "FF8_EN:CD0874" || CameraProgram.Data.IsDefault || CameraProgram.Data.Length != 0xEA || BinaryPrimitives.ReadUInt16LittleEndian(CameraProgram.Data.AsSpan().Slice(0xE8)) != 0x8000)
        {
            throw new InvalidDataException("Native Excalipoor camera requires its complete selected program and terminal WORD.");
        }
        if (Vibration is null || Vibration.SourceHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || Vibration.Offsets.IsDefault || !Vibration.Offsets.SequenceEqual(new[] { 0, 0x100 }) || Vibration.Bytes.IsDefault || Vibration.Bytes.Length != 0x14A)
        {
            throw new InvalidDataException("Native Excalipoor vibration requires its authenticated selected bank and original lookahead bytes.");
        }
        Vibration.Validate();
        ReadOnlySpan<byte> bytes = Vibration.Bytes.AsSpan();
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0 || BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4)) != 0x100 || Vibration.ReadWord(0x100) != 64 || Vibration.ReadWord(0x102) != 4 || BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(0x148)) != 0xAA93 || SoundInitial != 0x3AA93 || SoundMiddle != 0x3AA96 || SoundLate != 0x3AA99 || SoundClosing != 0x3AAA5)
        {
            throw new InvalidDataException("Native Excalipoor vibration or sounds contradict their selected original profile.");
        }
    }
}
