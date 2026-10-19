using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCarbuncle278ResourcesTests
{
    [Fact]
    public void Installed_resources_supply_complete_original_mirror_model_and_signed_bounds()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        NativeBattleCarbuncle278Resources resources = game.ReadNativeCarbuncle278Resources();
        Assert.Same(resources, game.ReadNativeCarbuncle278Resources());
        resources.Validate();
        Assert.Equal(0x10C0D08u, resources.MirrorBoundsMesh.Address);
        Assert.Equal(115u, BinaryPrimitives.ReadUInt32LittleEndian(resources.MirrorBoundsMesh.Bytes.AsSpan().Slice(4)));
        Assert.Equal(new NativeBattleEffectLocalBounds(new(-1606, 0, -400), new(1606, 0, 3200)), NativeBattleStaticMeshBounds.Read6D6070(resources.MirrorBoundsMesh.Bytes.AsSpan()));
        Assert.Throws<InvalidDataException>(() => (resources with { MirrorBoundsMesh = resources.MirrorBoundsMesh with { Address = 0 } }).Validate());
        NativeBattleEffectOwnedModelResource model = Assert.IsType<NativeBattleEffectOwnedModelResource>(resources.CreatureModel);
        Assert.Equal(30, model.PoseSource.BoneCount);
        Assert.Equal(0x5F04, model.Bytes.Length);
        Assert.NotEmpty(model.Geometry);
        NativeBattleModulePoolTask task = NativeBattleEffectTaskPool.Initialize(0x2508188, 0x20E235C, 0x5E0, 1).Slots[0];
        task = NativeBattleCarbuncle278Creature.Initialize6EC060(task, 0x20E235C, model);
        NativeBattleCarbuncleExecutableMemory executable = NativeBattleCarbuncleExecutableMemory.FromOriginal(model);
        NativeBattleCarbuncle278CreaturePose selected = NativeBattleCarbuncle278Creature.Select6574D0(task, executable, model, 0);
        Assert.Equal((byte)0, selected.Task.ReadByte(0x8C));
        Assert.Equal((byte)4, selected.Task.ReadByte(0x8D));
        Assert.Equal((byte)1, selected.Task.ReadByte(0x92));
        Assert.True(selected.Task.ReadByte(0x93) > 0);
        Assert.Equal(30, selected.Executable.Read(NativeBattleCarbuncleExecutableMemory.Address, 1)[0]);
        NativeBattleModuleResource sprite = Assert.IsType<NativeBattleModuleResource>(resources.SparkleSprite);
        Assert.Equal(0x10BD044u, sprite.Address);
        Assert.Equal(300, sprite.Bytes.Length);
        Assert.Equal((byte)7, sprite.Kind);
        for (int phase = 0; phase < 11; phase++)
        {
            Assert.Equal(20, sprite.ProjectedFrame571C80(phase).Quads.Length);
        }
        Assert.Throws<InvalidDataException>(() => sprite.ProjectedFrame571C80(11));
        NativeBattleVector cameraAnchor = NativeBattleCarbuncle278Anchors.ReadVertex682270(selected.Task.WriteWord(0x60, 4096).WriteWord(0x68, 4096).WriteWord(0x70, 4096).WriteDword(0x7C, 5000), selected.Executable, model, 0, 176);
        Assert.NotEqual(new NativeBattleVector(0, 0, 0), cameraAnchor);
        NativeVibrationBank vibration = Assert.IsType<NativeVibrationBank>(resources.Vibration);
        Assert.Equal(64, vibration.Offsets.Length);
        Assert.Equal(0x100, vibration.Offsets[1]);
        Assert.Equal(1, vibration.Offsets.Count(offset => offset != 0));
        Assert.Equal((ushort)54, vibration.ReadWord(0x100));
        Assert.Equal((ushort)4, vibration.ReadWord(0x102));
        Assert.Equal(0x140, vibration.Bytes.Length);
        Assert.Equal(115 * 8, resources.MirrorAlternateVertices.Length);
        Assert.Equal(0x10BFB08u, resources.SecondaryMesh!.Address);
        Assert.Equal(0x1200, resources.SecondaryMesh.Bytes.Length);
        NativeBattleModulePoolTask bodyTask = selected.Task.WriteWord(0x60, 4096).WriteWord(0x68, 4096).WriteWord(0x70, 4096).WriteDword(0x7C, 5000);
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleEffectMemory bodyMemory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(0x2E84, 0x1000), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400(), CarbuncleExecutableMemory = selected.Executable, Carbuncle278 = NativeBattleCarbuncle278State.FromColdProcess() with { ModelBuffer81F8 = 0x20DFAB8, PacketCursor81BC = 0x20E3984 } };
        NativeBattleCarbuncle278ModelDraw body = NativeBattleCarbuncle278Model.Draw6A79B0(bodyMemory, bodyTask, model, new(identity, 288, 160, 108), true);
        Assert.Equal(492 * 8, body.Memory.Arena!.Read(0x2E84, 492 * 8).Length);
        Assert.Throws<NotSupportedException>(() => body.Memory.Arena.Read(0x2E84 + 492 * 8, 0xA0));
        Assert.Equal(0x10BBAD0u, body.Memory.PrimitiveProcess!.ModelRendererRegisters!.SourceCursor);
        Assert.Equal(JsonSerializer.Serialize(selected.Executable), JsonSerializer.Serialize(body.Memory.CarbuncleExecutableMemory));
        Assert.Throws<NotSupportedException>(() => bodyMemory.Arena!.Read(0x2E84, 8));
        NativeBattleGfAnimationSource programme = Assert.IsType<NativeBattleGfAnimationSource>(resources.CreatureProgramme);
        Assert.Equal(0x10C2608u, programme.Address);
        Assert.Equal(0x3684, programme.Data.Length);
        Assert.Equal(6, BinaryPrimitives.ReadInt32LittleEndian(programme.Data.AsSpan()));
        NativeBattleModulePoolTask programmeTask = bodyTask.WriteWord(0xC, 110);
        string originalMemory = JsonSerializer.Serialize(bodyMemory);
        NativeBattleCarbuncle278ProgrammeDraw firstProgramme = NativeBattleCarbuncle278Programme.Advance681949(bodyMemory, programmeTask, programme, model, new(identity, 288, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(35, firstProgramme.Remaining);
        Assert.Equal(0x10C2608u, firstProgramme.Task.ReadDword(0xCC));
        Assert.Equal(1u, firstProgramme.Task.ReadDword(0xD0));
        Assert.Equal(programmeTask.Payload.Take(0xCC - 12), firstProgramme.Task.Payload.Take(0xCC - 12));
        Assert.Equal(originalMemory, JsonSerializer.Serialize(bodyMemory));
        NativeBattleEffectMemory restoredMemory = JsonSerializer.Deserialize<NativeBattleEffectMemory>(originalMemory)!;
        Assert.Equal(JsonSerializer.Serialize(firstProgramme), JsonSerializer.Serialize(NativeBattleCarbuncle278Programme.Advance681949(restoredMemory, programmeTask, programme, model, new(identity, 288, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270)));

        NativeBattleGfAnimationSource secondProgramme = Assert.IsType<NativeBattleGfAnimationSource>(resources.SecondCreatureProgramme);
        Assert.Equal(0x10C5C8Cu, secondProgramme.Address);
        Assert.Equal(0x1840, secondProgramme.Data.Length);
        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(secondProgramme.Data.AsSpan()));
        NativeBattleModulePoolTask filled = firstProgramme.Task with { Payload = firstProgramme.Task.Payload.Select((value, index) => index >= 0xCC - 12 + 0x3D8 ? (byte)0xA5 : value).ToImmutableArray() };
        NativeBattleCarbuncle278ProgrammeDraw secondProgrammeDraw = NativeBattleCarbuncle278Programme.Advance681949(bodyMemory, filled.WriteWord(0xC, 145), secondProgramme, model, new(identity, 288, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(31, secondProgrammeDraw.Remaining);
        Assert.Equal(0x10C5C8Cu, secondProgrammeDraw.Task.ReadDword(0xCC));
        Assert.Equal(1u, secondProgrammeDraw.Task.ReadDword(0xD0));
        Assert.All(secondProgrammeDraw.Task.Payload.Skip(0xCC - 12 + 0x3D8), value => Assert.Equal((byte)0xA5, value));
        NativeBattleGfAnimationSource recipient = Assert.IsType<NativeBattleGfAnimationSource>(resources.RecipientProgramme);
        Assert.Equal(0x10C1CD8u, recipient.Address);
        Assert.Equal(0x930, recipient.Data.Length);
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(recipient.Data.AsSpan()));
        Assert.Equal(new short[] { 717, 718 }, resources.Files.Select(file => file.FileId).ToArray());
        Assert.Equal(new[] { "MAG277_H.00", "MAG277_H.01" }, resources.Files.Select(file => file.Name.ToUpperInvariant()).ToArray());
        Assert.All(resources.Files, file => Assert.Equal(0x10u, BinaryPrimitives.ReadUInt32LittleEndian(file.Data.AsSpan())));
        Assert.Equal(0x10BF3A8u, resources.RecipientRayMesh!.Address);
        Assert.Equal(216, resources.RecipientRayMesh.Bytes.Length);
        Assert.Equal(0x10BCE98u, resources.RecipientShardSprite!.Address);
        Assert.Equal(428, resources.RecipientShardSprite.Bytes.Length);
        Assert.Equal(16384, resources.RecipientPolarTable.Length);
        Assert.Equal(new NativeBattleVector(4095, -6, 0), NativeBattleCarbuncle278RecipientChildren.Polar6836F0(new(4096, 0, 0), 1, resources.RecipientPolarTable));
        NativeBattleModulePoolTask child = new(1, 0, null, 0x683760, new byte[16].ToImmutableArray());
        child = child.WriteDword(0x18, 0x20DFAB8 + 0x1FA0);
        NativeBattleModulePoolTask recipientTask = new(1, 0, null, 0x6822F0, new byte[256].ToImmutableArray());
        byte[] cachedBytes = new byte[18];
        BinaryPrimitives.WriteInt16LittleEndian(cachedBytes, 4096);
        BinaryPrimitives.WriteInt16LittleEndian(cachedBytes.AsSpan(8), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(cachedBytes.AsSpan(16), 4096);
        byte[] cachedTranslation = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(cachedTranslation.AsSpan(8), 6000);
        NativeBattleEffectMemory childMemory = bodyMemory with { Arena = bodyMemory.Arena!.Write(0x1FA0 + 0x34, cachedBytes).Write(0x1FA0 + 0x48, cachedTranslation).MarkUnknown(0x1FA0 + 0x46, 2) };
        NativeBattleCarbuncle278ChildDraw rays = NativeBattleCarbuncle278ChildDrawing.Draw683760(childMemory, child, recipientTask, resources, new(identity, 288, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(4, rays.Packets.Attempts.Length);
        Assert.Equal<uint?>(0u, rays.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        NativeBattleCarbuncle278ChildDraw shards = NativeBattleCarbuncle278ChildDrawing.Draw683330(childMemory, child with { Callback = 0x683330 }, recipientTask, resources, new(identity, 288, 160, 108));
        Assert.Single(shards.Packets.Attempts);
        Assert.Equal(0x10BF480u, resources.RecipientRingMesh!.Address);
        Assert.Equal(1672, resources.RecipientRingMesh.Bytes.Length);
        NativeBattleCarbuncle278ChildDraw ring = NativeBattleCarbuncle278ChildDrawing.Draw6831D0(childMemory, child.WriteWord(0x10, 2048), resources, new(identity, 288, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(48, ring.Packets.Attempts.Length);
        NativeBattleCarbuncle278RangeDraw range = NativeBattleCarbuncle278RangeMesh.Emit682760(resources.CreatureBoundsMesh!, identity with { Z = 6000 }, new(identity, 288, 160, 108), short.MinValue, short.MaxValue, 2048, 0, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.True(range.FarColorWritten);
        Assert.Equal(768, range.Packets.Attempts.Length);
        Assert.Equal(14016, range.Packets.AllocationBytes);
        Assert.Equal(new uint[] { 0x10BD170, 0x10BD2FC }, resources.ReturnSprites.Select(sprite => sprite.Address).ToArray());
        for (int index = 0; index < resources.ReturnSprites.Length; index++)
        {
            NativeBattleModuleResource spriteResource = resources.ReturnSprites[index];
            Assert.Equal(396, spriteResource.Bytes.Length);
            Assert.Equal(40, spriteResource.ProjectedFrame571C80(0).Quads.Length);
            Assert.Equal(20, spriteResource.ProjectedFrame571C80(13).Quads.Length);
            Assert.Throws<InvalidDataException>(() => spriteResource.ProjectedFrame571C80(14));
            NativeBattleModulePoolTask particle = new(1, 0, null, 0x683DC0, new byte[16].ToImmutableArray());
            NativeBattleModuleDraw particleDraw = NativeBattleCarbuncle278ReturnParticles.Draw683DC0(particle.WriteWord(0x1A, (ushort)index), resources.ReturnSprites);
            Assert.Same(spriteResource, particleDraw.Resource);
            Assert.Equal(2048, particleDraw.Scale);
            Assert.Equal(-512, particleDraw.DepthOffset);
        }
    }
}
