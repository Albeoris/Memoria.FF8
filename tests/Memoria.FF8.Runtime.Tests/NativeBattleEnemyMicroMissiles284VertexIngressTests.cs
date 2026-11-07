using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEnemyMicroMissiles284Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEnemyMicroMissiles284VertexIngressTests
{
    private static uint Scratch => ArenaAddress + ScratchOffset;

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Morph_ingress_matches_detached_knownness_copy_with_signed_optional_W(int unknown)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(ScratchOffset, Point(short.MinValue, short.MaxValue, -2, -1234));
        if (unknown >= 0)
        {
            arena.MarkUnknown(ScratchOffset + (unknown == 8 ? 6 : unknown), unknown == 8 ? 2 : 1);
        }
        var expected = DetachedPoint(arena, ScratchOffset);
        var actual = BattleSession.ReadNativeEnemyMicroMissiles284Vertex(arena, Mesh(), Scratch, 0);
        Assert.Equal(expected, actual);
        Assert.Equal(new NativeBattleVector(short.MinValue, short.MaxValue, -2), actual.Vertex);
        Assert.Equal(unknown == -1 ? (short?)-1234 : null, actual.W);
        NativeBattleEffectArena restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(arena));
        Assert.Equal(actual, BattleSession.ReadNativeEnemyMicroMissiles284Vertex(restored, Mesh(), Scratch, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Unknown_XYZ_is_rejected_even_when_W_is_known(int unknown)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(ScratchOffset, Point(1, 2, 3, 4)).MarkUnknown(ScratchOffset + unknown, 1);
        Assert.Throws<NotSupportedException>(() => DetachedPoint(arena, ScratchOffset));
        Assert.Throws<NotSupportedException>(() => BattleSession.ReadNativeEnemyMicroMissiles284Vertex(arena, Mesh(), Scratch, 0));
    }

    [Fact]
    public void Immutable_shapes_require_no_live_arena_and_preserve_selected_row_and_W()
    {
        NativeBattleStaticMeshResource mesh = Mesh();
        var first = BattleSession.ReadNativeEnemyMicroMissiles284Vertex(null, mesh, mesh.Address + 12, 8);
        var second = BattleSession.ReadNativeEnemyMicroMissiles284Vertex(null, mesh, mesh.Address + 28, 0);
        Assert.Equal(new NativeBattleVector(400, -500, 6000), first.Vertex);
        Assert.Equal((short?)2222, first.W);
        Assert.Equal(new NativeBattleVector(100, 400, 5000), second.Vertex);
        Assert.Equal((short?)3333, second.W);
        Assert.Throws<InvalidDataException>(() => BattleSession.ReadNativeEnemyMicroMissiles284Vertex(null, mesh, mesh.Address + 13, 0));
        Assert.Throws<InvalidDataException>(() => BattleSession.ReadNativeEnemyMicroMissiles284Vertex(null, mesh, mesh.Address + 44, 0));
        Assert.Throws<InvalidDataException>(() => BattleSession.ReadNativeEnemyMicroMissiles284Vertex(null, mesh, mesh.Address + 12, 1));
        Assert.Throws<InvalidDataException>(() => BattleSession.ReadNativeEnemyMicroMissiles284Vertex(null, mesh, mesh.Address + 12, 16));
        Assert.Throws<NotSupportedException>(() => BattleSession.ReadNativeEnemyMicroMissiles284Vertex(null, mesh, Scratch, 0));
    }

    [Fact]
    public void Morph_ingress_observes_private_writes_and_rejects_revoked_owner()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(ScratchOffset, Point(1, 2, 3, 4));
        using NativeBattleEffectArenaTransaction transaction = NativeBattleEffectArenaTransaction.CreatePrivateChild(arena);
        transaction.View.Write(ScratchOffset, Point(-10, -20, -30, -40)).MarkUnknown(ScratchOffset + 7, 1);
        var actual = BattleSession.ReadNativeEnemyMicroMissiles284Vertex(transaction.View, Mesh(), Scratch, 0);
        Assert.Equal(new NativeBattleVector(-10, -20, -30), actual.Vertex);
        Assert.Null(actual.W);
        Assert.Equal((new NativeBattleVector(1, 2, 3), (short?)4), BattleSession.ReadNativeEnemyMicroMissiles284Vertex(arena, Mesh(), Scratch, 0));
        transaction.Dispose();
        Assert.Throws<ObjectDisposedException>(() => BattleSession.ReadNativeEnemyMicroMissiles284Vertex(transaction.View, Mesh(), Scratch, 0));
    }

    [Theory]
    [InlineData(0, -100, 200, 3000, 400, -500, 6000)]
    [InlineData(2048, 0, 300, 4000, 600, -300, 7000)]
    [InlineData(4096, 100, 400, 5000, 800, -100, 8000)]
    public void Reused_morph_source_copies_distinct_vertices_without_overwriting_W(int ratio, int ax, int ay, int az, int bx, int by, int bz)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(ScratchOffset, Point(11, 22, 33, -1234)).Write(ScratchOffset + 8, Point(44, 55, 66, short.MinValue)).MarkUnknown(ScratchOffset + 6, 1);
        var firstW = arena.ReadSnapshot(ScratchOffset + 6, 2);
        var secondW = arena.ReadSnapshot(ScratchOffset + 14, 2);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = arena, GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        NativeBattleOriginalEffectState effect = new(284, 283, new(284, Entry, Initializer), null, null);
        NativeBattleEnemyMicroMissiles284Frame frame = new(memory, effect, new(123), NativeBattleEnemyMicroMissiles284State.FromColdProcess());
        NativeBattleEnemyMicroMissiles284Frame actual = NativeBattleEnemyMicroMissiles284Controller.MorphProgramme284(frame, Mesh(), 0, 1, ratio, Scratch);
        Assert.Same(arena, actual.Memory.Arena);
        Assert.Same(effect, actual.Effect);
        Assert.Same(frame.State, actual.State);
        Assert.Equal(frame.Random, actual.Random);
        Assert.Equal(new NativeBattleVector(checked((short)ax), checked((short)ay), checked((short)az)), BattleSession.ReadNativeEnemyMicroMissiles284Vertex(arena, Mesh(), Scratch, 0).Vertex);
        Assert.Equal(new NativeBattleVector(checked((short)bx), checked((short)by), checked((short)bz)), BattleSession.ReadNativeEnemyMicroMissiles284Vertex(arena, Mesh(), Scratch, 8).Vertex);
        Assert.Equal(firstW.Data, arena.ReadSnapshot(ScratchOffset + 6, 2).Data);
        Assert.Equal(firstW.Known, arena.ReadSnapshot(ScratchOffset + 6, 2).Known);
        Assert.Equal(secondW.Data, arena.ReadSnapshot(ScratchOffset + 14, 2).Data);
        Assert.Equal(secondW.Known, arena.ReadSnapshot(ScratchOffset + 14, 2).Known);
    }

    private static (NativeBattleVector Vertex, short? W) DetachedPoint(NativeBattleEffectArena arena, int offset)
    {
        NativeBattleEffectArena point = NativeBattleEffectNodeContextMemory.Copy(arena, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
        byte[] xyz = point.Read(0, 6);
        var w = point.ReadSnapshot(6, 2);
        return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), w.Known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w.Data) : null);
    }

    private static NativeBattleStaticMeshResource Mesh()
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 2);
        Point(-100, 200, 3000, 1111).CopyTo(bytes, 12);
        Point(400, -500, 6000, 2222).CopyTo(bytes, 20);
        Point(100, 400, 5000, 3333).CopyTo(bytes, 28);
        Point(800, -100, 8000, 4444).CopyTo(bytes, 36);
        return new(0, 0x10000000, bytes.ToImmutableArray());
    }

    private static byte[] Point(short x, short y, short z, short w)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, x);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), z);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), w);
        return bytes;
    }
}
