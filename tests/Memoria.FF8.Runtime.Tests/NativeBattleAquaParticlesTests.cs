using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAquaParticlesTests
{
    [Theory]
    [InlineData(12)]
    [InlineData(15)]
    public void Animation_draws_the_last_frame_then_hides_and_releases_on_the_strict_greater_endpoint(ushort bound)
    {
        NativeBattleModulePoolTask task = Task().WriteWord(0x50, (ushort)(bound - 1)).WriteWord(0x52, bound).WriteByte(0x29, 1);
        task = NativeBattleAquaParticles.AdvanceAnimation(task);
        Assert.Equal(bound, task.ReadWord(0x50));
        Assert.Equal(0, task.ReadByte(0x26));
        Assert.Equal(1, task.ReadByte(0x29));
        task = NativeBattleAquaParticles.AdvanceAnimation(task);
        Assert.Equal(bound, task.ReadWord(0x50));
        Assert.Equal(5, task.ReadByte(0x26));
        Assert.Equal(2, task.ReadByte(0x29));
    }

    [Fact]
    public void Negative_damping_uses_truncation_and_preserves_the_fourth_words()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(0x80, 0x7FFF).WriteWord(0x82, 123).WriteWord(0x84, 0x8000).WriteWord(0x86, 0xABCD).WriteWord(0xA0, unchecked((ushort)-31)).WriteWord(0xA2, 7).WriteWord(0xA4, 31).WriteWord(0xA6, 0xDCBA);
        task = NativeBattleAquaParticles.AdvanceMotionRecord(task, 0, false);
        Assert.Equal(-28, unchecked((short)task.ReadWord(0xA0)));
        Assert.Equal(7, task.ReadWord(0xA2));
        Assert.Equal(28, task.ReadWord(0xA4));
        Assert.Equal(32766, task.ReadWord(0x80));
        Assert.Equal(123, task.ReadWord(0x82));
        Assert.Equal(-32767, unchecked((short)task.ReadWord(0x84)));
        Assert.Equal(0xABCD, task.ReadWord(0x86));
        Assert.Equal(0xDCBA, task.ReadWord(0xA6));
    }

    [Fact]
    public void Plume_gravity_wraps_the_word_before_damping_and_motion()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(0x82, 32760).WriteWord(0xA2, 32760);
        task = NativeBattleAquaParticles.AdvanceMotionRecord(task, 0, true);
        Assert.Equal(-28609, unchecked((short)task.ReadWord(0xA2)));
        Assert.Equal(30972, unchecked((short)task.ReadWord(0x82)));
    }

    [Fact]
    public void Caster_birth_consumes_twelve_CRT_draws_and_copies_its_actual_anchor_and_matrix_padding()
    {
        NativeBattleModulePoolTask parent = NativeBattleReflectController.WriteMatrix(Task(), 0x58, NativeBattleCuragaMatrices.Identity8DD770()).WriteWord(0x6A, 0xBEEF).WriteWord(0x1C, 123).WriteWord(0x1E, unchecked((ushort)-456)).WriteWord(0x20, 789).WriteWord(0x22, 0xCAFE);
        NativeCrtRandomState random = NativeCrtRandomState.FromColdProcess();
        NativeBattleModulePoolTask child = NativeBattleAquaParticles.InitializeCasterBirth(parent, Task(), ref random);
        Assert.Equal(After(12), random);
        Assert.Equal(2238, child.ReadWord(0x148));
        Assert.Equal(parent.ReadBytes(0x1C, 8), child.ReadBytes(0x78, 8));
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal(0xBEEF, child.ReadWord(0xC0 + index * 32 + 18));
        }
    }

    [Fact]
    public void Caster_birth_copies_unresolved_matrix_padding_and_anchor_fourth_word_masks()
    {
        NativeBattleModulePoolTask parent = NativeBattleReflectController.WriteMatrix(Task(), 0x58, NativeBattleCuragaMatrices.Identity8DD770()).MarkPayloadUnknown(0x6A, 2).MarkPayloadUnknown(0x22, 2);
        NativeCrtRandomState random = NativeCrtRandomState.FromColdProcess();
        NativeBattleModulePoolTask child = NativeBattleAquaParticles.InitializeCasterBirth(parent, Task(), ref random);
        Assert.Equal(After(12), random);
        Assert.Throws<NotSupportedException>(() => child.ReadWord(0x7E));
        for (int index = 0; index < 4; index++)
        {
            Assert.Throws<NotSupportedException>(() => child.ReadWord(0xC0 + index * 32 + 18));
            Assert.Equal(18, child.ReadBytes(0xC0 + index * 32, 18).Length);
        }
    }

    [Theory]
    [InlineData(false, 2, 10)]
    [InlineData(true, 4, 32)]
    public void Target_birth_samples_all_original_bones_before_velocity_random_draws(bool spray, int count, int draws)
    {
        NativeBattleModulePoolTask parent = NativeBattleReflectController.WriteMatrix(Task(), 0x58, NativeBattleCuragaMatrices.Identity8DD770());
        List<(short Bone, int Fraction)> anchors = [];
        NativeCrtRandomState random = NativeCrtRandomState.FromColdProcess();
        NativeBattleModulePoolTask destination = Task().WriteWord(0x86, 0x8001).WriteWord(0x8E, 0x8002).WriteWord(0x96, 0x8003).WriteWord(0x9E, 0x8004);
        NativeBattleModulePoolTask child = NativeBattleAquaParticles.InitializeTargetBirth(parent, destination, spray, 70, (bone, fraction) =>
        {
            anchors.Add((bone, fraction));
            return new(100, 200, 300, unchecked((short)(0x7000 + anchors.Count)));
        }, ref random);
        Assert.Equal(After(draws), random);
        Assert.Equal(count, anchors.Count);
        Assert.Equal(((short)41, 2083), anchors[0]);
        for (int index = 0; index < count; index++)
        {
            Assert.Equal(0x8001 + index, child.ReadWord(0x86 + index * 8));
        }
    }

    [Theory]
    [InlineData(31, 1009, 16)]
    [InlineData(-31, 1039, -2)]
    public void Wave_deformation_uses_signed_truncation_and_retains_source_padding_without_touching_faces(short z, short phase, short expectedX)
    {
        byte[] vertices = new byte[2504];
        BinaryPrimitives.WriteInt32LittleEndian(vertices.AsSpan(), 2504);
        BinaryPrimitives.WriteInt32LittleEndian(vertices.AsSpan(4), 312);
        BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(8), 7);
        BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(12), z);
        BinaryPrimitives.WriteUInt16LittleEndian(vertices.AsSpan(14), 0xBEEF);
        byte[] mesh = Enumerable.Repeat((byte)0xCD, 10600).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 312);
        NativeBattleAquaDeformation result = NativeBattleAquaParticles.DeformWave(mesh.ToImmutableArray(), vertices.ToImmutableArray(), phase, 0);
        Assert.Equal(expectedX, BinaryPrimitives.ReadInt16LittleEndian(result.Mesh.AsSpan().Slice(8)));
        Assert.Equal(z, BinaryPrimitives.ReadInt16LittleEndian(result.Mesh.AsSpan().Slice(12)));
        Assert.Equal(0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(result.Mesh.AsSpan().Slice(14)));
        Assert.Equal(mesh.AsSpan(2504).ToArray(), result.Mesh.AsSpan().Slice(2504).ToArray());
        Assert.Equal(312, result.Inputs.Length);
        Assert.Equal(unchecked((short)0xBEEF), result.InputWs[0]);
        Assert.Equal(7, BinaryPrimitives.ReadInt16LittleEndian(vertices.AsSpan(8)));
    }

    [Theory]
    [InlineData(55, 0x80B730u, false)]
    [InlineData(55, 0x80C1B0u, false)]
    [InlineData(55, 0x80C5A0u, false)]
    [InlineData(72, 0x80B730u, true)]
    [InlineData(72, 0x80C1B0u, true)]
    [InlineData(72, 0x80C5A0u, true)]
    public void Last_emitter_birth_retains_same_pool_links_and_child_count_until_every_descendant_releases(ushort effect, uint callback, bool existingChild)
    {
        NativeBattleAquaProfile profile = NativeBattleAquaProfile.ForEffect(effect);
        NativeBattleEffectHierarchy hierarchy = new(profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, profile.Callback(0x808970), 100);
        hierarchy = root.Hierarchy.Write(root.Address, root.Hierarchy.Read(root.Address).WriteDword(0x0C, 0x1D99A78));
        NativeBattleEffectHierarchyAppend emitter = hierarchy.Append8DC540(3, profile.Callback(callback), 340, root.Address, (_, _, _) => new(0, 3));
        hierarchy = emitter.Hierarchy;
        NativeBattleModulePoolTask task = NativeBattleReflectController.WriteMatrix(hierarchy.Read(emitter.Address), 0x58, NativeBattleCuragaMatrices.Identity8DD770()).WriteByte(0x29, 1).WriteWord(0x24, callback == 0x80B730 ? (ushort)16 : callback == 0x80C1B0 ? (ushort)18 : (ushort)28);
        hierarchy = hierarchy.Write(emitter.Address, task);
        if (existingChild)
        {
            NativeBattleEffectHierarchyAppend previous = hierarchy.Append8DC540(3, profile.Callback(0x80C430), 340, emitter.Address, (_, _, _) => new(0, 3));
            hierarchy = previous.Hierarchy.Write(previous.Address, previous.Hierarchy.Read(previous.Address).WriteByte(0x29, 1));
        }
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 8, 0, 0, 0, effect, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Aqua55 = NativeBattleAquaState.FromColdProcess(55), Aqua72 = NativeBattleAquaState.FromColdProcess(72) };
        NativeBattleOriginalEffectState selected = new(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), new(0, 1, 8, 0, 0, 0), profile.Pools[0].Descriptor) { Pools = hierarchy.Pools };
        NativeBattleAquaFrame frame = new(memory, selected, NativeCrtRandomState.FromColdProcess());
        NativeBattleActionMatrix camera = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleAquaInputs inputs = new(packets, camera, new(camera, 256, 160, 108), 0, NativeBattleGteColorInterpolation.CodeConstants45F270, index => new(index, 2) { Rotation14 = 0 }, _ => new(new byte[] { 1 }.ToImmutableArray(), []), (_, _, _) => new(0, 0, 0, 0), (current, _) => current);
        NativeBattleAquaResources resources = new(effect, new("camera", []), ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, [], [], 0, null);
        Advance();
        Assert.Equal((byte)1, hierarchy.Read(root.Address).ReadByte(0x28));
        Assert.Equal((byte)1, hierarchy.Read(emitter.Address).ReadByte(0x28));
        Assert.Equal((byte)2, hierarchy.Read(emitter.Address).ReadByte(0x29));
        Assert.Equal(2, hierarchy.Pools[3].Slots.Count(slot => (slot.Flags & 1) != 0));
        for (int callbackIndex = 0; callbackIndex < 64 && hierarchy.Pools[3].Head is not null; callbackIndex++)
        {
            Advance();
        }
        Assert.Null(hierarchy.Pools[3].Head);
        Assert.Null(hierarchy.Pools[3].Tail);
        Assert.All(hierarchy.Pools[3].Slots, slot => Assert.Equal(0, slot.Flags & 1));
        Assert.Equal((byte)0, hierarchy.Read(root.Address).ReadByte(0x28));

        void Advance()
        {
            NativeBattleEffectHierarchyStep step = hierarchy.Advance(3, (current, address) =>
            {
                NativeBattleAquaParticleStep advanced = NativeBattleAquaParticles.Advance(frame with { Effect = frame.Effect with { Pools = current.Pools } }, current, address, resources, inputs);
                frame = advanced.Frame;
                return new(advanced.Hierarchy, advanced.Result);
            });
            hierarchy = step.Hierarchy;
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        }
    }

    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 1, new byte[328].ToImmutableArray());

    private static NativeCrtRandomState After(int count)
    {
        NativeCrtRandomState result = NativeCrtRandomState.FromColdProcess();
        for (int index = 0; index < count; index++)
        {
            result = result.Draw(out _);
        }
        return result;
    }
}
