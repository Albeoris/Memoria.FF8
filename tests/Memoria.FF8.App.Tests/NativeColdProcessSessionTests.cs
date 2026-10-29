using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeColdProcessSessionTests
{
    [Fact]
    public void AutoPlayUsesInjectedColdSessionAndRestoresSavedExecutableBytesVerbatim()
    {
        byte[] bytes = new byte[NativeBattleQuezacotlExecutableMemory.Length];
        bytes[17] = 0xA5;
        NativeBattleRetainedMemory memory = NativeBattleRetainedMemory.FromColdProcess();
        memory = NativeBattleStorageSnapshots.Capture(memory with { EffectMemory = memory.EffectMemory! with { QuezacotlExecutableMemory = new(bytes.ToImmutableArray()) } });
        GameSession session = GameSession.CreateColdProcess();
        int creations = 0;
        GameSession CreateColdProcess()
        {
            creations++;
            return GameSession.CreateColdProcess(memory);
        }
        string directory = TestResources.ArtifactFile("native-cold-autoplay");
        AutoPlayController controller = new(session, () => throw new InvalidOperationException("No world is required to start."), session.Restore, directory, createColdProcess: CreateColdProcess);
        controller.Start();
        Assert.Equal(1, creations);
        Assert.Same(memory, session.State.NativeBattleMemory);
        string snapshot = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories).Single(path => !path.EndsWith(".run.json", StringComparison.OrdinalIgnoreCase));
        GameState saved = GameSession.Load(snapshot);
        Assert.Equal(bytes, saved.NativeBattleMemory!.EffectMemory!.QuezacotlExecutableMemory!.Data.ToArray());

        byte[] changed = (byte[])bytes.Clone();
        changed[17] = 0x5A;
        GameState modified = saved with { NativeBattleMemory = NativeBattleStorageSnapshots.Capture(memory with { EffectMemory = memory.EffectMemory! with { QuezacotlExecutableMemory = new(changed.ToImmutableArray()) } }) };
        session.Restore(modified);
        string roundTrip = Path.Combine(directory, "modified.json");
        session.Save(roundTrip, compact: true);
        session.Restore(GameSession.Load(roundTrip));
        Assert.Equal(1, creations);
        Assert.Equal(changed, session.State.NativeBattleMemory!.EffectMemory!.QuezacotlExecutableMemory!.Data.ToArray());
    }
}
