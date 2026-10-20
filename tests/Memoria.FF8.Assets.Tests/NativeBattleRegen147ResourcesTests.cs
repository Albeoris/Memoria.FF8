using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRegen147ResourcesTests
{
    [Fact]
    public void Original_envelope_and_leaf_sequence_keep_their_actual_model_and_all_ten_frames()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleRegen147Resources resources = game.ReadNativeRegen147Resources();
        NativeExecutableMemory memory = new(game.ReadNativeExecutable());
        byte[] cold = memory.ReadLoader(0x2476660, NativeBattleRegen147State.PhysicalLength);
        Assert.All(cold, value => Assert.Equal((byte)0, value));
        NativeBattleRegen147State state = NativeBattleRegen147State.FromColdProcess();
        state.Validate();
        Assert.Equal(cold, state.Storage.Read(0, cold.Length));
        Assert.Throws<NotSupportedException>(() => state.Storage.Read(NativeBattleRegen147State.PhysicalLength, 1));
        Assert.Equal(0x870, resources.Camera.Data.Length);
        Assert.NotEmpty(resources.Envelope.Bytes);
        for (ushort frame = 0; frame < 10; frame++)
        {
            Assert.NotEmpty(resources.Leaf.ProjectedFrame571C80(frame).Quads);
        }
    }
}
