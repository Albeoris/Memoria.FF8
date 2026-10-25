using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleContactSequenceTests
{
    [Fact]
    public void Missing_contact_reaction_preserves_the_loaded_animation_bank_and_continues_after_file_restore()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("battle-missing-contact-sequence")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        NativeBattleGraphicsModel before = battle.NativeGraphics!.Models[4];
        Assert.Equal(3217, battle.Frame);
        Assert.Equal((byte)50, before.ModelId4);
        Assert.Equal((byte)4, before.Sequence);
        Assert.Equal((ushort)8, before.SequenceState!.Offset46);
        NativeBattleGraphicsResource resource = ContactResource(battle, battle.NativeGraphics);
        Assert.Equal(12, resource.AnimationFrames.Length);
        Assert.False(resource.Contains(9));
        int completed = battle.Events.Count(item => item.Kind == "ActionComplete");

        battle.Tick();
        Assert.Equal(3218, battle.Frame);
        Assert.NotEqual((byte)9, battle.NativeGraphics!.Models[4].Sequence);
        Assert.InRange(battle.NativeGraphics.Models[4].SequenceState!.Pose!.Value, (byte)0, (byte)11);
        for (int callback = 1; callback < 30; callback++)
        {
            battle.Tick();
        }

        string path = TestResources.ArtifactFile("missing-contact-sequence-continued.json");
        world.CaptureBattleState();
        session.SetEventState(world.Events.Capture());
        session.Save(path, compact: true);
        GameSession restoredSession = new(GameSession.Load(path));
        using GameWorld restoredWorld = new(restoredSession, TestResources.GameDirectory);
        restoredWorld.EnsureField();
        BattleSession restored = Assert.IsType<BattleSession>(restoredWorld.Battle);
        AssertSameSnapshot(battle, restored);
        for (int callback = 30; callback < 120; callback++)
        {
            battle.Tick();
            restored.Tick();
            if (callback == 30)
            {
                AssertSameSnapshot(battle, restored);
            }
        }
        Assert.True(battle.Events.Count(item => item.Kind == "ActionComplete") > completed);
        AssertSameSnapshot(battle, restored);
    }

    [Fact]
    public void Non_ring_contact_selection_checks_the_active_resource_and_obeys_status_and_pending_ownership()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("battle-missing-contact-sequence")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        NativeBattleGraphicsState source = battle.NativeGraphics!;
        NativeBattleGraphicsModel original = source.Models[4];
        Assert.Null(original.ReadLinkedModelIndex140());
        Assert.True(ContactResource(battle, source).Contains(4));
        Assert.False(ContactResource(battle, source).Contains(9));
        MethodInfo select = typeof(BattleSession).GetMethod("SelectNativeRingContactSequence", BindingFlags.Instance | BindingFlags.NonPublic)!;
        (byte Requested, ushort Flags, uint Status, byte Sequence, ushort ResultFlags, ushort Offset, byte Pending)[] cases =
        [
            (9, 0x100, 0, 4, 0x140, 8, 0),
            (4, 0x100, 0, 4, 1, 0, 0),
            (4, 0x100, 0x18, 4, 0x100, 8, 0),
            (9, 0, 0, 4, 0, 8, 9),
            (0, 0x40, 0, 4, 0x40, 8, 0)
        ];
        foreach (var test in cases)
        {
            NativeBattleGraphicsModel model = original with { Flags8 = test.Status, ResourceFlags44 = test.Flags, SequenceState = original.SequenceState! with { Pending3 = 0 } };
            NativeBattleGraphicsState graphics = source with { Models = source.Models.SetItem(4, model) };
            NativeBattleGraphicsState result = Assert.IsType<NativeBattleGraphicsState>(select.Invoke(battle, [graphics, 4, test.Requested]));
            NativeBattleGraphicsModel selected = result.Models[4];
            Assert.Equal(test.Sequence, selected.Sequence);
            Assert.Equal(test.ResultFlags, selected.ResourceFlags44);
            Assert.Equal(test.Offset, selected.SequenceState!.Offset46);
            Assert.Equal(test.Pending, selected.SequenceState.Pending3);
            Assert.Equal(model.SequenceState.Default2, selected.SequenceState.Default2);
        }
    }

    private static NativeBattleGraphicsResource ContactResource(BattleSession battle, NativeBattleGraphicsState graphics)
    {
        MethodInfo resource = typeof(BattleSession).GetMethod("NativeActionResource", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return Assert.IsType<NativeBattleGraphicsResource>(resource.Invoke(battle, [graphics, 4]));
    }

    private static void AssertSameSnapshot(BattleSession expected, BattleSession actual)
    {
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected.Capture()), JsonSerializer.SerializeToElement(actual.Capture())));
    }
}

