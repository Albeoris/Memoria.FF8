using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedModelCpuBanksTests
{
    private static readonly BattlePartyModelOwner NativeParty = (BattlePartyModelOwner)OriginalOwner(0);
    private static readonly BattlePartyModelOwner FirstExtra = new BattlePartyActorModel(new("first"));
    private static readonly BattlePartyModelOwner SecondExtra = new BattlePartyActorModel(new("second"));
    private static readonly BattlePartyModelOwner ThirdExtra = new BattlePartyActorModel(new("third"));
    private static readonly BattlePartyModelOwner Unbound = new BattlePartyActorModel(new("unbound"));
    private static readonly BattleModelOwner Enemy = OriginalOwner(4);
    private static readonly BattleModelRoster Roster = new([NativeParty, FirstExtra, Enemy, SecondExtra, ThirdExtra]);
    private static readonly NativeBattleModelAliases Aliases = new(Roster, [(NativeBattleModelSlot.Decode(0), NativeParty), (NativeBattleModelSlot.Decode(4), Enemy)]);

    [Fact]
    public void Sparse_roster_maps_party_rows_without_aliasing_native_or_enemy_banks()
    {
        NativeBattleModelCpuBanks original = NativeBattleModelCpuBanks.Unknown().WithBank(0, NativeBattleModelCpuBank.Unknown().WriteGuest(0, [10]));
        NativeBattleModelCpuBanks banks = original.WithPresentationRoster(Roster, Aliases);
        banks.Validate();
        Assert.Equal(3, banks.Banks.Length);
        Assert.Same(original.Banks[0], banks.GetBank(0));
        Assert.Equal(0, banks.PartyBankIndex(NativeParty));
        Assert.Equal(5, banks.PartyBankIndex(FirstExtra));
        Assert.Equal(6, banks.PartyBankIndex(SecondExtra));
        Assert.Equal(7, banks.PartyBankIndex(ThirdExtra));
        Assert.Equal(new[] { 5, 6, 7 }, banks.PresentationBanks!.Keys.Order().ToArray());
        Assert.Throws<InvalidDataException>(() => banks.PartyBankIndex(Unbound));
        Assert.DoesNotContain(Enemy, banks.PartyBankOwners.Keys.Cast<BattleModelOwner>());
        Assert.Throws<InvalidDataException>(() => banks.GetBank(3));
        Assert.Throws<InvalidDataException>(() => banks.GetBank(4));
        Assert.Throws<InvalidDataException>(() => banks.CopyBank50B2A0(0, checked((byte)banks.PartyBankIndex(SecondExtra))));
        Assert.Same(banks.PartyBankOwners, banks.WithPresentationRoster(Roster, Aliases).PartyBankOwners);
    }

    [Fact]
    public void Section_writes_and_geometry_edits_preserve_distinct_pinned_bytes()
    {
        NativeBattleModelCpuResources cpu = Resources();
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleResourcePointer original = new(new(0, 12), null, 2);
        NativeBattleResourcePointer second = Pointer(cpu.Cpu.PartyBankIndex(SecondExtra), 12, 2);
        int bank = cpu.Cpu.PartyBankIndex(ThirdExtra);
        NativeBattleResourcePointer third = Pointer(bank, 12, 2);
        cpu = NativeBattleModelResourceView.WritePartySection(cpu, staging, original, 0, [1, 2]);
        cpu = NativeBattleModelResourceView.WritePartySection(cpu, staging, second, 0, [3, 4]);
        cpu = NativeBattleModelResourceView.WritePartySection(cpu, staging, third, 0, [5, 6]);
        NativeBattleResourcePointer pinned = third.Pin(cpu);
        NativeBattleModelResourceEditor edit = new(cpu, staging, third);
        edit.Write(1, [99]);
        Assert.Equal(new byte[] { 5, 99 }, edit.Geometry.Read(0, 2));
        Assert.Equal(new byte[] { 5, 6 }, NativeBattleModelResourceView.Open(edit.Current, staging, pinned, 2).Read(0, 2));
        Assert.Equal(new byte[] { 1, 2 }, NativeBattleModelResourceView.Open(edit.Current, staging, original, 2).Read(0, 2));
        Assert.Equal(new byte[] { 3, 4 }, NativeBattleModelResourceView.Open(edit.Current, staging, second, 2).Read(0, 2));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.WritePartySection(edit.Current, staging, pinned, 0, [8]));
        Assert.Throws<NotSupportedException>(() => edit.Current.Cpu.GetBank(bank).ReadGuest(12, 2));

        NativeBattleModelCpuResources restored = NativeBattleTestSnapshots.Independent(edit.Current);
        restored.Validate();
        Assert.Equal(bank, restored.Cpu.PartyBankIndex(ThirdExtra));
        Assert.Equal(new byte[] { 5, 99 }, NativeBattleModelResourceView.Open(restored, staging, third, 2).Read(0, 2));
    }

    [Fact]
    public void Unknown_and_out_of_extent_hosted_sections_are_rejected()
    {
        NativeBattleModelCpuResources cpu = Resources();
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        int bank = cpu.Cpu.PartyBankIndex(ThirdExtra);
        NativeBattleResourcePointer pointer = Pointer(bank, NativeBattleModelCpuBank.Length - 1, 1);
        NativeBattleModelResourceView view = NativeBattleModelResourceView.Open(cpu, staging, pointer, 1);
        Assert.False(Assert.Single(view.ReadSnapshot(0, 1).Known));
        Assert.Throws<NotSupportedException>(() => view.Read(0, 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.Open(cpu, staging, Pointer(bank, NativeBattleModelCpuBank.Length, 1), 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.Open(cpu, staging, Pointer(bank, int.MaxValue, 2), 2));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.Open(cpu, staging, Pointer(10, 0, 1), 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.Open(cpu, staging, pointer with { Cpu = pointer.Cpu! with { Layout = NativeBattlePartyCpuLayout.Remastered } }, 1));

        var row = cpu.Rows[0].Set(12, pointer.Cpu);
        (cpu with { Rows = cpu.Rows.SetItem(0, row) }).Validate();
        Assert.Throws<InvalidDataException>((cpu with { Rows = cpu.Rows.SetItem(0, row.Set(12, Pointer(bank, int.MaxValue, 1).Cpu)) }).Validate);
    }

    [Fact]
    public void Inconsistent_extension_owners_and_nonresource_banks_are_rejected()
    {
        NativeBattleModelCpuBanks banks = Resources().Cpu;
        ImmutableDictionary<int, NativeBattleModelCpuBank> extras = banks.PresentationBanks!;
        int bank = banks.PartyBankIndex(ThirdExtra);
        NativeBattleModelCpuBank extension = extras[bank];
        Assert.Throws<InvalidDataException>((banks with { PresentationBanks = extras.Remove(bank) }).Validate);
        foreach (int invalid in new[] { 0, 3, 4, 10, 27 })
        {
            Assert.Throws<InvalidDataException>((banks with { PresentationBanks = extras.Remove(bank).Add(invalid, extension) }).Validate);
        }
        BattleModelRoster different = new(Roster.Owners.Select(owner => owner == ThirdExtra ? new BattlePartyActorModel(new("replacement")) : owner));
        NativeBattleModelAliases differentAliases = new(different, [(NativeBattleModelSlot.Decode(0), NativeParty)]);
        Assert.Throws<InvalidDataException>(() => banks.WithPresentationRoster(different, differentAliases));
        Assert.Throws<InvalidDataException>(() => banks.WithBank(bank, NativeBattleModelCpuBank.Unknown()));
        Assert.Throws<InvalidDataException>(() => banks.WithBank(bank, NativeBattleModelCpuBank.Unknown(1).PromoteResourceStorage()));
        Assert.Throws<InvalidDataException>(() => banks.WithBank(bank, extension.WriteGuest(0, [5])));
    }

    [Fact]
    public void Hosted_resource_pointer_retains_metadata_without_native_address_bytes()
    {
        NativeBattleModelCpuReference reference = new(26, 4) { ResourceOnly = true };
        NativeBattleResourcePointer pointer = new NativeBattleResourcePointer(reference, null, 1).WithNativeAddressEncoding();
        Assert.True(pointer.HasValidOwner());
        Assert.True(reference.IsOwnedResource);
        Assert.Null(reference.TryPhysicalAddress());
        Assert.Null(pointer.NativeByteValue);
        Assert.False((pointer with { Cpu = reference with { ResourceOnly = false } }).HasValidOwner());
        Assert.False((pointer with { Cpu = reference with { EnemyResourceId = 0 } }).HasValidOwner());
        Assert.False((pointer with { Cpu = reference with { Offset = -1 } }).HasValidOwner());

        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().WriteUInt32(0x100, 0x12345678).WriteResourcePointer(0x100, pointer);
        Assert.Equal(pointer, staging.ReadResourcePointer(0x100));
        Assert.All(staging.ReadSnapshot(0x100, 4).Known, known => Assert.False(known));
    }

    [Fact]
    public void Native_serialization_and_legacy_shared_reference_addresses_stay_unchanged()
    {
        NativeBattleModelCpuBanks banks = NativeBattleModelCpuBanks.Unknown();
        string json = JsonSerializer.Serialize(banks);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal("Banks", Assert.Single(document.RootElement.EnumerateObject()).Name);
        NativeBattleModelCpuBanks restored = JsonSerializer.Deserialize<NativeBattleModelCpuBanks>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        BattlePartyModelOwner native = (BattlePartyModelOwner)OriginalOwner(2);
        Assert.Throws<InvalidDataException>(() => restored.PartyBankIndex(native));
        Assert.Equal(2, restored.BindPartyOwner(native, NativeBattleModelSlot.Decode(2)).PartyBankIndex(native));
        Assert.Equal(0x0196B01Cu, new NativeBattleModelCpuReference(0, 4).PhysicalAddress);
        Assert.Equal(0x0193901Cu, new NativeBattleModelCpuReference(3, 4).PhysicalAddress);
        Assert.Equal(0x01D999C4u, new NativeBattleModelCpuReference(4, 4).PhysicalAddress);
    }

    [Fact]
    public void Remastered_extension_uses_its_authenticated_resource_extent_without_an_address()
    {
        NativeBattlePartyCpuLayout layout = NativeBattlePartyCpuLayout.Remastered;
        NativeBattleModelCpuResources cold = NativeBattleModelCpuResources.FromColdProcess(layout.ExecutableHash);
        NativeBattleModelCpuResources cpu = cold.WithPresentationRoster(Roster, Aliases);
        int bank = cpu.Cpu.PartyBankIndex(ThirdExtra);
        Assert.Equal(0x20000, cpu.Cpu.GetBank(bank).Capacity);
        NativeBattleModelCpuReference reference = new(bank, 0x1FFFE) { Layout = layout, ResourceOnly = true };
        NativeBattleResourcePointer pointer = new NativeBattleResourcePointer(reference, null, 2).WithNativeAddressEncoding();
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        cpu = NativeBattleModelResourceView.WritePartySection(cpu, staging, pointer, 0, [20, 24]);
        cpu.Validate();
        Assert.Equal(new byte[] { 20, 24 }, NativeBattleModelResourceView.Open(cpu, staging, pointer, 2).Read(0, 2));
        Assert.Null(reference.TryPhysicalAddress());
        Assert.Null(pointer.NativeByteValue);
    }

    private static NativeBattleModelCpuResources Resources()
    {
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.FromColdProcess();
        return cpu.WithPresentationRoster(Roster, Aliases);
    }

    private static NativeBattleResourcePointer Pointer(int bank, int offset, int length) => new(new NativeBattleModelCpuReference(bank, offset) { ResourceOnly = true }, null, length);
}
