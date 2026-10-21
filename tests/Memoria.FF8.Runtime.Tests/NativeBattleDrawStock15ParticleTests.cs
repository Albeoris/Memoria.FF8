using System.Collections.Immutable;
using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDrawStock15ParticleTests
{
    [Fact]
    public void Scheduler_uses_exact_counter_equalities_five_trajectories_and_fifteen_CRT_draws()
    {
        var fixture = Fixture(0x8B5AE0);
        NativeBattleEffectHierarchy hierarchy = fixture.Hierarchy;
        NativeCrtRandomState random = NativeCrtRandomState.FromColdProcess();
        for (int age = 0; age <= 6; age++)
        {
            NativeBattleDrawStock15ParticleStep step = NativeBattleDrawStock15Particles.Advance(hierarchy, fixture.Address, random, Inputs());
            Assert.Equal(0, step.Result);
            Assert.Equal(age is 0 or 2 or 4 ? 3 : age == 6 ? 6 : 0, step.Operations.Count(operation => operation is NativeBattleDrawStock15Rotate or NativeBattleDrawStock15Transform));
            hierarchy = step.Hierarchy;
            random = step.Random;
        }
        NativeBattleModulePoolTask[] children = hierarchy.Pools[3].Slots.Where(task => task.Callback == 0x8B5C70 && (task.Flags & 1) != 0).ToArray();
        Assert.Equal(new ushort[] { 0, 1, 2, 3, 4 }, children.Select(task => task.ReadWord(0x120)));
        Assert.Equal(new ushort[] { 0, 0, 0, 0, 1 }, children.Select(task => task.ReadWord(0x122)));
        NativeCrtRandomState expected = NativeCrtRandomState.FromColdProcess();
        for (int draw = 0; draw < 15; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, random);
        Assert.NotEqual((byte)0, hierarchy.Read(fixture.Address).ReadByte(0x28));
        Assert.Equal((byte)1, (byte)(hierarchy.Read(fixture.Address).ReadByte(0x26) & 1));
    }

    [Fact]
    public void Trajectory_preserves_anchor_padding_and_basis_translation_while_emitting_head_before_ribbon()
    {
        var fixture = Fixture(0x8B5C70);
        NativeBattleModulePoolTask original = fixture.Hierarchy.Read(fixture.Address).WriteWord(0x86, 0xBEEF).WriteWord(0x72, 0xABCD).WriteDword(0x74, 0x12345678).WriteWord(0x22, 0x9876);
        NativeBattleEffectHierarchy hierarchy = fixture.Hierarchy.Write(fixture.Address, original);
        NativeBattleDrawStock15ParticleStep step = NativeBattleDrawStock15Particles.Advance(hierarchy, fixture.Address, NativeCrtRandomState.FromColdProcess(), Inputs());
        NativeBattleModulePoolTask task = step.Hierarchy.Read(fixture.Address);
        Assert.Equal((ushort)0xBEEF, task.ReadWord(0x86));
        Assert.Equal((ushort)0xABCD, task.ReadWord(0x72));
        Assert.Equal(0x12345678u, task.ReadDword(0x74));
        Assert.Equal((ushort)0x9876, task.ReadWord(0x22));
        Assert.Equal(0x162B520u, task.ReadDword(0x4C));
        Assert.Equal((ushort)1, task.ReadWord(0x124));
        int head = step.Operations.IndexOf(step.Operations.OfType<NativeBattleDrawStock15HeadSprite>().Single());
        int ribbon = step.Operations.IndexOf(step.Operations.OfType<NativeBattleDrawStock15Ribbon>().Single());
        Assert.True(head < ribbon);
        NativeCrtRandomState expected = NativeCrtRandomState.FromColdProcess().Draw(out _).Draw(out _);
        Assert.Equal(expected, step.Random);
        NativeBattleEffectHierarchy restored = JsonSerializer.Deserialize<NativeBattleEffectHierarchy>(JsonSerializer.Serialize(step.Hierarchy))!;
        NativeBattleDrawStock15ParticleStep actual = NativeBattleDrawStock15Particles.Advance(step.Hierarchy, fixture.Address, step.Random, Inputs());
        NativeBattleDrawStock15ParticleStep cold = NativeBattleDrawStock15Particles.Advance(restored, fixture.Address, step.Random, Inputs());
        Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(cold));
    }

    [Fact]
    public void Look_basis_uses_raw_signed_direction_and_GTE_cross_saturation_without_normalization()
    {
        NativeBattleActionMatrix matrix = NativeBattleDrawStock15Particles.LookBasis8DDA50(new(-32768, 21, 123));
        Assert.Equal(new short[] { 123, 0, -32768, 0, 4096, 21, 32767, 0, 123 }, matrix.Rotation);
    }

    [Fact]
    public void Sparkle_follows_its_actual_parent_and_retires_after_six_visible_frames_without_extra_RNG()
    {
        var fixture = Fixture(0x8B6440);
        NativeBattleEffectHierarchy hierarchy = fixture.Hierarchy;
        NativeCrtRandomState random = NativeCrtRandomState.FromColdProcess();
        int visible = 0;
        for (int age = 0; age <= 6; age++)
        {
            NativeBattleDrawStock15ParticleStep step = NativeBattleDrawStock15Particles.Advance(hierarchy, fixture.Address, random, Inputs());
            visible += step.Operations.Count(operation => operation is NativeBattleDrawStock15SparkleSprite);
            Assert.Equal(age == 6 ? 2 : 0, step.Result);
            hierarchy = step.Hierarchy;
            random = step.Random;
        }
        Assert.Equal(6, visible);
        Assert.Equal(NativeCrtRandomState.FromColdProcess().Draw(out _).Draw(out _), random);
    }

    [Fact]
    public void Ribbon_has_two_partially_written_GT3_packets_per_segment_with_exact_vertex_order()
    {
        var fixture = Fixture(0x8B5C70);
        NativeBattleModulePoolTask task = fixture.Hierarchy.Read(fixture.Address).WriteWord(0x128, 1).WriteWord(0x12A, 2);
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(0x60 + index * 2, (ushort)(index % 4 == 0 ? 4096 : 0));
        }
        task = task.WriteWord(0x98, 100).WriteWord(0x9A, 200).WriteWord(0x9C, 300).WriteWord(0xA0, 400).WriteWord(0xA2, 500).WriteWord(0xA4, 600);
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 10000, 4096);
        NativeBattleCameraProjection projection = new(camera, 216, 160, 108);
        NativeBattleDrawStock15RibbonStep rendered = NativeBattleDrawStock15Rendering.Ribbon8B5F30(task, camera, projection);
        Assert.Equal(80, rendered.Batch.AllocationBytes);
        Assert.Equal(2, rendered.Batch.Attempts.Length);
        Assert.All(rendered.Batch.Attempts, attempt =>
        {
            Assert.Equal(40, attempt.Data.Length);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[19]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[31]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[38]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[39]);
            Assert.Equal((byte)0x36, attempt.Data[7]);
        });
        Assert.Equal(new byte[] { 0, 32, 0 }, new[] { rendered.Batch.Attempts[0].Data[4], rendered.Batch.Attempts[0].Data[16], rendered.Batch.Attempts[0].Data[28] });
        Assert.Equal(new byte[] { 0, 32, 32 }, new[] { rendered.Batch.Attempts[1].Data[4], rendered.Batch.Attempts[1].Data[16], rendered.Batch.Attempts[1].Data[28] });
        Assert.Equal(rendered.Batch.Attempts[0].Data.AsSpan().Slice(32, 4).ToArray(), rendered.Batch.Attempts[1].Data.AsSpan().Slice(8, 4).ToArray());
    }

    [Fact]
    public void Sprite_receipts_preserve_native180_byte_padding_and_replay_each_rejected_or_accepted_quad()
    {
        var fixture = Fixture(0x8B5C70);
        NativeBattleModulePoolTask task = fixture.Hierarchy.Read(fixture.Address).WriteDword(0x4C, 0x162B520).WriteWord(0x52, 5);
        NativeBattleDrawStock15ParticleResources resources = new(Sprite(6, 4), Sprite(0, 2));
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 10000, 4096);
        NativeBattleCameraProjection projection = new(camera, 216, 160, 108);
        uint address = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, Enumerable.Repeat((byte)0xA5, 180).ToArray());
        NativeBattleDrawStock15SpriteTemporaryResult result = NativeBattleDrawStock15SpriteRendering.Emit180(staging, address, task, resources, camera, projection, 0);
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.Staging.Read(address + 118, 2));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(result.Staging.Read(address + 36, 2)));
        Assert.Equal(4, result.Operations.OfType<NativeBattleDrawStock15Matrix>().Count());
        Assert.Equal(4, result.Operations.OfType<NativeBattleDrawStock15Projection>().Count(operation => !operation.FinalCue));
        Assert.Single(result.Operations.OfType<NativeBattleDrawStock15AverageDepth>());
        Assert.NotEmpty(result.Batch.Primitives);
        NativeBattleDrawStock15SpriteTemporaryResult rejected = NativeBattleDrawStock15SpriteRendering.Emit180(staging, address, task, resources, camera with { Z = -10000 }, projection with { View = camera with { Z = -10000 } }, 0);
        Assert.Equal(4, rejected.Operations.OfType<NativeBattleDrawStock15Projection>().Count(operation => !operation.FinalCue));
        Assert.Empty(rejected.Operations.OfType<NativeBattleDrawStock15AverageDepth>());
    }

    [Theory]
    [InlineData(-17, -17, -17, 16)]
    [InlineData(160, 160, 160, 27)]
    public void Ribbon_bucket_keeps_two_distinct_native_signed_division_stages(int first, int second, int third, int expected)
    {
        Assert.Equal(expected, NativeBattleDrawStock15Rendering.RibbonBucket(first, second, third));
    }

    private static ImmutableArray<byte> Sprite(byte kind, int quads)
    {
        int stride = 4 + 20 * quads;
        byte[] bytes = new byte[24 + 6 * stride];
        bytes[3] = kind;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 6);
        for (int frame = 0; frame < 6; frame++)
        {
            int offset = 24 + frame * stride;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), (ushort)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), (uint)quads);
            for (int quad = 0; quad < quads; quad++)
            {
                bytes[offset + 4 + quad * 20] = 1;
                bytes[offset + 6 + quad * 20] = 1;
                bytes[offset + 7 + quad * 20] = 0x2C;
            }
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(22), ushort.MaxValue);
        return bytes.ToImmutableArray();
    }

    private static NativeBattleDrawStock15ParticleInputs Inputs() => new(0, (_, _, _) => new(0, 3), _ => new(120, 240, 360, 0x7777));

    private static (NativeBattleEffectHierarchy Hierarchy, uint Address) Fixture(uint callback)
    {
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(0x2732CE8, 0x2732C20, 100, 2),
            NativeBattleEffectTaskPool.Initialize(0x273ACD8, 0x273ACE8, 88, 4),
            NativeBattleEffectTaskPool.Initialize(0x273AA38, 0x273A248, 676, 3),
            NativeBattleEffectTaskPool.Initialize(0x2732CF8, 0x2732D08, 300, 100),
            NativeBattleEffectTaskPool.Initialize(0x273ACC8, 0x273AA48, 64, 10)
        ]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x8B0A50, 100);
        hierarchy = root.Hierarchy.Write(root.Address, root.Hierarchy.Read(root.Address).WriteDword(0x0C, 0x1D99A78));
        NativeBattleEffectHierarchyAppend target = hierarchy.Append8DC540(1, 0x8B0C70, 88, root.Address, (_, _, _) => new(0, 3));
        hierarchy = target.Hierarchy.Write(target.Address, target.Hierarchy.Read(target.Address).WriteWord(0x38, 10).WriteWord(0x3A, 20).WriteWord(0x3C, 30).WriteWord(0x3E, 40));
        NativeBattleEffectHierarchyAppend particle = hierarchy.Append8DC540(3, callback, 300, target.Address, (_, _, _) => new(0, 3));
        return (particle.Hierarchy, particle.Address);
    }
}
