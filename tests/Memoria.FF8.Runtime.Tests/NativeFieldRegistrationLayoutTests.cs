using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeFieldRegistrationLayoutTests
{
    [Fact]
    public void Only_remastered_cold_allocation_knows_the_exact_field_arena_zeroes()
    {
        NativeBattleRetainedMemory remastered = NativeBattleRetainedMemory.FromColdProcess(NativeFieldRuntimeProfile.RemasteredEnImageHash);
        Assert.Equal(new byte[4], remastered.FileStaging!.Read(0x0A300000, 4));
        Assert.Equal(new byte[4], remastered.FileStaging.Read(0x0A5FFFFC, 4));
        Assert.Throws<NotSupportedException>(() => remastered.FileStaging.Read(0x0A2FFFFF, 1));
        Assert.Throws<NotSupportedException>(() => remastered.FileStaging.Read(0x0A600000, 1));
        NativeBattleRetainedMemory classic = NativeBattleRetainedMemory.FromColdProcess();
        Assert.Throws<NotSupportedException>(() => classic.FileStaging!.Read(0x0A300000, 4));
        Assert.Throws<NotSupportedException>(() => classic.FileStaging!.Read(0x0A5FFFFC, 4));
    }

    [Theory]
    [InlineData(false, NativeFieldRegistrationEntry.FieldEntry, 0x018DD018u, 0x019F9818u)]
    [InlineData(false, NativeFieldRegistrationEntry.BattleReturn, 0x018DD018u, 0x019EF018u)]
    [InlineData(true, NativeFieldRegistrationEntry.FieldEntry, 0x0A300000u, 0x0A600000u)]
    [InlineData(true, NativeFieldRegistrationEntry.BattleReturn, 0x0A300000u, 0x0A5F0000u)]
    public void Authenticated_allocation_enforces_its_exact_exclusive_upper_bound(bool remastered, NativeFieldRegistrationEntry entry, uint start, uint limit)
    {
        NativeFieldRegistrationLayout layout = NativeFieldRegistrationLayout.ForImage(remastered ? NativeFieldRuntimeProfile.RemasteredEnImageHash : NativeFieldRuntimeProfile.FF8EnExecutableHash, entry);
        Assert.Equal(start, layout.ArenaAddress);
        Assert.Equal(limit, layout.Limit);
        NativeFieldRegistrationLayout restored = JsonSerializer.Deserialize<NativeFieldRegistrationLayout>(JsonSerializer.Serialize(layout))!;
        restored.Validate();
        Assert.Equal(layout, restored);
        NativeFieldRegistrationState state = new(1, 232, new string('A', 64), new string('B', 64), start, limit - 4, 4, limit, [], []) { Layout = restored };
        NativeBattleRetainedMemory memory = NativeBattleRetainedMemory.FromColdProcess();
        state.Validate(memory);
        Assert.Throws<InvalidDataException>(() => (state with { PreModelAddress = start - 1 }).Validate(memory));
        Assert.Throws<InvalidDataException>(() => (state with { EndAddress = limit + 1 }).Validate(memory));
        Assert.Throws<InvalidDataException>(() => (state with { RelocatedSourceLength = 5 }).Validate(memory));
        Assert.Throws<InvalidDataException>(() => (state with { RelocatedSourceAddress = uint.MaxValue }).Validate(memory));
    }

    [Fact]
    public void Unsupported_image_and_entry_and_missing_serialized_identity_are_rejected()
    {
        Assert.Throws<InvalidDataException>(() => NativeFieldRegistrationLayout.ForImage(new string('0', 64), NativeFieldRegistrationEntry.FieldEntry));
        Assert.Throws<InvalidDataException>(() => NativeFieldRegistrationLayout.ForImage(NativeFieldRuntimeProfile.RemasteredEnImageHash, (NativeFieldRegistrationEntry)2));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeFieldRegistrationLayout>("{}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeFieldRegistrationLayout>("{\"ExecutableHash\":\"unknown\"}"));
        NativeFieldRegistrationLayout unsupported = JsonSerializer.Deserialize<NativeFieldRegistrationLayout>("{\"ExecutableHash\":\"unknown\",\"Entry\":0}")!;
        Assert.Throws<InvalidDataException>(unsupported.Validate);
    }

    [Fact]
    public void Omitted_schema_one_layout_retains_the_original_classic_battle_return_allocation()
    {
        NativeFieldRegistrationState original = new(1, 232, new string('A', 64), new string('B', 64), 0x018DD018, 0x019EF014, 4, 0x019EF018, [], []);
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(original))!.AsObject();
        Assert.False(json.ContainsKey("Layout"));
        NativeFieldRegistrationState restored = JsonSerializer.Deserialize<NativeFieldRegistrationState>(json.ToJsonString())!;
        Assert.Null(restored.Layout);
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, restored.MemoryLayout.ExecutableHash);
        Assert.Equal(NativeFieldRegistrationEntry.BattleReturn, restored.MemoryLayout.Entry);
        restored.Validate(NativeBattleRetainedMemory.FromColdProcess());
        Assert.Throws<InvalidDataException>(() => (restored with { EndAddress = 0x019EF019 }).Validate(NativeBattleRetainedMemory.FromColdProcess()));
    }

    [Theory]
    [InlineData(0x1003u, 0x2000u, 0xFFCL, 0x1FFFu)]
    [InlineData(0x2003u, 0x1000u, -0x1004L, 0x0FFFu)]
    [InlineData(0x2000u, 0x1000u, -0x1000L, 0x1000u)]
    public void Relocation_aligns_signed_displacement_before_moving_sources(uint sourceEnd, uint limit, long expectedDisplacement, uint expectedEnd)
    {
        NativeFieldSourceRelocation relocation = NativeFieldSourceRelocation.ToLimit(sourceEnd, limit);
        Assert.Equal(expectedDisplacement, relocation.Displacement);
        Assert.Equal(expectedEnd, relocation.Apply(sourceEnd));
        Assert.InRange(limit - relocation.Apply(sourceEnd), 0u, 3u);
    }

    [Fact]
    public void Relocation_rejects_addresses_that_underflow_or_overflow_instead_of_wrapping()
    {
        Assert.Throws<OverflowException>(() => NativeFieldSourceRelocation.ToLimit(8, 0).Apply(4));
        Assert.Throws<OverflowException>(() => NativeFieldSourceRelocation.ToLimit(0, 8).Apply(uint.MaxValue - 3));
    }

    [Fact]
    public void Battle_return_moves_overlapping_model_input_down_from_the_reserved_tail()
    {
        byte[] program = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(6), 12);
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(10), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(program.AsSpan(12), 5);
        BinaryPrimitives.WriteUInt32LittleEndian(program.AsSpan(16), 6);
        const uint arena = 0x0A300000;
        const uint limit = 0x0A5F0000;
        const uint preModel = arena + 56;
        const uint temporary = preModel + 0x20000;
        byte[] one = new byte[4 + limit + 16 - (temporary + 4)];
        BinaryPrimitives.WriteUInt32LittleEndian(one, 1);
        Span<byte> model = one.AsSpan(4);
        BinaryPrimitives.WriteUInt32LittleEndian(model[56..], 64);
        BinaryPrimitives.WriteUInt16LittleEndian(model[64..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(model[66..], 1);
        Span<byte> texture = model[76..];
        BinaryPrimitives.WriteUInt32LittleEndian(texture, 0x10);
        BinaryPrimitives.WriteUInt32LittleEndian(texture[4..], 9);
        BinaryPrimitives.WriteUInt32LittleEndian(texture[8..], 524);
        BinaryPrimitives.WriteUInt16LittleEndian(texture[16..], 256);
        BinaryPrimitives.WriteUInt16LittleEndian(texture[18..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(texture[532..], 14);
        BinaryPrimitives.WriteUInt16LittleEndian(texture[540..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(texture[542..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(texture[544..], 0x1234);
        byte[] marker = [0xDE, 0xAD, 0xBE, 0xEF];
        marker.CopyTo(one.AsSpan(one.Length - marker.Length));
        NativeFieldModelSource code = Source("sample.jsm", program);
        NativeFieldOneModelEntry modelEntry = new(4, one.Length - 4, 0, 0, [76], "n000") { SourceWord8 = 0, RawName = [110, 48, 48, 48, 0, 0, 0, 0], RawTextureWords = [76] };
        NativeFieldRegistrationResources source = new(72, "sample", NativeFieldRuntimeProfile.RemasteredEnImageHash, code.Sha256, Source("chara.one", one), [modelEntry], [])
        {
            Loader = new(44, 0, code, null, [0x00F00200], [0x008003C0])
        };
        NativeFieldRegistrationStep result = NativeFieldRegistrationMachine.Register(NativeFieldRegistrationEntry.BattleReturn, source, [new(0, 0, 0, 1)], NativeBattleRetainedMemory.FromColdProcess(source.ExecutableHash));
        result.State.Validate(result.Memory);
        Assert.Equal(preModel, result.State.PreModelAddress);
        Assert.Equal(temporary - 16, result.State.RelocatedSourceAddress);
        Assert.Equal(limit, result.State.RelocatedSourceAddress + (uint)result.State.RelocatedSourceLength);
        Assert.Equal(marker, result.Memory.FileStaging!.Read(limit - 4, 4));
        Assert.Equal(new byte[] { 0x34, 0x12 }, result.Uploads[0].Bytes.ToArray());
        Assert.Equal(marker, source.One.Data.AsSpan()[(source.One.Data.Length - 4)..].ToArray());
        const uint untouched = arena + 16;
        result.Memory.FileStaging.Write(untouched, marker);
        NativeFieldRegistrationStep repeated = NativeFieldRegistrationMachine.Register(NativeFieldRegistrationEntry.BattleReturn, source, [new(0, 0, 0, 1)], result.Memory);
        repeated.State.Validate(repeated.Memory);
        Assert.Equal(marker, repeated.Memory.FileStaging!.Read(untouched, marker.Length));
    }

    private static NativeFieldModelSource Source(string member, byte[] bytes) => new(member, Convert.ToHexString(SHA256.HashData(bytes)), bytes.ToImmutableArray());
}
