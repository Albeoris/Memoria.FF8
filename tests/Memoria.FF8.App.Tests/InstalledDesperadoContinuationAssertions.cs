using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class InstalledDesperadoContinuationAssertions
{
    internal static BattleSession RestorePrivateJsonContinuation(BattleSession battle, string actorId)
    {
        BattleSnapshot captured = battle.CaptureForContinuation();
        string json = JsonSerializer.Serialize(captured);
        BattleSnapshot document = JsonSerializer.Deserialize<BattleSnapshot>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(document));
        BattleSnapshot continuation = document with { QueuedActions = captured.QueuedActions, ActiveAction = captured.ActiveAction, PresentationContinuation = captured.PresentationContinuation, LogicalContinuation = captured.LogicalContinuation };
        BattleSession restored = BattleSession.Restore(continuation);
        restored.AttachRestoredPresentation(captured.PresentationContinuation!.Resources, continuation);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(battle.Presentation!.CurrentCrtRandom, restored.Presentation!.CurrentCrtRandom);
        Assert.Equal(battle.Presentation.GlobalRandomChange, restored.Presentation.GlobalRandomChange);
        Assert.Equal(Model(battle, actorId).SequenceState!.DecodedPose!.HandAContact, Model(restored, actorId).SequenceState!.DecodedPose!.HandAContact);
        Assert.NotNull(Model(restored, actorId).SequenceState!.DecodedPose!.HandAContact);
        return restored;
    }

    internal static void AssertNextCallback(BattleSession original, BattleSession restored, BattleModel body, string actorId)
    {
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(original.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(original.Presentation!.Graphics.Callback, restored.Presentation!.Graphics.Callback);
        Assert.Equal(original.Presentation.CurrentCrtRandom, restored.Presentation.CurrentCrtRandom);
        Assert.Equal(original.Presentation.GlobalRandomChange, restored.Presentation.GlobalRandomChange);
        Assert.Equal(JsonSerializer.Serialize(Model(original, actorId).SequenceState), JsonSerializer.Serialize(Model(restored, actorId).SequenceState));
        Assert.Equal(Relative(original, body, actorId), Relative(restored, body, actorId));
    }

    private static NativeBattleGraphicsModel Model(BattleSession battle, string actorId) => battle.Presentation!.Graphics.ActorModels.Get(battle.ReadPresentationModelOwner(actorId));

    private static Vector3 Relative(BattleSession battle, BattleModel body, string actorId)
    {
        BattlePresentationProcess process = battle.Presentation!;
        NativeBattleEffectArena arena = process.Memory.Arena!;
        NativeBattleScriptedEffectMemory core = process.Memory.ScriptedEffects!;
        uint manager = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(0x27973EC, 4));
        uint bank = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(manager + 0x90, 4));
        BattleModelOwner owner = battle.ReadPresentationModelOwner(actorId);
        int? slot = null;
        uint geometry = 0;
        for (int order = 0; order < 128; order++)
        {
            byte index = core.Read(0x2797554u + (uint)order, 1)[0];
            if (index == byte.MaxValue)
            {
                break;
            }
            uint node = bank + (uint)(index * 0x100);
            if (arena.ReadByte(Offset(node + 0x1C)) != 3 || !arena.TryReadExecutionReference<BattleExecutionReference.Model>(Offset(node + 0x1B), 1, out var actor) || actor.Owner != owner)
            {
                continue;
            }
            uint model = arena.ReadUInt32(Offset(node + 0xBC));
            if (arena.ReadUInt16(Offset(model + 8)) != 13)
            {
                continue;
            }
            Assert.Null(slot);
            slot = index;
            uint table = arena.ReadUInt32(Offset(model + 0x14));
            geometry = arena.ReadUInt32(Offset(table + 4));
        }
        Assert.NotNull(slot);
        NativeBattlePilotModelPose? rope = null;
        NativeBattlePilotModelPose? human = null;
        foreach (ref readonly NativeBattleLogicalResolvedCommand command in battle.PreparedPresentation!.Primitives.LastCommands)
        {
            NativeBattlePilotGeometryReference pilot = command.Command.Pilot;
            if (pilot.Identity is { Effect: 261 } identity && identity.Bank == bank && identity.Slot == slot && pilot.ModelPose is { } prop)
            {
                rope = prop;
            }
            if (pilot.Identity.Bank == 0x502D40 && pilot.Identity.ModelOwner == owner && pilot.ModelPose is { } actor)
            {
                human = actor;
            }
        }
        Assert.NotNull(rope);
        Assert.NotNull(human);
        NativeBattleRawMesh mesh = NativeBattleRawGeometryReader.Read((offset, length) => arena.Read(Offset(geometry + (uint)offset), length), 17)[0];
        int[] ring = mesh.Vertices.Select((vertex, index) => (vertex, index)).Where(item => item.vertex.Bone == 1 && item.vertex.Position.Z == 0).DistinctBy(item => item.vertex.Position).Select(item => item.index).ToArray();
        Assert.Equal(4, ring.Length);
        ImmutableArray<Vector3> ropePoints = rope.Positions(0);
        Assert.Equal(mesh.Vertices.Length, ropePoints.Length);
        Vector3 terminal = ring.Aggregate(Vector3.Zero, (sum, index) => sum + ropePoints[index]) / ring.Length;
        int hand = BattleHumanoidRigProfiles.SemanticPairs(0, body, 0, body).Single(pair => pair.Joint == "hand-a").Source;
        ImmutableArray<Vector3> actorPoints = human.Positions(0);
        Assert.Equal(body.Meshes[0].Vertices.Count, actorPoints.Length);
        Vector3[] handPoints = body.Meshes[0].Vertices.Select((vertex, index) => (vertex, index)).Where(item => item.vertex.Bone == hand).Select(item => actorPoints[item.index]).ToArray();
        return terminal - handPoints.Aggregate(Vector3.Zero, (sum, point) => sum + point) / handPoints.Length;

        int Offset(uint address) => checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress));
    }
}
