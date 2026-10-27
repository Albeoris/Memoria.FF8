using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleThundaga105DebrisBatchTests
{
    private sealed class Copies : INativeBattleTelemetrySink
    {
        public long Total { get; private set; }
        public long ScalarBackingClones { get; private set; }

        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            if (kind == NativeBattleTrafficKind.CpuCopyPayload)
            {
                Total = checked(Total + payloadBytes);
                if (siteId == 1003)
                {
                    ScalarBackingClones = checked(ScalarBackingClones + payloadBytes);
                }
            }
        }
    }

    [Theory]
    [InlineData(18)]
    [InlineData(202)]
    [InlineData(991)]
    public void Ordered_batch_matches_independent_flat_record_update_and_keeps_saved_owners_isolated(int seed)
    {
        byte[] bytes = new byte[0x40000];
        Random random = new(seed);
        random.NextBytes(bytes);
        for (int row = 0; row < NativeBattleThundaga105Debris.Capacity; row++)
        {
            int offset = NativeBattleThundaga105Debris.ArenaOffset + row * NativeBattleThundaga105Debris.Stride;
            Word(bytes, offset + 6, row < 24 ? new[] { 0, 1024, 1100, 4096, 32760, -30000 }[row % 6] : 0);
            Word(bytes, offset + 0x4E, random.Next(-227, -99));
            NativeBattleActionMatrix spin = NativeBattleGfMatrices.Euler56CD50(new(unchecked((short)random.Next(-256, 256)), unchecked((short)random.Next(-256, 256)), unchecked((short)random.Next(-256, 256))));
            for (int index = 0; index < 9; index++)
            {
                Word(bytes, offset + 0x3C + index * 2, spin.Rotation[index]);
            }
        }
        byte[] expected = bytes.ToArray();
        int survivors = UpdateFlatRecords(expected);
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, bytes).MarkUnknown(NativeBattleThundaga105Debris.ArenaOffset + 0x1E, 2).MarkUnknown(0x30000, 16);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = source };
        NativeBattleEffectMemory preserved = NativeBattleTestSnapshots.Independent(memory);
        string before = NativeBattleTestSnapshots.Json(preserved);
        Copies copies = new();
        NativeBattleThundaga105DebrisUpdate changed;
        using (NativeBattleTelemetry.BeginScope(copies))
        {
            changed = NativeBattleThundaga105Debris.Update6D40E0(memory);
        }
        Assert.Equal(survivors, changed.Survivors);
        var snapshot = changed.Memory.Arena!.ReadSnapshot(0, bytes.Length);
        Assert.Equal(expected, snapshot.Data);
        Assert.Equal(source.ReadSnapshot(0, bytes.Length).Known, snapshot.Known);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(preserved));
        Assert.Equal(0, copies.ScalarBackingClones);
        Assert.InRange(copies.Total, 1, bytes.LongLength * 3);

        string saved = NativeBattleTestSnapshots.Json(changed.Memory);
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(saved)!;
        NativeBattleEffectMemory captured = NativeBattleStorageSnapshots.Capture(changed.Memory);
        _ = NativeBattleThundaga105Debris.Update6D40E0(changed.Memory);
        Assert.Equal(saved, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(expected, restored.Arena!.ReadSnapshot(0, bytes.Length).Data);
        Assert.Equal(snapshot.Known, restored.Arena.ReadSnapshot(0, bytes.Length).Known);

        byte[] cleared = bytes.ToArray();
        for (int row = 0; row < NativeBattleThundaga105Debris.Capacity; row++)
        {
            Word(cleared, NativeBattleThundaga105Debris.ArenaOffset + row * NativeBattleThundaga105Debris.Stride + 6, 0);
        }
        Copies clearCopies = new();
        NativeBattleEffectMemory clear;
        NativeBattleEffectMemory clearSource = NativeBattleTestSnapshots.Independent(preserved);
        using (NativeBattleTelemetry.BeginScope(clearCopies))
        {
            clear = NativeBattleThundaga105Debris.ClearAlive(clearSource);
        }
        Assert.Equal(cleared, clear.Arena!.ReadSnapshot(0, bytes.Length).Data);
        Assert.Equal(0, clearCopies.ScalarBackingClones);
        Assert.InRange(clearCopies.Total, 1, bytes.LongLength * 3);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(preserved));
    }

    private static int UpdateFlatRecords(byte[] bytes)
    {
        int survivors = 0;
        for (int row = 0; row < NativeBattleThundaga105Debris.Capacity; row++)
        {
            int start = NativeBattleThundaga105Debris.ArenaOffset + row * NativeBattleThundaga105Debris.Stride;
            short oldScale = Word(bytes, start + 6);
            if (oldScale == 0)
            {
                continue;
            }
            NativeBattleActionMatrix spin = new(Enumerable.Range(0, 9).Select(index => Word(bytes, start + 0x3C + index * 2)).ToImmutableArray(), 0, 0, 0, 4096);
            for (int corner = 0; corner < 3; corner++)
            {
                int point = start + 0x18 + corner * 8;
                NativeBattleVector rotated = NativeBattleGfMatrices.Transform460840(spin, new(Word(bytes, point), Word(bytes, point + 2), Word(bytes, point + 4)));
                Word(bytes, point, rotated.X);
                Word(bytes, point + 2, rotated.Y);
                Word(bytes, point + 4, rotated.Z);
            }
            short scale = unchecked((short)(oldScale + Word(bytes, start + 0x4E)));
            Word(bytes, start + 6, scale <= 1024 ? 0 : scale);
            if (scale <= 1024)
            {
                continue;
            }
            for (int axis = 0; axis < 3; axis++)
            {
                int component = axis * 2;
                Word(bytes, start + component, Word(bytes, start + component) + (Word(bytes, start + 8 + component) >> 4));
            }
            if (Word(bytes, start + 2) >= 0)
            {
                Word(bytes, start + 10, -Word(bytes, start + 10));
            }
            for (int axis = 0; axis < 3; axis++)
            {
                int component = axis * 2;
                Word(bytes, start + 8 + component, Word(bytes, start + 8 + component) + Word(bytes, start + 0x10 + component));
            }
            survivors++;
        }
        return survivors;
    }

    private static short Word(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
    private static void Word(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), unchecked((short)value));
}
