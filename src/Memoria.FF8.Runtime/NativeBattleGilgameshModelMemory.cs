using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshModelOwner([property: JsonRequired] uint Address, [property: JsonRequired] uint ControlAddress, [property: JsonRequired] NativeBattleCactuarRawModelState State);

/// <summary>The eleven distinct retained raw model/control owners reached by Gilgamesh.</summary>
public sealed record NativeBattleGilgameshModelMemory([property: JsonRequired] ImmutableArray<NativeBattleGilgameshModelOwner> Owners)
{
    public static NativeBattleGilgameshModelMemory FromColdProcess()
    {
        ImmutableArray<NativeBattleGilgameshModelOwner>.Builder owners = ImmutableArray.CreateBuilder<NativeBattleGilgameshModelOwner>();
        Add(0x21FF760, 0x21FF4B8);
        Add(0x2200F00, 0x21FF2B0);
        for (uint index = 0; index < 4; index++)
        {
            Add(0x21FF800 + index * 0x9C, 0x21FFC78 + index * 0x34);
        }
        Add(0x2200FC0, 0x2200EC8);
        for (uint index = 0; index < 4; index++)
        {
            Add(0x21FF4F0 + index * 0x9C, 0x2200D20 + index * 0x34);
        }
        return new(owners.ToImmutable());

        void Add(uint address, uint control) => owners.Add(new(address, control, NativeBattleCactuarRawModelState.FromColdProcess()));
    }

    public NativeBattleGilgameshModelOwner Read(uint address)
    {
        Validate();
        return Owners.SingleOrDefault(owner => owner.Address == address) ?? throw new NotSupportedException("Native Gilgamesh model address has no supplied retained physical owner.");
    }

    public NativeBattleGilgameshModelMemory Write(uint address, NativeBattleCactuarRawModelState state)
    {
        NativeBattleGilgameshModelOwner owner = Read(address);
        NativeBattleGilgameshModelMemory memory = this with { Owners = Owners.SetItem(Owners.IndexOf(owner), owner with { State = state }) };
        memory.Validate();
        return memory;
    }

    public void Validate()
    {
        if (Owners.IsDefault || Owners.Length != 11 || Owners.Any(owner => owner is null || owner.State is null || owner.State.Model.IsDefault || owner.State.Model.Length != 0x90 || owner.State.Control.IsDefault || owner.State.Control.Length != 0x34 || owner.State.ProjectedScratch is not null) || Owners.Select(owner => owner.Address).Distinct().Count() != Owners.Length || Owners.Select(owner => owner.ControlAddress).Distinct().Count() != Owners.Length)
        {
            throw new InvalidDataException("Native Gilgamesh requires eleven distinct raw model/control owners and one separate shared projected bank.");
        }
        foreach (NativeBattleGilgameshModelOwner owner in Owners)
        {
            uint expected = owner.Address switch
            {
                0x21FF760 => 0x21FF4B8,
                0x2200F00 => 0x21FF2B0,
                0x2200FC0 => 0x2200EC8,
                _ => IndexedControl(owner.Address)
            };
            if (owner.ControlAddress != expected)
            {
                throw new InvalidDataException("Native Gilgamesh raw model/control binding changed its physical bank.");
            }
        }
    }

    private static uint IndexedControl(uint address)
    {
        if (address >= 0x21FF800 && address < 0x21FF800 + 4 * 0x9C && (address - 0x21FF800) % 0x9C == 0)
        {
            return 0x21FFC78 + (address - 0x21FF800) / 0x9C * 0x34;
        }
        if (address >= 0x21FF4F0 && address < 0x21FF4F0 + 4 * 0x9C && (address - 0x21FF4F0) % 0x9C == 0)
        {
            return 0x2200D20 + (address - 0x21FF4F0) / 0x9C * 0x34;
        }
        throw new InvalidDataException("Native Gilgamesh raw model address leaves its physical banks.");
    }
}
