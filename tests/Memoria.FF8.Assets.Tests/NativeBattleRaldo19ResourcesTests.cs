using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRaldo19ResourcesTests
{
    [Fact]
    public void Original_Raldo_resources_have_separate_loader_owners_without_task_or_cursor_overlap()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check the Raldo module.");
        using GameAssets game = new(directory!);
        NativeBattleRaldo19Resources resources = game.ReadNativeRaldo19Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeRaldo19Resources());
        Assert.Equal(0x26C, resources.Animation.Data.Length);
        Assert.Equal(0x17F37BCu, resources.Executable.ResourceAddress);
        Assert.Equal(0x4760, resources.Executable.Data.Length);
        Assert.Equal(0x64254u, resources.Sound);
        Assert.Equal(new uint[] { 0x404040, 0xFF000000 }, resources.Overlay.Commands);
        Assert.Equal((ushort)10, BinaryPrimitives.ReadUInt16LittleEndian(resources.DebrisHigh.AsSpan().Slice(8)));
        Assert.Equal((ushort)12, BinaryPrimitives.ReadUInt16LittleEndian(resources.DebrisLow.AsSpan().Slice(8)));
        Assert.Equal(0x110, resources.DebrisHigh.Length);
        Assert.Equal(0x144, resources.DebrisLow.Length);
        Assert.Equal(new[] { 10, 4, 3, 0 }, Enumerable.Range(0, 4).Select(table => Enumerable.Range(0, 16).Count(index => BinaryPrimitives.ReadUInt32LittleEndian(resources.Executable.Data.AsSpan().Slice(4 + table * 64 + index * 4)) != 0)));
        NativeBattleRaldo19State cold = NativeBattleRaldo19State.FromColdProcess(resources);
        cold.Validate();
        Assert.Null(cold.Nodes);
        Assert.All(cold.Shared84, value => Assert.Equal((byte)0, value));
        Assert.All(cold.BallC4.Read(0, 0xC4), value => Assert.Equal((byte)0, value));
        Assert.All(cold.Descriptor48.Read(0, 48), value => Assert.Equal((byte)0, value));
        Assert.Equal(0u, cold.Counter273048C);
        Assert.Equal(0u, cold.Counter272DDDC);
        Assert.Equal(0x2730418u, NativeBattleRaldo19Profile.DescriptorAddress + 48);
        Assert.True(NativeBattleRaldo19Profile.BodyScratchAddress + NativeBattleRaldo19Profile.BodyScratchLength <= 0x272B8F0u);
        Assert.Equal(0x272FFE0u, NativeBattleRaldo19Profile.VertexScratchAddress + NativeBattleRaldo19Profile.VertexScratchLength);
        NativeBattleRaldo19State restored = NativeBattleSnapshotJson.Deserialize<NativeBattleRaldo19State>(NativeBattleSnapshotJson.Serialize(cold))!;
        restored.Validate();
        Assert.Equal(NativeBattleSnapshotJson.Serialize(cold), NativeBattleSnapshotJson.Serialize(restored));
    }
}
