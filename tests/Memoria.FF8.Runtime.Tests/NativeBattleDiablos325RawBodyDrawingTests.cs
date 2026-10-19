using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325RawBodyDrawingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Closed_renderer_skips_unknown_geometry_and_masked_renderer_skips_invalid_mesh_body_while_restoring_pose(bool draw)
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        byte[] resource = new byte[0x6E];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(resource.AsSpan(4 + index * 4), new[] { 0x18, 0x58, 0x60, 0x6E }[index]);
        }
        resource[0x18] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(resource.AsSpan(0x1A), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(resource.AsSpan(0x28), -1);
        BinaryPrimitives.WriteInt32LittleEndian(resource.AsSpan(0x58), 1);
        BinaryPrimitives.WriteInt32LittleEndian(resource.AsSpan(0x5C), int.MaxValue);
        BinaryPrimitives.WriteInt32LittleEndian(resource.AsSpan(0x60), 1);
        BinaryPrimitives.WriteInt32LittleEndian(resource.AsSpan(0x64), 8);
        resource[0x68] = 2;
        NativeBattleModulePoolTask task = new(1, 0, null, 0x6545F0, new byte[0x994 - 12].ToImmutableArray());
        NativeBattleDiablos325BodyStep body = NativeBattleDiablos325Body.Initialize655190(frame.Memory with { Arena = frame.Memory.Arena!.Write(0x2185C, resource) }, task, 0x20DFAB8);
        task = body.Task.WriteDword(0x188, 0).WriteDword(0x134, 0x123456);
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Reset5099C0().PublishModelColor(0xABCDEF).Publish50FDF0(123, 456);
        process = process with { Software = process.Software! with { Environment = process.Software!.Environment with { Debug = 0 } } };
        NativeBattleEffectArena arena = draw ? body.Memory.Arena! : body.Memory.Arena!.MarkUnknown(0x218B4, 8);
        NativeBattleEffectMemory memory = body.Memory with { Arena = arena, PrimitiveProcess = process };
        string original = JsonSerializer.Serialize(memory);
        byte[] beforePose = arena.Read(0x21874, 64);
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096), 288, 160, 108);
        NativeBattleDiablos325BodyDraw result = NativeBattleDiablos325RawBodyDrawing.Draw6A79B0(memory, task, projection, draw);
        Assert.Empty(result.Packets.Attempts);
        Assert.Equal(0, result.Packets.AllocationBytes);
        Assert.Equal(beforePose, result.Memory.Arena!.Read(0x21874, 64));
        NativeBattlePrimitiveProcess actual = Assert.IsType<NativeBattlePrimitiveProcess>(result.Memory.PrimitiveProcess);
        Assert.Equal<uint?>(1u, actual.RendererGateB8B9F8);
        Assert.Equal((draw ? process.PublishModelColor(0x123456) : process).ModelRendererRegisters, actual.ModelRendererRegisters);
        Assert.Equal(original, JsonSerializer.Serialize(memory));
    }
}
