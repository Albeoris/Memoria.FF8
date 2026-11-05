using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class PlayerControlledFieldMovementSpeedTests
{
    [Fact]
    public void ScriptedPlayerAndNpcMovementKeepTheirTrajectoryAndCompletionWhenInputSpeedChanges()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field resources are required.");
        string fieldName;
        int triangle;
        using (GameAssets original = new(directory))
        {
            FieldAssets field = original.LoadField(165);
            fieldName = field.Name;
            triangle = FieldPartyTraceGeometry.Resolve(field.Walkmesh, -700, -8593, 567)!.Value;
        }
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, Program(triangle)));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, false);
        List<MovementSample>? expected = null;
        foreach (int percent in new[] { 100, 50, 300 })
        {
            GameSession session = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
            using GameWorld world = new(session, directory, sharedAssets: assets);
            world.EnsureField();
            world.ConfigureMovementSpeeds(percent, 100);
            List<MovementSample> samples = [];
            bool rejectedInput = false;
            bool completed = false;
            for (int tick = 0; tick < 90; tick++)
            {
                if (!world.Events.IsUserControlEnabled)
                {
                    long frame = world.Events.Frame;
                    ActorState player = world.Events.Actors[0];
                    var before = (player.X, player.Y, player.Z, player.NativeScriptMovement);
                    Assert.False(world.Apply(new(PlayerActionKind.Move, Horizontal: -1)).Accepted);
                    Assert.Equal(frame, world.Events.Frame);
                    Assert.Equal(before, (player.X, player.Y, player.Z, player.NativeScriptMovement));
                    rejectedInput = true;
                }
                Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
                Assert.Null(world.Events.LastFault);
                foreach (ActorState actor in world.Events.Actors.Take(2))
                {
                    samples.Add(new(world.Events.Frame, actor.ObjectId, actor.X, actor.Y, actor.Z, actor.WalkmeshTriangle, actor.Direction, actor.MovementSpeed, actor.NativeMovementCompletion542, actor.NativeScriptMovement, world.Events.IsUserControlEnabled));
                }
                if (rejectedInput && world.Events.IsUserControlEnabled && world.Events.Actors.Take(2).All(actor => actor.NativeMovementCompletion542 == 2))
                {
                    completed = true;
                    break;
                }
            }
            Assert.True(rejectedInput);
            Assert.True(completed, "Both script-driven bodies must complete and UCON must restore player control.");
            Assert.Contains(samples, sample => sample.ObjectId == 0 && sample.Completion == 1 && sample.Movement is { Active: true });
            Assert.Contains(samples, sample => sample.ObjectId == 1 && sample.Completion == 1 && sample.Movement is { Active: true });
            Assert.True(samples.Where(sample => sample.ObjectId == 0).Select(sample => sample.X).Distinct().Count() > 2);
            Assert.True(samples.Where(sample => sample.ObjectId == 1).Select(sample => sample.X).Distinct().Count() > 2);
            if (expected is null)
            {
                expected = samples;
            }
            else
            {
                Assert.Equal(expected, samples);
            }
            ActorState restoredPlayer = world.Events.Actors[0];
            var scriptedEnd = (restoredPlayer.X, restoredPlayer.Y);
            Assert.NotNull(world.Events.FieldFade);
            Assert.NotEqual(4, world.Events.FieldFade.Physical.Mode);
            Assert.True(world.Apply(new(PlayerActionKind.Move, Horizontal: 1)).Accepted);
            Assert.Null(world.Events.LastFault);
            Assert.NotEqual(scriptedEnd, (restoredPlayer.X, restoredPlayer.Y));
        }
    }

    private static byte[] Program(int triangle)
    {
        (Jsm.Opcode, int)[] Initialize(int label, bool player)
        {
            List<(Jsm.Opcode, int)> commands = [(Jsm.Opcode.LBL, label), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.TALKOFF, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, -700), (Jsm.Opcode.PSHN_L, -8593), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.SET3, triangle), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0)];
            if (player)
            {
                commands.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0)]);
            }
            commands.Add((Jsm.Opcode.RET, 8));
            return commands.ToArray();
        }
        (Jsm.Opcode, int)[] Move(int label, bool player)
        {
            List<(Jsm.Opcode, int)> commands = [(Jsm.Opcode.LBL, label)];
            if (player)
            {
                commands.Add((Jsm.Opcode.UCOFF, 0));
            }
            commands.AddRange([(Jsm.Opcode.PSHN_L, -650), (Jsm.Opcode.PSHN_L, -8593), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0)]);
            if (player)
            {
                commands.Add((Jsm.Opcode.FADENONE, 0));
                commands.Add((Jsm.Opcode.UCON, 0));
            }
            commands.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            return commands.ToArray();
        }
        return GameplaySaveFieldScriptsTests.Encode([1, (16 << 7) | 1], [Initialize(0, true), Move(1, true), Initialize(16, false), Move(17, false)]);
    }

    private sealed record MovementSample(long Frame, int ObjectId, float X, float Y, float Z, int Triangle, float Direction, int Speed, ushort? Completion, NativeScriptMovementState? Movement, bool UserControl);

    private sealed class ScriptOverride(string fieldName, byte[] script) : IAssetOverrideSource
    {
        public bool TryRead(string logicalPath, out byte[] content)
        {
            if (Path.GetFileName(logicalPath).Equals(fieldName + ".jsm", StringComparison.OrdinalIgnoreCase))
            {
                content = script.ToArray();
                return true;
            }
            content = [];
            return false;
        }
    }
}
