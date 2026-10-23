using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleResonance49Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleResonance49ResourcesTests
{
    [Fact]
    public void Original_programmes_keep_all_shapes_materials_camera_and_sound_fronts()
    {
        using GameAssets game = Open();
        NativeBattleResonance49Resources resources = game.ReadNativeResonance49Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeResonance49Resources());
        Assert.Equal(ProgrammeALength, resources.ProgrammeA.Data.Length);
        Assert.Equal(ProgrammeBLength, resources.ProgrammeB.Data.Length);
        Assert.Equal(CameraLength, resources.Camera.Data.Length);
        Assert.Equal(0x61B5Cu, resources.Sound);
        foreach (NativeBattleGfAnimationSource source in new[] { resources.ProgrammeA, resources.ProgrammeB })
        {
            Assert.Same(source, resources.ReadProgramme(source.Address));
            for (int index = 0; index < 4; index++)
            {
                int offset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(8 + index * 4, 4)));
                NativeBattleStaticMeshResource geometry = resources.ReadGeometry(source.Address + (uint)offset);
                uint vertices = BinaryPrimitives.ReadUInt32LittleEndian(geometry.Bytes.AsSpan().Slice(4, 4));
                uint shapes = BinaryPrimitives.ReadUInt32LittleEndian(geometry.Bytes.AsSpan().Slice(8, 4));
                Assert.Equal(12u + vertices * shapes * 8, BinaryPrimitives.ReadUInt32LittleEndian(geometry.Bytes.AsSpan()));
                Assert.Equal(source.Data.AsSpan().Slice(offset, geometry.Bytes.Length).ToArray(), geometry.Bytes.ToArray());
            }
        }
        Assert.Throws<NotSupportedException>(() => resources.ReadProgramme(CameraAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadGeometry(ProgrammeAAddress));
    }

    [Fact]
    public void Original_resource_JSON_preserves_separate_sources_and_physical_mutable_storage_is_not_embedded()
    {
        using GameAssets game = Open();
        NativeBattleResonance49Resources resources = game.ReadNativeResonance49Resources();
        NativeBattleResonance49Resources restored = JsonSerializer.Deserialize<NativeBattleResonance49Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Throws<NotSupportedException>(() => restored.ReadGeometry(EffectObjectAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadProgramme(ScratchAddress));
        NativeBattleResonance49State state = NativeBattleResonance49State.FromUnknown().WriteWord(EffectObjectAddress, 0x1234);
        Assert.Equal((ushort)0x1234, state.ReadWord(EffectObjectAddress));
        Assert.Equal(resources.ProgrammeA.Data.ToArray(), restored.ProgrammeA.Data.ToArray());
    }

    [Fact]
    public void Truncated_sources_wrong_node_headers_faces_camera_and_sound_fail_at_selected_fronts()
    {
        using GameAssets game = Open();
        NativeBattleResonance49Resources resources = game.ReadNativeResonance49Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeA = resources.ProgrammeA with { Data = resources.ProgrammeA.Data.RemoveAt(0) } }).Validate());
        byte[] source = resources.ProgrammeB.Data.ToArray();
        int table = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(4)));
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(table), 9);
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeB = resources.ProgrammeB with { Data = source.ToImmutableArray() } }).Validate());
        source = resources.ProgrammeA.Data.ToArray();
        int geometry = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(8)));
        int cursor = geometry + checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(geometry)));
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(cursor)));
            if (count != 0)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(cursor + 8), 0xFFFF);
                break;
            }
            cursor += 4 + count * strides[group];
        }
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeA = resources.ProgrammeA with { Data = source.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Resonance resources.");
        return new(directory!);
    }
}
