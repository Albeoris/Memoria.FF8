using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect231SourceAdmissionTests
{
    [Fact]
    public void Selected_source_preserves_current_owners_and_queued_action()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT231_SOURCE_SNAPSHOT");
        Assert.SkipUnless(directory is not null && path is not null, "Select original resources and the current Effect231 source snapshot.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        string before = JsonSerializer.Serialize(source);
        ValidateSource(source, assets);
        Assert.Equal(before, JsonSerializer.Serialize(source));
        Assert.Equal(before, JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(before)!).Capture()));
        BattleSnapshot queued = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(Path.GetDirectoryName(path!)!, "queued-action104.battle.json"));
        ValidateQueuedAction(source, queued);
        string queuedJson = JsonSerializer.Serialize(queued);
        Assert.Equal(queuedJson, JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(queuedJson)!).Capture()));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([231])[230];
        Assert.Equal(0xA664A0u, binding.Entry);
        Assert.Equal(0xA66470u, binding.Initializer);
    }

    internal static void ValidateSource(BattleSnapshot source, BattleAssets assets, bool cold = true)
    {
        Assert.Equal(67, source.Definition.EncounterId);
        Assert.Equal(14, source.Definition.StageId);
        BattleEncounter formation = assets.LoadEncounter(67);
        Assert.Equal(14, formation.StageId);
        Assert.Equal((byte)0, formation.Flags);
        Assert.Equal(8, formation.Slots.Count);
        BattleEnemyPlacement enemy = Assert.Single(formation.Slots, slot => slot.Enabled);
        Assert.Equal(0, enemy.Slot);
        Assert.Equal(25, enemy.ModelId);
        Assert.Equal(41, enemy.NativeModelId);
        Assert.Equal(255, enemy.Level);
        Assert.Equal(new NativeBattleVector(-100, 0, -1400), enemy.NativePosition);
        BattleSession session = BattleSession.Restore(source);
        Assert.True(session.NativeEntryReady);
        NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(source.NativeGraphics);
        if (cold)
        {
            Assert.Null(graphics.OriginalEffect);
        }
        else
        {
            Assert.True(graphics.OriginalEffect is { Binding.Effect: 231, ReturnedDescriptor: 0 });
        }
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.Equal(0, source.NativeEffectMemory!.Active);
        Assert.Null(source.NativeAiVariables);
        Assert.Null(source.PersistentAiVariables);
        source.NativeEffectMemory.ScriptedEffects!.Validate();
        source.NativeEffectMemory.GeometryRegisters!.Validate();
        NativeBattleModelCpuResources cpu = Assert.IsType<NativeBattleModelCpuResources>(source.NativeModelCpuResources);
        NativeBattleFileStaging staging = Assert.IsType<NativeBattleFileStaging>(source.NativeFileStaging);
        cpu.Validate();
        staging.Validate();
        if (cold)
        {
            Assert.Equal(25739264u, staging.BaseAddress);
        }
        foreach (int physical in new[] { 0, 3 })
        {
            NativeBattleGraphicsModel model = graphics.Models[physical];
            Assert.NotEqual(0, model.Flags0 & 2);
            Assert.Equal(physical == 3 ? (byte)41 : (byte)0, model.ModelId4);
            Assert.Equal(physical == 3 ? 39 : 23, model.CurrentBodyBoneMatrices!.Matrices.Length);
            Assert.Equal(physical == 3 ? 39 : 23, model.SequenceState!.DecodedPose!.Bones.Length);
            Assert.All(model.SequenceState.DecodedPose.Bones, bone => Assert.NotNull(bone));
            Assert.All(model.CurrentBodyBoneMatrices.Matrices, matrix => matrix.Validate());
            model.GeometryWorldMatrix!.Validate();
            Assert.Null(model.ReadLinkedModelIndex140());
            Assert.Equal(physical == 3 ? 0 : 1, model.ActiveResourceRow100);
            Assert.Null(model.ActiveStagedResourceRow100);
            NativeBattleModelCpuResourceRow row = cpu.Rows[model.ActiveResourceRow100!.Value];
            Assert.Equal(physical == 3 ? (byte)3 : (byte)0, row.Require(12).Bank);
            if (physical == 3)
            {
                foreach ((int field, int offset, int length) in new (int, int, int)[] { (12, 22436, 1888), (16, 24324, 14600), (20, 38924, 48284) })
                {
                    NativeBattleModelCpuReference reference = row.Require(field);
                    Assert.Equal((byte)3, reference.Bank);
                    Assert.Equal(offset, reference.Offset);
                    Assert.Equal(length, row.Lengths[field]);
                    Assert.Equal(checked(0x1939018u + (uint)offset), reference.SharedAddress);
                    AssertExplicitKnownExtent(staging, reference, length);
                }
                byte[] skeletonFront = NativeBattleModelEnemyHeap.Read(cpu, staging, row.Require(12), 4);
                byte[] geometryFront = NativeBattleModelEnemyHeap.Read(cpu, staging, row.Require(16), 4);
                byte[] animationFront = NativeBattleModelEnemyHeap.Read(cpu, staging, row.Require(20), 4);
                Assert.Equal(new byte[] { 0x27, 0, 0x87, 0 }, skeletonFront);
                Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(geometryFront));
                Assert.Equal(29, BinaryPrimitives.ReadInt32LittleEndian(animationFront));
                Assert.False(model.HasLinkedModelRing140);
                string actorId = Assert.IsType<string>(graphics.ActorIds[physical]);
                BattleCombatantSnapshot actor = Assert.Single(source.Combatants, actor => actor.Id == actorId);
                Assert.Null(actor.NativeParticipant!.ResolvedLevel);
            }
        }
        Assert.Equal(new NativeBattleVector(-100, 0, -1400), graphics.Models[3].Transform!.Initial20);
        Assert.Null(graphics.Models[3].CurrentLinkedBoneMatrices);
        Assert.Equal(2, graphics.Models[0].CurrentLinkedBoneMatrices!.Matrices.Length);
        Assert.Equal(2, graphics.Models[0].SequenceState!.LinkedDecodedPose!.Bones.Length);
        Assert.All(graphics.Models[0].CurrentLinkedBoneMatrices!.Matrices, matrix => matrix.Validate());
        Assert.All(source.Definition.Combatants, actor =>
        {
            Assert.Null(actor.NativeTemplate);
            Assert.Null(actor.NativeAi);
        });
        foreach (int physical in new[] { 1, 2, 4, 5, 6 })
        {
            Assert.Equal(0, graphics.Models[physical].Flags0 & 2);
        }
        NativeBattleStageState stage = Assert.IsType<NativeBattleStageState>(source.NativeStage);
        Assert.Equal(4, stage.Groups.Length);
        foreach (NativeBattleStageGroup group in stage.Groups)
        {
            Assert.Equal(7, group.Flags);
            Assert.Equal(2, group.Matrices.Length);
            Assert.All(group.Matrices, matrix => matrix.Validate());
            Assert.NotNull(group.Source);
            group.Source!.Validate();
            if (cold)
            {
                Assert.Equal((short)0, group.ColorDepth);
                Assert.Equal(0u, group.ColorFlags);
                Assert.Equal((byte)1, group.Counter);
                Assert.Equal((byte)1, group.FrameCount);
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
        Assert.Fail("The selected current section extent is not explicitly represented.");
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
        Assert.Equal((byte)14, header.ReadByte(2));
        Assert.Equal((ushort)65, BinaryPrimitives.ReadUInt16LittleEndian(header.Payload.AsSpan().Slice(4)));
        Assert.Equal((ushort)231, BinaryPrimitives.ReadUInt16LittleEndian(header.Payload.AsSpan().Slice(6)));
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
