namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTaskPublicationNestingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retained_overlay_enlists_nested_task_scopes_before_opening_its_publication_mark(bool foreignBelow)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0);
        NativeBattleEffectArena arena = memory.Arena!;
        NativeBattleWorkingMemory storage = arena.Working;
        if (foreignBelow)
        {
            storage.BeginCheckpoint();
        }
        arena.Write(8, [1]);
        using (NativeBattleTaskOperation root = new(memory, []))
        {
            int rootDepth = storage.CheckpointDepth;
            using (NativeBattleTaskOperation parent = new([]))
            {
                using (NativeBattleTaskOperation child = new([]))
                {
                    using NativeBattleEffectArenaWriter writer = new(arena);
                    writer.Write(8, [2]);
                    writer.Freeze();
                    Assert.Equal((byte)2, arena.ReadByte(8));
                    child.Commit();
                }
                Assert.Equal(rootDepth + 1, storage.CheckpointDepth);
                parent.Commit();
            }
            Assert.Equal(rootDepth, storage.CheckpointDepth);
        }
        Assert.Equal((byte)1, arena.ReadByte(8));
        Assert.Equal(foreignBelow ? 1 : 0, storage.CheckpointDepth);
        if (foreignBelow)
        {
            storage.RollbackCheckpoint();
            Assert.Equal((byte)0, arena.ReadByte(8));
        }
        Assert.False(NativeBattleTaskOperation.IsActive);
    }

    [Fact]
    public void Arena_publication_cannot_pop_a_foreign_mark_opened_above_its_own_mark()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleWorkingMemory storage = arena.Working;
        NativeBattleArenaOverlay overlay = arena.RentOverlay();
        try
        {
            overlay.BeginPublication();
            int publication = storage.CheckpointDepth;
            storage.BeginNestedCheckpoint();
            Assert.Throws<InvalidOperationException>(() => overlay.CommitPublication());
            Assert.Equal(publication + 1, storage.CheckpointDepth);
            Assert.Throws<InvalidOperationException>(() => overlay.RollbackPublication());
            Assert.Equal(publication + 1, storage.CheckpointDepth);
            storage.RollbackNestedCheckpoint();
            overlay.RollbackPublication();
            Assert.Equal(0, storage.CheckpointDepth);
        }
        finally
        {
            arena.ReturnOverlay(overlay);
        }
    }
}
