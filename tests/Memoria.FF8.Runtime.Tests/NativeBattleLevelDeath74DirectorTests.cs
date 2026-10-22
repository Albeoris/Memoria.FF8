using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLevelDeath74DirectorTests
{
    [Theory]
    [InlineData(14, 0, 1, 0)]
    [InlineData(14, 1, 1, 0)]
    [InlineData(74, 0, 12, 2)]
    [InlineData(74, 1, 1, 0)]
    public void Setup_uploads_then_binds_and_schedules_caster_before_the_native_delay(int effect, int repetition, int delay, int operations)
    {
        var (frame, resources, address) = Fixture((ushort)effect, (byte)repetition);
        List<NativeBattleDeathOperation> observed = [];
        NativeBattleDeathFrame result = NativeBattleDeathController.InitializeDirector(frame, address, resources, Execute);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(result.Effect.Pools).Read(address);
        Assert.Equal((ushort)delay, task.ReadWord(0x44));
        Assert.Equal((byte)(operations == 0 ? 1 : 10), task.ReadByte(0x29));
        Assert.Equal(repetition == 0 ? 2 : 0, result.Memory.Textures!.Count);
        Assert.Equal(operations, observed.Count);
        if (operations != 0)
        {
            NativeBattleDeathBindCaster bind = Assert.IsType<NativeBattleDeathBindCaster>(observed[0]);
            Assert.Equal((byte)2, bind.Actor);
            Assert.Equal(0x25F3F00u, bind.SavedRow);
            Assert.Equal(0x15863B0u, bind.Directory);
            Assert.Equal(new NativeBattleDeathRingSequence(2, 1), observed[1]);
        }

        NativeBattleDeathFrame Execute(NativeBattleDeathFrame current, NativeBattleDeathOperation operation)
        {
            Assert.Equal(2, current.Memory.Textures!.Count);
            NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
            NativeBattleModulePoolTask before = hierarchy.Read(address);
            if (observed.Count == 0)
            {
                Assert.Equal((byte)0, before.ReadByte(0x29));
                hierarchy = hierarchy.Write(address, before.WriteByte(0x29, 9));
            }
            else
            {
                Assert.Equal((byte)9, before.ReadByte(0x29));
            }
            observed.Add(operation);
            return current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
        }
    }

    [Fact]
    public void First_level_setup_requires_caster_resource_and_foreign_director_is_rejected()
    {
        var (frame, resources, address) = Fixture(74, 0);
        Assert.Throws<NotSupportedException>(() => NativeBattleDeathController.InitializeDirector(frame, address, resources with { CasterResource = null }, (_, _) => throw new InvalidOperationException()));
        Assert.Throws<InvalidDataException>(() => NativeBattleDeathController.InitializeDirector(frame with { Effect = frame.Effect with { Binding = frame.Effect.Binding with { Effect = 14 } } }, address, resources, (_, _) => throw new InvalidOperationException()));
    }

    private static (NativeBattleDeathFrame Frame, NativeBattleDeathResources Resources, uint Address) Fixture(ushort effect, byte repetition)
    {
        NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect(effect);
        NativeBattleEffectHierarchy hierarchy = new(profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(2, profile.Callback(0x8B6E30), 72);
        hierarchy = appended.Hierarchy;
        hierarchy = hierarchy.Write(appended.Address, hierarchy.Read(appended.Address).WriteByte(0x2E, repetition).WriteByte(0x2C, 2).WriteByte(0x29, 0));
        NativeBattleOriginalEffectState original = new(effect, effect, new(effect, profile.Entry, profile.Initializer), null, null) { Pools = hierarchy.Pools };
        NativeBattleDeathResources resources = new(new("unused", []), null!, [], [], Tim(), Tim(), null!, [], 0, 0) { Effect = effect, CasterResource = effect == 74 ? new(0, 0x15863B0, 0, 0, []) : null };
        return (new(NativeBattleEffectMemory.FromColdProcess(), original, new(1)), resources, appended.Address);
    }

    private static NativeBattleEffectHeapAllocation Tim()
    {
        byte[] bytes = new byte[32];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 12);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), 12);
        return new("synthetic", bytes.ToImmutableArray());
    }
}
