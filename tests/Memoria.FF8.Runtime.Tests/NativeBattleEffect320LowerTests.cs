using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect320Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect320Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect320LowerTests
{
    [Theory]
    [InlineData(2, false, 40)]
    [InlineData(3, false, 48)]
    [InlineData(6, false, 48)]
    [InlineData(7, false, 60)]
    [InlineData(3, true, 72)]
    [InlineData(7, true, 84)]
    public void Custom_textured_groups_keep_raw_prefix_then_link_E1_and_optional_E2(int group, bool window, int allocation)
    {
        Fixture fixture = new();
        NativeBattleStaticMeshResource mesh = Mesh(0x1000000, group, 2000);
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh), Programmes = fixture.Resources.Programmes.Add(mesh.Address, new(mesh.Address, mesh.Bytes)) };
        (fixture.Frame, uint temporary) = Allocate(fixture.Frame, 108);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 108).Write(0, BitConverter.GetBytes(mesh.Address)).Write(4, BitConverter.GetBytes(mesh.Address + 12)).Write(8, new byte[3]).Write(12, BitConverter.GetBytes(1024)).Write(0x18, BitConverter.GetBytes(-128)).Write(0x1C, BitConverter.GetBytes(0x20F0u)).Write(0x20, new byte[4]);
        if (window)
        {
            context = context.Write(0x22, BitConverter.GetBytes((ushort)112)).Write(0x24, new byte[4]).Write(0x28, BitConverter.GetBytes((ushort)256)).Write(0x2A, BitConverter.GetBytes((ushort)256)).Write(0x2C, new byte[4]).Write(0x30, BitConverter.GetBytes((ushort)256)).Write(0x32, BitConverter.GetBytes((ushort)128));
        }
        fixture.Frame = PublishContext(fixture.Frame, temporary, context, 108);
        NativeBattleEffect320Frame result = NativeBattleEffect320Lower.Draw65C750(fixture.Frame, temporary, fixture.Resources, fixture.Inputs());
        NativeBattleGfGeometryBatch batch = Assert.Single(fixture.Deliveries).Batch;
        Assert.Equal(allocation, batch.AllocationBytes);
        Assert.Null(batch.Attempts[0].Primitive);
        NativeBattleGfGeometryAttempt mode = batch.Attempts[1];
        int bodyLength = group switch { 2 => 32, 7 => 52, _ => 40 };
        Assert.Equal(bodyLength, mode.ByteOffsetRelative);
        Assert.Equal(0xE1000220u, mode.Primitive!.Words[1]);
        Assert.Equal(new[] { 0, 0, 0, 0 }, mode.Primitive.VertexPayload4);
        NativeBattleGfGeometryAttempt body = batch.Attempts.Single(attempt => attempt.Primitive is { Words.Length: > 3 });
        Assert.Equal(0, body.ByteOffsetRelative);
        Assert.All(body.WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.Equal((group & 1) == 0 ? new[] { 2000, 2000, 2000, 0 } : new[] { 2000, 2000, 2000, 2000 }, body.Primitive!.VertexPayload4);
        if (window)
        {
            Assert.Equal(5, batch.Attempts.Length);
            Assert.Equal(bodyLength + 8, batch.Attempts[2].ByteOffsetRelative);
            Assert.Equal(0xE2000000u, batch.Attempts[2].Primitive!.Words[1]);
            Assert.Equal(bodyLength + 20, batch.Attempts[4].ByteOffsetRelative);
            Assert.Equal(0xE2000200u, batch.Attempts[4].Primitive!.Words[1]);
            Assert.Equal(234, batch.Attempts[0].Data[13]);
        }
        Assert.Equal(mesh.Address + (uint)mesh.Bytes.Length, BinaryPrimitives.ReadUInt32LittleEndian(Context(result, temporary, 108).Read(0x34, 4)));
        Assert.NotNull(result.Memory.GeometryRegisters!.Otz);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(7)]
    public void Rejected_custom_faces_keep_native_prefix_and_allocate_no_modes(int group)
    {
        Fixture fixture = new();
        NativeBattleStaticMeshResource mesh = Mesh(0x1000000, group, 0);
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh), Programmes = fixture.Resources.Programmes.Add(mesh.Address, new(mesh.Address, mesh.Bytes)) };
        (fixture.Frame, uint temporary) = Allocate(fixture.Frame, 108);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 108).Write(0, BitConverter.GetBytes(mesh.Address)).Write(4, BitConverter.GetBytes(mesh.Address + 12)).Write(0x1C, BitConverter.GetBytes(0x2030u)).Write(0x20, new byte[4]);
        fixture.Frame = PublishContext(fixture.Frame, temporary, context, 108);
        NativeBattleEffect320Frame result = NativeBattleEffect320Lower.Draw65C750(fixture.Frame, temporary, fixture.Resources, fixture.Inputs());
        NativeBattleGfGeometryBatch batch = Assert.Single(fixture.Deliveries).Batch;
        Assert.Equal(0, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt raw = Assert.Single(batch.Attempts);
        Assert.Null(raw.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, raw.WriteMask[12]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, raw.WriteMask[8]);
        Assert.DoesNotContain(true, Context(result, temporary, 108).ReadSnapshot(0x38, 4).Known);
    }

    [Fact]
    public void Flash_brackets_keep_native_physical_order_and_table_bucket()
    {
        Fixture fixture = new();
        NativeBattleEffect320Lower.Flash65D970(fixture.Frame, 0x808080, fixture.Inputs());
        NativeBattleGfGeometryBatch batch = Assert.Single(fixture.Deliveries).Batch;
        Assert.Equal(40, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 8, 32 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.Equal(new uint[] { 0xE10002A0, 0x2A808080, 0xE10002C0 }, batch.Attempts.Select(attempt => attempt.Primitive!.Words[1]));
        Assert.All(batch.Attempts, attempt => Assert.Equal(8, attempt.Primitive!.Bucket));
        Assert.Equal(0x00D80140u, batch.Attempts[1].Primitive!.Words[5]);
    }

    [Theory]
    [InlineData(0, 1, 12, -2, 0)]
    [InlineData(15, 16, 10, 0, 2)]
    public void Particle_draws_before_motion_and_last_draw_returns_before_motion(int phase, int expectedPhase, int expectedX, int expectedVelocityY, int resultCode)
    {
        Fixture fixture = new();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0, 0x20D0000, 32, 1);
        (pool, int slot) = pool.Append(ParticleCallback);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0xC, (ushort)phase).WriteWord(0x10, 10).WriteWord(0x12, 20).WriteWord(0x14, 2000).WriteWord(0x16, 4096).WriteWord(0x18, 2).WriteWord(0x1A, 0).WriteWord(0x1C, 0);
        fixture.Frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [pool with { Slots = pool.Slots.SetItem(slot, task) }] } };
        NativeBattleEffect320Step result = NativeBattleEffect320Lower.Advance(fixture.Frame, pool.BankAddress, fixture.Resources, fixture.Inputs());
        NativeBattleModulePoolTask after = Read(result.Frame, pool.BankAddress);
        Assert.Equal(resultCode, result.Result);
        Assert.Equal(expectedPhase, after.ReadWord(0xC));
        Assert.Equal(expectedX, after.ReadWord(0x10));
        Assert.Equal(expectedVelocityY, unchecked((short)after.ReadWord(0x1A)));
        Assert.Single(fixture.Deliveries);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    private sealed class Fixture
    {
        public NativeBattleEffect320Frame Frame;
        public NativeBattleEffect320Resources Resources;
        public List<NativeBattleEffect320DrawBatch> Deliveries = [];

        public Fixture()
        {
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Identity(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(Identity(), null) };
            Frame = new(memory, new(320, 319, new(320, Entry, Initializer), null, null) { Pools = [] }, new(123), NativeBattleEffect320State.FromColdProcess().WriteDword(ArenaGlobal, 0x20DFAB8).WriteDword(CursorGlobal, 0x20E0DCC)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            byte[] sprite = new byte[48];
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 16);
            for (int index = 0; index <= 16; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + index * 2), 44);
            }
            Resources = new(new("fixture", []), new("fixture", []), ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, new(0, 0xFE7470, 0, 0, sprite.ToImmutableArray()), 0, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
        }

        public NativeBattleEffect320Inputs Inputs()
        {
            return new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, null!, null!, _ => NativeBattlePackedMatrix32.FromMatrix(Identity(), null), _ => new(null, 100, 160, 108), _ => 0, null!, null!, (frame, operation) =>
            {
                NativeBattleEffect320DrawBatch draw = Assert.IsType<NativeBattleEffect320DrawBatch>(operation);
                Deliveries.Add(draw);
                return frame with { State = frame.State.WriteDword(CursorGlobal, frame.State.ReadDword(CursorGlobal) + (uint)draw.Batch.AllocationBytes) };
            });
        }
    }

    private static NativeBattleActionMatrix Identity()
    {
        return NativeBattleCuragaMatrices.Identity8DD770();
    }

    private static NativeBattleStaticMeshResource Mesh(uint address, int group, short z)
    {
        int count = (group & 1) == 0 ? 3 : 4;
        int polygon = 12 + count * 8;
        int size = group switch { 2 => 20, 3 => 24, 6 => 28, _ => 36 };
        byte[] data = new byte[polygon + 32 + size];
        BinaryPrimitives.WriteUInt32LittleEndian(data, (uint)polygon);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), (uint)count);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 1);
        short[] xy = [-200, -200, 200, -200, -200, 200, 200, 200];
        for (int index = 0; index < count; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12 + index * 8), xy[index * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(14 + index * 8), xy[index * 2 + 1]);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(16 + index * 8), z);
        }
        int face = polygon + group * 4 + 4;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(face), group switch { 2 => 0x2680A0C0u, 3 => 0x2E80A0C0u, 6 => 0x3680A0C0u, _ => 0x3E80A0C0u });
        for (int index = 0; index < count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(face + 4 + index * 2), (ushort)(index * 2));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(face + 12), 0xFAF0);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(face + 16), 0xFAF0);
        if (count == 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(face + 20), 0xFAF0FAF0);
        }
        else
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(face + 10), 0xFAF0);
        }
        if (group >= 6)
        {
            int colors = count == 4 ? 24 : 20;
            for (int index = 0; index < count - 1; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(face + colors + index * 4), 0x00102030u + (uint)index * 0x101010);
            }
        }
        return new(0, address, data.ToImmutableArray());
    }
}
