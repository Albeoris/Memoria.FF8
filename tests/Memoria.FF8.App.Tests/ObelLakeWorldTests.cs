using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ObelLakeWorldTests(ITestOutputHelper output)
{
    [Fact]
    public void Saved_foot_position_can_walk_to_the_lake_and_open_the_choice()
    {
        string directory = TestResources.GameDirectory;
        string path = TestResources.FixturePath("App.Tests.ObelLakeWorldTests.Approach");
        var session = new GameSession(GameSession.Load(path));
        using var world = new GameWorld(session, directory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        for (int frame = 0; frame < 240; frame++)
        {
            WorldState state = session.State.World!;
            if (Math.Abs(state.X + 24270) + Math.Abs(state.Z + 12940) < 100)
            {
                break;
            }
            Assert.True(world.Apply(WorldNavigationGuide.Next(state, -24270, -12940)).Accepted);
        }
        WorldState arrived = session.State.World!;
        Assert.True(Math.Abs(arrived.X + 24270) + Math.Abs(arrived.Z + 12940) < 100, $"Walk ended at ({arrived.X}, {arrived.Z}).");
        Assert.True(world.Apply(new(PlayerActionKind.Confirm)).Accepted);
        Assert.Equal(58, world.CurrentDialogue!.MessageId);
    }

    [Fact]
    public void Saved_world_completes_the_Obel_Lake_quest()
    {
        string directory = TestResources.GameDirectory;
        string path = TestResources.FixturePath("App.Tests.ObelLakeWorldTests.Start");
        using var assets = new GameAssets(directory);
        using var terrain = new WorldAssets(directory);
        var session = new GameSession(PlayerStateLoader.Load(path, directory, sharedAssets: assets));
        GameWorld world = new(session, directory, sharedAssets: assets, validateWorldPresentation: false);
        try
        {
            world.EnsureField();
            Apply(new(PlayerActionKind.Wait, Frames: 24));
            output.WriteLine($"Loaded field {session.State.FieldId}, ({State().X}, {State().Z}), world frame {State().Frame}.");
            int scrollBefore = Stock(48);
            int starsBefore = Stock(99);

            RestoreAt(-24270, -12940, -890);
            Press();
            Assert.Equal(58, Drain());
            using (var save = new MemoryStream())
            {
                GameplaySaveCodec.Write(save, world.CaptureGameplaySave());
                save.Position = 0;
                Reload(GameplaySaveCodec.Read(save));
            }
            Assert.Equal(58, Drain());
            Choose(1);
            Drain();
            for (int attempt = 0; !Flag(21) && attempt < 128; attempt++)
            {
                Press();
                Assert.Equal(59, Drain());
                Choose(1);
                Drain();
            }
            Assert.True(Flag(21), "The lake never produced the many-skips message.");

            Press();
            Assert.Equal(59, Drain());
            Choose(2);
            Assert.Equal(60, Drain());
            Choose(2);
            Assert.Null(Drain());
            Press();
            Assert.Equal(75, Drain());
            Choose(1);
            Assert.Null(Drain());
            Assert.Equal((byte)2, session.State.SavedWorld!.WorldEventVariable124);

            RestoreAt(-25862, -45722, -992);
            MeetMonkey();
            Choose(2);
            Assert.Null(Drain());
            Assert.True(Flag(22));

            RestoreAt(-24270, -12940, -890);
            Press();
            Assert.Equal(59, Drain());
            Choose(2);
            Assert.Null(Drain());
            Assert.False(Flag(22));
            for (int attempt = 0; !(Flag(30) && Flag(46)) && attempt < 128; attempt++)
            {
                Press();
                if (Drain() == 59)
                {
                    Choose(2);
                    Assert.Null(Drain());
                }
            }
            Assert.True(Flag(30), "The shadow did not give the Eldbeak clue.");
            Assert.True(Flag(46), "The shadow did not give the Balamb clue.");

            RestoreAt(-25862, -45722, -992);
            MeetMonkey();
            Choose(1);
            Assert.Null(Drain());
            Assert.True(Flag(23));
            MeetMonkey();
            Choose(1);
            Assert.Null(Drain());
            Assert.True(Flag(42));

            RestoreAt(19608, -22962, -59);
            for (int attempt = 0; !Flag(41) && attempt < 256; attempt++)
            {
                Apply(WorldNavigationGuide.Next(State(), 19608, -22962));
                Press();
                Assert.Null(Drain());
            }
            Assert.True(Flag(41), "Balamb did not yield its stone.");

            RestoreAt(-4192, -4192);
            Press();
            Assert.Null(Drain());
            Assert.True(Flag(43));

            RestoreAt(-40203, -17518, -1900);
            Press();
            Assert.Equal(105, Drain());
            Choose(1);
            Apply(new(PlayerActionKind.Wait));
            Assert.Equal((ushort)532, session.State.RandomEncounters?.Pending?.Formation);
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 0));
            for (int turn = 0; world.Battle is { } battle && turn < 12000; turn++)
            {
                world.ApplyBattleInput(AutoPlayController.PlanBattleInput(battle));
            }
            Assert.Null(world.Battle);
            Assert.Equal((byte)4, session.State.LastNativeBattle?.Result);
            Apply(new(PlayerActionKind.Wait, Frames: 25));
            Assert.Null(Drain());
            Assert.True(Flag(44));
            Assert.True(Flag(45));

            RestoreAt(800, -46952);
            Press();
            Assert.Null(Drain());
            Assert.True(Flag(31));

            RestoreAt(46960, 38568);
            Press();
            Assert.Null(Drain());
            Assert.True(Flag(32));
            Assert.Equal(scrollBefore + 1, Stock(48));
            Press();
            Assert.Null(Drain());
            Assert.Equal(scrollBefore + 1, Stock(48));

            RestoreAt(69632, -20480);
            for (int attempt = 0; !Flag(33) && attempt < 128; attempt++)
            {
                Press();
                Assert.Null(Drain());
                if (session.State.SavedWorld!.WorldEventVariable125 == 122)
                {
                    Press();
                    Assert.Null(Drain());
                }
                else
                {
                    Apply(WorldNavigationGuide.Next(State(), 69632, -20480));
                }
            }
            Assert.True(Flag(33), "The red rock never found the Mordred reward.");
            Assert.Equal(starsBefore + 1, Stock(99));
            Press();
            Assert.Null(Drain());
            Assert.Equal(starsBefore + 1, Stock(99));
            Assert.True(OriginalAchievementState.Observe(session.State).ObelLake);

            Apply(new(PlayerActionKind.Wait, Frames: 4));
            Assert.Empty(State().EventUi!.ClosingChannels);
            string checkpoint = Environment.GetEnvironmentVariable("MEMORIA_FF8_OBEL_LAKE_OUTPUT") ?? TestResources.ArtifactFile("obel-lake-complete.ff8save");
            session.Save(checkpoint, compact: true);
            var resumed = new GameSession(GameSession.Load(checkpoint));
            using var restored = new GameWorld(resumed, directory, sharedAssets: assets, validateWorldPresentation: false);
            restored.EnsureField();
            Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(OriginalAchievementState.Observe(resumed.State).ObelLake);
            Assert.Equal((byte)4, resumed.State.LastNativeBattle?.Result);
            Assert.Equal(scrollBefore + 1, resumed.State.Economy!.Items.Single(item => item.ItemId == 48).Count);
            Assert.Equal(starsBefore + 1, resumed.State.Economy!.Items.Single(item => item.ItemId == 99).Count);
        }
        finally
        {
            world.Dispose();
        }

        WorldState State() => session.State.World!;
        bool Flag(int bit) => (session.State.SavedWorld!.WorldEventFlags116!.Value & (1UL << bit)) != 0;
        int Stock(int item) => session.State.Economy!.Items.Where(entry => entry.ItemId == item).Sum(entry => entry.Count);

        void Apply(PlayerAction action)
        {
            Assert.True(world.Apply(action).Accepted, $"Rejected {action.Kind} at frame {State().Frame} and event channel {session.State.SavedWorld!.WorldEventVariable125}.");
        }

        void Press()
        {
            Apply(new(PlayerActionKind.Wait));
            Apply(new(PlayerActionKind.Confirm));
        }

        void Choose(int line)
        {
            Apply(new(PlayerActionKind.Wait));
            Apply(new(PlayerActionKind.Choose, line));
        }

        int? Drain()
        {
            for (int tick = 0; tick < 2400; tick++)
            {
                NativeWorldEventUi ui = State().EventUi!;
                if (ui.CanAcceptChoice)
                {
                    return world.CurrentDialogue!.MessageId;
                }
                if (ui.ScriptMode == 0 && ui.Windows.IsEmpty)
                {
                    return null;
                }
                var text = (ui.FocusedPageMessage ?? ui.Windows.LastOrDefault())?.Text;
                bool press = text is { TextComplete: true } or { Phase: 9 or 10 };
                if (press && !ui.HasPendingChoice && (session.State.WorldProcess?.InputHistory?.CurrentButtons & 0x40) == 0)
                {
                    Apply(new(PlayerActionKind.Confirm));
                }
                else
                {
                    Apply(new(PlayerActionKind.Wait));
                }
            }
            throw new InvalidOperationException($"Dialogue did not settle at frame {State().Frame}, channel {session.State.SavedWorld!.WorldEventVariable125}, message {world.CurrentDialogue?.MessageId}.");
        }

        void MeetMonkey()
        {
            for (int frame = 0; frame < 900 && !State().EventUi!.HasPendingChoice; frame++)
            {
                Apply(WorldNavigationGuide.Next(State(), -25862, -45722));
            }
            Assert.Equal(91, Drain());
        }

        void RestoreAt(int x, int z, int? height = null)
        {
            GameplaySaveComponentDocument before = world.CaptureGameplaySave();
            if (height is null)
            {
                WorldGroundSelection? ground = terrain.LookupNativeGround(x, z, WorldTerrainSelection.Replacements(State().ReplacementMask), []);
                height = checked((int)ground!.Ground.Height);
            }
            GameplaySaveWorldPosition position = new(x, z, checked((short)height.Value), 0, 0) { MapDisplayMode = 0, AnimationVariant = 0 };
            // Keep the captured progression and change only the current world position between quest sites.
            Reload(before with { Resume = before.Resume with { World = position } });
            Apply(new(PlayerActionKind.Wait, Frames: 24));
            output.WriteLine($"Entered ({State().X}, {State().Z}) ground {State().GroundType}, flags {session.State.SavedWorld!.WorldEventFlags116!.Value:X}.");
        }

        void Reload(GameplaySaveComponentDocument document)
        {
            world.Dispose();
            session = new(GameplaySaveSessionLoader.Restore(document, directory, sharedAssets: assets));
            world = new(session, directory, sharedAssets: assets, validateWorldPresentation: false);
            world.EnsureField();
        }
    }
}
