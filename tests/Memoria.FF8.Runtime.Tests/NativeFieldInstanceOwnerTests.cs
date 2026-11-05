using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeFieldInstanceOwnerTests
{
    private static readonly uint[] GraphicsPointers = [4, 84, 88, 92, 128];

    [Theory]
    [InlineData(NativeFieldRuntimeProfile.FF8EnExecutableHash)]
    [InlineData(NativeFieldRuntimeProfile.RemasteredEnImageHash)]
    public void Independent_instance_bones_masks_packets_and_history_use_one_selected_owner(string image)
    {
        NativeFieldRegistrationStep registered = Register(image, true);
        registered.State.Validate(registered.Memory);
        NativeFieldResourceOwner first = Instance(registered.Memory, 0);
        NativeFieldResourceOwner second = Instance(registered.Memory, 1);
        Assert.NotSame(first.Storage.OwnerIdentity, second.Storage.OwnerIdentity);
        Assert.Equal(412, first.Length);
        Assert.Equal((uint)152, first.Storage.ReadUInt32(4));
        Assert.Equal((ushort)123, Read16(first.Storage, first.Storage.ReadUInt32(4) + 64 + 8));
        Assert.Equal((ushort)1024, Read16(first.Storage, 152 + 14));
        Assert.Equal(new byte[2], first.Storage.Read(first.Storage.ReadUInt32(128), 2));
        Assert.Equal(96u + 152 * 2, registered.State.EndAddress - registered.State.PreModelAddress);
        foreach (NativeFieldRegisteredInstance instance in registered.State.Instances)
        {
            Assert.Equal(first.Length, instance.ResourceReference!.GraphicsLength);
            foreach (uint field in GraphicsPointers)
            {
                Assert.All(registered.Memory.FileStaging!.ReadSnapshot(instance.Address + field, 4).Known, value => Assert.False(value));
            }
            Assert.Equal(registered.State.Resources[0].Address, registered.Memory.FileStaging!.ReadUInt32(instance.Address));
            Assert.Equal((byte)0, registered.Memory.FileStaging.ReadByte(instance.Address + 114));
        }
        foreach (uint address in new uint[] { 0x1DCB4C0, 0x1DC4004, 0x1DCA320, 0x1DC3FEC })
        {
            Assert.All(registered.Memory.FileStaging!.ReadSnapshot(address, 8).Known, value => Assert.False(value));
        }
        uint packet = first.Storage.ReadUInt32(88);
        using NativeBattleStagingReadWindow history = first.Storage.OpenReadWindow(packet + 4, 4, maximumRetainedBytes: 0);
        NativeBattleFieldResourceReloadResult presented = NativeFieldRegistrationMachine.RestoreRetainedPresentation(registered.State, registered.Memory, [Presentation(0), Presentation(1) with { Red = 128, Green = 128, Blue = 128, PrimitiveTransparency = false }]);
        for (uint field = 88; field <= 92; field += 4)
        {
            uint a = first.Storage.ReadUInt32(field);
            uint b = second.Storage.ReadUInt32(field);
            Assert.Equal(new byte[] { 31, 63, 95, 38 }, first.Storage.Read(a + 4, 4));
            Assert.Equal(new byte[] { 128, 128, 128, 36 }, second.Storage.Read(b + 4, 4));
        }
        Assert.Equal(new byte[] { 128, 128, 128, 36 }, Enumerable.Range(0, 4).Select(history.ReadByte));
        Assert.Equal(0, history.RetainedByteCount);
        NativeFieldEyeProcess snapshot = new NativeFieldEyeProcess(1, presented.Memory, null).CaptureSnapshot();
        string json = JsonSerializer.Serialize(snapshot);
        NativeFieldEyeProcess restored = JsonSerializer.Deserialize<NativeFieldEyeProcess>(json)!.RestoreOwned();
        restored.Validate(registered.State);
        NativeFieldResourceOwner independent = Instance(restored.Memory, 0);
        Assert.NotSame(first.Storage.OwnerIdentity, independent.Storage.OwnerIdentity);
        Assert.Equal(first.Storage.ReadSnapshot(0, first.Length).Data, independent.Storage.ReadSnapshot(0, independent.Length).Data);
        NativeFieldRegistrationMachine.RestoreRetainedPresentation(registered.State, restored.Memory, [Presentation(0) with { Red = 7 }, Presentation(1)]);
        Assert.Equal((byte)31, first.Storage.ReadByte(packet + 4));
        Assert.Equal((byte)7, independent.Storage.ReadByte(packet + 4));
        Assert.Equal(json, JsonSerializer.Serialize(snapshot));
    }

    [Fact]
    public void Parent_abort_restores_instance_bone_mask_packet_and_scalar_changes()
    {
        NativeFieldRegistrationStep registered = Register(NativeFieldRuntimeProfile.FF8EnExecutableHash, false);
        string before = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(registered.Memory));
        NativeFieldResourceOwner owner = Instance(registered.Memory, 0);
        using NativeBattleFileStagingTransaction parent = new(registered.Memory.FileStaging!);
        parent.BeginCheckpoint();
        NativeBattleRetainedMemory retained = registered.Memory with { FileStaging = parent.View };
        NativeFieldRegistrationMachine.RestoreRetainedPresentation(registered.State, retained, [Presentation(0)]);
        using (NativeFieldRegistrationMemory memory = new(parent.View, registered.State.MemoryLayout.MemoryStart, registered.State.MemoryLayout.MemoryEnd))
        {
            memory.Write16(new NativeFieldResourceCursor(owner, owner.Storage.ReadUInt32(4) + 64 + 8), 999);
            memory.Write16(new NativeFieldResourceCursor(owner, owner.Storage.ReadUInt32(128)), 0xFFFF);
            memory.Finish();
        }
        Assert.Equal((ushort)999, Read16(parent.EnlistFieldResource(owner), owner.Storage.ReadUInt32(4) + 64 + 8));
        parent.RollbackCheckpoint();
        parent.Freeze(parent.View);
        Assert.Equal(before, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(registered.Memory)));
    }

    [Fact]
    public void Previous_animation_sidecar_and_guest_graphics_snapshot_uses_the_same_packet_kernel()
    {
        NativeFieldRegistrationStep owned = Register(NativeFieldRuntimeProfile.FF8EnExecutableHash, false);
        NativeBattleRetainedMemory legacy = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(owned.Memory));
        NativeFieldRegisteredInstance instance = owned.State.Instances[0];
        NativeFieldResourceOwner graphics = Instance(legacy, 0);
        (byte[] bytes, bool[] known) = graphics.Storage.ReadSnapshot(0, graphics.Length);
        (byte[] header, bool[] headerKnown) = legacy.FileStaging!.ReadSnapshot(instance.Address, 152);
        header.CopyTo(bytes, 0);
        headerKnown.CopyTo(known, 0);
        foreach (uint field in GraphicsPointers)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan((int)field), checked(instance.Address + graphics.Storage.ReadUInt32(field)));
            known.AsSpan((int)field, 4).Fill(true);
        }
        legacy.FileStaging.WorkingOwner.WriteWithKnownness(instance.Address, bytes, known);
        NativeFieldResourceOwner sidecar = NativeFieldResourceOwner.Create(152);
        foreach (uint field in new uint[] { 8, 104, 108, 148 })
        {
            sidecar.Storage.WriteUInt32(field, graphics.Storage.ReadUInt32(field));
        }
        legacy.FileStaging.WriteResourcePointer(0x1DCB340, new(null, instance.Address) { FieldStorage = new(0) { InstanceBindings = sidecar } });
        NativeFieldRegisteredInstance old = instance with { ResourceReference = instance.ResourceReference! with { GraphicsLength = null } };
        NativeFieldRegistrationState state = owned.State with { Instances = [old], EndAddress = checked(instance.Address + (uint)graphics.Length) };
        state.Validate(legacy);
        NativeBattleRetainedMemory restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(legacy)))!);
        NativeBattleFieldResourceReloadResult a = NativeFieldRegistrationMachine.RestoreRetainedPresentation(owned.State, owned.Memory, [Presentation(0)]);
        NativeBattleFieldResourceReloadResult b = NativeFieldRegistrationMachine.RestoreRetainedPresentation(state, restored, [Presentation(0)]);
        for (uint field = 88; field <= 92; field += 4)
        {
            uint current = Instance(a.Memory, 0).Storage.ReadUInt32(field);
            uint original = b.Memory.FileStaging!.ReadUInt32(old.Address + field);
            Assert.Equal(Instance(a.Memory, 0).Storage.ReadSnapshot(current, 32).Data, b.Memory.FileStaging.ReadSnapshot(original, 32).Data);
            Assert.Equal(Instance(a.Memory, 0).Storage.ReadSnapshot(current, 32).Known, b.Memory.FileStaging.ReadSnapshot(original, 32).Known);
        }
    }

    [Fact]
    public void Original_ushort_triangle_count_creates_and_updates_double_packets_beyond_the_guest_arena()
    {
        const ushort faces = ushort.MaxValue;
        NativeFieldRegistrationResources template = NativeFieldInputOwnerTests.Fixture(NativeFieldRuntimeProfile.RemasteredEnImageHash, false, 0);
        int parts = 160 + faces * 64;
        int skins = parts + 32;
        int animation = skins + 8;
        int texture = animation + 28;
        byte[] bytes = new byte[4 + texture + 546];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 1);
        Span<byte> model = bytes.AsSpan(4);
        template.One.Data.AsSpan(4, 160).CopyTo(model);
        BinaryPrimitives.WriteUInt32LittleEndian(model[12..], faces);
        BinaryPrimitives.WriteUInt32LittleEndian(model[28..], faces);
        BinaryPrimitives.WriteUInt32LittleEndian(model[48..], (uint)parts);
        BinaryPrimitives.WriteUInt32LittleEndian(model[52..], (uint)skins);
        BinaryPrimitives.WriteUInt32LittleEndian(model[56..], (uint)animation);
        for (int face = 0; face < faces; face++)
        {
            template.One.Data.AsSpan(4 + 160, 64).CopyTo(model.Slice(160 + face * 64, 64));
        }
        template.One.Data.AsSpan(4 + 224, 32 + 8 + 28).CopyTo(model[parts..]);
        template.One.Data.AsSpan(4 + 292, 546).CopyTo(model[texture..]);
        NativeFieldModelSource one = new("chara.one", Convert.ToHexString(SHA256.HashData(bytes)), bytes.ToImmutableArray());
        NativeFieldOneModelEntry entry = template.Directory[0] with { Size = bytes.Length - 4, Textures = [texture], RawTextureWords = [(uint)texture] };
        NativeFieldRegistrationResources source = template with { One = one, Directory = [entry] };
        NativeFieldRegistrationStep registered = NativeFieldRegistrationMachine.Register(NativeFieldRegistrationEntry.BattleReturn, source, [new(0, 0, 0, 1)], NativeBattleRetainedMemory.FromColdProcess(source.ExecutableHash));
        NativeFieldResourceOwner owner = Instance(registered.Memory, 0);
        Assert.Equal(4325656, owner.Length);
        Assert.True(owner.Length > 3 * 1024 * 1024);
        Assert.Equal(248u, registered.State.EndAddress - registered.State.PreModelAddress);
        Assert.Equal(0x0A5F0000u, registered.State.MemoryLayout.Limit);
        uint last = owner.Storage.ReadUInt32(92) + (faces - 1u) * 32;
        Assert.True(last > 3 * 1024 * 1024);
        Assert.Equal(new byte[] { 128, 128, 128, 36 }, owner.Storage.Read(last + 4, 4));
        NativeFieldEyeProcess snapshot = new NativeFieldEyeProcess(1, registered.Memory, null).CaptureSnapshot();
        NativeFieldEyeProcess restored = JsonSerializer.Deserialize<NativeFieldEyeProcess>(JsonSerializer.Serialize(snapshot))!.RestoreOwned();
        NativeBattleFieldResourceReloadResult updated = NativeFieldRegistrationMachine.RestoreRetainedPresentation(registered.State, restored.Memory, [Presentation(0)]);
        NativeFieldResourceOwner independent = Instance(updated.Memory, 0);
        Assert.Equal(new byte[] { 31, 63, 95, 38 }, independent.Storage.Read(last + 4, 4));
        Assert.Equal(new byte[] { 128, 128, 128, 36 }, owner.Storage.Read(last + 4, 4));
        registered.State.Validate(updated.Memory);
    }

    private static NativeFieldRegistrationStep Register(string image, bool two)
    {
        NativeFieldRegistrationResources source = NativeFieldInputOwnerTests.Fixture(image, false, 0);
        NativeFieldRetainedActor[] actors = two ? [new(0, 0, 0, 1), new(1, 0, 0, 1)] : [new(0, 0, 0, 1)];
        return NativeFieldRegistrationMachine.Register(NativeFieldRegistrationEntry.BattleReturn, source, actors, NativeBattleRetainedMemory.FromColdProcess(image));
    }

    private static NativeFieldResourceOwner Instance(NativeBattleRetainedMemory memory, int index) => memory.FileStaging!.ReadResourcePointer(0x1DCB340 + (uint)index * 4)!.FieldStorage!.InstanceBindings!;
    private static ushort Read16(NativeBattleFileStaging storage, uint offset) => BinaryPrimitives.ReadUInt16LittleEndian(storage.Read(offset, 2));
    private static NativeFieldRetainedPresentation Presentation(int objectId) => new(objectId, 1, 0, false, 0, true, 17, 0, 31, 63, 95, false, true, 0);
}
