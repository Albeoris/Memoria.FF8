using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDarkMist13Tests
{
    [Fact]
    public void Ring_warp_uses_signed_integer_division_and_retains_mesh_z_and_padding()
    {
        byte[] ring = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(ring.AsSpan(4), 1);
        BinaryPrimitives.WriteInt16LittleEndian(ring.AsSpan(8), -7);
        BinaryPrimitives.WriteInt16LittleEndian(ring.AsSpan(10), -11);
        byte[] mesh = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(12), 0xBEEF1234);
        ImmutableArray<byte> output = NativeBattleDarkMist13.Warp8BE7C0(ring.ToImmutableArray(), mesh.ToImmutableArray(), 0);
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(output.AsSpan().Slice(8)));
        Assert.Equal((short)-11, BinaryPrimitives.ReadInt16LittleEndian(output.AsSpan().Slice(10)));
        Assert.Equal(0xBEEF1234u, BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan().Slice(12)));
    }

    [Fact]
    public void Swirl_scroll_wraps_all_four_v_components_together_and_retains_other_uv_bytes()
    {
        byte[] mesh = Enumerable.Repeat((byte)0xA5, 100).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 32);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(60), 1);
        int[] offsets = [77, 81, 85, 87];
        byte[] values = [2, 12, 20, 40];
        for (int index = 0; index < offsets.Length; index++)
        {
            mesh[offsets[index]] = values[index];
        }
        ImmutableArray<byte> output = NativeBattleDarkMist13.Scroll8BE6B0(mesh.ToImmutableArray());
        Assert.Equal(new byte[] { 62, 72, 80, 100 }, offsets.Select(offset => output[offset]));
        Assert.Equal((byte)0xA5, output[76]);
        Assert.Equal((byte)0xA5, output[86]);
    }

    [Fact]
    public void Mist_initialization_draws_one_random_and_odd_variant_starts_half_a_step_ahead()
    {
        NativeBattleModulePoolTask task = new(0, 0, null, 0x8BEC80, new byte[104 - 12].ToImmutableArray());
        task = NativeBattleDarkMist13.Store(task, 0x1C, new(120, 240, 360)).WriteWord(0x62, 1);
        NativeCrtRandomState initial = new(1);
        NativeCrtRandomState expected = initial.Draw(out ushort value);
        NativeBattleDarkMist13ParticleStep step = NativeBattleDarkMist13.AdvanceParticle(task, new(0, 0, 0), null!, initial);
        short velocityY = (short)(unchecked((short)(-(value & 255) - 240 - 192)) / 12);
        Assert.Equal(expected, step.Random);
        Assert.Equal(new NativeBattleVector(115, (short)(240 + velocityY / 2), 345), NativeBattleDarkMist13.Vector(step.Task, 0x1C));
        Assert.Equal((ushort)0, step.Task.ReadWord(0x50));
        Assert.Equal((ushort)0x155, step.Task.ReadWord(0x66));
        Assert.Equal((byte)1, step.Task.ReadByte(0x29));
    }
}
