using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleThundaga105DebrisTests
{
    private static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static NativeBattleEffectMemory Memory => NativeBattleEffectMemory.FromColdProcess() with { Arena = new NativeBattleEffectArena([]) };

    [Fact]
    public void ClearOnlyTouchesAliveWordsAndFullBankAllocationConsumesNoRandom()
    {
        byte[] warm = Enumerable.Repeat((byte)0xA5, 310 * 0x50).ToArray();
        NativeBattleEffectMemory memory = Memory with { Arena = Memory.Arena!.Write(0x2A24, warm) };
        NativeCrtRandomState random = new(123);
        NativeBattleThundaga105DebrisSpawn full = NativeBattleThundaga105Debris.Spawn6D5CF0(memory, random, Triangle(), new(0, 0, 0), new(0, 0, 0), new(0, 0, 0), Identity);
        Assert.Null(full.Row);
        Assert.Same(memory, full.Memory);
        Assert.Same(random, full.Random);
        byte[] cleared = NativeBattleThundaga105Debris.ClearAlive(memory).Arena!.Read(0x2A24, warm.Length);
        for (int offset = 0; offset < warm.Length; offset++)
        {
            Assert.Equal(offset % 0x50 is 6 or 7 ? (byte)0 : (byte)0xA5, cleared[offset]);
        }
    }

    [Fact]
    public void SpawnUsesFirstFreePhysicalRowTruncatedCentroidAndLiteralDuplicatedPage()
    {
        NativeCrtRandomState random = new(91);
        NativeBattleThundaga105DebrisSpawn spawned = NativeBattleThundaga105Debris.Spawn6D5CF0(Memory, random, Triangle(), new(100, 200, 300), new(10, 20, 30), new(40, 50, 60), Identity);
        Assert.Equal<int?>(0, spawned.Row);
        NativeCrtRandomState expected = random;
        for (int draw = 0; draw < 4; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, spawned.Random);
        byte[] row = spawned.Memory.Arena!.Read(0x2A24, 0x50);
        Assert.Equal((short)98, S(row, 0));
        Assert.Equal((short)198, S(row, 2));
        Assert.Equal((short)9, S(row, 8));
        Assert.Equal((short)18, S(row, 10));
        Assert.Equal((short)50, S(row, 0x12));
        Assert.Equal((short)0, S(row, 0x10));
        Assert.Equal((short)0x13B, S(row, 0xE));
        Assert.Equal(S(row, 0xE), S(row, 0x16));
        Assert.Equal((short)-2, S(row, 0x18));
        Assert.Equal((short)0x1234, S(row, 0x1E));
        Assert.Equal((byte)0x34, row[0x33]);
        Assert.Equal((short)4096, S(row, 6));
        Assert.InRange(S(row, 0x4E), -227, -100);
        Assert.Equal<int?>(1, NativeBattleThundaga105Debris.Spawn6D5CF0(spawned.Memory, spawned.Random, Triangle(), new(0, 0, 0), new(0, 0, 0), new(0, 0, 0), Identity).Row);
    }

    [Fact]
    public void UpdateMutatesRelativeVerticesAndBouncesAfterPositionBeforeAcceleration()
    {
        NativeBattleThundaga105DebrisSpawn spawned = NativeBattleThundaga105Debris.Spawn6D5CF0(Memory, new(1), Triangle(), new(0, 0, 0), new(0, 0, 0), new(0, 0, 0), Identity);
        byte[] row = spawned.Memory.Arena!.Read(0x2A24, 0x50);
        W(row, 0, 0);
        W(row, 2, -1);
        W(row, 0xA, 32);
        W(row, 0x12, 30);
        W(row, 0x4E, -100);
        W(row, 0x18, 100);
        W(row, 0x1A, 0);
        W(row, 0x1C, 0);
        short[] rotation = [0, -4096, 0, 4096, 0, 0, 0, 0, 4096];
        for (int index = 0; index < rotation.Length; index++)
        {
            W(row, 0x3C + index * 2, rotation[index]);
        }
        NativeBattleEffectMemory memory = spawned.Memory with { Arena = spawned.Memory.Arena.Write(0x2A24, row) };
        NativeBattleThundaga105DebrisUpdate first = NativeBattleThundaga105Debris.Update6D40E0(memory);
        byte[] changed = first.Memory.Arena!.Read(0x2A24, 0x50);
        Assert.Equal(1, first.Survivors);
        Assert.Equal((short)1, S(changed, 2));
        Assert.Equal((short)-2, S(changed, 0xA));
        Assert.Equal((short)0, S(changed, 0x18));
        Assert.Equal((short)100, S(changed, 0x1A));
        Assert.Equal(S(row, 0x1E), S(changed, 0x1E));
        string json = JsonSerializer.Serialize(first.Memory);
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(json)!;
        NativeBattleThundaga105DebrisUpdate next = NativeBattleThundaga105Debris.Update6D40E0(restored);
        Assert.Equal((short)-100, S(next.Memory.Arena!.Read(0x2A24, 0x50), 0x18));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleThundaga105Debris.Update6D40E0(first.Memory)), JsonSerializer.Serialize(next));
        W(changed, 6, 1100);
        NativeBattleThundaga105DebrisUpdate dead = NativeBattleThundaga105Debris.Update6D40E0(first.Memory with { Arena = first.Memory.Arena.Write(0x2A24, changed) });
        Assert.Equal(0, dead.Survivors);
        Assert.Equal((short)1, S(dead.Memory.Arena!.Read(0x2A24, 0x50), 2));
    }

    [Fact]
    public void DrawKeepsFinalUvUpperWordUntouchedAndRejectsOnlyNonpositiveDepthAfterStores()
    {
        NativeBattleThundaga105DebrisSpawn spawned = NativeBattleThundaga105Debris.Spawn6D5CF0(Memory, new(3), Triangle(), new(0, 0, 0), new(0, 0, 0), new(0, 0, 0), Identity);
        NativeBattleActionMatrix view = Identity with { Z = 4096 };
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleGfGeometryBatch draw = NativeBattleThundaga105Debris.Draw6D41E0(spawned.Memory, view, projection);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(draw.Attempts);
        Assert.Equal(40, draw.AllocationBytes);
        Assert.NotNull(attempt.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[36]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[37]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[38]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[39]);
        Assert.Equal(4, attempt.Primitive!.VertexPayload4.Length);
        NativeBattleGfGeometryBatch rejected = NativeBattleThundaga105Debris.Draw6D41E0(spawned.Memory, Identity with { Z = -4096 }, projection);
        Assert.Equal(0, rejected.AllocationBytes);
        Assert.Null(Assert.Single(rejected.Attempts).Primitive);
        Assert.Equal(attempt.WriteMask.ToArray(), rejected.Attempts[0].WriteMask.ToArray());
    }

    [Fact]
    public void UnknownInactiveRowsDoNotBlockAliveClearAllocationOrActivePasses()
    {
        NativeBattleEffectMemory unknown = Memory with { Arena = Memory.Arena!.MarkUnknown(0x2A24, 310 * 0x50) };
        NativeBattleEffectMemory cleared = NativeBattleThundaga105Debris.ClearAlive(unknown);
        Assert.Equal(new byte[2], cleared.Arena!.Read(0x2A24 + 6, 2));
        Assert.Throws<NotSupportedException>(() => cleared.Arena.Read(0x2A24 + 8, 1));
        NativeBattleThundaga105DebrisSpawn spawned = NativeBattleThundaga105Debris.Spawn6D5CF0(cleared, new(5), Triangle(), new(0, 0, 0), new(0, 0, 0), new(0, 0, 0), Identity);
        Assert.Equal<int?>(0, spawned.Row);
        Assert.Equal(0x50, spawned.Memory.Arena!.Read(0x2A24, 0x50).Length);
        Assert.Throws<NotSupportedException>(() => spawned.Memory.Arena.Read(0x2A24 + 0x50 + 8, 1));
        NativeBattleThundaga105DebrisUpdate update = NativeBattleThundaga105Debris.Update6D40E0(spawned.Memory);
        Assert.Equal(1, update.Survivors);
        NativeBattleActionMatrix view = Identity with { Z = 4096 };
        Assert.Single(NativeBattleThundaga105Debris.Draw6D41E0(update.Memory, view, new(view, 216, 160, 108)).Attempts);
        Assert.Equal(spawned.Memory.Arena.UnknownRegions.ToArray(), update.Memory.Arena!.UnknownRegions.ToArray());
    }

    [Fact]
    public void ActiveUpdatePreservesUnknownUvWhileDrawDoesNotReadVelocityOrRotation()
    {
        NativeBattleThundaga105DebrisSpawn spawned = NativeBattleThundaga105Debris.Spawn6D5CF0(Memory, new(11), Triangle(), new(0, 0, 0), new(0, 0, 0), new(0, 0, 0), Identity);
        NativeBattleEffectMemory unknownUv = spawned.Memory with { Arena = spawned.Memory.Arena!.MarkUnknown(0x2A24 + 0x1E, 2) };
        NativeBattleThundaga105DebrisUpdate update = NativeBattleThundaga105Debris.Update6D40E0(unknownUv);
        Assert.Equal(1, update.Survivors);
        Assert.Throws<NotSupportedException>(() => update.Memory.Arena!.Read(0x2A24 + 0x1E, 2));
        NativeBattleActionMatrix view = Identity with { Z = 4096 };
        NativeBattleEffectMemory unknownVelocity = spawned.Memory with { Arena = spawned.Memory.Arena.MarkUnknown(0x2A24 + 8, 6).MarkUnknown(0x2A24 + 0x3C, 20) };
        string before = JsonSerializer.Serialize(unknownVelocity);
        Assert.Single(NativeBattleThundaga105Debris.Draw6D41E0(unknownVelocity, view, new(view, 216, 160, 108)).Attempts);
        Assert.Equal(before, JsonSerializer.Serialize(unknownVelocity));
        Assert.Throws<NotSupportedException>(() => NativeBattleThundaga105Debris.Draw6D41E0(update.Memory, view, new(view, 216, 160, 108)));
        byte[] scale = new byte[2];
        W(scale, 0, 1100);
        byte[] decay = new byte[2];
        W(decay, 0, -100);
        NativeBattleEffectMemory dying = spawned.Memory with { Arena = spawned.Memory.Arena.Write(0x2A24 + 6, scale).Write(0x2A24 + 0x4E, decay).MarkUnknown(0x2A24, 6).MarkUnknown(0x2A24 + 8, 14) };
        Assert.Equal(0, NativeBattleThundaga105Debris.Update6D40E0(dying).Survivors);
        Assert.Throws<NotSupportedException>(() => NativeBattleThundaga105Debris.Update6D40E0(unknownVelocity));
    }

    private static NativeBattleThundaga105Triangle Triangle() => new([new(-4, -4, -4), new(-2, -2, -2), new(-2, -2, -2)], [0x1234, 0x5678, 0x9ABC], [0x24808080, 0x00112233, 0x00445566], 0x13B);
    private static short S(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
    private static void W(byte[] bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
}
