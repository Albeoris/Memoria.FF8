using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleOffsetBodySourceTests
{
    [Theory]
    [InlineData(290, 1, false)]
    [InlineData(290, 2, false)]
    [InlineData(290, 3, false)]
    [InlineData(290, 4, false)]
    [InlineData(298, 1, false)]
    [InlineData(298, 2, false)]
    [InlineData(298, 3, false)]
    [InlineData(298, 4, false)]
    [InlineData(308, 1, false)]
    [InlineData(308, 2, false)]
    [InlineData(308, 3, false)]
    [InlineData(308, 4, false)]
    [InlineData(290, 1, true)]
    [InlineData(290, 2, true)]
    [InlineData(290, 3, true)]
    [InlineData(290, 4, true)]
    [InlineData(298, 1, true)]
    [InlineData(298, 2, true)]
    [InlineData(298, 3, true)]
    [InlineData(298, 4, true)]
    [InlineData(308, 1, true)]
    [InlineData(308, 2, true)]
    [InlineData(308, 3, true)]
    [InlineData(308, 4, true)]
    public void Production_selection_retains_actual_current_row_or_staged_pinned_publication(int family, int domain, bool staged)
    {
        byte[] bytes = Enumerable.Range(0, 16).Select(value => (byte)value).ToArray();
        NativeBattleOffsetBodyResourceFixture fixture = NativeBattleOffsetBodyResourceFixture.Create(bytes, domain);
        NativeBattleModelCpuResources cpu = fixture.Current;
        NativeBattleResourcePointer pointer = fixture.Pointer;
        if (!staged)
        {
            NativeBattleModelCpuResourceRow row = cpu.Rows[0].Set(16, pointer.Cpu) with { Lengths = ImmutableDictionary<int, int>.Empty.Add(16, bytes.Length) };
            cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, pointer.CpuPublication!) }, Rows = cpu.Rows.SetItem(0, row) };
        }
        BattleDefinition definition = new()
        {
            NativeCrtRandom = new(42),
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, MaximumHp = 100, CurrentHp = 100 }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 100, CurrentHp = 100 }]
        };
        BattleSession session = new(definition, BattleAtbMode.Normal);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeModelCpuResources))!.SetValue(session, cpu);
        uint rowAddress = NativeBattleFileStaging.Address + 0x1000;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        if (staged)
        {
            staging = staging.WriteResourcePointer(rowAddress + 4, pointer);
        }
        NativeBattleGraphicsState graphics = new([new(0, 3) { ActiveResourceRow100 = staged ? null : 0, ActiveStagedResourceRow100 = staged ? rowAddress : null }], [], 0, 0);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleOriginalEffectState effect = new((ushort)family, family - 1, new((ushort)family, 1, 1), null, 0);
        object frame = family switch
        {
            290 => new NativeBattleEffect290Frame(memory, effect, new(42), NativeBattleEffect290State.FromColdProcess()) { Staging = staging },
            298 => new NativeBattleEffect298Frame(memory, effect, NativeBattleEffect298State.FromColdProcess()) { Staging = staging },
            308 => new NativeBattleEffect308Frame(memory, effect, new(42), NativeBattleEffect308State.FromColdProcess()) { Staging = staging },
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
        MethodInfo select = typeof(BattleSession).GetMethod($"ReadNativeEffect{family}Geometry", BindingFlags.Instance | BindingFlags.NonPublic)!;
        NativeBattleModelGeometrySource source = (NativeBattleModelGeometrySource)select.Invoke(session, [graphics, frame, 0])!;
        Assert.Same(pointer.CpuPublication, source.Owner.CpuPublication);
        Assert.Equal(bytes, source.Read(0, bytes.Length));
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeModelCpuResources))!.SetValue(session, fixture.Current);
        Assert.Equal(bytes, source.Read(0, bytes.Length));
        Assert.Throws<InvalidDataException>(() => source.Read(bytes.Length, 1));
        NativeBattleEffectArena context = new NativeBattleModelGeometryCursor(source, 4).Write(NativeBattleEffectArena.Clear(), 0);
        staging = NativeBattleOffsetBodyContextMemory.Publish(staging, NativeBattleTemporaryMemory.Address, context, 4);
        fixture.AssertCursor(staging, NativeBattleTemporaryMemory.Address, 4, bytes);
    }
}
