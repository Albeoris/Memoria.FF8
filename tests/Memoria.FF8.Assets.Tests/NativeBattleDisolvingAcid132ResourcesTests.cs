using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleDisolvingAcid132Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDisolvingAcid132ResourcesTests
{
    [Fact]
    public void Original_ACTs_keep_all_three_typed_fronts_and_workspaces()
    {
        using GameAssets game = Open();
        NativeBattleDisolvingAcid132Resources resources = game.ReadNativeDisolvingAcid132Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeDisolvingAcid132Resources());
        AssertShapes(resources.PrimaryAct, [68], [2]);
        AssertShapes(resources.SecondaryAct, [68], [2]);
        AssertShapes(resources.ImpactAct, [33], [1]);
        AssertWorkspace(resources.PrimaryAct, 0xB0, 16);
        AssertWorkspace(resources.SecondaryAct, 0xB0, 16);
        AssertWorkspace(resources.ImpactAct, 0x78, 13);
        Assert.Equal(68 * 8, MorphBytes);
        Assert.Equal(80, resources.EmissionVectors.Length);
        Assert.Equal(25, resources.Envelope.Length);
        Assert.Equal(0x4000, resources.DistanceTable.Length);
        Assert.Equal(0x6419Au, resources.Sound);
    }

    [Fact]
    public void Immutable_original_emission_rows_survive_resource_JSON_and_stay_outside_scalar_state()
    {
        using GameAssets game = Open();
        NativeBattleDisolvingAcid132Resources resources = game.ReadNativeDisolvingAcid132Resources();
        NativeBattleDisolvingAcid132Resources restored = JsonSerializer.Deserialize<NativeBattleDisolvingAcid132Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(resources.EmissionVectors.ToArray(), restored.EmissionVectors.ToArray());
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(restored.EmissionVectors.AsSpan().Slice(14)));
        NativeBattleDisolvingAcid132State state = NativeBattleDisolvingAcid132State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadWord(EmissionVectorsAddress + 14));
        Assert.Throws<InvalidDataException>(() => state.WriteWord(EmissionVectorsAddress + 14, 0));
    }

    [Fact]
    public void Resource_roundtrip_rejects_truncated_ACT_envelope_and_emission_rows()
    {
        using GameAssets game = Open();
        NativeBattleDisolvingAcid132Resources resources = game.ReadNativeDisolvingAcid132Resources();
        NativeBattleDisolvingAcid132Resources restored = JsonSerializer.Deserialize<NativeBattleDisolvingAcid132Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Same(restored.PrimaryAct, restored.Act(PrimaryActAddress));
        Assert.Same(restored.TemplateSprite, restored.Sprite(TemplateSpriteAddress));
        Assert.Throws<NotSupportedException>(() => restored.Act(TemplateSpriteAddress));
        Assert.Throws<NotSupportedException>(() => restored.Sprite(PrimaryActAddress));
        Assert.Throws<InvalidDataException>(() => (restored with { Envelope = restored.Envelope.Take(24).ToImmutableArray() }).Validate());
        Assert.Throws<InvalidDataException>(() => (restored with { EmissionVectors = restored.EmissionVectors.Take(79).ToImmutableArray() }).Validate());
        Assert.Throws<InvalidDataException>(() => (restored with { ImpactAct = restored.ImpactAct with { Data = restored.ImpactAct.Data.Take(0x577).ToImmutableArray() } }).Validate());
    }

    private static void AssertShapes(NativeBattleGfAnimationSource source, int[] vertices, int[] variants)
    {
        ReadOnlySpan<byte> bytes = source.Data.AsSpan();
        Assert.Equal(vertices.Length, BinaryPrimitives.ReadInt32LittleEndian(bytes));
        for (int index = 0; index < vertices.Length; index++)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8 + index * 4));
            Assert.Equal(vertices[index], BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4)));
            Assert.Equal(variants[index], BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 8)));
        }
    }
    private static void AssertWorkspace(NativeBattleGfAnimationSource source, int size, int duration)
    {
        NativeBattleGfAnimationWorkspace workspace = new(new byte[size].ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, size);
        int emitted = 0;
        for (int frame = 0; frame < duration; frame++)
        {
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(source, 0, (current, node) => current);
            emitted += next.EmittedNodes;
            workspace = next.Workspace;
        }
        Assert.True(emitted > 0);
        Assert.Equal(duration, workspace.Frame);
        Assert.Equal(0, workspace.Advance701970(source, 0, (current, node) => current).EmittedNodes);
    }
    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Disolving Acid resources.");
        return new(directory!);
    }
}
