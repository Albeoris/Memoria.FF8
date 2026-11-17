using static Memoria.FF8.App.Tests.NativeBattleOriginalModelAssertions;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect233SourceAdmissionTests
{
    [Fact]
    public void Selected_source_preserves_current_owners_and_queued_action()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT233_SOURCE_SNAPSHOT");
        Assert.SkipUnless(directory is not null && path is not null, "Select original resources and the current Effect233 source snapshot.");
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
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([233])[232];
        Assert.Equal(0xA51F40u, binding.Entry);
        Assert.Equal(0xA51F10u, binding.Initializer);
    }

    internal static void ValidateSource(BattleSnapshot source, BattleAssets assets, bool cold = true)
    {
        Assert.Equal(185, source.Definition.EncounterId);
        Assert.Equal(48, source.Definition.StageId);
        BattleEncounter formation = assets.LoadEncounter(185);
        Assert.Equal(48, formation.StageId);
        Assert.Equal((byte)0, formation.Flags);
        Assert.Equal(8, formation.Slots.Count);
        BattleEnemyPlacement enemy = Assert.Single(formation.Slots, slot => slot.Enabled);
        Assert.Equal(0, enemy.Slot);
        Assert.Equal(27, enemy.ModelId);
        Assert.Equal(43, enemy.NativeModelId);
        Assert.Equal(255, enemy.Level);
        Assert.Equal(new NativeBattleVector(100, 0, -1900), enemy.NativePosition);
        BattleSession session = BattleSession.Restore(source);
        Assert.True(session.NativeEntryReady);
        NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(source.ReadFixturePresentation().NativeGraphics);
        if (cold)
        {
            Assert.Null(graphics.OriginalEffect);
        }
        else
        {
            Assert.True(graphics.OriginalEffect is { Binding.Effect: 233, ReturnedDescriptor: 0 });
        }
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.Equal(0, source.ReadFixturePresentation().NativeEffectMemory!.Active);
        Assert.Null(source.NativeAiVariables);
        Assert.Null(source.PersistentAiVariables);
        source.ReadFixturePresentation().NativeEffectMemory.ScriptedEffects!.Validate();
        source.ReadFixturePresentation().NativeEffectMemory.GeometryRegisters!.Validate();
        source.ReadFixturePresentation().NativeModelCpuResources!.Validate();
        source.ReadFixturePresentation().NativeFileStaging!.Validate();
        foreach (int physical in new[] { 0, 3 })
        {
            NativeBattleGraphicsModel model = ReadOriginalModelCell(graphics, physical);
            Assert.NotEqual(0, model.Flags0 & 2);
            Assert.Equal(physical == 3 ? (byte)43 : (byte)0, model.ModelId4);
            Assert.Equal(physical == 3 ? 39 : 23, model.CurrentBodyBoneMatrices!.Matrices.Length);
            Assert.Equal(physical == 3 ? 39 : 23, model.SequenceState!.DecodedPose!.Bones.Length);
            Assert.All(model.CurrentBodyBoneMatrices.Matrices, matrix => matrix.Validate());
            model.GeometryWorldMatrix!.Validate();
            Assert.Null(model.ReadLinkedModel());
        }
        Assert.Equal(new NativeBattleVector(100, 0, -1900), ReadOriginalModelCell(graphics, 3).Transform!.Initial20);
        Assert.Null(ReadOriginalModelCell(graphics, 3).CurrentLinkedBoneMatrices);
        Assert.Equal(2, ReadOriginalModelCell(graphics, 0).CurrentLinkedBoneMatrices!.Matrices.Length);
        Assert.All(ReadOriginalModelCell(graphics, 0).CurrentLinkedBoneMatrices!.Matrices, matrix => matrix.Validate());
        Assert.All(source.Definition.Combatants, actor =>
        {
            Assert.Null(actor.NativeTemplate);
            Assert.Null(actor.NativeAi);
        });
        foreach (int physical in new[] { 1, 2, 4, 5, 6 })
        {
            Assert.Equal(0, ReadOriginalModelCell(graphics, physical).Flags0 & 2);
        }
    }

    internal static void ValidateQueuedAction(BattleSnapshot source, BattleSnapshot queued)
    {
        Assert.Equal(source.Frame, queued.Frame);
        NativeBattleGraphicsState initialGraphics = Assert.IsType<NativeBattleGraphicsState>(source.ReadFixturePresentation().NativeGraphics);
        NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(queued.ReadFixturePresentation().NativeGraphics);
        Assert.Null(graphics.OriginalEffect);
        Assert.Equal(0, queued.ReadFixturePresentation().NativeEffectMemory!.Active);
        Assert.Equal(JsonSerializer.Serialize(ReadOriginalModelCells(initialGraphics)), JsonSerializer.Serialize(ReadOriginalModelCells(graphics)));
        Assert.Equal(JsonSerializer.Serialize(source.ReadFixturePresentation().NativeModelCpuResources), JsonSerializer.Serialize(queued.ReadFixturePresentation().NativeModelCpuResources));
        Assert.Equal(JsonSerializer.Serialize(source.ReadFixturePresentation().NativeFileStaging), JsonSerializer.Serialize(queued.ReadFixturePresentation().NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(source.ReadFixturePresentation().NativeStage), JsonSerializer.Serialize(queued.ReadFixturePresentation().NativeStage));
        Assert.Equal(JsonSerializer.Serialize(source.NativeAiVariables), JsonSerializer.Serialize(queued.NativeAiVariables));
        Assert.Equal(JsonSerializer.Serialize(source.PersistentAiVariables), JsonSerializer.Serialize(queued.PersistentAiVariables));
        Assert.Equal(JsonSerializer.Serialize(source.Random), JsonSerializer.Serialize(queued.Random));
        Assert.Equal(JsonSerializer.Serialize(source.NativeCrtRandom), JsonSerializer.Serialize(queued.NativeCrtRandom));
        NativeBattleActionPacketsState packets = Assert.IsType<NativeBattleActionPacketsState>(queued.NativeActionPackets);
        packets.Validate();
        Assert.Equal((byte)1, packets.ActionCount);
        Assert.Equal((byte)1, packets.ContactCount);
        NativeBattleActionHeaderSlot header = packets.ReadHeader(new BattleActionHeaderReference(0));
        Assert.Equal(0u, packets.ExportRetained().Headers[0].UnknownByteMask);
        Assert.Equal((byte)3, ReadOriginalModelOperand(packets.ModelAliases, header.Source));
        Assert.Equal((byte)8, header.Command);
        Assert.Equal((byte)12, header.Sequence);
        Assert.Equal((ushort)49, BinaryPrimitives.ReadUInt16LittleEndian(packets.ExportRetained().Headers[0].Payload.AsSpan().Slice(4)));
        Assert.Equal((ushort)233, BinaryPrimitives.ReadUInt16LittleEndian(packets.ExportRetained().Headers[0].Payload.AsSpan().Slice(6)));
        Assert.Equal((byte)1, header.ReadContactCount());
        Assert.Equal((byte)0, header.ReadRepeatModeMinusOne());
        Assert.Equal(0, header.FirstContact.Ordinal);
        NativeBattleContactPacket contact = packets.ReadContact(header.FirstContact);
        Assert.Equal((byte)0, ReadOriginalModelOperand(packets.ModelAliases, contact.Primary.Recipient));
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        Assert.Null(contact.Secondary);
        NativeBattleDescriptorState descriptors = graphics.Descriptors;
        int packetIndex = Assert.Single(descriptors.Queue);
        NativeBattleDescriptorPacket packet = descriptors.Packets[packetIndex];
        Assert.Equal((ushort)104, packet.Command);
        Assert.Equal(0x1D280C4u, packet.Argument);
    }

}
