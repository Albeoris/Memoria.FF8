using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelRetainedFlagsTests
{
    [Fact]
    public void Loader_reads_exact_word2_and_cold_process_has_its_loader_zero()
    {
        byte[] bytes = new byte[156];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0x3456);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 0xA180);
        bytes[4] = 0xFF;
        Assert.Equal((ushort)0xA180, NativeBattleModelRetainedFields.FromLoaderBytes(bytes).Flags2);
        Assert.Equal((ushort)0, NativeBattleModelRetainedFields.FromColdProcess().Flags2);
    }

    [Fact]
    public void Bank_clear_changes_only_word0_and_preserves_each_rows_captured_word2()
    {
        ImmutableArray<NativeBattleModelRetainedFields> retained = Enumerable.Range(0, 7).Select(index => NativeBattleModelRetainedFields.Capture(new(index, 0xFFFF) { Flags2 = (ushort)(0x8000 + 17 * index) })).ToImmutableArray();
        NativeBattleGraphicsModel[] cleared = NativeBattleGraphicsModels.ClearBank(retainedTransformRows: retained);
        for (int index = 0; index < cleared.Length; index++)
        {
            Assert.Equal((ushort)0, cleared[index].Flags0);
            Assert.Equal((ushort)(0x8000 + 17 * index), cleared[index].Flags2);
        }
    }

    [Fact]
    public void Unknown_word2_remains_unknown_through_capture_restore_and_serialization()
    {
        NativeBattleModelRetainedFields retained = NativeBattleModelRetainedFields.Capture(new(1, 0));
        NativeBattleModelRetainedFields restored = JsonSerializer.Deserialize<NativeBattleModelRetainedFields>(JsonSerializer.Serialize(retained))!;
        Assert.Null(restored.Flags2);
        Assert.Null(restored.Restore502340(new(1, 0)).Flags2);
    }

    [Fact]
    public void Previously_saved_retained_rows_without_word2_require_an_explicit_writer()
    {
        string json = """{"Rotation12":0,"Rotation14":0,"Rotation16":0,"RotationPadding18":0,"Transform":null,"Color44":0}""";
        NativeBattleModelRetainedFields retained = JsonSerializer.Deserialize<NativeBattleModelRetainedFields>(json)!;
        Assert.Null(retained.Flags2);
        Assert.Null(retained.Restore502340(new(2, 0) { Flags2 = 0xBEEF }).Flags2);
    }
}
