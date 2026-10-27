namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRetainedSourceSerializationTests
{
    [Fact]
    public void Compact_save_preserves_imported_sources_without_reenabling_cold_publication()
    {
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.FromColdProcess();
        NativeBattleEffectMemory effects = retained.EffectMemory! with { Breath129 = NativeBattleBreath129State.FromUnknown(), Sonic139 = NativeBattleSonic139State.FromUnknown() };
        GameSession session = new(new GameState { NativeBattleMemory = NativeBattleStorageSnapshots.Capture(retained with { EffectMemory = effects }) });
        string path = Path.Combine(Path.GetTempPath(), $"retained-source-{Guid.NewGuid():N}.json");
        try
        {
            session.Save(path, compact: true);
            NativeBattleEffectMemory restored = GameSession.Load(path).NativeBattleMemory!.EffectMemory!;
            Assert.False(restored.Breath129!.PendingColdSources);
            Assert.False(restored.Sonic139!.PendingColdSources);
            Assert.NotNull(restored.Breath129.MutableSources);
            Assert.NotNull(restored.Sonic139.OriginalFrames);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
