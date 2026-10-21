using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHeroDrawingTests
{
    [Theory]
    [InlineData(309)]
    [InlineData(310)]
    public void Act88_retains_unwritten_header_bytes_and_publishes_actual_unknown_farcolor_and_input_padding(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleTemporaryAllocation allocation = Allocate(88);
        NativeBattleFileStaging staging = allocation.Staging.MarkUnknown(allocation.Address + 9, 1);
        NativeBattleHeroAct operation = new(fixture.Resources.Main, Node(0), new(new(100, 200, 300), unchecked((short)0xCAFE), 4096, 4096, 4096, true, -128, 0x1000));
        NativeBattleHeroDrawResult result = NativeBattleHeroDrawing.DrawAct(fixture.Frame, operation, fixture.Resources, fixture.Projection, Colors, fixture.View, staging, allocation.Address);
        Assert.Equal(fixture.Resources.Main.Address + 16, Read(result.Staging, allocation.Address));
        Assert.Equal(fixture.Resources.Main.Address + 28, Read(result.Staging, allocation.Address + 4));
        Assert.Equal(0u, Read(result.Staging, allocation.Address + 12));
        Assert.Equal(0xA5A5A5A5u, Read(result.Staging, allocation.Address + 16));
        Assert.Equal(unchecked((uint)-128), Read(result.Staging, allocation.Address + 24));
        Assert.Equal(0x2030u, Read(result.Staging, allocation.Address + 28));
        Assert.Equal(fixture.Resources.Main.Address + 284, Read(result.Staging, allocation.Address + 32));
        Assert.Equal(0xA5A5A5A5u, Read(result.Staging, allocation.Address + 40));
        Assert.All(result.Staging.Read(allocation.Address + 52, 36), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal((uint?)0xA50, result.Frame.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Null(result.Frame.Memory.PrimitiveProcess.ModelRendererRegisters.FarGreen);
        Assert.Equal((uint?)0xA50, result.Frame.Memory.PrimitiveProcess.ModelRendererRegisters.FarBlue);
        Assert.Equal((uint?)123, result.Frame.Memory.PrimitiveProcess.ModelRendererRegisters.PacketCursor);
        Assert.Null(result.Frame.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Null(result.Frame.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Equal(new int?[] { 110, 220, 1324 }, result.Frame.Memory.GeometryRegisters.Translation);
        Assert.Equal(new int?[] { 100, 200, 300 }, result.Frame.Memory.ScriptedColorRegisters!.Mac);
        Assert.True(result.Frame.Memory.ScriptedColorRegisters.NormalInputKnown!.Value[6]);
        Assert.Equal(new byte[] { 0xFE, 0xCA }, result.Frame.Memory.ScriptedColorRegisters.NormalInput!.Value.AsSpan().Slice(6, 2).ToArray());
        Assert.Equal(40, result.Batch.AllocationBytes);
        Assert.Equal(0x20000, result.CursorStart);
        Assert.Equal(0x20028, result.Frame.State.PacketCursor);
        Assert.Equal(allocation.Address + 88, Read(result.Staging, NativeBattleTemporaryMemory.Cursor));
        NativeBattleFileStaging released = NativeBattleTemporaryMemory.Release5082D0(result.Staging, 88);
        Assert.Equal(allocation.Address, Read(released, NativeBattleTemporaryMemory.Cursor));
    }

    [Fact]
    public void Nonzero_fade_writes_real_RGB_and_orbit_does_not_invent_anchor_WORD6()
    {
        var fixture = Fixture(309);
        NativeBattleTemporaryAllocation allocation = Allocate(88);
        NativeBattleHeroAct operation = new(fixture.Resources.Orbit, Node(500), new(new(1, 2, 3), null, 3072, 3072, 3072, true, 0, 0x1000));
        NativeBattleHeroDrawResult result = NativeBattleHeroDrawing.DrawAct(fixture.Frame, operation, fixture.Resources, fixture.Projection, Colors, fixture.View, allocation.Staging, allocation.Address);
        Assert.Equal(0x20F0u, Read(result.Staging, allocation.Address + 28));
        Assert.Equal(0x00332211u, Read(result.Staging, allocation.Address + 8));
        Assert.Equal((uint?)0x110, result.Frame.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal((uint?)0x220, result.Frame.Memory.PrimitiveProcess.ModelRendererRegisters.FarGreen);
        Assert.Equal((uint?)0x330, result.Frame.Memory.PrimitiveProcess.ModelRendererRegisters.FarBlue);
        Assert.False(result.Frame.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(result.Frame.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        Assert.Equal((ushort)0xA5A5, BinaryPrimitives.ReadUInt16LittleEndian(result.Staging.Read(allocation.Address + 18, 2)));
    }

    [Fact]
    public void Fan136_uses_its_distinct_context_base_and_copies_unknown_CPU_matrix_padding()
    {
        var fixture = Fixture(310);
        NativeBattleTemporaryAllocation allocation = Allocate(136);
        NativeBattleHeroFan operation = Fan(fixture.Frame.Profile, 0);
        NativeBattleHeroDrawResult result = NativeBattleHeroDrawing.DrawFan(fixture.Frame, operation, fixture.Resources, fixture.Projection, Colors, fixture.View, allocation.Staging, allocation.Address);
        Assert.Equal(operation.MeshAddress, Read(result.Staging, allocation.Address + 0x20));
        Assert.Equal(operation.MeshAddress + 8, Read(result.Staging, allocation.Address + 0x24));
        Assert.Equal(0u, Read(result.Staging, allocation.Address + 0x28));
        Assert.Equal((uint)operation.Fade, Read(result.Staging, allocation.Address + 0x2C));
        Assert.Equal(unchecked((uint)operation.Depth), Read(result.Staging, allocation.Address + 0x38));
        Assert.Equal(0xF0u, Read(result.Staging, allocation.Address + 0x3C));
        Assert.Equal(operation.MeshAddress + 288, Read(result.Staging, allocation.Address + 0x40));
        Assert.Equal(0xA5A5A5A5u, Read(result.Staging, allocation.Address + 0x48));
        Assert.All(result.Staging.Read(allocation.Address + 0x54, 0x24), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal((uint)3072, Read(result.Staging, allocation.Address + 0x78));
        Assert.Equal((uint)3072, Read(result.Staging, allocation.Address + 0x7C));
        Assert.Equal((uint)3072, Read(result.Staging, allocation.Address + 0x80));
        Assert.Equal(0xA5A5A5A5u, Read(result.Staging, allocation.Address + 0x84));
        Assert.All(result.Staging.ReadSnapshot(allocation.Address + 18, 2).Known, value => Assert.False(value));
        Assert.Null(result.Frame.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Null(result.Frame.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Equal(new int?[] { 110, 220, 1324 }, result.Frame.Memory.GeometryRegisters.Translation);
        Assert.Equal((uint?)0, result.Frame.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal(36, result.Batch.AllocationBytes);
        Assert.Equal(0x20024, result.Frame.State.PacketCursor);
        Assert.Equal(allocation.Address + 136, Read(result.Staging, NativeBattleTemporaryMemory.Cursor));
    }

    [Fact]
    public void Fan_reuses_actual_allocation_and_header_with_repeatable_snapshot_next_iteration()
    {
        var fixture = Fixture(309);
        NativeBattleTemporaryAllocation allocation = Allocate(136);
        NativeBattleHeroDrawResult first = NativeBattleHeroDrawing.DrawFan(fixture.Frame, Fan(fixture.Frame.Profile, 0), fixture.Resources, fixture.Projection, Colors, fixture.View, allocation.Staging, allocation.Address);
        NativeBattleHeroFrame restored = JsonSerializer.Deserialize<NativeBattleHeroFrame>(JsonSerializer.Serialize(first.Frame))!;
        NativeBattleFileStaging staging = JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(first.Staging))!;
        NativeBattleHeroFan next = Fan(fixture.Frame.Profile, 1) with { Angles = new(0, 20, 256) };
        NativeBattleHeroDrawResult expected = NativeBattleHeroDrawing.DrawFan(first.Frame, next, fixture.Resources, fixture.Projection, Colors, fixture.View, first.Staging, allocation.Address);
        NativeBattleHeroDrawResult actual = NativeBattleHeroDrawing.DrawFan(restored, next, fixture.Resources, fixture.Projection, Colors, fixture.View, staging, allocation.Address);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal(allocation.Address + 136, Read(actual.Staging, NativeBattleTemporaryMemory.Cursor));
        NativeBattleFileStaging broken = first.Staging.MarkUnknown(allocation.Address + 0x28, 4);
        Assert.Throws<NotSupportedException>(() => NativeBattleHeroDrawing.DrawFan(first.Frame, next, fixture.Resources, fixture.Projection, Colors, fixture.View, broken, allocation.Address));
        Assert.Throws<InvalidDataException>(() => NativeBattleHeroDrawing.DrawFan(first.Frame, next with { Index = 16 }, fixture.Resources, fixture.Projection, Colors, fixture.View, first.Staging, allocation.Address));
        Assert.Throws<InvalidDataException>(() => NativeBattleHeroDrawing.DrawFan(first.Frame, next, fixture.Resources, fixture.Projection, Colors, fixture.View, NativeBattleTemporaryMemory.Release5082D0(first.Staging, 136), allocation.Address));
    }

    [Fact]
    public void Skipped_ACT_requires_no_phantom_temporary_view_or_GTE_owner()
    {
        var fixture = Fixture(309);
        NativeBattleGfAnimationNode hidden = Node(4096);
        hidden = hidden with { Data = hidden.Data.SetItem(32, 0).SetItem(33, 0).SetItem(34, 0).SetItem(35, 0) };
        NativeBattleHeroAct operation = new(fixture.Resources.Main, hidden, new(new(0, 0, 0), null, 0, 0, 0, null, 0, 0));
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleHeroDrawResult result = NativeBattleHeroDrawing.DrawAct(fixture.Frame, operation, null!, null!, null!, null!, staging, 0);
        Assert.Same(fixture.Frame, result.Frame);
        Assert.Same(staging, result.Staging);
        Assert.Null(result.CursorStart);
        Assert.Empty(result.Batch.Attempts);
    }

    private static NativeBattleSummonColors Colors => NativeBattleGteColorInterpolation.CodeConstants45F270;

    private static NativeBattleHeroFan Fan(NativeBattleHeroProfile profile, int index) => new(new(0, 0, 0), new(100, 200, 300, 0x1234), 3072, 1000, -384, profile.FanSource, index);

    private static NativeBattleTemporaryAllocation Allocate(int length)
    {
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, Enumerable.Repeat((byte)0xA5, length).ToArray());
        return NativeBattleTemporaryMemory.Allocate5082B0(staging, length);
    }

    private static uint Read(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4));

    private static (NativeBattleHeroFrame Frame, NativeBattleHeroResources Resources, NativeBattlePackedMatrix32 View, NativeBattleCameraProjection Projection) Fixture(ushort effect)
    {
        NativeBattleHeroProfile profile = NativeBattleHeroProfile.ForEffect(effect);
        NativeBattleHeroResources resources = new(effect, Source(profile.MainSource, 0x888, 4, 40), Source(profile.OrbitSource, 0x7BC, 3, 18), Mesh(28, 8, 5, 0x2B8), 300188);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        memory = memory with { PrimitiveProcess = memory.PrimitiveProcess! with { ModelRendererRegisters = new(10, 20, 30, 123, 456) } };
        NativeBattleOriginalEffectState selected = new(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), new(0, 0, 8, 0, 0, 0), null);
        NativeBattleHeroFrame frame = new(memory, selected, new(1), new(effect, 0x1000, 0x20000));
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 1024, 4096);
        return (frame, resources, NativeBattlePackedMatrix32.FromMatrix(view, 0xBEEF), new(view, 216, 160, 108));
    }

    private static NativeBattleGfAnimationSource Source(uint address, int length, int nodes, int duration)
    {
        byte[] bytes = new byte[length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 0x700);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), 644);
        Mesh(25, 12, 3, 268).AsSpan().CopyTo(bytes.AsSpan(16));
        Mesh(25, 12, 3, 268).AsSpan().CopyTo(bytes.AsSpan(644));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x700), nodes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x704), duration);
        return new(address, bytes.ToImmutableArray());
    }

    private static ImmutableArray<byte> Mesh(int count, int header, int group, int length)
    {
        byte[] bytes = new byte[length];
        int directory = header + count * 8;
        BinaryPrimitives.WriteInt32LittleEndian(bytes, directory);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), count);
        if (header == 12)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 1);
        }
        for (int index = 0; index < 4; index++)
        {
            int row = header + index * 8;
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(row), (short)((index & 1) == 0 ? -10 : 10));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(row + 2), (short)(index < 2 ? -10 : 10));
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(directory + group * 4), 1);
        int face = directory + (group + 1) * 4;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), group == 3 ? 0x2C808080u : 0x38808080u);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + index * 2), (ushort)(index * 2));
        }
        if (group == 5)
        {
            for (int offset = 12; offset < 24; offset += 4)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + offset), 0x00808080);
            }
        }
        return bytes.ToImmutableArray();
    }

    private static NativeBattleGfAnimationNode Node(short fade)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x200);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28), 4096);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 0x00332211);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), fade);
        return new(0, 0, bytes.ToImmutableArray());
    }
}
