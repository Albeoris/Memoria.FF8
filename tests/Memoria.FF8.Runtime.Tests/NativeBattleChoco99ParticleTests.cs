using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco99ParticleTests
{
    private static NativeBattleActionMatrix View => NativeBattleCuragaMatrices.Identity8DD770() with { Z = 4000 };
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);

    [Fact]
    public void Trail_appends_one_mandatory_child_before24_RNG_draws_and_preserves_raw_fourth_words()
    {
        (NativeBattleEffectHierarchy hierarchy, uint parent) = Hierarchy();
        NativeBattleEffectHierarchy before = NativeBattleStorageSnapshots.Capture(hierarchy);
        NativeBattleChoco99ParticleEmission emission = NativeBattleChoco99Particles.EmitTrail7209B0(hierarchy, parent, 8, new(1), SelectActors);
        Assert.Equal(SeedAfter(24), emission.Random.Seed);
        Assert.Equal((byte)1, emission.Hierarchy.Read(parent).ReadByte(0x28));
        Assert.Equal((byte)0, before.Read(parent).ReadByte(0x28));
        Assert.Equal((byte)1, hierarchy.Read(parent).ReadByte(0x28));
        NativeBattleModulePoolTask child = emission.Hierarchy.Read(emission.Address);
        Assert.Equal(NativeBattleChoco99Particles.TrailCallback, child.Callback);
        Assert.Equal((ushort)8, child.ReadWord(0xAE));
        Assert.Equal((byte)1, child.ReadByte(0x2C));
        Assert.Equal((byte)6, child.ReadByte(0x2D));
        short[] velocities = [388, 225, -148, 400, 73, -15, -21, 435, 166, -244, 318, -359];
        for (int point = 0; point < 4; point++)
        {
            Assert.Equal((ushort)0x4321, child.ReadWord(0x72 + point * 8));
            Assert.Equal((ushort)0, child.ReadWord(0x92 + point * 8));
            for (int axis = 0; axis < 3; axis++)
            {
                Assert.Equal(velocities[point * 3 + axis], Word(child, 0x8C + point * 8 + axis * 2));
            }
        }
        Assert.Equal(child.ReadDword(0x84), child.ReadDword(0x1C));
        Assert.Equal(child.ReadDword(0x88), child.ReadDword(0x20));
        NativeBattleActionMatrix kick = NativeBattleCuragaMatrices.RotateX8DD7E0(NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), 41), 18467 & 4095);
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(kick, new(0, 0, 6334 % 256));
        Assert.Equal(unchecked((short)(100 + offset.X)), Word(child, 0x6C));
        Assert.Equal(unchecked((short)(200 + offset.Y)), Word(child, 0x6E));
        Assert.Equal(unchecked((short)(300 + offset.Z)), Word(child, 0x70));
    }

    [Fact]
    public void Radial_uses13_RNG_draws_and_signed_division_on_negative_trig_products()
    {
        (NativeBattleEffectHierarchy hierarchy, uint parent) = Hierarchy();
        NativeBattleChoco99ParticleEmission emission = NativeBattleChoco99Particles.EmitRadial720D70(hierarchy, parent, 10, new(1), SelectActors);
        NativeBattleModulePoolTask child = emission.Hierarchy.Read(emission.Address);
        Assert.Equal(SeedAfter(13), emission.Random.Seed);
        Assert.Equal((byte)1, emission.Hierarchy.Read(parent).ReadByte(0x28));
        Assert.Equal((ushort)10, child.ReadWord(0xAE));
        int angle = (41 + 2048) & 4095;
        int sine = NativeBattleCameraMath.Sine(angle);
        int cosine = NativeBattleCameraMath.Cosine(angle);
        int radius = (29358 & 127) + 128;
        int horizontal = (26962 & 2047) + 1024;
        Assert.True(sine * radius < 0);
        Assert.NotEqual(sine * radius / 4096, sine * radius >> 12);
        Assert.Equal(unchecked((short)(100 + sine * radius / 4096)), Word(child, 0x7C));
        Assert.Equal(unchecked((short)(300 + cosine * radius / 4096)), Word(child, 0x80));
        Assert.Equal(unchecked((short)(sine * horizontal / 4096)), Word(child, 0x9C));
        Assert.Equal((short)((24464 & 1023) + 512), Word(child, 0x9E));
        Assert.Equal((ushort)0x4321, child.ReadWord(0x82));
    }

    [Fact]
    public void Mandatory_allocation_failure_does_not_return_an_optional_skip_or_mutate_parent()
    {
        (NativeBattleEffectHierarchy hierarchy, uint parent) = Hierarchy();
        NativeBattleEffectHierarchyAppend filled = hierarchy.Append8DC540(1, NativeBattleChoco99Particles.TrailCallback, 0xB0, parent, SelectActors);
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco99Particles.EmitTrail7209B0(filled.Hierarchy, parent, 8, new(1), SelectActors));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco99Particles.EmitRadial720D70(filled.Hierarchy, parent, 10, new(1), SelectActors));
        Assert.Equal((byte)1, filled.Hierarchy.Read(parent).ReadByte(0x28));
    }

    [Theory]
    [InlineData(NativeBattleChoco99Particles.TrailCallback)]
    [InlineData(NativeBattleChoco99Particles.RadialCallback)]
    public void Four_points_draw_frames0_to11_then_terminal_physics_still_updates_with_mode_query_short_circuit(uint callback)
    {
        NativeBattleModulePoolTask task = Particle(callback).WriteWord(0xAE, 10).WriteWord(0x54, 0xFF00);
        for (int point = 0; point < 4; point++)
        {
            task = task.WriteWord(0x6C + point * 8, (ushort)(100 + point)).WriteWord(0x72 + point * 8, 0x4321).WriteWord(0x8C + point * 8, unchecked((ushort)-129)).WriteWord(0x92 + point * 8, 0x7654);
        }
        int queries = 0;
        for (int callbackIndex = 0; callbackIndex < 13; callbackIndex++)
        {
            NativeBattleModulePoolTask before = NativeBattleStorageSnapshots.Capture(task);
            NativeBattleModulePoolTask restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModulePoolTask>(NativeBattleTestSnapshots.Json(task));
            NativeBattleChoco99ParticleStep step = NativeBattleChoco99Particles.Advance(task, Resources(), mode =>
            {
                Assert.Equal((short)10, mode);
                queries++;
                return false;
            }, View, Projection, 0);
            Assert.Equal(callbackIndex == 12 ? 0 : 4, step.Packets.Length);
            Assert.Equal(callbackIndex == 12, step.ReadyToComplete);
            Assert.Equal((ushort)Math.Min(callbackIndex, 11), step.Task.ReadWord(0x50));
            short damped = unchecked((short)(Word(before, 0x8C) - Word(before, 0x8C) / 8));
            Assert.Equal(damped, Word(step.Task, 0x8C));
            Assert.Equal(unchecked((short)(Word(before, 0x6C) + damped / 16)), Word(step.Task, 0x6C));
            Assert.Equal((ushort)0x4321, step.Task.ReadWord(0x72));
            Assert.Equal((ushort)0x7654, step.Task.ReadWord(0x92));
            Assert.Equal((ushort)0xFF00, step.Task.ReadWord(0x54));
            Assert.Equal(step.Task.ReadDword(0x84), step.Task.ReadDword(0x1C));
            Assert.Equal(step.Task.ReadDword(0x88), step.Task.ReadDword(0x20));
            Assert.Equal(NativeBattleTestSnapshots.Json(step), NativeBattleTestSnapshots.Json(NativeBattleChoco99Particles.Advance(restored, Resources(), _ => false, View, Projection, 0)));
            task = step.Task;
        }
        Assert.Equal(11, queries);
        Assert.Equal((byte)5, task.ReadByte(0x26));
        Assert.False(NativeBattleChoco99Particles.Advance(task.WriteByte(0x28, 1), Resources(), _ => throw new InvalidOperationException(), View, Projection, 0).ReadyToComplete);
    }

    [Fact]
    public void Early_mode_terminal_hides_all_four_draws_but_keeps_damping_and_original_word_padding()
    {
        NativeBattleModulePoolTask task = Particle(NativeBattleChoco99Particles.TrailCallback).WriteByte(0x29, 1).WriteWord(0x52, 11).WriteWord(0xAE, 8).WriteWord(0x8C, unchecked((ushort)-129)).WriteWord(0x6C, 32760);
        NativeBattleChoco99ParticleStep step = NativeBattleChoco99Particles.Advance(task, Resources(), mode => mode == 8, View, Projection, 0);
        Assert.True(step.ReadyToComplete);
        Assert.Empty(step.Packets);
        Assert.Equal((short)-113, Word(step.Task, 0x8C));
        Assert.Equal((short)32753, Word(step.Task, 0x6C));
        Assert.Equal((byte)2, step.Task.ReadByte(0x29));
    }

    internal static NativeBattleChoco99ParticleResources Resources() => new(Sprite(0x152A958), Sprite(0x152A31C));
    private static NativeBattleChoco97SpriteResources Sprite(uint address)
    {
        byte[] bytes = new byte[0x144];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 12);
        for (int frame = 0; frame < 12; frame++)
        {
            int cursor = 0x24 + frame * 24;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), (ushort)cursor);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), 1);
            bytes[cursor + 4] = 16;
            bytes[cursor + 5] = 128;
            bytes[cursor + 6] = 8;
            bytes[cursor + 7] = 0x2C;
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(34), 0xFFFF);
        return new(address, bytes.ToImmutableArray());
    }
    private static NativeBattleModulePoolTask Particle(uint callback) => new(1, 0, null, callback, new byte[0xB0 - 12].ToImmutableArray());
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static uint SeedAfter(int count)
    {
        NativeCrtRandomState random = new(1);
        for (int index = 0; index < count; index++)
        {
            random = random.Draw(out _);
        }
        return random.Seed;
    }
    private static NativeBattleEffectTaskActors SelectActors(uint parameters, sbyte header, sbyte target)
    {
        Assert.Equal(0x1D99A78u, parameters);
        Assert.Equal((sbyte)-2, header);
        Assert.Equal((sbyte)3, target);
        return new(1, 6);
    }
    private static (NativeBattleEffectHierarchy Hierarchy, uint Parent) Hierarchy()
    {
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x256B9D8, 0x256B000, 0x548, 1), NativeBattleEffectTaskPool.Initialize(NativeBattleChoco99Particles.AuxiliaryDescriptor, 0x2566000, 0xB0, 1)]);
        NativeBattleEffectHierarchyAppend parent = hierarchy.Append8DC540(0, 0x71FFB0, 0x548);
        NativeBattleModulePoolTask task = parent.Hierarchy.Read(parent.Address).WriteDword(0xC, 0x1D99A78).WriteByte(0x2A, 0xFE).WriteByte(0x2B, 3).WriteByte(0x2C, 7).WriteByte(0x2D, 7).WriteWord(0x1C, 100).WriteWord(0x1E, 200).WriteWord(0x20, 300).WriteWord(0x22, 0x4321).WriteWord(0x540, 87).WriteWord(0x542, 215).WriteWord(0x544, 280);
        return (parent.Hierarchy.Write(parent.Address, task), parent.Address);
    }
}
