using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedDescriptorResourceTests
{
    private static readonly MethodInfo ReadByte = typeof(BattleSession).GetMethod("ReadNativeDescriptorByte", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Theory]
    [InlineData(11)]
    [InlineData(36)]
    public void Descriptor_reads_hosted_scalar_and_null_fields_but_rejects_fabricated_section_addresses(int alias)
    {
        BattleModelRoster roster = Roster();
        NativeBattleModelCpuResources cpu = Resources(roster);
        NativeBattleModelCpuResourceRow native = cpu.Rows[0].Set(12, new(0, 0x24) { ResourceOnly = true });
        NativeBattleModelCpuResourceRow hosted = NativeBattleModelCpuResourceRow.Unknown().Set(12, null).Set(16, new(9, 0x24) { ResourceOnly = true }) with
        {
            PrivateLength4 = 0x03020100,
            SharedLength8 = 0x07060504,
            LinkedPhysicalIndex36 = 7
        };
        cpu = cpu with { Rows = cpu.Rows.SetItem(0, native).SetItem(alias, hosted) };
        cpu.Validate();
        BattleSession session = Session(cpu);
        NativeBattleGraphicsState graphics = new(NativeBattleGraphicsModels.ClearPresentationBank(roster).ToImmutableArray(), roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = roster };
        uint row = NativeBattleModelResourceRowAddress.Encode(alias, cpu.Rows.Length);
        Assert.Equal((byte)3, Read(session, graphics, row + 7));
        Assert.Equal((byte)6, Read(session, graphics, row + 10));
        Assert.Equal((byte)0, Read(session, graphics, row + 15));
        Assert.Equal((byte)7, Read(session, graphics, row + 36));

        uint nativeRow = NativeBattleModelResourceRowAddress.Encode(0);
        uint nativePointer = NativeBattlePartyCpuLayout.Classic.BaseAddress + 0x24;
        Assert.Equal(BitConverter.GetBytes(nativePointer), Enumerable.Range(0, 4).Select(offset => Read(session, graphics, nativeRow + 12 + (uint)offset)).ToArray());

        NotSupportedException section = Assert.IsType<NotSupportedException>(Assert.Throws<TargetInvocationException>(() => Read(session, graphics, row + 16)).InnerException);
        Assert.Contains("no numeric guest address", section.Message);
        uint unowned = NativeBattleModelResourceRowAddress.Encode(cpu.Rows.Length, NativeBattleModelResourceRowAddress.MaximumRowCount);
        NotSupportedException boundary = Assert.IsType<NotSupportedException>(Assert.Throws<TargetInvocationException>(() => Read(session, graphics, unowned)).InnerException);
        Assert.Contains("owned CPU alias array", boundary.Message);
    }

    [Fact]
    public void Descriptor_native_hole_reads_external_bytes_in_a_hosted_roster()
    {
        BattleModelRoster roster = Roster();
        BattleSession session = Session(NativeBattleModelCpuResources.Unknown());
        uint hole = NativeBattleGraphicsModelAddress.Encode(1);
        NativeBattleFileStaging staging = NativeBattleFileStaging.FromColdProcess().Write(hole, [0xB3]);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeFileStaging))!.SetValue(session, staging);
        NativeBattleGraphicsState graphics = new(NativeBattleGraphicsModels.ClearPresentationBank(roster).ToImmutableArray(), roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = roster };

        Assert.Equal((byte)0xB3, Read(session, graphics, hole));
    }

    [Fact]
    public void Victory_rebind_copies_typed_hosted_section_owners_without_numeric_pointer_bytes()
    {
        const int alias = 36;
        uint directory = NativeBattleFileStaging.Address;
        uint destination = directory + 128;
        byte[] dat = new byte[32];
        int[] fields = [3, 20, 24, 28, 32];
        for (int index = 0; index < fields.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(dat.AsSpan(4 * index), fields[index]);
        }
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(directory, dat).Write(destination, new byte[16]);
        NativeBattleModelCpuResources cpu = Resources(Roster());
        NativeBattleModelCpuReference skeleton = new(9, 0x40) { ResourceOnly = true };
        NativeBattleModelCpuReference geometry = new(9, 0x80) { ResourceOnly = true };
        cpu = cpu with { Rows = cpu.Rows.SetItem(alias, NativeBattleModelCpuResourceRow.Unknown().Set(12, skeleton).Set(16, geometry)) };
        cpu.Validate();
        NativeBattleGraphicsModel model = new(7, 2) { ModelId4 = 0, Flags2 = 4, ActiveResourceRow100 = alias, SequenceState = new() { Default2 = 6 } };

        NativeBattleVictoryResourceBinding bound = NativeBattleVictoryResources.Bind5022C0(staging, cpu, model, destination, directory);
        Assert.Equal(destination, bound.Model.ActiveStagedResourceRow100);
        Assert.Equal(skeleton, bound.Staging.ReadResourcePointer(destination)!.Cpu);
        Assert.Equal(geometry, bound.Staging.ReadResourcePointer(destination + 4)!.Cpu);
        Assert.All(bound.Staging.ReadSnapshot(destination, 8).Known, known => Assert.False(known));
        Assert.Equal(directory + 20, bound.Staging.ReadUInt32(destination + 8));
        Assert.Throws<InvalidDataException>(() => NativeBattleVictoryResources.Bind5022C0(staging, cpu, model with { ActiveResourceRow100 = cpu.Rows.Length }, destination, directory));
    }

    private static byte Read(BattleSession session, NativeBattleGraphicsState graphics, uint address)
    {
        return (byte)ReadByte.Invoke(session, [graphics, address])!;
    }

    private static BattleSession Session(NativeBattleModelCpuResources cpu)
    {
        BattleDefinition definition = new()
        {
            Combatants =
            [
                new() { Id = "ally", Name = "Ally", IsPlayer = true },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeModelCpuResources))!.SetValue(session, cpu);
        return session;
    }

    private static NativeBattleModelCpuResources Resources(BattleModelRoster roster)
    {
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown().WithPresentationRoster(roster);
        return cpu with { Rows = cpu.Rows.AddRange(Enumerable.Range(0, 37 - cpu.Rows.Length).Select(_ => NativeBattleModelCpuResourceRow.Unknown())) };
    }

    private static BattleModelRoster Roster()
    {
        BattleModelOwner?[] rows = new BattleModelOwner?[25];
        rows[0] = new("ally", true);
        rows[7] = new("extra", true);
        for (int row = 8; row < rows.Length; row++)
        {
            rows[row] = new($"enemy-{row}", false);
        }
        return new(rows.ToImmutableArray());
    }
}
