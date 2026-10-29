using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDiablos325ResourcesTests
{
    [Fact]
    public void Installed_opening_programme_and_null_first_vibration_entry_preserve_original_bytes()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        NativeBattleDiablos325Resources resources = game.ReadNativeDiablos325Resources();
        Assert.Same(resources, game.ReadNativeDiablos325Resources());
        resources.Validate();
        Assert.Equal(new byte[] { 0x30, 0x30, 0x31, 0x30 }, resources.PrimitiveKinds);
        Assert.Equal(new byte[] { 0x30, 0x30, 0x30, 0x30 }, resources.TextureKinds);
        Assert.Equal(new byte[] { 0x31 }, resources.ParticlePrimitiveKinds!.Value);
        Assert.Equal(new byte[] { 0x30 }, resources.ParticleTextureKinds!.Value);
        Assert.Equal(Enumerable.Range(727, 8).Select(id => (short)id), resources.Files.Select(file => file.FileId));
        Assert.Equal(new[] { "MAG324_H.00", "MAG324_H.01", "MAG324_H.02", "MAG324_H.03", "MAG324_H.04", "MAG324_H.05", "MAG324_H.06", "MAG324_H.07" }, resources.Files.Select(file => file.Name));
        Assert.All(resources.Files, file => Assert.NotEmpty(file.Data));
        Assert.Equal(new uint[] { 240248, 240251, 240254, 240257, 240260, 240263 }, resources.Sounds);
        Assert.Equal(0xFD8584u, resources.OpeningProgramme.Address);
        Assert.Equal(0x3890, resources.OpeningProgramme.Data.Length);
        Assert.Equal(6680, resources.WellGeometry.Length);
        Assert.Equal(266, System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(resources.WellGeometry.AsSpan().Slice(4, 4)));
        Assert.Equal(116, resources.PuffSprite.Length);
        for (byte life = 1; life <= 27; life++)
        {
            NativeBattleGfGeometryBatch puffs = NativeBattleDiablos325PuffPackets.Emit6FC9E0(resources.PuffSprite, life, new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1000, 4096), new(null, 288, 0, 0), 19);
            Assert.Equal(48, puffs.AllocationBytes);
            Assert.Equal(3, puffs.Attempts.Length);
            Assert.Equal(0x2E808080u, puffs.Attempts[2].Primitive!.Words[1]);
        }
        Assert.Equal(164, System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(resources.Files.Single(file => file.FileId == 733).Data.AsSpan()));
        NativeBattleEffectFileAsset lateShapes = resources.Files.Single(file => file.FileId == 734);
        Assert.Equal(121508, lateShapes.Data.Length);
        Assert.Equal(0x810, System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(lateShapes.Data.AsSpan()));
        foreach (uint pointer in new uint[] { 0xFD6814, 0xFD69C0 })
        {
            ImmutableArray<byte> sprite = resources.ReadPuffSprite(pointer);
            Assert.Equal(428, sprite.Length);
            for (byte life = 1; life <= 16; life++)
            {
                NativeBattleGfGeometryBatch ring = NativeBattleDiablos325PuffPackets.Emit6FC9E0(sprite, life, new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 16000, 4096), new(null, 128, 160, 108), 19);
                Assert.Equal(48, ring.AllocationBytes);
                Assert.Equal(3, ring.Attempts.Length);
            }
        }
        Assert.Throws<NotSupportedException>(() => (resources with { RingPuffSprite0 = null }).ReadPuffSprite(0xFD6814));
        Assert.Throws<NotSupportedException>(() => resources.ReadPuffSprite(0xFD6815));
        NativeBattleEffectFileAsset thirteen = resources.Files.Single(file => file.FileId == 734);
        NativeBattleModulePoolTask task = new(1, 0, null, 0x6545F0, new byte[0x994 - 12].ToImmutableArray());
        NativeBattleCameraProjection shapeProjection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 8792, 16000, 4096), 288, 160, 108);
        for (int index = 0; index < 13; index++)
        {
            NativeBattleEffectArena isolated = NativeBattleEffectArena.Clear().Write(0x3505C, thirteen.Data.AsSpan().Slice(index * 0x1FF0, 0x1FF0)).MarkUnknown(0x3704C, NativeBattleEffectArena.Length - 0x3704C);
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = isolated, PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess() };
            NativeBattleDiablos325ShapeDraw shape = NativeBattleDiablos325Shapes.Draw659680(memory, task, 0x20DFAB8u + 0x3505C, new(90, -8792, 3840), shapeProjection);
            Assert.NotEmpty(shape.Packets.Attempts);
            Assert.True(shape.Packets.AllocationBytes > 0);
        }
        ImmutableArray<byte> model = resources.Files.Single(file => file.FileId == 729).Data;
        int skeleton = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(model.AsSpan().Slice(4, 4));
        int animation = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(model.AsSpan().Slice(12, 4));
        int endAnimation = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(model.AsSpan().Slice(16, 4));
        ReadOnlySpan<byte> animationBytes = model.AsSpan().Slice(animation, endAnimation - animation);
        Assert.Equal(new byte[] { 15, 30, 15, 9, 12, 42, 18 }, NativeBattlePoseDecoder.ReadFrameCounts(animationBytes));
        foreach (byte selector in new byte[] { 1, 2, 3 })
        {
            NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(animationBytes, model[skeleton], selector);
            NativeBattlePoseFirstFrame second = NativeBattlePoseDecoder.AdvanceRegular508F90(animationBytes, model[skeleton], selector, first.Pose, first.ByteOffset, first.BitOffset);
            Assert.Equal(69, second.Pose.Bones.Length);
            Assert.InRange((int)second.ByteOffset, (int)first.ByteOffset, animationBytes.Length);
            Assert.InRange(second.BitOffset, (ushort)0, (ushort)7);
        }
        Assert.Equal(64, resources.Vibration.Offsets.Length);
        Assert.Equal(0, resources.Vibration.Offsets[0]);
        Assert.Equal(0x100, resources.Vibration.Offsets[1]);
        Assert.All(resources.Vibration.Offsets.Skip(2), value => Assert.Equal(0, value));
        Assert.Equal((ushort)62, resources.Vibration.ReadWord(0x100));
        Assert.Equal((ushort)4, resources.Vibration.ReadWord(0x102));
        Assert.Equal(0x148, resources.Vibration.Bytes.Length);
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(ImmutableArray.CreateRange(new byte[0xE0])).Initialize7016B0(resources.OpeningProgramme, 0xE0);
        Assert.Equal(0xFD8584u, workspace.ResourceAddress);
        Assert.Equal(0, workspace.Frame);
        NativeBattleDiablos325Resources restored = NativeBattleSnapshotJson.Deserialize<NativeBattleDiablos325Resources>(NativeBattleSnapshotJson.Serialize(resources))!;
        Assert.Equal(NativeBattleSnapshotJson.Serialize(resources), NativeBattleSnapshotJson.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (resources with { OpeningProgramme = resources.OpeningProgramme with { Address = 0 } }).Validate());
    }
}
