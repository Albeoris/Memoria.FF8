using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325PointTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void Signed_bone_selector_composes_model_before_transforming_original_vector(short selector)
    {
        NativeBattleModulePoolTask task = Task().WriteDword(0x160, 10).WriteDword(0x164, 20).WriteDword(0x168, 30);
        short[] model = [0, -4096, 0, 4096, 0, 0, 0, 0, 4096];
        for (int index = 0; index < model.Length; index++)
        {
            task = task.WriteWord(0x14C + index * 2, unchecked((ushort)model[index]));
        }
        NativeBattleEffectArena arena = NativeBattleArenaPose.WriteMatrix(NativeBattleEffectArena.Clear(), 0x1000 + 32 + selector * 48, new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 100, 200, 300, 4096), true);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = arena };
        var preserved = NativeBattleStorageSnapshots.Capture(memory);
        string original = NativeBattleTestSnapshots.Json(preserved);
        byte[] row = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(row, 1);
        BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(2), 2);
        BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(4), 3);
        BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(6), selector);
        Assert.Equal(new NativeBattleVector(-192, 121, 333), NativeBattleDiablos325Point.Sample6F0CC0(memory, task, row));
        Assert.Equal(original, NativeBattleTestSnapshots.Json(preserved));
    }

    [Theory]
    [InlineData(1u, 1, 0x4B558u, 0xC823F683u)]
    [InlineData(10000u, 10, 0x4B558u, 0x31C76E0Au)]
    [InlineData(2u, 1, 0x4BBC4u, 0x72205E8Cu)]
    [InlineData(3u, 1, 0x4AFACu, 0x1C1CC695u)]
    public void Two_CRT_draws_select_all_ten_vector_rows_and_three_programme_addresses_without_mutating_input(uint seed, short x, uint programme, uint nextSeed)
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        byte[] source = new byte[0x3890];
        for (short index = 0; index < 10; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(0x3824 + index * 8), (short)(index + 1));
        }
        NativeBattleDiablos325Resources resources = new(new(0xFD8584, source.ToImmutableArray()), new(new string('0', 64), [0], [0, 0, 0, 0]));
        NativeBattleEffectArena arena = NativeBattleArenaPose.WriteMatrix(frame.Memory.Arena!, 0x1020, new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), false);
        frame = frame with { Random = new(seed), Memory = frame.Memory with { Arena = arena }, Effect = frame.Effect with { Binding = frame.Effect.Binding with { Diablos325Resources = resources } } };
        var preserved = NativeBattleStorageSnapshots.Capture(frame);
        string original = NativeBattleTestSnapshots.Json(preserved);
        NativeBattleDiablos325PointChoice chosen = NativeBattleDiablos325Point.Choose6557E5(frame, Task());
        Assert.Equal(new NativeBattleVector(x, 0, 0), chosen.Position);
        Assert.Equal(0x20DFAB8u + programme, chosen.Programme);
        Assert.Equal(nextSeed, chosen.Random.Seed);
        Assert.Equal(original, NativeBattleTestSnapshots.Json(preserved));
    }

    private static NativeBattleModulePoolTask Task()
    {
        NativeBattleModulePoolTask task = new(1, 0, null, 0x6545F0, new byte[0x994 - 12].ToImmutableArray());
        task = task.WriteDword(0x244, 0x20DFAB8u + 0x1000);
        foreach (int offset in new[] { 0x14C, 0x154, 0x15C })
        {
            task = task.WriteWord(offset, 4096);
        }
        return task;
    }
}
