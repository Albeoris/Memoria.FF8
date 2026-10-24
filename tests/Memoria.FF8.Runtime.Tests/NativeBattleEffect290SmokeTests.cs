using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect290Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect290Lower;
using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect290LowerTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect290SmokeTests
{
    [Fact]
    public void Last_smoke_draw_precedes_decrement_and_signed_BYTE_motion_and_releases_once()
    {
        Fixture fixture = new();
        fixture.Resources = fixture.Resources with { ProjectedQuads = fixture.Resources.ProjectedQuads.Add(SmokeSourceAddress, Source()) };
        uint address = Fixture.Main + 0x5ED8;
        NativeBattleEffectArena row = PutWord290(PutWord290(PutWord290(ReadBytes(fixture.Frame, address, 12), 0, 100), 4, 4000), 6, 1).Write(8, [253, 2, 1]);
        fixture.Frame = WriteBytes(fixture.Frame, address, row, 12);
        NativeBattleEffect290Frame result = DrawSmoke(fixture.Frame, Fixture.Main, fixture.Resources, fixture.Inputs());
        NativeBattleEffect290DrawBatch delivery = Assert.Single(fixture.Deliveries);
        NativeBattleGeometryPrimitive polygon = Assert.Single(delivery.Batch.Attempts).Primitive!;
        Assert.NotNull(polygon);
        Assert.Equal(19, polygon.Bucket);
        Assert.Equal(40, delivery.Batch.AllocationBytes);
        Assert.Equal(result.Memory.GeometryRegisters!.Sz.Select(value => unchecked((int)value!.Value)), polygon.VertexPayload4);
        row = ReadBytes(result, address, 12);
        Assert.Equal(0, Word290(row, 6));
        Assert.Equal(88, Word290(row, 0));
        Assert.Equal(8, Word290(row, 2));
        Assert.Equal(4004, Word290(row, 4));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(fixture.Frame.Random, result.Random);
        Assert.Equal(0, fixture.ProcessDraws);
    }

    [Fact]
    public void Empty_smoke_scan_still_publishes_source_and_only_the_native_initial_WORDs()
    {
        Fixture fixture = new();
        fixture.Resources = fixture.Resources with { ProjectedQuads = fixture.Resources.ProjectedQuads.Add(SmokeSourceAddress, Source()) };
        fixture.Frame = fixture.Frame with { Staging = fixture.Frame.Staging!.MarkUnknown(NativeBattleTemporaryMemory.Address + 0x95, 1) };
        NativeBattleEffect290Frame result = DrawSmoke(fixture.Frame, Fixture.Main, fixture.Resources, fixture.Inputs());
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(result.Staging!, NativeBattleTemporaryMemory.Address, 180);
        Assert.Equal(SmokeSourceAddress, Dword290(temporary, 0));
        foreach (int offset in new[] { 0x92, 0x90, 0x8E, 0x8A, 0x88, 0x86, 0x60, 0x58, 0x50, 0x48 })
        {
            Assert.Equal(0, Word290(temporary, offset));
        }
        Assert.False(temporary.ReadSnapshot(0x95, 1).Known[0]);
        Assert.Empty(fixture.Deliveries);
        Assert.Equal(fixture.Frame.Memory.GeometryRegisters, result.Memory.GeometryRegisters);
    }

    [Fact]
    public void Shared_quad_leaf_keeps_first_FLAG_admission_and_current_fourth_RTps_FIFO()
    {
        Fixture fixture = new();
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 180);
        foreach (int offset in new[] { 0x48, 0x50, 0x58, 0x60 })
        {
            context = PutWord290(context, offset, 0);
        }
        NativeBattlePackedMatrix32 parent = NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, -4096, -4096, 4096], 0, 0, 33, 4096), null);
        context = CopyBytes(PackedArena(parent), 0, context, 0x84, 32);
        NativeBattleProjectedQuads672DB0Step result = NativeBattleProjectedQuads672DB0.DrawLoaded(fixture.Frame.Memory, context, Source(-1, -1), 44, 1, new(null, 60, 160, 108));
        NativeBattleGeometryPrimitive polygon = Assert.Single(result.Batch.Attempts).Primitive!;
        Assert.NotNull(polygon);
        Assert.Equal(new[] { 65, 33, 33, 1 }, polygon.VertexPayload4);
        Assert.NotEqual(0u, result.Memory.ScriptedColorRegisters!.Flags!.Value & 0x60000u);
        Assert.Equal(0u, Dword290(result.Context180, 0x40) & 0x60000u);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, result.Batch.Attempts[0].WriteMask[38]);
    }

    private static NativeBattleModuleResource Source(short x = 0, short y = 0)
    {
        byte[] bytes = new byte[64];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 12);
        for (int directory = 10; directory <= 32; directory += 2)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(directory), 40);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40), 1);
        bytes[44] = 2;
        bytes[46] = 2;
        bytes[47] = 0x2C;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(52), x);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(54), y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(60), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(62), 4096);
        return new(0, SmokeSourceAddress, (uint)bytes.Length, 0, bytes.ToImmutableArray());
    }
}
