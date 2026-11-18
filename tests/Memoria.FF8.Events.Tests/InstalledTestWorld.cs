using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private sealed class InstalledTestWorld : IDisposable
    {
        private readonly string gameDirectory;
        private readonly GameAssets assets;
        public GameSession Session { get; private set; }
        public GameWorld World { get; private set; }
        public EventEngine Engine => World.Events;

        public InstalledTestWorld(string gameDirectory)
        {
            this.gameDirectory = gameDirectory;
            assets = new GameAssets(gameDirectory);
            Session = GameSession.CreateColdProcess();
            Session.StartNewGame();
            World = CreateWorld();
        }

        private GameWorld CreateWorld()
        {
            return new GameWorld(Session, gameDirectory, validateWorldPresentation: false, sharedAssets: assets);
        }

        public void RestoreIndependently()
        {
            System.Text.Json.JsonElement checkpoint = Engine.Capture();
            GameState state = Session.State with { EventState = checkpoint };
            World.Dispose();
            Session = new GameSession(state);
            World = CreateWorld();
            World.EnsureField();
            Require(System.Text.Json.JsonElement.DeepEquals(checkpoint, Engine.Capture()), "Installed host checkpoint changed across independent reconstruction.");
        }

        public void Dispose()
        {
            World.Dispose();
            assets.Dispose();
        }
    }
}
