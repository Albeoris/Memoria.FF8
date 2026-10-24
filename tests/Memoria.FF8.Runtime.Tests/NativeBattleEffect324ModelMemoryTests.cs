using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect324ModelMemoryTests
{
    [Fact]
    public void World_restore_consumes_known_matrix_lanes_and_preserves_unknown_padding()
    {
        NativeBattleActionMatrix world = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 17, -23, 91, 4096);
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(world, null);
        NativeBattleEffectArena bytes = NativeBattleEffectArena.Clear();
        for (int index = 0; index < 32; index++)
        {
            bytes = packed.Known[index] ? bytes.Write(index, packed.Bytes.AsSpan(index, 1)) : bytes.MarkUnknown(index, 1);
        }
        NativeBattleGraphicsModel model = new(3, 0x800) { Alpha7 = 19, Color40 = 0x02123456 };
        NativeBattleGraphicsModel restored = BattleSession.ApplyNativeEffect324ModelBytes(model, new(0x1D97494, 64, bytes, 32));
        Assert.Equal(world.Rotation.ToArray(), restored.GeometryWorldMatrix!.Rotation.ToArray());
        Assert.Equal((world.X, world.Y, world.Z), (restored.GeometryWorldMatrix.X, restored.GeometryWorldMatrix.Y, restored.GeometryWorldMatrix.Z));
        Assert.Equal(model.Flags0, restored.Flags0);
        Assert.Equal(model.Alpha7, restored.Alpha7);
        Assert.Equal(model.Color40, restored.Color40);
        Assert.Throws<NotSupportedException>(() => BattleSession.ApplyNativeEffect324ModelBytes(model, new(0x1D97494, 64, bytes.MarkUnknown(20, 1), 32)));
    }

    [Fact]
    public void Byte_alpha_and_word_flags_stores_preserve_other_model_fields()
    {
        NativeBattleGraphicsModel model = new(3, 0x823) { Alpha7 = 19, Color40 = 0x02123456 };
        NativeBattleGraphicsModel hidden = BattleSession.ApplyNativeEffect324ModelBytes(model, new(0x1D97494, 7, NativeBattleEffectArena.Clear(), 1));
        NativeBattleGraphicsModel restored = BattleSession.ApplyNativeEffect324ModelBytes(hidden, new(0x1D97494, 0, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)0x23)), 2));
        Assert.Equal((byte)0, restored.Alpha7);
        Assert.Equal((ushort)0x23, restored.Flags0);
        Assert.Equal(model.Color40, restored.Color40);
        Assert.Throws<NotSupportedException>(() => BattleSession.ApplyNativeEffect324ModelBytes(model, new(0x1D97494, 0, NativeBattleEffectArena.Clear(), 4)));
    }
}
