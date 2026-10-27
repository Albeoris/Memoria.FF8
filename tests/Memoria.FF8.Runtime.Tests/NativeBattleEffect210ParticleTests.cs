using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect210Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect210ParticleTests
{
    [Fact]
    public void Target_births_skip_random_when_full_and_pause_precedes_anchor_read()
    {
        Fixture fixture = new(0x6F6C00, 1);
        fixture.Task(task => task.WriteWord(12, 1).WriteWord(0x20, 0));
        NativeBattleEffect210Step result = fixture.Advance();
        Assert.Equal(fixture.Frame.Random, result.Frame.Random);
        Assert.Equal(1, fixture.AnchorReads);
        Assert.Equal((ushort)2, Read(result.Frame, fixture.Address).ReadWord(12));
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(FrozenGlobal, 1) };
        fixture.AnchorReads = 0;
        result = fixture.Advance();
        Assert.Equal(0, fixture.AnchorReads);
        Assert.Equal((ushort)1, Read(result.Frame, fixture.Address).ReadWord(12));
        Assert.Empty(fixture.Draws);
    }

    [Fact]
    public void Target_births_copy_anchor_then_replace_W_with_model_angle_in_native_random_order()
    {
        Fixture fixture = new(0x6F6C00);
        fixture.Task(task => task.WriteWord(12, 1).WriteWord(0x20, 0));
        NativeBattleEffect210Step result = fixture.Advance();
        NativeCrtRandomState expected = fixture.Frame.Random;
        for (int index = 0; index < 4; index++)
        {
            int delay = Draw(6) + index * 2;
            int x = 100 + Draw(478) - 239;
            int y = 200 + Draw(478) - 239;
            int z = 300 + Draw(478) - 239;
            if (y > 400 - 550)
            {
                y += -500 - Draw(300);
            }
            int scale = 3328 + Draw(1536);
            NativeBattleModulePoolTask child = result.Frame.Effect.Pools[0].Slots[index + 1];
            Assert.Equal(0x6F6DA0u, child.Callback);
            Assert.Equal((ushort)delay, child.ReadWord(14));
            Assert.Equal(unchecked((ushort)x), child.ReadWord(0x10));
            Assert.Equal(unchecked((ushort)y), child.ReadWord(0x12));
            Assert.Equal(unchecked((ushort)z), child.ReadWord(0x14));
            Assert.Equal((ushort)400, child.ReadWord(0x16));
            Assert.Equal((ushort)scale, child.ReadWord(0x1C));
        }
        Assert.Equal(expected, result.Frame.Random);
        Assert.Equal((ushort)2, Read(result.Frame, fixture.Address).ReadWord(12));

        int Draw(int divisor)
        {
            expected = expected.Draw(out ushort value);
            return value % divisor;
        }
    }

    [Fact]
    public void Target_zero_radius_preserves_native_divide_boundary_after_delay_random()
    {
        Fixture fixture = new(0x6F6C00);
        fixture.Model = fixture.Model.Write(0x26, new byte[2]);
        fixture.Task(task => task.WriteWord(12, 1).WriteWord(0x20, 0));
        Assert.Throws<DivideByZeroException>(() => fixture.Advance());
    }

    [Theory]
    [InlineData(0x6F6DA0)]
    [InlineData(0x6F7130)]
    [InlineData(0x6F7230)]
    public void Positive_delay_only_decrements_unpaused_and_never_draws(uint callback)
    {
        Fixture fixture = new(callback);
        fixture.Task(task => task.WriteWord(14, 1));
        NativeBattleEffect210Step result = fixture.Advance();
        Assert.Equal((ushort)0, Read(result.Frame, fixture.Address).ReadWord(14));
        Assert.Empty(fixture.Draws);
        Assert.Equal(fixture.Frame.Random, result.Frame.Random);
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(FrozenGlobal, 1) };
        result = fixture.Advance();
        Assert.Equal((ushort)1, Read(result.Frame, fixture.Address).ReadWord(14));
        Assert.Empty(fixture.Draws);
    }

    [Fact]
    public void Parent_draws_both_sprites_before_pause_and_retains_exact_second_matrix()
    {
        Fixture fixture = new(0x6F6DA0);
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(FrozenGlobal, 1) };
        NativeBattleEffect210Step result = fixture.Advance();
        Assert.Equal(2, fixture.Draws.Count);
        Assert.Equal(fixture.Frame.Random, result.Frame.Random);
        Assert.Equal((ushort)0, Read(result.Frame, fixture.Address).ReadWord(12));
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CD50(new(1024, 0, 0)), 4096, 4096, 4096) with { X = 100, Y = 400, Z = 300 };
        NativeBattleActionMatrix expected = NativeBattleGfMatrices.Compose56C2F0(Identity(0, 0, 4000), local);
        Assert.Equal(new int?[] { expected.X, expected.Y, expected.Z }, result.Frame.Memory.GeometryRegisters!.Translation);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Parent_births_consume_six_then_nine_random_values_and_failed_appends_consume_none()
    {
        Fixture fixture = new(0x6F6DA0);
        NativeBattleEffect210Step result = fixture.Advance();
        NativeBattleEffectTaskPool pool = Assert.Single(result.Frame.Effect.Pools);
        Assert.Equal(4, pool.Slots.Count(task => task.Callback == 0x6F7230));
        Assert.Equal(10, pool.Slots.Count(task => task.Callback == 0x6F7130));
        NativeCrtRandomState expected = fixture.Frame.Random;
        for (int index = 0; index < 114; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, result.Frame.Random);
        fixture = new(0x6F6DA0, 1);
        result = fixture.Advance();
        Assert.Equal(fixture.Frame.Random, result.Frame.Random);
        Assert.Equal(2, fixture.Draws.Count);
    }

    [Fact]
    public void Shared_particle_draws_before_pause_then_uses_signed_word_motion_and_retirement()
    {
        Fixture fixture = new(0x6F7130);
        fixture.Task(task => task.WriteWord(12, 0).WriteWord(0x18, unchecked((ushort)-17)).WriteWord(0x1A, unchecked((ushort)-17)).WriteWord(0x1C, 17).WriteWord(0x20, 100));
        NativeBattleEffect210Step result = fixture.Advance();
        NativeBattleModulePoolTask current = Read(result.Frame, fixture.Address);
        Assert.Equal(2, result.Result);
        Assert.Equal(new[] { 5 }, fixture.SharedResourceReads);
        Assert.Single(fixture.Draws);
        Assert.Equal((ushort)83, current.ReadWord(0x10));
        Assert.Equal((ushort)183, current.ReadWord(0x12));
        Assert.Equal((ushort)317, current.ReadWord(0x14));
        Assert.Equal(unchecked((ushort)-15), current.ReadWord(0x18));
        Assert.Equal(unchecked((ushort)-19), current.ReadWord(0x1A));
        Assert.Equal((ushort)16, current.ReadWord(0x1C));
        Assert.Equal((ushort)36, current.ReadWord(0x20));
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(FrozenGlobal, 1) };
        fixture.Draws.Clear();
        result = fixture.Advance();
        Assert.Single(fixture.Draws);
        Assert.Equal((ushort)100, Read(result.Frame, fixture.Address).ReadWord(0x20));
        Assert.Equal(0, result.Result);
    }

    [Fact]
    public void Rising_particle_terminal_frame_updates_after_draw_and_survives_JSON_next()
    {
        Fixture fixture = new(0x6F7230);
        fixture.Task(task => task.WriteWord(12, 15).WriteWord(0x16, unchecked((ushort)-33)).WriteWord(0x1C, 100));
        NativeBattleEffect210Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect210Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(fixture.Frame)))!);
        NativeBattleEffect210Step direct = fixture.Advance();
        fixture.Frame = restored;
        NativeBattleEffect210Step cold = fixture.Advance();
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(direct)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(cold)));
        NativeBattleModulePoolTask task = Read(cold.Frame, fixture.Address);
        Assert.Equal(2, cold.Result);
        Assert.Equal((ushort)167, task.ReadWord(0x12));
        Assert.Equal(unchecked((ushort)-35), task.ReadWord(0x16));
        Assert.Equal((ushort)99, task.ReadWord(0x1C));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(255)]
    public void First_palette_override_is_one_retained_signed_BYTE_and_preserves_native_CLUT_addition(byte palette)
    {
        Fixture fixture = new(0x6F7230);
        NativeBattleEffectMemory memory = fixture.Frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(fixture.Frame.Memory.GeometryRegisters!, Identity(0, 0, 2000), true, true, null) };
        NativeBattleModuleResource resource = QuadSprite();
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 180).Write(0, BitConverter.GetBytes(resource.Address)).Write(4, new byte[2]).Write(0x20, [palette]).Write(0x24, BitConverter.GetBytes((ushort)0x10));
        Assert.Throws<InvalidDataException>(() => NativeBattleModuleSpriteDrawing.Draw571C80(memory, context, _ => resource, new(null, 100, 160, 108), 0));
        NativeBattleModuleSpriteDraw result = NativeBattleModuleSpriteDrawing.Draw571C80(memory, context, _ => resource, new(null, 100, 160, 108), 0, allowFirstPaletteOverride: true);
        Assert.Equal(new byte[] { palette, 0xB6, 0xC7, 0xD8 }, result.Temporary180.Read(0x20, 4));
        NativeBattleGfGeometryAttempt prefix = Assert.Single(result.Batch.Attempts, attempt => attempt.Primitive is null);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts, attempt => attempt.Primitive is not null);
        ushort expected = unchecked((ushort)(0x123 + (unchecked((sbyte)palette) << 6)));
        Assert.Equal(expected, (ushort)(attempt.Primitive!.Words[3] >> 16));
        Assert.Equal(expected, BinaryPrimitives.ReadUInt16LittleEndian(prefix.Data.AsSpan(14, 2)));
        Assert.Equal(0, prefix.ByteOffsetRelative);
        Assert.Equal(prefix.ByteOffsetRelative, attempt.ByteOffsetRelative);
        Assert.Equal(NativeBattleGfPacketByteState.Known, prefix.WriteMask[14]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[14]);
        NativeBattleEffectArena d8 = NativeBattleEffectNodeContextMemory.Copy(context, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 216), 0, 180);
        Assert.Throws<InvalidDataException>(() => NativeBattleProjectedSpriteD8.Draw(new(memory, d8), _ => resource, new(null, 100, 160, 108), 0, 17));
    }

    [Fact]
    public void Empty_sprite_does_not_consume_unknown_preserved_palette_BYTE()
    {
        Fixture fixture = new(0x6F7230);
        NativeBattleModuleResource resource = Sprite(0x1000000);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 180).Write(0, BitConverter.GetBytes(resource.Address)).Write(4, new byte[2]).Write(0x24, BitConverter.GetBytes((ushort)0x10));
        NativeBattleModuleSpriteDraw result = NativeBattleModuleSpriteDrawing.Draw571C80(fixture.Frame.Memory, context, _ => resource, new(null, 100, 160, 108), 0, allowFirstPaletteOverride: true);
        Assert.Empty(result.Batch.Attempts);
        Assert.False(result.Temporary180.ReadSnapshot(0x20, 1).Known[0]);
        Assert.Equal(new byte[3], result.Temporary180.Read(0x21, 3));
    }

    private sealed class Fixture
    {
        public const uint Bank = 0x20DFAB8;
        public uint Address => Bank;
        public NativeBattleEffect210Frame Frame;
        public NativeBattleEffectArena Model = NativeBattleEffectArena.Clear().Write(0x24, BitConverter.GetBytes((short)400)).Write(0x26, BitConverter.GetBytes((short)700));
        public NativeBattleEffect210Resources Resources;
        public List<NativeBattleEffect210DrawBatch> Draws = [];
        public List<int> SharedResourceReads = [];
        public int AnchorReads;

        public Fixture(uint callback, int count = 150)
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(EarlyDescriptor, Bank, 36, checked((ushort)count));
            (pool, _) = pool.Append(callback);
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Identity(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(Identity(), null) };
            Frame = new(memory, new(210, 209, new(210, Entry, Initializer), null, null) { Pools = [pool] }, new(123), NativeBattleEffect210State.FromColdProcess().WriteDword(ArenaGlobal, Bank).WriteDword(CursorGlobal, Bank + 0xB1B8)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            ImmutableDictionary<uint, NativeBattleModuleResource> sprites = new uint[] { 0x14708C0, 0x1470A6C, 0x1470C18 }.ToImmutableDictionary(address => address, Sprite);
            Resources = new(new("fixture", []), new("fixture", []), ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, sprites, ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
            Task(task => task.WriteWord(12, 0).WriteWord(14, 0).WriteWord(0x10, 100).WriteWord(0x12, 200).WriteWord(0x14, 300).WriteWord(0x16, 400).WriteWord(0x1C, 4096));
        }

        public void Task(Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> change) => Frame = Write(Frame, Address, change(Read(Frame, Address)));
        public NativeBattleEffect210Step Advance() => NativeBattleEffect210Lower.Advance(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(Frame)), Address, Resources, Inputs());
        public NativeBattleEffect210Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => 0, null!, null!, (_, _) => Model, null!, _ => NativeBattlePackedMatrix32.FromMatrix(Identity(0, 0, 4000), null), _ => new(null, 100, 160, 108), _ => 0, null!, (frame, _, _) =>
        {
            AnchorReads++;
            return new(frame, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)100)).Write(2, BitConverter.GetBytes((short)200)).Write(4, BitConverter.GetBytes((short)300)).Write(6, BitConverter.GetBytes((short)0x1234)));
        }, null!, null!, (frame, operation) =>
        {
            Draws.Add(Assert.IsType<NativeBattleEffect210DrawBatch>(operation));
            return frame;
        })
        {
            ReadCommonSprite5710B0 = (_, id) =>
            {
                SharedResourceReads.Add(id);
                return Sprite(0x1000000);
            }
        };
    }

    private static NativeBattleActionMatrix Identity(int x = 0, int y = 0, int z = 0) => NativeBattleCuragaMatrices.Identity8DD770() with { X = x, Y = y, Z = z };

    private static NativeBattleModuleResource Sprite(uint address)
    {
        byte[] bytes = new byte[48];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 16);
        for (int index = 0; index <= 16; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + index * 2), 44);
        }
        return new(5, address, 0, 0, bytes.ToImmutableArray());
    }

    private static NativeBattleModuleResource QuadSprite()
    {
        byte[] bytes = new byte[38];
        bytes[4] = 0xA5;
        bytes[5] = 0xB6;
        bytes[6] = 0xC7;
        bytes[7] = 0xD8;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 0xFFFF);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14), 1);
        bytes[18] = bytes[20] = 2;
        bytes[19] = 100;
        bytes[21] = 0x2C;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(24), 0x123);
        return new(5, 0x1000000, 0, 0, bytes.ToImmutableArray());
    }
}
