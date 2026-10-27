using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDeathFamilyProfileTests
{
    [Theory]
    [InlineData(14, 0x8B68C0u, 0x273B0B8u, 0x273AFF0u, false)]
    [InlineData(74, 0x7C4460u, 0x25EC750u, 0x25EC688u, true)]
    public void Profiles_retain_actual_roots_pools_callbacks_and_selected_contact_policy(int effect, uint entry, uint descriptor, uint bank, bool single)
    {
        NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect((ushort)effect);
        Assert.Equal(entry, profile.Entry);
        Assert.Equal(descriptor, profile.RootDescriptor);
        Assert.Equal(bank, profile.RootBank);
        Assert.Equal(single, profile.SingleSelectedTarget);
        Assert.Equal(new ushort[] { 100, 88, 72, 320, 676, 112, 280, 64 }, profile.Pools.Select(pool => pool.Stride));
        Assert.Equal(new ushort[] { 2, 4, 3, 3, 4, 10, 50, 10 }, profile.Pools.Select(pool => pool.Capacity));
        foreach (uint callback in new uint[] { 0x8B6A40, 0x8B6C90, 0x8B6E30, 0x8B6F70, 0x8B78C0, 0x8BD580, 0x8BD840, 0x8BDBD0, 0x8BDF40 })
        {
            Assert.Equal(callback, profile.ReferenceCallback(profile.Callback(callback)));
        }
        Assert.Throws<NotSupportedException>(() => profile.Callback(0x8C5350));
        Assert.Throws<NotSupportedException>(() => profile.ReferenceCallback(0x8C5350));
    }

    [Fact]
    public void LevelDeath_global_segments_preserve_the_inserted_caster_row_and_separate_retained_owners()
    {
        NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect(74);
        Assert.Equal(0x25F3EFCu, profile.ModuleAddress(0x2742864));
        Assert.Equal(0x25F3F20u, profile.ModuleAddress(0x2742868));
        Assert.Equal(0x25F3F24u, profile.ModuleAddress(0x274286C));
        Assert.Equal(0x25F3F2Cu, profile.ModuleAddress(0x2742874));
        Assert.Equal(0x25F3F00u, profile.SavedCasterRow);
        Assert.Equal(0x15863B0u, profile.CasterDirectory);
        Assert.Equal(0x7A40, profile.ModuleLength);
        Assert.Throws<ArgumentOutOfRangeException>(() => profile.ModuleAddress(0x27428A0));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleDeathState death = NativeBattleDeathState.FromColdProcess();
        NativeBattleDeathState level = NativeBattleDeathState.FromColdProcess();
        memory = memory with { Death14 = death, LevelDeath74 = level };
        NativeBattleDeathState changed = level with { Module = level.Module.Write(profile.ModuleOffset(0x274286C), [0x12, 0x34]) };
        memory = profile.WithState(memory, changed);
        Assert.Same(death, memory.Death14);
        Assert.Same(changed, profile.RequireState(memory));
        Assert.Same(level, profile.RequireState(memory with { LevelDeath74 = level }));
        Assert.Throws<NotSupportedException>(() => profile.RequireState(memory with { LevelDeath74 = null }));
        string json = NativeBattleTestSnapshots.Json(changed);
        Assert.Equal(json, NativeBattleTestSnapshots.Json(NativeBattleTestSnapshots.RestoreJson<NativeBattleDeathState>(json)));
    }

    [Theory]
    [InlineData(74, 0, 1, 3, 0, 2, 3)]
    [InlineData(74, 0, 2, 2, 0, 2, 5)]
    [InlineData(74, 4, 0, 0, 4, 0, 5)]
    [InlineData(74, 4, 0, -32768, 4, 0, 5)]
    [InlineData(74, 7, 255, 1, 7, 0, 3)]
    [InlineData(74, 7, 127, 129, 7, 128, 3)]
    [InlineData(14, 0, 2, 3, 1, 2, 3)]
    [InlineData(14, 2, 0, 2, 2, 0, 5)]
    public void Root_loop_uses_original_signed_selector_and_count_widths(int effect, int header, int contact, int count, int expectedHeader, int expectedContact, int expectedPhase)
    {
        NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect((ushort)effect);
        NativeBattleModulePoolTask task = RootTask().WriteByte(0x2A, (byte)header).WriteByte(0x2B, (byte)contact).WriteByte(0x2E, 255).WriteByte(0x29, 4).WriteWord(0x5A, unchecked((ushort)count)).WriteWord(0x58, unchecked((ushort)count));
        NativeBattleModulePoolTask result = profile.AdvanceTargetLoop(task);
        Assert.Equal((byte)expectedHeader, result.ReadByte(0x2A));
        Assert.Equal((byte)expectedContact, result.ReadByte(0x2B));
        Assert.Equal((byte)expectedPhase, result.ReadByte(0x29));
        Assert.Equal(expectedPhase == 3 ? (byte)0 : (byte)255, result.ReadByte(0x2E));
    }

    [Fact]
    public void Root_loop_waits_for_actual_completion_byte_without_touching_selectors()
    {
        NativeBattleModulePoolTask task = RootTask().WriteByte(0x63, 1).WriteByte(0x2A, 7).WriteByte(0x2B, 3).WriteByte(0x29, 4);
        Assert.Same(task, NativeBattleDeathFamilyProfile.ForEffect(14).AdvanceTargetLoop(task));
        Assert.Same(task, NativeBattleDeathFamilyProfile.ForEffect(74).AdvanceTargetLoop(task));
    }

    [Theory]
    [InlineData(14, 2, 3)]
    [InlineData(74, 1, 4)]
    public void Context_initialization_uses_the_actual_contact_selector_only_for_level_death(int effect, int expectedCount, int expectedActor)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 4, 0, 8, (ushort)effect, null));
        foreach (byte actor in new byte[] { 3, 4 })
        {
            (packets, _) = packets.AppendContact(new(new(actor, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish(1);
        NativeBattleModulePoolTask root = RootTask().WriteWord(0x5A, 2);
        NativeBattleModulePoolTask task = new(1, 0, null, 0, new byte[676 - 12].ToImmutableArray());
        task = task.WriteByte(0x2A, 0).WriteByte(0x2B, 1).WriteByte(0x2C, 0).WriteWord(0x29C, 2).WriteWord(0x29E, 20).WriteDword(0x34 + 8, 0xDEADBEEF);
        List<int> reads = [];
        NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect((ushort)effect);
        NativeBattleModulePoolTask initialized = NativeBattleEffectNodeContext.Initialize(task, root, new(0, 1, 0x18, 0, 0, 0), packets, Model, _ => throw new InvalidOperationException(), profile.AuraAddress, singleSelectedTarget: profile.SingleSelectedTarget, dogOrigins: new(new(-1, 0, -2), new(7, 0, 8)));
        Assert.Equal((ushort)expectedCount, initialized.ReadWord(0x34 + 0x1C));
        Assert.Equal((uint)expectedActor, initialized.ReadDword(0x34 + 4));
        Assert.Equal(profile.AuraAddress + 4, initialized.ReadDword(0x34 + 0x224));
        if (effect == 74)
        {
            Assert.Equal(0xDEADBEEFu, initialized.ReadDword(0x34 + 8));
            Assert.DoesNotContain(3, reads);
        }
        else
        {
            Assert.Equal(4u, initialized.ReadDword(0x34 + 8));
            Assert.Contains(3, reads);
        }

        NativeBattleGraphicsModel Model(int actor)
        {
            reads.Add(actor);
            return new(actor, 0) { Transform = new(new(0, 0, 0), new((short)(actor * 100), 0, (short)actor), 0, 0, 4096), GeometryBounds52 = new(new(-1, -2, -3), new(1, 2, 3)) };
        }
    }

    private static NativeBattleModulePoolTask RootTask() => new(1, 0, null, 0, new byte[100 - 12].ToImmutableArray());
}
