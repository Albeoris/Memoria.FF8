using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshModelMemoryTests
{
    [Fact]
    public void Cold_memory_contains_exact_eleven_distinct_physical_model_control_bindings()
    {
        NativeBattleGilgameshModelMemory memory = NativeBattleGilgameshModelMemory.FromColdProcess();
        memory.Validate();
        uint[] models = [0x21FF760, 0x2200F00, 0x21FF800, 0x21FF89C, 0x21FF938, 0x21FF9D4, 0x2200FC0, 0x21FF4F0, 0x21FF58C, 0x21FF628, 0x21FF6C4];
        uint[] controls = [0x21FF4B8, 0x21FF2B0, 0x21FFC78, 0x21FFCAC, 0x21FFCE0, 0x21FFD14, 0x2200EC8, 0x2200D20, 0x2200D54, 0x2200D88, 0x2200DBC];
        Assert.Equal(models, memory.Owners.Select(owner => owner.Address));
        Assert.Equal(controls, memory.Owners.Select(owner => owner.ControlAddress));
        for (int index = 0; index < models.Length; index++)
        {
            NativeBattleGilgameshModelOwner owner = memory.Read(models[index]);
            Assert.Equal(controls[index], owner.ControlAddress);
            Assert.Equal(new byte[0x90], owner.State.Model.ToArray());
            Assert.Equal(new byte[0x34], owner.State.Control.ToArray());
            Assert.Null(owner.State.ResourceAddress);
            Assert.Null(owner.State.WorkingBase);
            Assert.Null(owner.State.DecodedPose);
            Assert.Null(owner.State.ProjectedScratch);
        }
    }

    [Fact]
    public void Immutable_write_changes_only_selected_physical_owner_and_restores_without_hydration()
    {
        NativeBattleGilgameshModelMemory memory = NativeBattleGilgameshModelMemory.FromColdProcess();
        NativeBattleCactuarRawModelState state = memory.Read(0x21FF938).State;
        NativeBattleCactuarRawModelState changed = state with { Model = state.Model.SetItem(0x2C, 0xA5), Control = state.Control.SetItem(0x20, 0xB6), ResourceAddress = 0x123456, WorkingBase = 0x234567 };
        NativeBattleGilgameshModelMemory written = memory.Write(0x21FF938, changed);
        Assert.Same(changed, written.Read(0x21FF938).State);
        Assert.Equal((byte)0, memory.Read(0x21FF938).State.Model[0x2C]);
        foreach (NativeBattleGilgameshModelOwner owner in memory.Owners.Where(owner => owner.Address != 0x21FF938))
        {
            Assert.Same(owner, written.Read(owner.Address));
        }
        NativeBattleGilgameshModelMemory restored = JsonSerializer.Deserialize<NativeBattleGilgameshModelMemory>(JsonSerializer.Serialize(written))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(written), JsonSerializer.Serialize(restored));
        Assert.Equal((byte)0xA5, restored.Read(0x21FF938).State.Model[0x2C]);
        Assert.Equal((byte)0xB6, restored.Read(0x21FF938).State.Control[0x20]);
        Assert.Null(restored.Read(0x21FF938).State.DecodedPose);
        Assert.Null(restored.Read(0x21FF938).State.ProjectedScratch);
        NativeBattleCactuarRawModelState next = restored.Read(0x21FF938).State with { Control = changed.Control.SetItem(0x21, 0xC7) };
        Assert.Equal(JsonSerializer.Serialize(written.Write(0x21FF938, next)), JsonSerializer.Serialize(restored.Write(0x21FF938, next)));
    }

    [Fact]
    public void Missing_unknown_duplicate_or_incomplete_physical_owners_fail_without_filling_defaults()
    {
        NativeBattleGilgameshModelMemory memory = NativeBattleGilgameshModelMemory.FromColdProcess();
        Assert.Throws<NotSupportedException>(() => memory.Read(0x21FF801));
        Assert.Throws<NotSupportedException>(() => memory.Write(0x21FF801, memory.Owners[0].State));
        Assert.Throws<InvalidDataException>(() => (memory with { Owners = default }).Validate());
        Assert.Throws<InvalidDataException>(() => (memory with { Owners = memory.Owners.RemoveAt(10) }).Validate());
        Assert.Throws<InvalidDataException>(() => (memory with { Owners = memory.Owners.SetItem(1, memory.Owners[0]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (memory with { Owners = memory.Owners.SetItem(1, memory.Owners[1] with { ControlAddress = memory.Owners[0].ControlAddress }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (memory with { Owners = memory.Owners.SetItem(0, memory.Owners[0] with { Address = 0x21FF761 }) }).Validate());
        Assert.Throws<InvalidDataException>(() => memory.Write(memory.Owners[0].Address, memory.Owners[0].State with { Model = default }));
        Assert.Throws<InvalidDataException>(() => memory.Write(memory.Owners[0].Address, memory.Owners[0].State with { Control = new byte[0x30].ToImmutableArray() }));
        JsonObject missing = JsonNode.Parse(JsonSerializer.Serialize(memory))!.AsObject();
        missing.Remove(nameof(NativeBattleGilgameshModelMemory.Owners));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleGilgameshModelMemory>(missing.ToJsonString()));
    }

    [Fact]
    public void Per_model_projected_storage_is_rejected_including_empty_arrays_and_shared_aliases()
    {
        NativeBattleGilgameshModelMemory memory = NativeBattleGilgameshModelMemory.FromColdProcess();
        ImmutableArray<byte> scratch = new byte[0x100].ToImmutableArray();
        Assert.Throws<InvalidDataException>(() => memory.Write(memory.Owners[0].Address, memory.Owners[0].State with { ProjectedScratch = [] }));
        NativeBattleGilgameshModelMemory aliases = memory with { Owners = memory.Owners.Select(owner => owner with { State = owner.State with { ProjectedScratch = scratch } }).ToImmutableArray() };
        Assert.Throws<InvalidDataException>(() => aliases.Validate());
        NativeBattleGilgameshModelMemory restored = JsonSerializer.Deserialize<NativeBattleGilgameshModelMemory>(JsonSerializer.Serialize(aliases))!;
        Assert.Throws<InvalidDataException>(() => restored.Validate());
    }
}
