using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleUltima149ResourcesTests
{
    [Fact]
    public void Original_Ultima_resources_retain_all_model_sprite_vibration_and_morph_sources()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleUltima149Resources resources = game.ReadNativeUltima149Resources();
        NativeExecutableMemory image = new(game.ReadNativeExecutable());
        NativeBattleUltima149State cold = NativeBattleUltima149State.FromColdProcess();
        Assert.Equal(image.ReadLoader(0x2461EC0, 2400), cold.ParticlesA.ToArray());
        Assert.Equal(image.ReadLoader(0x2462820, 2400), cold.ParticlesB.ToArray());
        for (int actor = 0; actor < 7; actor++)
        {
            Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(image.ReadLoader(0x2463FA0u + (uint)actor * 24, 4)), cold.SideFlags[actor]);
        }
        Assert.Equal(image.ReadLoader(0x2464418, 1920), cold.CoreVertices.ToArray());
        Assert.Equal(BinaryPrimitives.ReadInt32LittleEndian(image.ReadLoader(0x2461E88, 4)), cold.RingScale);
        Assert.Equal(BinaryPrimitives.ReadInt32LittleEndian(image.ReadLoader(0x2464BA4, 4)), cold.VibrationHandle);

        Assert.Equal(0x3A8, resources.Camera.Data.Length);
        Assert.Equal((ushort)24, Frames(resources.Orbit));
        Assert.Equal((ushort)16, Frames(resources.Dust));
        Assert.Equal((ushort)32, Frames(resources.Spark));
        Assert.Equal(0x4941Bu, resources.StartSound);
        Assert.Equal(0x49477u, resources.BlastSound);
        Assert.Equal(new NativeBattleVector(10000, -3000, 5000), resources.ReferencePoint);
        Assert.Equal(960, resources.CoreShape1.Length);
        Assert.Equal(120, BinaryPrimitives.ReadInt32LittleEndian(resources.Core.Bytes.AsSpan().Slice(4)));
        Assert.Equal(new[] { 0, 256, 512, 768, 1024, 1280, 1568, 1904, 2304, 1904, 1568, 1280, 1024, 768, 512, 256 }, resources.RingFades);
        Assert.Equal(256, resources.Vibration.Offsets[1]);
        Assert.Equal((ushort)16, resources.Vibration.ReadWord(256));
        Assert.Equal((ushort)0, resources.Vibration.ReadWord(258));
        foreach (NativeBattleStaticMeshResource model in new[] { resources.Streak, resources.Shard, resources.Energy, resources.Ring, resources.Dome, resources.Core })
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(model.Bytes.AsSpan().Slice(4));
            Assert.True(count > 0);
            Assert.Equal(8 + count * 8, BinaryPrimitives.ReadInt32LittleEndian(model.Bytes.AsSpan()));
        }
        for (int frame = 0; frame < 24; frame++)
        {
            Assert.Equal(frame == 23, resources.Orbit.ProjectedFrame571C80(frame).NextOffset < 0);
        }

        static ushort Frames(NativeBattleModuleResource sprite) => BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8));
    }
}
