using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa86ParticlesTests
{
    [Theory]
    [InlineData(0, 30, 26, 4, 2)]
    [InlineData(1, 33, 26, 4, 2)]
    [InlineData(2, 28, 26, 4, 2)]
    [InlineData(3, 29, 26, 4, 2)]
    [InlineData(4, 28, 10, 2, 0)]
    [InlineData(5, 29, 10, 2, 0)]
    [InlineData(6, 241, 10, 2, 0)]
    public void Native_emission_preserves_exact_bone_selection_physical_family_counts_and_RNG_order_after_restore(int mode, byte bone, int draws, int sprites, int meshes)
    {
        NativeBattleEffectHierarchy hierarchy = new(Enumerable.Range(0, 9).Select(index => NativeBattleEffectTaskPool.Initialize(NativeBattleRinoa86Controller.Descriptors[index], NativeBattleRinoa86Controller.Banks[index], NativeBattleRinoa86Controller.Strides[index], NativeBattleRinoa86Controller.Capacities[index])).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(7, 0x77FF90, 352);
        List<byte> bones = [];
        NativeBattleVector ReadBone(byte selector)
        {
            bones.Add(selector);
            return new(100, -200, 300);
        }
        NativeBattleEffectTaskActors Select(uint _, sbyte __, sbyte ___) => new(0, 3);
        NativeBattleEffectHierarchy clone = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectHierarchy>(NativeBattleTestSnapshots.Json(root.Hierarchy))!;
        NativeBattleRinoa86ParticleEmission emitted = NativeBattleRinoa86Emitters.Emit(root.Hierarchy, root.Address, mode, new(1), ReadBone, Select);
        Assert.Equal(Enumerable.Repeat(bone, sprites + meshes), bones);
        Assert.Equal(sprites, emitted.Hierarchy.Pools[4].Slots.Count(task => task.Callback == 0x782930));
        Assert.Equal(meshes, emitted.Hierarchy.Pools[5].Slots.Count(task => task.Callback == 0x780640));
        Assert.Equal((byte)(sprites + meshes), emitted.Hierarchy.Read(root.Address).ReadByte(40));
        NativeCrtRandomState expected = new(1);
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, emitted.Random);
        Assert.Equal(NativeBattleTestSnapshots.Json(emitted), NativeBattleTestSnapshots.Json(NativeBattleRinoa86Emitters.Emit(clone, root.Address, mode, new(1), _ => new(100, -200, 300), Select)));
    }

    [Fact]
    public void Layered_particle_truncates_acceleration_to_WORD_before_signed_damping_and_moves_after_floor_clamp()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(28, 1).WriteWord(30, 4).WriteWord(32, 0).WriteWord(124, 32767).WriteWord(126, 32760).WriteWord(128, unchecked((ushort)-32768)).WriteWord(72, 65520);
        task = NativeBattleRinoa86Particles.Advance782850(task);
        Assert.Equal((ushort)48, task.ReadWord(72));
        Assert.Equal((short)30720, unchecked((short)task.ReadWord(124)));
        Assert.Equal((short)-30608, unchecked((short)task.ReadWord(126)));
        Assert.Equal((short)-30720, unchecked((short)task.ReadWord(128)));
        Assert.Equal((short)1921, unchecked((short)task.ReadWord(28)));
        Assert.Equal((short)-1913, unchecked((short)task.ReadWord(30)));
        Assert.Equal((short)-1920, unchecked((short)task.ReadWord(32)));
    }

    [Fact]
    public void Age10_scale_expiry_finishes_with_native_scale16_and_preserves_last_draw_visibility()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(36, 10).WriteByte(41, 1).WriteDword(48, 1024).WriteDword(52, 1024).WriteDword(56, 1024);
        task = NativeBattleRinoa86Particles.Advance782850(task);
        Assert.Equal((uint)16, task.ReadDword(48));
        Assert.Equal((uint)16, task.ReadDword(52));
        Assert.Equal((uint)16, task.ReadDword(56));
        Assert.Equal((byte)1, task.ReadByte(38));
        Assert.Equal((byte)2, task.ReadByte(41));
    }

    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x780640, ImmutableArray.CreateRange(new byte[140]));
}
