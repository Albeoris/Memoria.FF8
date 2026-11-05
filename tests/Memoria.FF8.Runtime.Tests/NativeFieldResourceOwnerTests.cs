using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeFieldResourceOwnerTests
{
    private const uint ResourceTable = 0x1DCB0F0;
    private const uint InstanceTable = 0x1DCB340;
    private static readonly int[] HeaderPointers = [0, 4, 8, 12, 16, 20, 24, 28, 32, 64, 84, 88];
    private static readonly int[] AnimationPointers = [8, 104, 108, 148];

    [Theory]
    [InlineData(NativeFieldRuntimeProfile.FF8EnExecutableHash)]
    [InlineData(NativeFieldRuntimeProfile.RemasteredEnImageHash)]
    public void Production_conversion_owns_one_shared_resource_and_small_instance_cursors(string image)
    {
        NativeFieldRegistrationStep registered = Register(image);
        registered.State.Validate(registered.Memory);
        NativeFieldRegisteredResource resource = Assert.Single(registered.State.Resources);
        NativeFieldResourceOwner owner = ResourceOwner(registered.Memory);
        Assert.Equal(resource.ResourceLength, owner.Length);
        Assert.Equal(0u, owner.Storage.BaseAddress);
        Assert.Equal(0x12345678u, owner.Storage.ReadUInt32(76));
        foreach (int offset in HeaderPointers)
        {
            Assert.All(registered.Memory.FileStaging!.ReadSnapshot(resource.Address + (uint)offset, 4).Known, value => Assert.False(value));
        }
        foreach (NativeFieldRegisteredInstance instance in registered.State.Instances)
        {
            Assert.Equal(0, instance.ResourceReference!.ResourceIndex);
            NativeBattleResourcePointer receipt = registered.Memory.FileStaging!.ReadResourcePointer(InstanceTable + (uint)instance.ResourceReference.InstanceIndex * 4)!;
            Assert.Null(receipt.FieldStorage!.Resource);
            Assert.Equal(instance.ResourceReference.GraphicsLength, receipt.FieldStorage.GraphicsLength);
            Assert.Equal(instance.ResourceReference.GraphicsLength, receipt.FieldStorage.InstanceBindings!.Length);
            foreach (int offset in AnimationPointers)
            {
                Assert.All(registered.Memory.FileStaging.ReadSnapshot(instance.Address + (uint)offset, 4).Known, value => Assert.False(value));
                Assert.InRange(receipt.FieldStorage.InstanceBindings.Storage.ReadUInt32((uint)offset), 0u, (uint)owner.Length);
            }
        }
        NativeBattleFieldResourceReloadResult started = NativeFieldRegistrationMachine.SetEyes532340(registered.State, registered.Memory, 0, 0x80000, 7, NativeCrtRandomState.FromColdProcess());
        uint eyes = owner.Storage.ReadUInt32(20);
        Assert.Equal((ushort)7, Read16(ResourceOwner(started.Memory).Storage, eyes + 10));
        Assert.Same(owner, ResourceOwner(started.Memory));
        NativeFieldRetainedPresentation[] presentation = [Presentation(0, 1), Presentation(1, 0)];
        NativeBattleFieldResourceReloadResult selected = NativeFieldRegistrationMachine.RestoreRetainedPresentation(registered.State, started.Memory, presentation, started.CrtRandomAfter);
        uint stream = owner.Storage.ReadUInt32(88);
        NativeFieldResourceOwner first = selected.Memory.FileStaging!.ReadResourcePointer(InstanceTable)!.FieldStorage!.InstanceBindings!;
        NativeFieldResourceOwner second = selected.Memory.FileStaging.ReadResourcePointer(InstanceTable + 4)!.FieldStorage!.InstanceBindings!;
        Assert.Equal(stream + 8, first.Storage.ReadUInt32(8));
        Assert.Equal(stream, second.Storage.ReadUInt32(8));
        Assert.Same(owner, ResourceOwner(selected.Memory));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_unwritten_root_bytes_inherit_bounded_guest_data_knownness_and_zero_proofs(bool known)
    {
        NativeFieldRegistrationResources source = Fixture(NativeFieldRuntimeProfile.RemasteredEnImageHash);
        NativeBattleRetainedMemory retained = NativeBattleStorageSnapshots.Restore(NativeBattleRetainedMemory.FromColdProcess(source.ExecutableHash));
        NativeFieldRegistrationLayout layout = NativeFieldRegistrationLayout.ForImage(source.ExecutableHash, NativeFieldRegistrationEntry.BattleReturn);
        uint root = layout.ArenaAddress + 56 + 96;
        retained.FileStaging!.WriteByte(root, 0xA5);
        if (!known)
        {
            retained.FileStaging.MarkUnknown(root, 1);
        }
        NativeFieldRegistrationStep registered = NativeFieldRegistrationMachine.Register(NativeFieldRegistrationEntry.BattleReturn, source, [new(0, 0, 0, 1)], retained);
        NativeFieldResourceOwner owner = ResourceOwner(registered.Memory);
        uint bones = owner.Storage.ReadUInt32(0);
        Assert.Equal(new byte[] { 0xA5 }, owner.Storage.ReadSnapshot(bones, 1).Data);
        Assert.Equal(new[] { known }, owner.Storage.ReadSnapshot(bones, 1).Known);
        Assert.Equal(new byte[63], owner.Storage.Read(bones + 1, 63));
        registered.State.Validate(registered.Memory);
    }

    [Fact]
    public void Packed_ushort_clips_convert_beyond_the_Remastered_guest_arena_without_widening_the_format()
    {
        const ushort frames = ushort.MaxValue;
        const int clips = 7;
        NativeFieldRegistrationResources template = Fixture(NativeFieldRuntimeProfile.RemasteredEnImageHash);
        int textureOffset = 88 + 2 + clips * (4 + frames * 6);
        byte[] bytes = new byte[4 + textureOffset + 546];
        template.One.Data.AsSpan(0, 4 + 88).CopyTo(bytes);
        template.One.Data.AsSpan(4 + 116, 546).CopyTo(bytes.AsSpan(4 + textureOffset));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4 + 88), clips);
        int input = 4 + 90;
        for (int clip = 0; clip < clips; clip++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(input), frames);
            input += 4 + frames * 6;
        }
        NativeFieldOneModelEntry entry = template.Directory[0] with { Size = bytes.Length - 4, Textures = [textureOffset], RawTextureWords = [(uint)textureOffset] };
        NativeFieldRegistrationResources source = template with { One = Source("chara.one", bytes), Directory = [entry] };
        NativeFieldRegistrationStep registered = NativeFieldRegistrationMachine.Register(NativeFieldRegistrationEntry.BattleReturn, source, [new(0, 0, 0, 1)], NativeBattleRetainedMemory.FromColdProcess(source.ExecutableHash));
        NativeFieldResourceOwner owner = ResourceOwner(registered.Memory);
        registered.State.Validate(registered.Memory);
        Assert.True(bytes.Length < 3 * 1024 * 1024);
        Assert.True(owner.Length > 3 * 1024 * 1024);
        Assert.Equal(96 + 64 + 24 * 8 + clips * 4 + clips * frames * 8, owner.Length);
        Assert.Equal((ushort)clips, Read16(owner.Storage, 82));
        uint table = owner.Storage.ReadUInt32(84);
        Assert.Equal(frames, Read16(owner.Storage, table + (clips - 1) * 4));
        Assert.Equal((ushort)1, Read16(owner.Storage, table + (clips - 1) * 4 + 2));
        Assert.True(registered.State.EndAddress - registered.State.PreModelAddress < 1024);
        Assert.Equal(0x0A5F0000u, registered.State.MemoryLayout.Limit);
        Assert.Equal(0, owner.Storage.MaterializedPageCount);
        Assert.Equal(new[] { false, false }, owner.Storage.ReadSnapshot((uint)owner.Length - 2, 2).Known);
        NativeFieldEyeProcess captured = new NativeFieldEyeProcess(1, registered.Memory, null).CaptureSnapshot();
        NativeFieldEyeProcess restored = JsonSerializer.Deserialize<NativeFieldEyeProcess>(JsonSerializer.Serialize(captured))!.RestoreOwned();
        NativeBattleFieldResourceReloadResult selected = NativeFieldRegistrationMachine.RestoreRetainedPresentation(registered.State, restored.Memory, [Presentation(0, clips - 1)]);
        NativeFieldResourceOwner bindings = selected.Memory.FileStaging!.ReadResourcePointer(InstanceTable)!.FieldStorage!.InstanceBindings!;
        Assert.Equal(owner.Storage.ReadUInt32(88) + (clips - 1) * frames * 8, bindings.Storage.ReadUInt32(8));
        registered.State.Validate(selected.Memory);
    }

    [Fact]
    public void Memory_only_diagnostic_restore_rebinds_shared_control_and_instance_cursors_independently()
    {
        NativeFieldRegistrationStep registered = Register(NativeFieldRuntimeProfile.FF8EnExecutableHash);
        NativeBattleFieldResourceReloadResult started = NativeFieldRegistrationMachine.SetEyes532340(registered.State, registered.Memory, 0, 0x80000, 5, NativeCrtRandomState.FromColdProcess());
        NativeFieldEyeProcess snapshot = new NativeFieldEyeProcess(1, started.Memory, started.CrtRandomAfter).CaptureSnapshot();
        string json = JsonSerializer.Serialize(snapshot);
        NativeFieldEyeProcess restored = JsonSerializer.Deserialize<NativeFieldEyeProcess>(json)!.RestoreOwned();
        NativeFieldResourceOwner original = ResourceOwner(started.Memory);
        NativeFieldResourceOwner independent = ResourceOwner(restored.Memory);
        Assert.NotSame(original.Storage.OwnerIdentity, independent.Storage.OwnerIdentity);
        NativeBattleFieldResourceReloadResult changed = NativeFieldRegistrationMachine.SetEyes532340(registered.State, started.Memory, 1, 0x80000, 9, started.CrtRandomAfter);
        uint eyes = original.Storage.ReadUInt32(20);
        Assert.Equal((ushort)9, Read16(ResourceOwner(changed.Memory).Storage, eyes + 10));
        Assert.Equal((ushort)5, Read16(independent.Storage, eyes + 10));
        NativeBattleFieldResourceReloadResult continued = NativeFieldRegistrationMachine.SetEyes532340(registered.State, restored.Memory, 1, 0x80000, 6, restored.CrtRandom);
        Assert.Equal((ushort)6, Read16(ResourceOwner(continued.Memory).Storage, eyes + 10));
        Assert.Equal((ushort)9, Read16(original.Storage, eyes + 10));
        registered.State.Validate(continued.Memory);
        Assert.Equal(json, JsonSerializer.Serialize(snapshot));
    }

    [Fact]
    public void Failed_presentation_rolls_back_shared_eye_control_instance_cursors_and_guest_state()
    {
        NativeFieldRegistrationStep registered = Register(NativeFieldRuntimeProfile.FF8EnExecutableHash);
        string before = Snapshot(registered.Memory);
        NativeFieldRetainedPresentation[] presentation = [Presentation(0, 1) with { EyeMode = 0x80000, EyeBlinkParameter611 = 9 }, Presentation(1, 1) with { EyeMode = 0x123456 }];
        Assert.Throws<InvalidDataException>(() => NativeFieldRegistrationMachine.RestoreRetainedPresentation(registered.State, registered.Memory, presentation, NativeCrtRandomState.FromColdProcess()));
        Assert.Equal(before, Snapshot(registered.Memory));
        registered.State.Validate(registered.Memory);
        NativeBattleFieldResourceReloadResult resumed = NativeFieldRegistrationMachine.RestoreRetainedPresentation(registered.State, registered.Memory, [Presentation(0, 1), Presentation(1, 0)]);
        registered.State.Validate(resumed.Memory);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Completed_child_scopes_follow_outer_nested_commit_rollback_and_final_publication(bool commit)
    {
        NativeFieldRegistrationStep registered = Register(NativeFieldRuntimeProfile.FF8EnExecutableHash);
        string before = Snapshot(registered.Memory);
        NativeFieldResourceOwner owner = ResourceOwner(registered.Memory);
        uint eyes = owner.Storage.ReadUInt32(20);
        using NativeBattleFileStagingTransaction parent = new(registered.Memory.FileStaging!);
        parent.BeginCheckpoint();
        int depth = parent.BeginNestedCheckpoint();
        NativeBattleRetainedMemory retained = registered.Memory with { FileStaging = parent.View };
        NativeCrtRandomState random = NativeCrtRandomState.FromColdProcess();
        NativeBattleFieldResourceReloadResult changed = NativeFieldRegistrationMachine.SetEyes532340(registered.State, retained, 0, 0x80000, 7, random);
        Assert.Same(parent.EnlistFieldResource(owner), parent.EnlistFieldResource(owner with { }));
        Assert.Equal((ushort)7, Read16(parent.EnlistFieldResource(owner), eyes + 10));
        Assert.Equal((ushort)3, Read16(owner.Storage, eyes + 10));
        NativeFieldRegistrationMachine.RestoreRetainedPresentation(registered.State, changed.Memory, [Presentation(0, 1), Presentation(1, 0)], changed.CrtRandomAfter);
        NativeFieldResourceOwner bindings = parent.View.ReadResourcePointer(InstanceTable)!.FieldStorage!.InstanceBindings!;
        Assert.Equal(owner.Storage.ReadUInt32(88) + 8, parent.EnlistFieldResource(bindings).ReadUInt32(8));
        parent.CommitNestedCheckpoint(depth);
        if (commit)
        {
            parent.CommitCheckpoint();
        }
        else
        {
            parent.RollbackCheckpoint();
        }
        parent.Freeze(parent.View);
        if (commit)
        {
            Assert.Equal((ushort)7, Read16(owner.Storage, eyes + 10));
            Assert.Equal(owner.Storage.ReadUInt32(88) + 8, bindings.Storage.ReadUInt32(8));
            Assert.Equal((byte)0x10, registered.Memory.FileStaging!.ReadByte(registered.State.Instances[0].Address + 114));
        }
        else
        {
            Assert.Equal(before, Snapshot(registered.Memory));
        }
        registered.State.Validate(registered.Memory);
    }

    [Fact]
    public void Private_child_publication_remains_inside_parent_resource_rollback()
    {
        NativeFieldRegistrationStep registered = Register(NativeFieldRuntimeProfile.FF8EnExecutableHash);
        string before = Snapshot(registered.Memory);
        NativeFieldResourceOwner owner = ResourceOwner(registered.Memory);
        uint eyes = owner.Storage.ReadUInt32(20);
        using NativeBattleFileStagingTransaction parent = new(registered.Memory.FileStaging!);
        parent.BeginCheckpoint();
        using (NativeBattleFileStagingTransaction child = NativeBattleFileStagingTransaction.CreatePrivateChild(parent.View))
        {
            NativeBattleRetainedMemory retained = registered.Memory with { FileStaging = child.View };
            NativeBattleFieldResourceReloadResult changed = NativeFieldRegistrationMachine.SetEyes532340(registered.State, retained, 1, 0x80000, 8, NativeCrtRandomState.FromColdProcess());
            child.Freeze(changed.Memory.FileStaging!);
        }
        Assert.Equal((ushort)8, Read16(parent.EnlistFieldResource(owner), eyes + 10));
        Assert.Equal((ushort)3, Read16(owner.Storage, eyes + 10));
        parent.RollbackCheckpoint();
        parent.Freeze(parent.View);
        Assert.Equal(before, Snapshot(registered.Memory));
    }

    [Fact]
    public void Old_guest_snapshot_adapter_uses_the_same_animation_and_eye_algorithms()
    {
        NativeFieldRegistrationStep owned = Register(NativeFieldRuntimeProfile.FF8EnExecutableHash);
        NativeFieldRegistrationStep legacy = Legacy(owned);
        string stateJson = JsonSerializer.Serialize(legacy.State);
        Assert.DoesNotContain("ResourceLength", stateJson);
        Assert.DoesNotContain("ResourceReference", stateJson);
        legacy = legacy with { State = JsonSerializer.Deserialize<NativeFieldRegistrationState>(stateJson)!, Memory = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleRetainedMemory>(Snapshot(legacy.Memory))!) };
        NativeFieldRetainedPresentation[] presentation = [Presentation(0, 1) with { EyeMode = 0x80000, EyeBlinkParameter611 = 7 }, Presentation(1, 0)];
        NativeCrtRandomState random = NativeCrtRandomState.FromColdProcess();
        NativeBattleFieldResourceReloadResult a = NativeFieldRegistrationMachine.RestoreRetainedPresentation(owned.State, owned.Memory, presentation, random);
        NativeBattleFieldResourceReloadResult b = NativeFieldRegistrationMachine.RestoreRetainedPresentation(legacy.State, legacy.Memory, presentation, random);
        Assert.Equal(a.CrtRandomAfter, b.CrtRandomAfter);
        for (int index = 0; index < 2; index++)
        {
            foreach (int offset in new[] { 12, 14, 80, 82, 98, 102, 103, 114, 116, 124, 126 })
            {
                Assert.Equal(a.Memory.FileStaging!.Read(owned.State.Instances[index].Address + (uint)offset, 1), b.Memory.FileStaging!.Read(legacy.State.Instances[index].Address + (uint)offset, 1));
            }
            uint local = a.Memory.FileStaging!.ReadResourcePointer(InstanceTable + (uint)index * 4)!.FieldStorage!.InstanceBindings!.Storage.ReadUInt32(8);
            Assert.Equal(local, b.Memory.FileStaging!.ReadUInt32(legacy.State.Instances[index].Address + 8) - legacy.State.Resources[0].Address);
        }
        NativeFieldResourceOwner owner = ResourceOwner(a.Memory);
        uint eyeOffset = owner.Storage.ReadUInt32(20);
        Assert.Equal(Read16(owner.Storage, eyeOffset + 10), Read16(b.Memory.FileStaging!, legacy.State.Resources[0].Address + eyeOffset + 10));
        for (int frame = 0; frame < 12; frame++)
        {
            a = NativeFieldRegistrationMachine.AdvanceEyes530810(owned.State, a.Memory, a.CrtRandomAfter);
            b = NativeFieldRegistrationMachine.AdvanceEyes530810(legacy.State, b.Memory, b.CrtRandomAfter);
            Assert.Equal(a.CrtRandomAfter, b.CrtRandomAfter);
            foreach (uint offset in new uint[] { 102, 103, 114, 116 })
            {
                Assert.Equal(a.Memory.FileStaging!.ReadByte(owned.State.Instances[0].Address + offset), b.Memory.FileStaging!.ReadByte(legacy.State.Instances[0].Address + offset));
            }
        }
    }

    [Fact]
    public void Local_cursor_bounds_write_domains_history_and_table_receipt_lifetimes_are_checked()
    {
        NativeFieldResourceOwner first = NativeFieldResourceOwner.Create(8192);
        NativeFieldResourceOwner second = NativeFieldResourceOwner.Create(8192);
        first.Storage.BindResource(0, Enumerable.Range(0, 8192).Select(index => (byte)(index % 251)).ToImmutableArray());
        using NativeBattleStagingReadWindow history = first.Storage.OpenReadWindow(0, 8192, maximumRetainedBytes: 0);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        using (NativeFieldRegistrationMemory memory = new(staging, NativeBattleFileStaging.Address, 0x1DCD6E4))
        {
            NativeFieldResourceCursor cursor = new(first, 0);
            NativeBattleResourcePointer pointer = new(null, 0x199B100) { FieldStorage = new(0) { Resource = first } };
            memory.WriteResourcePointer(ResourceTable, pointer);
            Assert.Same(pointer, memory.ReadResourcePointer(ResourceTable));
            memory.Write32(ResourceTable, pointer.StagingAddress!.Value);
            Assert.Null(memory.ReadResourcePointer(ResourceTable)!.FieldStorage);
            memory.WriteResourcePointer(ResourceTable, pointer);
            Assert.Throws<InvalidDataException>(() => memory.WritePointer(cursor, new(second, 4)));
            Assert.Throws<InvalidDataException>(() => memory.Read8(cursor + 8192));
            Assert.Throws<OverflowException>(() => _ = cursor + -1);
            memory.Write8(cursor + 5000, 0xFE);
            Assert.Equal((byte)0xFE, memory.Read8(cursor + 5000));
            memory.Copy(cursor + 4999, new NativeFieldResourceCursor(second, 0), 4);
            Assert.Equal((byte)0xFE, memory.Read8(new NativeFieldResourceCursor(second, 1)));
            memory.Finish();
        }
        Assert.Equal((byte)(5000 % 251), history.ReadByte(5000));
        Assert.Equal(0, history.RetainedByteCount);
        Assert.Equal((byte)0xFE, first.Storage.ReadByte(5000));
        Assert.Equal((byte)0xFE, second.Storage.ReadByte(1));
        Assert.Throws<InvalidDataException>(() => staging.WriteResourcePointer(ResourceTable + 4, new(null, 0x199B100) { FieldStorage = new(0) { Resource = first } }));
        Assert.Throws<InvalidDataException>(() => staging.WriteResourcePointer(ResourceTable, new(new(0, 0), null) { FieldStorage = new(0) { Resource = first } }));
    }

    [Fact]
    public void Malformed_optional_owner_payloads_fail_as_invalid_data()
    {
        Assert.Throws<InvalidDataException>(() => new NativeFieldResourceOwner(null!, 16).Validate());
        Assert.Throws<InvalidDataException>(() => new NativeFieldResourceOwner(NativeBattleFileStaging.Unknown(), 16).Validate());
        NativeFieldRegistrationStep registered = Register(NativeFieldRuntimeProfile.FF8EnExecutableHash);
        JsonObject json = JsonNode.Parse(Snapshot(registered.Memory))!.AsObject();
        JsonObject pointer = json["FileStaging"]!["ResourcePointers"]![ResourceTable.ToString()]!.AsObject();
        pointer["FieldStorage"]!["Resource"]!["Storage"] = null;
        Assert.Throws<InvalidDataException>(() => JsonSerializer.Deserialize<NativeBattleRetainedMemory>(json.ToJsonString()));
    }

    private static NativeFieldRetainedPresentation Presentation(int objectId, byte animation) => new(objectId, animation, 0, false, 0, true, 17, 0, 128, 128, 128, false, false, 0);
    private static NativeFieldResourceOwner ResourceOwner(NativeBattleRetainedMemory memory) => memory.FileStaging!.ReadResourcePointer(ResourceTable)!.FieldStorage!.Resource!;
    private static ushort Read16(NativeBattleFileStaging storage, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(storage.Read(address, 2));
    private static string Snapshot(NativeBattleRetainedMemory memory) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(memory));

    private static NativeFieldRegistrationStep Register(string image) => NativeFieldRegistrationMachine.Register(NativeFieldRegistrationEntry.BattleReturn, Fixture(image), [new(0, 0, 0, 1), new(1, 0, 0, 1)], NativeBattleRetainedMemory.FromColdProcess(image));

    internal static NativeFieldRegistrationResources Fixture(string image)
    {
        byte[] program = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(6), 12);
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(10), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(program.AsSpan(12), 5);
        BinaryPrimitives.WriteUInt32LittleEndian(program.AsSpan(16), 6);
        const int textureOffset = 116;
        byte[] one = new byte[4 + textureOffset + 546];
        BinaryPrimitives.WriteUInt32LittleEndian(one, 1);
        Span<byte> model = one.AsSpan(4);
        BinaryPrimitives.WriteUInt32LittleEndian(model[8..], 24);
        BinaryPrimitives.WriteUInt32LittleEndian(model[40..], 64);
        BinaryPrimitives.WriteUInt32LittleEndian(model[56..], 88);
        Span<byte> eyes = model.Slice(64, 24);
        eyes[2] = 8;
        eyes[3] = 8;
        eyes[4] = 1;
        eyes[5] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(eyes[10..], 3);
        BinaryPrimitives.WriteUInt16LittleEndian(eyes[16..], 2);
        Span<byte> animation = model.Slice(88, 28);
        BinaryPrimitives.WriteUInt16LittleEndian(animation, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(animation[2..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(animation[12..], 2);
        Span<byte> texture = model[textureOffset..];
        BinaryPrimitives.WriteUInt32LittleEndian(texture, 0x10);
        BinaryPrimitives.WriteUInt32LittleEndian(texture[4..], 9);
        BinaryPrimitives.WriteUInt32LittleEndian(texture[8..], 524);
        BinaryPrimitives.WriteUInt16LittleEndian(texture[16..], 256);
        BinaryPrimitives.WriteUInt16LittleEndian(texture[18..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(texture[532..], 14);
        BinaryPrimitives.WriteUInt16LittleEndian(texture[540..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(texture[542..], 1);
        NativeFieldModelSource code = Source("sample.jsm", program);
        NativeFieldOneModelEntry entry = new(4, one.Length - 4, 0, 0, [textureOffset], "n000") { SourceWord8 = 0, RawName = [110, 48, 48, 48, 0, 0, 0, 0], RawTextureWords = [textureOffset] };
        return new(72, "sample", image, code.Sha256, Source("chara.one", one), [entry], []) { Loader = new(44, 0, code, null, [0x00F00200], [0x008003C0]) };
    }

    private static NativeFieldRegistrationStep Legacy(NativeFieldRegistrationStep owned)
    {
        NativeBattleRetainedMemory memory = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(owned.Memory));
        NativeFieldRegisteredResource resource = owned.State.Resources[0];
        NativeFieldResourceOwner owner = ResourceOwner(memory);
        (byte[] data, bool[] known) = owner.Storage.ReadSnapshot(0, owner.Length);
        foreach (int field in HeaderPointers)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(field), checked(BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(field)) + resource.Address));
        }
        NativeBattleFileStaging staging = memory.FileStaging!;
        staging.WriteResourcePointer(ResourceTable, new(null, resource.Address));
        staging.WorkingOwner.WriteWithKnownness(resource.Address, data, known);
        ImmutableArray<NativeFieldRegisteredInstance>.Builder instances = ImmutableArray.CreateBuilder<NativeFieldRegisteredInstance>();
        uint end = 0;
        for (int index = 0; index < owned.State.Instances.Length; index++)
        {
            NativeFieldRegisteredInstance instance = owned.State.Instances[index];
            uint address = checked(resource.Address + 0x1000u + (uint)index * 0x1000);
            NativeFieldResourceOwner bindings = staging.ReadResourcePointer(InstanceTable + (uint)index * 4)!.FieldStorage!.InstanceBindings!;
            (byte[] bytes, bool[] observed) = bindings.Storage.ReadSnapshot(0, bindings.Length);
            (byte[] header, bool[] headerKnown) = owned.Memory.FileStaging!.ReadSnapshot(instance.Address, 152);
            header.CopyTo(bytes, 0);
            headerKnown.CopyTo(observed, 0);
            foreach (int field in AnimationPointers)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(field), checked(bindings.Storage.ReadUInt32((uint)field) + resource.Address));
                observed.AsSpan(field, 4).Fill(true);
            }
            foreach (int field in new[] { 4, 84, 88, 92, 128 })
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(field), checked(bindings.Storage.ReadUInt32((uint)field) + address));
                observed.AsSpan(field, 4).Fill(true);
            }
            staging.WorkingOwner.WriteWithKnownness(address, bytes, observed);
            staging.WriteResourcePointer(InstanceTable + (uint)index * 4, new(null, address));
            instances.Add(instance with { Address = address, ResourceReference = null });
            end = checked(address + (uint)bytes.Length);
        }
        NativeFieldRegistrationResources original = Fixture(owned.State.MemoryLayout.ExecutableHash);
        uint relocated = checked(owned.State.MemoryLayout.Limit - (uint)original.One.Data.Length);
        staging.BindResource(relocated, original.One.Data);
        uint geometry = relocated + 4;
        uint animation = geometry + 88;
        staging.WriteUInt32(geometry + 56, animation);
        NativeFieldRegistrationState state = owned.State with { Resources = [resource with { ResourceLength = null, Input = null, GeometryAddress = geometry, AnimationAddress = animation }], Instances = instances.ToImmutable(), EndAddress = end, InputsOwned = null, RelocatedSourceAddress = relocated, RelocatedSourceLength = original.One.Data.Length };
        state.Validate(memory);
        return new(state, memory, []);
    }

    private static NativeFieldModelSource Source(string member, byte[] bytes) => new(member, Convert.ToHexString(SHA256.HashData(bytes)), bytes.ToImmutableArray());
}
