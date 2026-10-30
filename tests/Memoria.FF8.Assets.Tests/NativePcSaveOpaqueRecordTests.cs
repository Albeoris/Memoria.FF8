using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed partial class NativePcSaveConverterTests
{
    [Fact]
    public void Original_export_import_preserves_every_opaque_character_and_guardian_force_byte()
    {
        GameState source = Defaults();
        NativeCharacterVitals vitals = source.CharacterVitals!.DeepCopy();
        for (int row = 0; row < 8; row++)
        {
            vitals.Records[row] = vitals.Records[row] with { NativeByte90 = (byte)(row + 1) };
        }
        for (int id = 0; id < 16; id++)
        {
            vitals.GuardianForces[id] = vitals.GuardianForces[id] with { NativeOpaqueBytes = new((byte)(id + 1), (byte)(id + 17), (byte)(id + 33), (byte)(id + 49)) };
        }
        source = source with { CharacterVitals = vitals };
        byte[] exported = NativePcSaveConverter.Export(source);
        GameState imported = NativePcSaveConverter.Import(exported, Defaults(), _ => "fixture", "en");
        NativePcSaveData data = NativePcSaveCodec.Decode(exported);
        for (int row = 0; row < 8; row++)
        {
            Assert.Equal(vitals.Records[row].NativeByte90, imported.CharacterVitals!.Records[row].NativeByte90);
            Assert.Equal((byte)(row + 1), data.Block[NativePcSaveData.MainOffset + 1088 + row * 152 + 90]);
        }
        for (int id = 0; id < 16; id++)
        {
            Assert.Equal(vitals.GuardianForces[id].NativeOpaqueBytes, imported.CharacterVitals!.GuardianForces[id].NativeOpaqueBytes);
            int start = NativePcSaveData.MainOffset + id * 68;
            Assert.Equal((byte)(id + 1), data.Block[start + 16]);
            Assert.Equal(new byte[] { (byte)(id + 17), (byte)(id + 33), (byte)(id + 49) }, data.Block.AsSpan(start + 57, 3).ToArray());
        }
    }

    [Fact]
    public void Opaque_record_bytes_survive_memoria_json_and_copy_with_value_identity()
    {
        NativeCharacterVital character = Defaults().CharacterVitals!.Records[0] with { NativeByte90 = 2 };
        NativeCharacterVital restored = JsonSerializer.Deserialize<NativeCharacterVital>(JsonSerializer.Serialize(character))!;
        Assert.Equal(character, restored);
        Assert.NotEqual(character, restored with { NativeByte90 = 1 });
        NativeGuardianForceVital gf = Defaults().CharacterVitals!.GuardianForces[0] with { NativeOpaqueBytes = new(17, 57, 58, 59) };
        NativeGuardianForceVital restoredGf = JsonSerializer.Deserialize<NativeGuardianForceVital>(JsonSerializer.Serialize(gf))!;
        Assert.Equal(gf.NativeOpaqueBytes, restoredGf.NativeOpaqueBytes);
        Assert.Equal(gf.NativeOpaqueBytes, (gf with { HitPoints = 1 }).NativeOpaqueBytes);
        NativeCharacterVitals vitals = Defaults().CharacterVitals!.DeepCopy();
        vitals.Records[0] = character;
        vitals.GuardianForces[0] = gf;
        Assert.Equal(character, vitals.DeepCopy().Records[0]);
        Assert.Equal(gf.NativeOpaqueBytes, vitals.DeepCopy().GuardianForces[0].NativeOpaqueBytes);
    }

    [Fact]
    public void Native_initializer_reads_opaque_bytes_from_selected_resources_without_zero_seeding()
    {
        int[] lengths = new int[56];
        lengths[1] = 60 * 57;
        lengths[2] = 132 * 16;
        lengths[6] = 36 * 11;
        int[] counts = [20, 19, 19, 20, 5, 9, 24];
        for (int index = 0; index < counts.Length; index++)
        {
            lengths[11 + index] = counts[index] * 8;
        }
        byte[] kernel = new byte[228 + lengths.Sum()];
        BinaryPrimitives.WriteUInt32LittleEndian(kernel, 56);
        int offset = 228;
        int[] offsets = new int[56];
        for (int index = 0; index < lengths.Length; index++)
        {
            offsets[index] = offset;
            BinaryPrimitives.WriteUInt32LittleEndian(kernel.AsSpan(4 + index * 4), (uint)offset);
            offset += lengths[index];
        }
        BinaryPrimitives.WriteUInt32LittleEndian(kernel.AsSpan(4 + 56 * 4), (uint)offset);
        for (int id = 0; id < 11; id++)
        {
            kernel[offsets[6] + id * 36 + 9] = 1;
        }
        byte[] initial = new byte[2812];
        for (int id = 0; id < 16; id++)
        {
            kernel[offsets[2] + id * 132 + 21] = 1;
            initial[id * 68 + 16] = (byte)(id + 1);
            initial[id * 68 + 57] = 17;
            initial[id * 68 + 58] = 18;
            initial[id * 68 + 59] = 19;
        }
        for (int row = 0; row < 8; row++)
        {
            initial[1088 + row * 152 + 8] = (byte)row;
            initial[1088 + row * 152 + 90] = (byte)(row + 1);
            initial[1088 + row * 152 + 148] = 1;
        }
        initial[2725] = 255;
        initial[2726] = 255;
        NativeCharacterVitals vitals = NativeCharacterInitialization.Read(initial, kernel);
        for (int row = 0; row < 8; row++)
        {
            Assert.Equal((byte)(row + 1), vitals.Records[row].NativeByte90);
        }
        for (int id = 0; id < 16; id++)
        {
            Assert.Equal(new NativeGuardianForceOpaqueBytes((byte)(id + 1), 17, 18, 19), vitals.GuardianForces[id].NativeOpaqueBytes);
        }
    }
}
