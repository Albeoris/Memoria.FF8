using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleTargetGuardianCastTests
{
    [Fact]
    public void Loaded_save_initializes_absent_process_state_and_completes_Shiva_with_disk_continuations()
    {
        string source = TestResources.FixturePath("App.Tests.GuardianLoadedSave.1");
        byte[] original = File.ReadAllBytes(source);
        GameState state = GameSession.Load(source);
        Assert.NotNull(state.NativeBattleMemory!.EffectMemory!.Shiva);
        Assert.Null(state.Battle);
        GameSession session = new(state);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_TARGET_GUARDIAN_EVIDENCE") ?? TestResources.ArtifactDirectory;
        Directory.CreateDirectory(directory);
        string output = Path.Combine(directory, "loaded-guardian-save.json");
        session.Save(output, compact: true);
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(GameSession.Load(output)));
        string gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using (GameWorld world = new(session, gameDirectory))
        using (GameWorld independent = new(new GameSession(GameSession.Load(output)), gameDirectory))
        {
            world.EnsureField();
            independent.EnsureField();
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.True(independent.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.Null(world.Events.LastFault);
            Assert.Null(independent.Events.LastFault);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), independent.Events.Capture()));
            session.SetEventState(world.Events.Capture());
            session.Save(Path.Combine(directory, "loaded-guardian-next.json"), compact: true);
        }
        Run(1, 185, retainedMemory: state.NativeBattleMemory);
        Assert.Equal(original, File.ReadAllBytes(source));
    }
}
