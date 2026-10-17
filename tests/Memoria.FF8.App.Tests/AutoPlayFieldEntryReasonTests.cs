using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayFieldEntryReasonTests
{
    [Fact]
    public void OrdinaryFieldEntryRunsTheOriginalUpperArrivalLadderAndReturnsControlOnTheGround()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayFieldEntryReasonTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(734, run.Session.State.FieldId);
        Assert.True(run.Session.State.CharacterVitals!.DreamEnabled);
        Assert.Equal(new[] { 8, 9, 10 }, run.World.Events.Scene.Party);
        string originalPolicy = JsonSerializer.Serialize(run.Controller.State.Coverage.Visits.SelectMany(visit => visit.Events).SelectMany(item => item.ParticipantPolicyDeferrals), AutoPlayController.JsonOptions);

        for (int tick = 0; tick < 512 && !run.AtGroundControl && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
        }

        Assert.Null(run.Controller.Error);
        Assert.True(run.Controller.Running);
        Assert.True(run.AtGroundControl);
        Assert.Contains(run.Commands, command => command is { FieldId: 733, ObjectId: 10, ScriptId: 59, Operation: 536, Name: "PREQ" });
        Assert.Contains(run.Commands, command => command is { FieldId: 733, ObjectId: 11, ScriptId: 67, Operation: 617, Name: "LADDERDOWN2" });
        Assert.True(run.LadderCompleted);
        Assert.Equal((byte)1, run.World.Events.Scene.FieldLoadReasonByte87);
        Assert.Equal((ushort)734, run.World.Events.Scene.PreviousFieldWord84);
        ActorState player = Assert.IsType<ActorState>(run.World.Events.PlayerActor);
        Assert.Equal(11, player.ObjectId);
        Assert.Equal(8, player.CharacterId);
        Assert.Equal(58, player.Z);
        Assert.Equal(8, player.WalkmeshTriangle);
        Assert.False(player.NativeLadder?.Active == true);
        Assert.False(player.NativeLadderMode3?.Active == true);
        Assert.True(run.World.Events.CanAcceptMovementInput);
        Assert.Null(run.World.Events.LastFault);
        Assert.True(run.Session.State.CharacterVitals!.DreamEnabled);
        Assert.Equal(new[] { 8, 9, 10 }, run.World.Events.Scene.Party);
        Assert.All(run.PartySamples, party => Assert.Equal(new[] { 8, 9, 10 }, party));
        Assert.DoesNotContain(run.Actions, action => action.Action.Kind == PlayerActionKind.Move);
        Assert.All(run.EntrySamples, sample =>
        {
            Assert.Equal((byte)1, sample.Reason);
            Assert.Equal((ushort)734, sample.PreviousField);
        });
        Assert.Equal(originalPolicy, JsonSerializer.Serialize(run.Controller.State.Coverage.Visits.SelectMany(visit => visit.Events).SelectMany(item => item.ParticipantPolicyDeferrals), AutoPlayController.JsonOptions));
        Assert.False(run.Controller.State.ReachedEnding);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    private sealed class InstalledRun : IDisposable
    {
        private readonly string gameDirectory = TestResources.GameDirectory;
        private readonly List<GameWorld> observedWorlds = [];
        private GameWorld? world;

        public InstalledRun()
        {
            Controller = new(Session, () => World, Restore, Output);
        }

        public GameSession Session { get; } = GameSession.CreateColdProcess();
        public string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-autoplay-field-entry-reason-" + Guid.NewGuid().ToString("N"));
        public AutoPlayController Controller { get; }
        public List<ScriptCommand> Commands { get; } = [];
        public List<int[]> PartySamples { get; } = [];
        public List<(byte? Reason, ushort? PreviousField)> EntrySamples { get; } = [];
        public IEnumerable<PlayerActionObservation> Actions => observedWorlds.SelectMany(item => item.Events.ActionLog);
        public bool LadderCompleted { get; private set; }
        public bool AtGroundControl => world is not null && Session.State.FieldId == 733 && LadderCompleted && world.Events.IsUserControlEnabled && world.Events.PlayerActor is { Z: 58, WalkmeshTriangle: 8 } player && player.NativeLadder?.Active != true && player.NativeLadderMode3?.Active != true;

        public GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, gameDirectory, validateWorldPresentation: false);
                    observedWorlds.Add(world);
                    GameWorld observed = world;
                    observed.Events.CommandExecuted += command =>
                    {
                        Commands.Add(command);
                        PartySamples.Add(observed.Events.Scene.Party.ToArray());
                        if (command.FieldId == 733)
                        {
                            EntrySamples.Add((observed.Events.Scene.FieldLoadReasonByte87, observed.Events.Scene.PreviousFieldWord84));
                        }
                        if (command is { FieldId: 733, ObjectId: 11, ScriptId: 67, Operation: 617, Name: "LADDERDOWN2" } && observed.Events.Actors[11] is { NativeMovementCompletion542: 2, NativeLadder.Active: false })
                        {
                            LadderCompleted = true;
                        }
                    };
                }
                return world;
            }
        }

        private void Restore(GameState state)
        {
            world?.Dispose();
            world = null;
            Session.Restore(state);
        }

        public void Dispose()
        {
            world?.Dispose();
            if (Directory.Exists(Output))
            {
                Directory.Delete(Output, true);
            }
        }
    }
}
