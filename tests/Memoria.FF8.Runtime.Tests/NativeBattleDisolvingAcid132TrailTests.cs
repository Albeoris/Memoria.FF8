using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleDisolvingAcid132Profile;
using static Memoria.FF8.Runtime.NativeBattleDisolvingAcid132Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDisolvingAcid132TrailTests
{
    private const uint Working = NativeBattleEffectNodeMemory.ArenaAddress;

    [Theory]
    [InlineData(0, 150, 0, 1)]
    [InlineData(1, 100, 200, 0)]
    public void Create_reads_original_flag_and_sets_endpoint_tag_without_mutating_source(short flag, short expectedX, short expectedY, ushort expectedTag)
    {
        NativeBattleDisolvingAcid132Frame frame = Frame();
        NativeBattleEffectArena original = PointView(new(100, 400, 600), flag);
        (frame, uint point) = NativeBattleDisolvingAcid132Trail.Create6B1EC0(frame, PointView(new(10, 20, 30)), PointView(new(100, 200, 300)), 3, 150, PointView(new(60, 80, 100)), original, NativeBattleCuragaMatrices.Identity8DD770());
        NativeBattleModulePoolTask curve = Assert.Single(frame.Effect.Pools[4].Slots, task => (task.Flags & 1) != 0);
        Assert.Equal(Working + PointsOffset, point);
        Assert.Equal((ushort)1, curve.ReadWord(14));
        Assert.Equal((ushort)8, curve.ReadWord(60));
        Assert.Equal((ushort)150, curve.ReadWord(58));
        Assert.Equal((ushort)3, curve.ReadWord(56));
        Assert.Equal((ushort)0, curve.ReadWord(30));
        Assert.Throws<NotSupportedException>(() => curve.ReadWord(38));
        Assert.Equal((ushort)40, curve.ReadWord(32));
        Assert.Equal((ushort)100, curve.ReadWord(34));
        Assert.Equal((ushort)80, curve.ReadWord(36));
        Assert.Equal(unchecked((ushort)expectedX), curve.ReadWord(40));
        Assert.Equal(unchecked((ushort)expectedY), curve.ReadWord(42));
        Assert.Equal(expectedTag, curve.ReadWord(46));
        Assert.Equal(flag, BinaryPrimitives.ReadInt16LittleEndian(original.Read(6, 2)));
        Assert.Equal((short)150, PointWord(frame, point + 6));
        Assert.Throws<NotSupportedException>(() => Arena(frame).Read(Offset(point), 6));
    }

    [Fact]
    public void Point_bank_ends_at_scratch_and_exhaustion_does_not_create_a_curve()
    {
        NativeBattleDisolvingAcid132Frame frame = Frame();
        for (int index = 0; index < PointCount; index++)
        {
            frame = WriteArena(frame, Working + PointsOffset + (uint)(index * PointStride) + 6, BitConverter.GetBytes((short)150));
        }
        frame = WriteArena(frame, Working + MorphOffset, BitConverter.GetBytes((uint)0xAABBCCDD));
        (frame, uint point) = NativeBattleDisolvingAcid132Trail.Create6B1EC0(frame, PointView(new(0, 0, 0)), PointView(new(0, 0, 0)), 3, 150, PointView(new(0, 0, 0)), PointView(new(0, 0, 0), 0), NativeBattleCuragaMatrices.Identity8DD770());
        Assert.Equal(0u, point);
        Assert.All(frame.Effect.Pools[4].Slots, task => Assert.Equal(0, task.Flags & 1));
        Assert.Equal(0xAABBCCDDu, BinaryPrimitives.ReadUInt32LittleEndian(Arena(frame).Read(MorphOffset, 4)));
        Assert.Equal(MorphOffset, PointsOffset + PointCount * PointStride);
        Assert.Equal(FirstPacketOffset, MorphOffset + MorphBytes);
    }

    [Fact]
    public void First_tick_appends_point_runs_two_canonical_blends_and_consumes_three_selectors()
    {
        NativeBattleDisolvingAcid132Frame frame = Frame();
        (frame, _) = NativeBattleDisolvingAcid132Trail.Create6B1EC0(frame, PointView(new(10, 20, 30)), PointView(new(100, 200, 300)), 3, 150, PointView(new(60, 80, 100)), PointView(new(0, 0, 0), 1), NativeBattleCuragaMatrices.Identity8DD770());
        uint taskAddress = frame.Effect.Pools[4].BankAddress;
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 3; index++)
        {
            expected = expected.Draw(out _);
        }
        // Prevent particle admission so the only CRT draws are the three selectors.
        NativeBattleEffectTaskPool sprites = frame.Effect.Pools[5];
        for (int index = 0; index < sprites.Slots.Length; index++)
        {
            (sprites, _) = sprites.Append(SpriteCallback);
        }
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(5, sprites) } };
        frame = frame with { Memory = frame.Memory with { ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) } };
        NativeBattleDisolvingAcid132Inputs inputs = Inputs();
        (NativeBattleDisolvingAcid132Frame actual, int result) = NativeBattleDisolvingAcid132Trail.Advance6B2030(frame, taskAddress, Resources(), inputs);
        Assert.Equal(0, result);
        Assert.Equal(expected, actual.Random);
        NativeBattleModulePoolTask task = Read(actual, taskAddress);
        Assert.Equal((ushort)1, task.ReadWord(12));
        Assert.Equal((ushort)2, task.ReadWord(14));
        Assert.Equal((ushort)256, task.ReadWord(30));
        Assert.Equal((ushort)7, task.ReadWord(60));
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), actual.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        NativeBattleDisolvingAcid132Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleDisolvingAcid132Frame>(NativeBattleTestSnapshots.Json(actual))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(actual), NativeBattleTestSnapshots.Json(restored));
    }

    [Fact]
    public void Ribbon_uses_512_depth_denominator_dynamic_UV_and_sparse_halfwords()
    {
        NativeBattleDisolvingAcid132Frame frame = Frame();
        uint first = Working + PointsOffset;
        uint second = first + PointStride;
        frame = Point(frame, first, 100, 200, 512, 150, 0, second);
        frame = Point(frame, second, 116, 200, 512, 150, 1, 0);
        (frame, NativeBattleGfGeometryBatch batch) = NativeBattleDisolvingAcid132Trail.DrawRibbon6B2870(frame, first, 2, Resources());
        NativeBattleGfGeometryAttempt packet = Assert.Single(batch.Attempts);
        Assert.Equal(52, batch.AllocationBytes);
        Assert.Equal(196, PointWord(frame, first + 10));
        Assert.Equal(204, PointWord(frame, first + 18));
        Assert.Equal(0x3E808080u, U32(packet.Data, 4));
        Assert.Equal(0x3C140048u, U32(packet.Data, 12));
        Assert.Equal(0x00B90078u, U32(packet.Data, 24));
        Assert.Equal((ushort)0xE048, BinaryPrimitives.ReadUInt16LittleEndian(packet.Data.AsSpan().Slice(36)));
        Assert.Equal(145, packet.Primitive!.Bucket);
        Assert.All(packet.WriteMask.Skip(38).Take(2), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.All(packet.WriteMask.Skip(50).Take(2), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
    }

    [Fact]
    public void Coincident_first_edge_marks_depth_invalid_and_emits_no_packet()
    {
        NativeBattleDisolvingAcid132Frame frame = Frame();
        uint first = Working + PointsOffset;
        uint second = first + PointStride;
        frame = Point(frame, first, 100, 200, 512, 150, 0, second);
        frame = Point(frame, second, 100, 200, 512, 150, 1, 0);
        (frame, NativeBattleGfGeometryBatch batch) = NativeBattleDisolvingAcid132Trail.DrawRibbon6B2870(frame, first, 2, Resources());
        Assert.Equal((short)-1, PointWord(frame, first + 12));
        Assert.Empty(batch.Attempts);
    }

    private static NativeBattleDisolvingAcid132Frame Frame()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0, FirstPacketOffset);
        for (int index = 0; index < PointCount; index++)
        {
            arena = arena.Write(PointsOffset + index * PointStride + 6, BitConverter.GetBytes(ushort.MaxValue));
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = arena, GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        ImmutableArray<NativeBattleEffectTaskPool> pools = [NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1), NativeBattleEffectTaskPool.Initialize(MainDescriptor, Working + MainOffset, MainStride, MainCount), NativeBattleEffectTaskPool.Initialize(ActDescriptor, Working + ActOffset, ActStride, ActCount), NativeBattleEffectTaskPool.Initialize(SimpleSpriteDescriptor, Working + SimpleSpriteOffset, SimpleSpriteStride, SimpleSpriteCount), NativeBattleEffectTaskPool.Initialize(CurveDescriptor, Working + CurveOffset, CurveStride, CurveCount), NativeBattleEffectTaskPool.Initialize(SpriteDescriptor, Working, SpriteStride, SpriteCount)];
        NativeBattleOriginalEffectState effect = new(132, 131, new(132, Entry, Initializer), null, RootDescriptor) { Pools = pools };
        return new(NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, pools), effect, new(123), NativeBattleDisolvingAcid132State.FromColdProcess().WriteDword(WorkingGlobal, Working)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
    private static NativeBattleDisolvingAcid132Inputs Inputs() => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, 0), _ => new(null, 100, 160, 108), (_, _, _, _, _, _) => throw new InvalidOperationException(), (frame, _) => frame);
    private static NativeBattleDisolvingAcid132Resources Resources()
    {
        byte[] distance = new byte[16384];
        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                distance[y * 128 + x] = unchecked((byte)Math.Sqrt(x * x + y * y));
            }
        }
        return new(new(PrimaryActAddress, []), new(SecondaryActAddress, []), new(ImpactActAddress, []), new(0, TemplateSpriteAddress, 0, 0, []), new(0, SpriteAAddress, 0, 0, []), new(0, SpriteBAddress, 0, 0, []), new(0, SpriteCAddress, 0, 0, []), new("unused", []), 0, new byte[80].ToImmutableArray(), [(short)0, 326, 639, 936, 1216, 1475, 1710, 1910, 2048, 2185, 2385, 2620, 2879, 3159, 3456, 3769, 4096, 4434, 4785, 5146, 5517, 5897, 6287, 6684, 7090], distance.ToImmutableArray());
    }
    private static NativeBattleDisolvingAcid132Frame Point(NativeBattleDisolvingAcid132Frame frame, uint point, short x, short y, short depth, short width, short ordinal, uint next)
    {
        frame = WriteArena(frame, point + 6, BitConverter.GetBytes(width));
        frame = WriteArena(frame, point + 8, BitConverter.GetBytes(x));
        frame = WriteArena(frame, point + 10, BitConverter.GetBytes(y));
        frame = WriteArena(frame, point + 12, BitConverter.GetBytes(depth));
        frame = WriteArena(frame, point + 14, BitConverter.GetBytes(ordinal));
        return WriteArena(frame, point + 24, BitConverter.GetBytes(next));
    }
    private static short PointWord(NativeBattleDisolvingAcid132Frame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(Arena(frame).Read(Offset(address), 2));
    private static int Offset(uint address) => checked((int)(address - Working));
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
}
