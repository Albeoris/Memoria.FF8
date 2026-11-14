using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedModelCpuBanksTests
{
    [Fact]
    public void Sparse_roster_maps_party_rows_without_aliasing_native_or_enemy_banks()
    {
        NativeBattleModelCpuBanks original = NativeBattleModelCpuBanks.Unknown().WithBank(0, NativeBattleModelCpuBank.Unknown().WriteGuest(0, [10]));
        NativeBattleModelCpuBanks banks = original.WithPresentationRoster(Roster());
        banks.Validate();
        Assert.Equal(3, banks.Banks.Length);
        Assert.Same(original.Banks[0], banks.GetBank(0));
        Assert.Equal(0, banks.PartyBankIndex(0));
        Assert.Equal(5, banks.PartyBankIndex(3));
        Assert.Equal(9, banks.PartyBankIndex(7));
        Assert.Equal(26, banks.PartyBankIndex(24));
        Assert.Equal(new[] { 5, 9, 26 }, banks.PresentationBanks!.Keys.Order().ToArray());
        Assert.Throws<InvalidDataException>(() => banks.PartyBankIndex(4));
        Assert.Throws<InvalidDataException>(() => banks.PartyBankIndex(8));
        Assert.Throws<InvalidDataException>(() => banks.GetBank(3));
        Assert.Throws<InvalidDataException>(() => banks.GetBank(4));
        Assert.Throws<InvalidDataException>(() => banks.CopyBank50B2A0(0, 9));
        Assert.Same(banks, banks.WithPresentationRoster(Roster()));
    }

    [Fact]
    public void Section_writes_and_geometry_edits_preserve_distinct_pinned_bytes()
    {
        NativeBattleModelCpuResources cpu = Resources();
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleResourcePointer original = new(new(0, 12), null, 2);
        NativeBattleResourcePointer row7 = Pointer(9, 12, 2);
        NativeBattleResourcePointer row24 = Pointer(26, 12, 2);
        cpu = NativeBattleModelResourceView.WritePartySection(cpu, staging, original, 0, [1, 2]);
        cpu = NativeBattleModelResourceView.WritePartySection(cpu, staging, row7, 0, [3, 4]);
        cpu = NativeBattleModelResourceView.WritePartySection(cpu, staging, row24, 0, [5, 6]);
        NativeBattleResourcePointer pinned = row24.Pin(cpu);
        NativeBattleModelResourceEditor edit = new(cpu, staging, row24);
        edit.Write(1, [99]);
        Assert.Equal(new byte[] { 5, 99 }, edit.Geometry.Read(0, 2));
        Assert.Equal(new byte[] { 5, 6 }, NativeBattleModelResourceView.Open(edit.Current, staging, pinned, 2).Read(0, 2));
        Assert.Equal(new byte[] { 1, 2 }, NativeBattleModelResourceView.Open(edit.Current, staging, original, 2).Read(0, 2));
        Assert.Equal(new byte[] { 3, 4 }, NativeBattleModelResourceView.Open(edit.Current, staging, row7, 2).Read(0, 2));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.WritePartySection(edit.Current, staging, pinned, 0, [8]));
        Assert.Throws<NotSupportedException>(() => edit.Current.Cpu.GetBank(26).ReadGuest(12, 2));

        string json = JsonSerializer.Serialize(edit.Current);
        NativeBattleModelCpuResources restored = JsonSerializer.Deserialize<NativeBattleModelCpuResources>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(new byte[] { 5, 99 }, NativeBattleModelResourceView.Open(restored, staging, row24, 2).Read(0, 2));
    }

    [Fact]
    public void Unknown_and_out_of_extent_hosted_sections_are_rejected()
    {
        NativeBattleModelCpuResources cpu = Resources();
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleResourcePointer pointer = Pointer(26, NativeBattleModelCpuBank.Length - 1, 1);
        NativeBattleModelResourceView view = NativeBattleModelResourceView.Open(cpu, staging, pointer, 1);
        Assert.False(Assert.Single(view.ReadSnapshot(0, 1).Known));
        Assert.Throws<NotSupportedException>(() => view.Read(0, 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.Open(cpu, staging, Pointer(26, NativeBattleModelCpuBank.Length, 1), 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.Open(cpu, staging, Pointer(26, int.MaxValue, 2), 2));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.Open(cpu, staging, Pointer(10, 0, 1), 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.Open(cpu, staging, pointer with { Cpu = pointer.Cpu! with { Layout = NativeBattlePartyCpuLayout.Remastered } }, 1));

        var row = cpu.Rows[0].Set(12, pointer.Cpu);
        (cpu with { Rows = cpu.Rows.SetItem(0, row) }).Validate();
        Assert.Throws<InvalidDataException>((cpu with { Rows = cpu.Rows.SetItem(0, row.Set(12, Pointer(26, int.MaxValue, 1).Cpu)) }).Validate);
    }

    [Fact]
    public void Inconsistent_extension_owners_and_nonresource_banks_are_rejected()
    {
        NativeBattleModelCpuBanks banks = Resources().Cpu;
        ImmutableDictionary<int, NativeBattleModelCpuBank> extras = banks.PresentationBanks!;
        NativeBattleModelCpuBank extension = extras[26];
        Assert.Throws<InvalidDataException>((banks with { PresentationRoster = null }).Validate);
        Assert.Throws<InvalidDataException>((banks with { PresentationBanks = extras.Remove(26) }).Validate);
        foreach (int invalid in new[] { 0, 3, 4, 6, 10, 27 })
        {
            Assert.Throws<InvalidDataException>((banks with { PresentationBanks = extras.Remove(26).Add(invalid, extension) }).Validate);
        }
        NativeBattleGraphicsRoster different = new(banks.PresentationRoster!.Rows.SetItem(24, new("replacement", true)));
        Assert.Throws<InvalidDataException>(() => banks.WithPresentationRoster(different));
        Assert.Throws<InvalidDataException>(() => banks.WithBank(26, NativeBattleModelCpuBank.Unknown()));
        Assert.Throws<InvalidDataException>(() => banks.WithBank(26, NativeBattleModelCpuBank.Unknown(1).PromoteResourceStorage()));
        Assert.Throws<InvalidDataException>(() => banks.WithBank(26, extension.WriteGuest(0, [5])));
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
        Assert.Equal(2, restored.PartyBankIndex(2));
        Assert.Throws<InvalidDataException>(() => restored.PartyBankIndex(3));
        Assert.Equal(0x0196B01Cu, new NativeBattleModelCpuReference(0, 4).PhysicalAddress);
        Assert.Equal(0x0193901Cu, new NativeBattleModelCpuReference(3, 4).PhysicalAddress);
        Assert.Equal(0x01D999C4u, new NativeBattleModelCpuReference(4, 4).PhysicalAddress);
    }

    [Fact]
    public void Remastered_extension_uses_its_authenticated_resource_extent_without_an_address()
    {
        NativeBattlePartyCpuLayout layout = NativeBattlePartyCpuLayout.Remastered;
        NativeBattleModelCpuResources cold = NativeBattleModelCpuResources.FromColdProcess(layout.ExecutableHash);
        NativeBattleModelCpuResources cpu = cold with { Cpu = cold.Cpu.WithPresentationRoster(Roster()) };
        Assert.Equal(0x20000, cpu.Cpu.GetBank(26).Capacity);
        NativeBattleModelCpuReference reference = new(26, 0x1FFFE) { Layout = layout, ResourceOnly = true };
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
        return cpu with { Cpu = cpu.Cpu.WithPresentationRoster(Roster()) };
    }

    private static NativeBattleGraphicsRoster Roster()
    {
        NativeBattleGraphicsActor?[] rows = new NativeBattleGraphicsActor?[25];
        rows[0] = new("native", true);
        rows[3] = new("party-3", true);
        rows[4] = new("enemy", false);
        rows[7] = new("party-7", true);
        rows[24] = new("party-24", true);
        return new(rows.ToImmutableArray());
    }

    private static NativeBattleResourcePointer Pointer(byte bank, int offset, int length) => new(new NativeBattleModelCpuReference(bank, offset) { ResourceOnly = true }, null, length);
}
