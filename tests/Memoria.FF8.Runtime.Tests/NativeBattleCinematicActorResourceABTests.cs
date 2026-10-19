using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorResourceABTests
{
    [Fact]
    public void BindingUsesSelectedPhysicalActorSignedSequenceAndOriginalAlignedAllocation()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        uint manager = WaterSchedulerFixture.ReadCoreDword(frame, 0x27973EC);
        uint owner = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        frame = WaterSchedulerFixture.CoreDword(frame, manager + 0x74, 0x20E4AB8);
        frame = WaterSchedulerFixture.CoreDword(frame, owner + 0x60 + 4 * 5, 0x1D975CC);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = frame with { Arena = frame.Arena.Write(0x201B, [5]).Write(0x1002, [0xFE, 0xFF]) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorResourceAB.Execute(frame, new(0x1000, 4, 0x1000), (current, model, row) =>
        {
            Assert.Equal(0x1D975CCu, model);
            Assert.Equal(0x20E4AB8u, row);
            Assert.Equal(0x20E4AD8u, WaterSchedulerFixture.ReadCoreDword(current, manager + 0x74));
            Assert.Equal(0x1000u, WaterSchedulerFixture.ReadCoreDword(current, 0x2797450));
            return current with { Arena = current.Arena.Write(0x1002, [0xFD, 0xFF]) };
        }, (current, model, sequence) =>
        {
            Assert.Equal(0x1D975CCu, model);
            Assert.Equal((short)-3, sequence);
            return current;
        });
        Assert.Equal(0x1004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BindingWritesPhysicalArenaRowsAndPreservesTheUnwrittenTail(bool cold)
    {
        const uint directory = 0x187C20C;
        const uint row = NativeBattleEffectNodeMemory.ArenaAddress + 0x5000;
        byte[] dat = new byte[32];
        int[] words = [3, 20, 24, 28, 32];
        for (int i = 0; i < words.Length; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(dat.AsSpan(i * 4), words[i]);
        }
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x5020, [7, 8, 9, 10]);
        NativeBattleModelCpuResources cpu = cold ? NativeBattleModelCpuResources.FromColdProcess() : NativeBattleModelCpuResources.Unknown();
        cpu = cpu with { Rows = cpu.Rows.SetItem(0, cpu.Rows[0].Set(12, new(0, 100)).Set(16, new(0, 200))).SetItem(1, cpu.Rows[1].Set(12, new(1, 300)).Set(16, new(1, 400))) };
        NativeBattleGraphicsModel model = new(0, 2) { ModelId4 = 8, Flags2 = 4, ActiveResourceRow100 = 0, OriginalResourceRow104 = 0, LinkedTextureResourceIndex = 1, LinkedTransform = new(new(0, 0, 0), new(0, 0, 0)), SequenceState = new() { Default2 = 6 } };
        var result = NativeBattleVictoryResources.Bind5022C0(dat.ToImmutableArray(), cpu, model, row, directory, address => arena.ReadResourcePointer(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress))), (address, pointer) => arena = arena.WriteResourcePointer(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), pointer));
        Assert.Equal(row + 32, result.NextRow);
        Assert.Equal(row, result.Model.ActiveStagedResourceRow100);
        Assert.Equal(row + 16, result.Model.LinkedStagedResourceRow);
        Assert.Equal(new NativeBattleModelCpuReference(0, 100), arena.ReadResourcePointer(0x5000)!.Cpu);
        Assert.Equal(directory + 20, BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(0x5008, 4)));
        Assert.Equal(directory + 24, arena.ReadResourcePointer(0x500C)!.StagingAddress);
        Assert.Equal(directory + 28, arena.ReadResourcePointer(0x5018)!.StagingAddress);
        if (cold)
        {
            Assert.Equal(0u, arena.ReadResourcePointer(0x501C)!.StagingAddress);
            Assert.Equal(new byte[4], arena.Read(0x501C, 4));
        }
        else
        {
            Assert.Null(arena.ReadResourcePointer(0x501C));
            Assert.Throws<NotSupportedException>(() => arena.Read(0x501C, 4));
        }
        Assert.Equal(new byte[] { 7, 8, 9, 10 }, arena.Read(0x5020, 4));
        Assert.Equal((short?)6, result.Model.SequenceState!.Locals[7]);
        Assert.Equal(0x196B07Cu, BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(0x5000, 4)));
    }

    [Fact]
    public void ArenaPointerAliasSurvivesSnapshotAndPartialOverwriteInvalidatesItsTypedOwner()
    {
        NativeBattleResourcePointer pointer = new(new(2, 40), null);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().WriteResourcePointer(100, pointer);
        arena = JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(arena))!;
        arena.Validate();
        Assert.Equal(pointer, arena.ReadResourcePointer(100));
        arena = arena.Write(101, [7]);
        Assert.Null(arena.ResourcePointers);
        Assert.Null(arena.ReadResourcePointer(100)!.Cpu);
        Assert.Equal(0x01980740u, arena.ReadResourcePointer(100)!.StagingAddress);
    }

    [Fact]
    public void UnobservedPointerStaysUnknownWhileAnExplicitColdNullCopiesAsZero()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().WriteResourcePointer(40, null);
        Assert.Null(arena.ReadResourcePointer(40));
        Assert.Throws<NotSupportedException>(() => arena.Read(40, 4));
        arena = arena.Write(40, [1]);
        Assert.Throws<NotSupportedException>(() => arena.ReadResourcePointer(40));
        Assert.False(NativeBattleModelCpuResources.Unknown().Rows[2].Fields.ContainsKey(44));
        Assert.True(NativeBattleModelCpuResources.FromColdProcess().Rows[2].Fields.ContainsKey(44));
        Assert.Null(NativeBattleModelCpuResources.FromColdProcess().Rows[2].Fields[44]);
    }

    [Fact]
    public void TransactionCheckpointsRetainAndRollbackActualResourcePointerOwnership()
    {
        NativeBattleResourcePointer pointer = new(new(1, 80), null);
        using NativeBattleEffectArenaTransaction transaction = new(NativeBattleEffectArena.Clear());
        transaction.BeginCheckpoint();
        transaction.View.WriteResourcePointer(20, pointer);
        transaction.CommitCheckpoint();
        string first = JsonSerializer.Serialize(transaction.Freeze());
        transaction.BeginCheckpoint();
        transaction.View.Write(21, [5]);
        Assert.Null(transaction.View.ReadResourcePointer(20)!.Cpu);
        transaction.RollbackCheckpoint();
        Assert.Equal(pointer, transaction.View.ReadResourcePointer(20));
        Assert.Equal(first, JsonSerializer.Serialize(transaction.Freeze()));
        transaction.BeginCheckpoint();
        transaction.View.Write(20, [1, 2, 3, 4]);
        transaction.CommitCheckpoint();
        Assert.Null(transaction.Freeze().ResourcePointers);
        Assert.Equal(0x04030201u, transaction.View.ReadResourcePointer(20)!.StagingAddress);
    }
}
