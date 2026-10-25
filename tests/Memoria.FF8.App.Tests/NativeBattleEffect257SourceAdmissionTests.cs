using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect257SourceAdmissionTests
{
    [Fact]
    public void Selected_source_preserves_current_resource_owners_and_complete_rigs()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT257_SOURCE_SNAPSHOT");
        Assert.SkipUnless(directory is not null && path is not null, "Select original resources and the current Effect257 source snapshot.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        string before = JsonSerializer.Serialize(source);
        ValidateSource(source, game, assets, true);
        Assert.Null(source.NativeGraphics!.OriginalEffect);
        Assert.Null(source.ActiveAction);
        Assert.Empty(source.QueuedActions);
        Assert.Equal(before, JsonSerializer.Serialize(source));
        BattleSnapshot decoded = JsonSerializer.Deserialize<BattleSnapshot>(before) ?? throw new InvalidDataException("The selected source snapshot is null.");
        BattleSnapshot restored = BattleSession.Restore(decoded).Capture();
        Assert.Equal(before, JsonSerializer.Serialize(restored));
        string queuedPath = Path.Combine(Path.GetDirectoryName(path!)!, "queued-action104.battle.json");
        BattleSnapshot queued = NativeBattleEffectFamilyRunner.LoadSnapshot(queuedPath);
        ValidateQueuedAction(source, queued);
        string queuedJson = JsonSerializer.Serialize(queued);
        BattleSnapshot queuedDecoded = JsonSerializer.Deserialize<BattleSnapshot>(queuedJson) ?? throw new InvalidDataException("The selected queued fixture is null.");
        Assert.Equal(queuedJson, JsonSerializer.Serialize(BattleSession.Restore(queuedDecoded).Capture()));
    }

    internal static void ValidateSource(BattleSnapshot source, GameAssets game, BattleAssets assets, bool cold)
    {
        ArgumentNullException.ThrowIfNull(game);
        Assert.Equal(326, source.Definition.EncounterId);
        Assert.Equal(26, source.Definition.StageId);
        BattleEncounter formation = assets.LoadEncounter(326);
        Assert.Equal(26, formation.StageId);
        Assert.Equal((byte)137, formation.Flags);
        Assert.Equal((byte)0, formation.PrimaryCamera);
        Assert.Equal((byte)0, formation.AlternativeCamera);
        Assert.Equal(8, formation.Slots.Count);
        (int Model, int Level, short X, short Z, bool Enabled, bool DisabledFlags)[] expectedSlots =
        [
            (67, 254, 0, -5300, true, false),
            (0, 254, -400, -5700, false, false),
            (0, 254, -800, -5700, false, false),
            (0, 254, -2800, -5700, false, false),
            (0, 254, -2000, -5700, false, false),
            (0, 254, -1200, -5700, false, false),
            (0, 254, -2400, -5700, false, false),
            (0, 254, -1600, -5700, false, false)
        ];
        for (int index = 0; index < expectedSlots.Length; index++)
        {
            BattleEnemyPlacement slot = formation.Slots[index];
            var expected = expectedSlots[index];
            Assert.Equal(index, slot.Slot);
            Assert.Equal(expected.Model, slot.ModelId);
            Assert.Equal(expected.Model + 16, slot.NativeModelId);
            Assert.Equal(expected.Level, slot.Level);
            Assert.Equal(new NativeBattleVector(expected.X, 0, expected.Z), Assert.IsType<NativeBattleVector>(slot.NativePosition));
            Assert.Equal(expected.Enabled, slot.Enabled);
            Assert.Equal(expected.DisabledFlags, slot.Hidden);
            Assert.Equal(expected.DisabledFlags, slot.InitiallySuspended);
            Assert.Equal(expected.DisabledFlags, slot.Untargetable);
        }
        Assert.Equal(0, Assert.Single(formation.Slots, slot => slot.Enabled).Slot);
        BattleSession session = BattleSession.Restore(source);
        Assert.True(session.NativeEntryReady);
        NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(source.NativeGraphics);
        NativeBattleEffectMemory memory = Assert.IsType<NativeBattleEffectMemory>(source.NativeEffectMemory);
        NativeBattleModelCpuResources cpu = Assert.IsType<NativeBattleModelCpuResources>(source.NativeModelCpuResources);
        NativeBattleFileStaging staging = Assert.IsType<NativeBattleFileStaging>(source.NativeFileStaging);
        Assert.Equal(7, graphics.Models.Length);
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.Equal(0, memory.Active);
        Assert.NotNull(memory.ScriptedGraphics);
        Assert.NotNull(memory.ScriptedColorRegisters);
        Assert.NotNull(memory.PrimitiveProcess);
        Assert.NotNull(memory.Textures);
        Assert.NotNull(source.NativeFileIo);
        Assert.Null(source.NativeAiVariables);
        Assert.Null(source.PersistentAiVariables);
        Assert.All(source.Definition.Combatants, actor =>
        {
            Assert.Null(actor.NativeTemplate);
            Assert.Null(actor.NativeAi);
        });
        NativeBattleScriptedEffectMemory core = Assert.IsType<NativeBattleScriptedEffectMemory>(memory.ScriptedEffects);
        NativeBattleGeometryRegisters geometry = Assert.IsType<NativeBattleGeometryRegisters>(memory.GeometryRegisters);
        core.Validate();
        geometry.Validate();
        cpu.Validate();
        staging.Validate();
        foreach (int physical in new[] { 0, 3 })
        {
            string id = Assert.IsType<string>(graphics.ActorIds[physical]);
            BattleCombatantDefinition definition = session.GetCombatantDefinition(id);
            BattleCombatantSnapshot actor = Assert.Single(source.Combatants, candidate => candidate.Id == id);
            Assert.True(actor.NativeParticipant!.Active);
            Assert.Null(actor.NativeParticipant.ResolvedLevel);
            Assert.False(actor.NativeParticipant.Hidden);
            Assert.False(actor.NativeParticipant.Suspended);
            Assert.False(actor.NativeParticipant.Untargetable);
            NativeBattleGraphicsModel model = graphics.Models[physical];
            Assert.NotEqual(0, model.Flags0 & 2);
            Assert.Null(model.ReadLinkedModelIndex140());
            int rowIndex = Assert.IsType<int>(model.ActiveResourceRow100);
            Assert.Equal(physical == 3 ? 0 : 1, rowIndex);
            NativeBattleModelCpuResourceRow row = cpu.Rows[rowIndex];
            NativeBattleModelCpuReference skeleton = row.Require(12);
            Assert.Equal(physical == 3 ? (byte)3 : (byte)0, skeleton.Bank);
            NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
            if (physical == 3)
            {
                Assert.False(definition.IsPlayer);
                Assert.Equal(67, definition.ModelId);
                Assert.Equal((byte)83, model.ModelId4);
                Assert.Equal(new NativeBattleVector(0, 0, -5300), model.Transform!.Initial20);
                Assert.Equal(0, definition.SceneSlot);
                Assert.Null(model.ActiveStagedResourceRow100);
                Assert.Equal(2128, row.Lengths[12]);
                Assert.Equal(43024, row.Lengths[16]);
                Assert.Equal(58456, row.Lengths[20]);
                Assert.Equal((byte)3, row.Require(16).Bank);
                Assert.Equal((byte)3, row.Require(20).Bank);
                Assert.Equal(14360, skeleton.Offset);
                Assert.Equal(16488, row.Require(16).Offset);
                Assert.Equal(59512, row.Require(20).Offset);
                AssertExplicitKnownExtent(staging, row.Require(16), row.Lengths[16]);
                AssertExplicitKnownExtent(staging, row.Require(20), row.Lengths[20]);
                byte[] skeletonFront = NativeBattleModelEnemyHeap.Read(cpu, staging, skeleton, 4);
                byte[] geometryFront = NativeBattleModelEnemyHeap.Read(cpu, staging, row.Require(16), 4);
                byte[] animationFront = NativeBattleModelEnemyHeap.Read(cpu, staging, row.Require(20), 4);
                Assert.Equal((byte)44, skeletonFront[0]);
                Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(geometryFront));
                Assert.Equal(16, BinaryPrimitives.ReadInt32LittleEndian(animationFront));
            }
            else
            {
                Assert.True(definition.IsPlayer);
                Assert.Equal(0, definition.ModelId);
                Assert.Equal((byte)0, model.ModelId4);
            }
            NativeBattlePoseSource pose = Assert.IsType<NativeBattlePoseSource>(resource.PoseSource);
            pose.Validate();
            Assert.Equal(physical == 3 ? 44 : 23, pose.BoneCount);
            if (physical == 3)
            {
                byte[] currentSkeleton = NativeBattleModelEnemyHeap.Read(cpu, staging, skeleton, 2128);
                Assert.Equal(16 + 48 * pose.BoneCount, currentSkeleton.Length);
                Assert.Equal(pose.BoneCount, currentSkeleton[0]);
                for (int bone = 0; bone < pose.BoneCount; bone++)
                {
                    Assert.True(pose.Skeleton.AsSpan().Slice(16 + bone * 48, 4).SequenceEqual(currentSkeleton.AsSpan(16 + bone * 48, 4)));
                }
                pose = pose with { Skeleton = currentSkeleton.ToImmutableArray() };
                pose.Validate();
                NativeBattleDecodedPose decodedPose = Assert.IsType<NativeBattleDecodedPose>(model.SequenceState!.DecodedPose);
                Assert.Equal(44, decodedPose.Bones.Length);
            }
            Assert.Equal(pose.BoneCount, NativeBattleBodyBoneMatrices.Require(model, pose).Length);
            Assert.All(model.CurrentBodyBoneMatrices!.Matrices, matrix => matrix.Validate());
            Assert.NotNull(model.GeometryWorldMatrix);
            model.GeometryWorldMatrix!.Validate();
            if (physical == 0)
            {
                NativeBattlePoseSource linked = Assert.IsType<NativeBattlePoseSource>(resource.LinkedPoseSource);
                linked.Validate();
                Assert.Equal(2, linked.BoneCount);
                Assert.Equal(2, NativeBattleBodyBoneMatrices.Require(model with { CurrentBodyBoneMatrices = model.CurrentLinkedBoneMatrices }, linked).Length);
                Assert.All(model.CurrentLinkedBoneMatrices!.Matrices, matrix => matrix.Validate());
            }
            else
            {
                Assert.Null(model.CurrentLinkedBoneMatrices);
            }
        }
        foreach (int physical in new[] { 1, 2, 4, 5, 6 })
        {
            Assert.Equal(0, graphics.Models[physical].Flags0 & 2);
            Assert.Null(graphics.Models[physical].HasLinkedModelRing140);
        }
        NativeBattleStageState stage = Assert.IsType<NativeBattleStageState>(source.NativeStage);
        Assert.Equal(4, stage.Groups.Length);
        for (int index = 0; index < stage.Groups.Length; index++)
        {
            NativeBattleStageGroup group = stage.Groups[index];
            Assert.Equal(7, group.Flags);
            if (cold)
            {
                Assert.Equal((short)0, group.ColorDepth);
                Assert.Equal(0u, group.ColorFlags);
            }
            Assert.NotNull(group.Source);
            group.Source!.Validate();
            Assert.Equal(index == 2 ? 3 : 2, group.Matrices.Length);
            Assert.All(group.Matrices, matrix => matrix.Validate());
            if (cold && index == 2)
            {
                Assert.Equal((byte)74, group.Counter);
                Assert.Equal((byte)120, group.FrameCount);
            }
        }
    }

    private static void AssertExplicitKnownExtent(NativeBattleFileStaging staging, NativeBattleModelCpuReference reference, int length)
    {
        ulong cursor = reference.SharedAddress;
        ulong end = cursor + checked((uint)length);
        foreach (NativeBattleEffectMemoryRegion range in staging.KnownRanges.OrderBy(range => range.Offset))
        {
            ulong first = staging.BaseAddress + checked((uint)range.Offset);
            ulong last = first + checked((uint)range.Data.Length);
            if (last <= cursor)
            {
                continue;
            }
            Assert.True(first <= cursor, "The selected current section extent contains unknown storage.");
            cursor = Math.Min(end, last);
            if (cursor == end)
            {
                return;
            }
        }
        Assert.Fail("The selected current section extent is not explicitly known.");
    }

    internal static void ValidateQueuedAction(BattleSnapshot source, BattleSnapshot queued)
    {
        Assert.Equal(source.Frame, queued.Frame);
        NativeBattleGraphicsState initialGraphics = Assert.IsType<NativeBattleGraphicsState>(source.NativeGraphics);
        NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(queued.NativeGraphics);
        Assert.Null(graphics.OriginalEffect);
        Assert.Equal(0, queued.NativeEffectMemory!.Active);
        Assert.Equal(JsonSerializer.Serialize(initialGraphics.Models), JsonSerializer.Serialize(graphics.Models));
        Assert.Equal(JsonSerializer.Serialize(source.NativeModelCpuResources), JsonSerializer.Serialize(queued.NativeModelCpuResources));
        Assert.Equal(JsonSerializer.Serialize(source.NativeFileStaging), JsonSerializer.Serialize(queued.NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(source.NativeStage), JsonSerializer.Serialize(queued.NativeStage));
        Assert.Equal(JsonSerializer.Serialize(source.NativeAiVariables), JsonSerializer.Serialize(queued.NativeAiVariables));
        Assert.Equal(JsonSerializer.Serialize(source.PersistentAiVariables), JsonSerializer.Serialize(queued.PersistentAiVariables));
        Assert.Equal(JsonSerializer.Serialize(source.Random), JsonSerializer.Serialize(queued.Random));
        Assert.Equal(JsonSerializer.Serialize(source.NativeCrtRandom), JsonSerializer.Serialize(queued.NativeCrtRandom));
        NativeBattleActionPacketsState packets = Assert.IsType<NativeBattleActionPacketsState>(queued.NativeActionPackets);
        packets.Validate();
        Assert.Equal((byte)1, packets.ActionCount);
        Assert.Equal((byte)1, packets.ContactCount);
        NativeBattleActionHeaderSlot header = packets.Headers[0];
        Assert.Equal(0u, header.UnknownByteMask);
        Assert.Equal((byte)3, header.ReadByte(0));
        Assert.Equal((byte)8, header.ReadByte(1));
        Assert.Equal((byte)12, header.ReadByte(2));
        Assert.Equal((ushort)234, BinaryPrimitives.ReadUInt16LittleEndian(header.Payload.AsSpan().Slice(4)));
        Assert.Equal((ushort)257, BinaryPrimitives.ReadUInt16LittleEndian(header.Payload.AsSpan().Slice(6)));
        Assert.Equal((byte)1, header.ReadByte(0x10));
        Assert.Equal((byte)0, header.ReadByte(0x11));
        Assert.Equal(0, header.FirstContactIndex);
        NativeBattleContactPacket contact = Assert.IsType<NativeBattleContactPacket>(packets.Contacts[0]);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        Assert.Equal((byte)255, contact.Secondary.ActorIndex);
        NativeBattleDescriptorState descriptors = graphics.Descriptors;
        int packetIndex = Assert.Single(descriptors.Queue);
        NativeBattleDescriptorPacket packet = descriptors.Packets[packetIndex];
        Assert.Equal((ushort)104, packet.Command);
        Assert.Equal(0x1D280C4u, packet.Argument);
    }

}
