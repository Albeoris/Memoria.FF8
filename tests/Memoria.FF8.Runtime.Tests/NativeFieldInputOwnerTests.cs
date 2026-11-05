using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeFieldInputOwnerTests
{
    [Theory]
    [InlineData(NativeFieldRuntimeProfile.FF8EnExecutableHash, false)]
    [InlineData(NativeFieldRuntimeProfile.RemasteredEnImageHash, false)]
    [InlineData(NativeFieldRuntimeProfile.FF8EnExecutableHash, true)]
    [InlineData(NativeFieldRuntimeProfile.RemasteredEnImageHash, true)]
    public void Geometry_texture_and_split_animation_sources_beyond_the_guest_arena_use_the_same_conversion(string image, bool mainCharacter)
    {
        NativeFieldRegistrationResources small = Fixture(image, mainCharacter, 0);
        NativeFieldRegistrationResources large = Fixture(image, mainCharacter, 3 * 1024 * 1024 + 64);
        NativeFieldRegistrationStep a = Register(small);
        NativeFieldRegistrationStep b = Register(large);
        NativeFieldRegisteredResource resource = Assert.Single(b.State.Resources);
        Assert.True(b.State.InputsOwned);
        Assert.Null(b.State.RelocatedSourceAddress);
        Assert.Null(b.State.RelocatedSourceLength);
        Assert.Null(resource.GeometryAddress);
        Assert.Null(resource.AnimationAddress);
        Assert.True(resource.SourceOffset > 3 * 1024 * 1024);
        Assert.NotNull(resource.Input);
        Assert.Equal(mainCharacter ? large.One.Sha256 : resource.SourceHash, resource.Input.AnimationSourceHash);
        Assert.Equal(mainCharacter ? 4 : resource.SourceOffset + 264, resource.Input.AnimationSourceOffset);
        Assert.Equal(a.State.EndAddress, b.State.EndAddress);
        Assert.True(b.State.EndAddress - b.State.PreModelAddress < 2048);
        NativeFieldResourceOwner first = Owner(a.Memory);
        NativeFieldResourceOwner second = Owner(b.Memory);
        Assert.Equal(first.Length, second.Length);
        Assert.Equal(first.Storage.ReadSnapshot(0, first.Length).Data, second.Storage.ReadSnapshot(0, second.Length).Data);
        Assert.Equal(first.Storage.ReadSnapshot(0, first.Length).Known, second.Storage.ReadSnapshot(0, second.Length).Known);
        Assert.Equal(a.Uploads.Select(upload => upload.Bytes.ToArray()), b.Uploads.Select(upload => upload.Bytes.ToArray()));
        Assert.Equal(new byte[] { 0x34, 0x12 }, b.Uploads[0].Bytes.ToArray());
        uint bones = second.Storage.ReadUInt32(0);
        Assert.Equal((ushort)123, Read16(second.Storage, bones + 64 + 8));
        Assert.Equal(0u, second.Storage.ReadUInt32(bones + 64 + 52));
        uint vertices = second.Storage.ReadUInt32(24);
        Assert.Equal(new short[] { -16, 32, -48 }, Enumerable.Range(0, 3).Select(axis => unchecked((short)Read16(second.Storage, vertices + (uint)axis * 2))));
        Assert.Equal((ushort)0, Read16(second.Storage, second.Storage.ReadUInt32(28) + 14));
        Assert.Equal((ushort)0, Read16(second.Storage, second.Storage.ReadUInt32(32) + 6));
        NativeBattleFileStaging guest = b.Memory.FileStaging!;
        foreach (uint address in new uint[] { 0x1DCA328 + 16, 0x1DCA328 + 40, 0x1DCA328 + 44, 0x1DC3CDC, 0x1DC3CD8, 0x1DC3CE8 })
        {
            Assert.All(guest.ReadSnapshot(address, 4).Known, value => Assert.False(value));
        }
        Assert.Equal(mainCharacter ? large.MainCharacters[0].Sha256 : large.One.Sha256, resource.SourceHash);
        NativeFieldRegistrationState state = JsonSerializer.Deserialize<NativeFieldRegistrationState>(JsonSerializer.Serialize(b.State))!;
        NativeFieldEyeProcess snapshot = new NativeFieldEyeProcess(1, b.Memory, NativeCrtRandomState.FromColdProcess()).CaptureSnapshot();
        NativeFieldEyeProcess restored = JsonSerializer.Deserialize<NativeFieldEyeProcess>(JsonSerializer.Serialize(snapshot))!.RestoreOwned();
        restored.Validate(state);
        NativeBattleFieldResourceReloadResult next = NativeFieldRegistrationMachine.RestoreRetainedPresentation(state, restored.Memory, [new(0, 1, 0, false, 0, true, 17, 0, 31, 63, 95, false, false, 0)]);
        state.Validate(next.Memory);
        NativeFieldResourceOwner sidecar = next.Memory.FileStaging!.ReadResourcePointer(0x1DCB340)!.FieldStorage!.InstanceBindings!;
        Assert.Equal(second.Storage.ReadUInt32(88) + 8, sidecar.Storage.ReadUInt32(8));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Repeated_TIM_source_observes_palette_COW_without_mutating_immutable_ONE_or_MCH(bool mainCharacter)
    {
        NativeFieldRegistrationResources source = Fixture(NativeFieldRuntimeProfile.FF8EnExecutableHash, mainCharacter, 0, true);
        NativeFieldModelSource raw = mainCharacter ? source.MainCharacters[0] : source.One;
        ImmutableArray<byte> original = raw.Data;
        int texture = mainCharacter ? 16 + 292 : 4 + 292;
        byte[] first = NativeFieldRegistrationMachine.ProjectPalette532A40(raw.Data.AsSpan(texture + 20, 512), "n000"u8, source.Loader!.Colors);
        byte[] second = NativeFieldRegistrationMachine.ProjectPalette532A40(first, "n000"u8, source.Loader.Colors);
        NativeFieldRegistrationStep step = Register(source);
        Assert.Equal(first, step.Uploads[1].Bytes.ToArray());
        Assert.Equal(second, step.Uploads[3].Bytes.ToArray());
        Assert.NotEqual(first, second);
        Assert.Equal(original, raw.Data);
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(raw.Data.AsSpan(texture + 20, 2)));
    }

    [Fact]
    public void Native_texture_alias_flags_remain_format_words_while_source_cursors_stay_logical()
    {
        NativeFieldRegistrationResources template = Fixture(NativeFieldRuntimeProfile.RemasteredEnImageHash, false, 0);
        int size = template.Directory[0].Size;
        byte[] bytes = new byte[4 + size * 2];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 2);
        template.One.Data.AsSpan(4, size).CopyTo(bytes.AsSpan(4));
        template.One.Data.AsSpan(4, size).CopyTo(bytes.AsSpan(4 + size));
        NativeFieldOneModelEntry original = template.Directory[0];
        NativeFieldOneModelEntry alias = original with { Offset = 4 + size, Flag = 0xA0000000 };
        NativeFieldRegistrationResources source = template with { One = Source("chara.one", bytes), Directory = [original, alias] };
        NativeFieldRegistrationStep step = NativeFieldRegistrationMachine.Register(NativeFieldRegistrationEntry.BattleReturn, source, [new(0, 0, 0, 1), new(1, 1, 0, 1)], NativeBattleRetainedMemory.FromColdProcess(source.ExecutableHash));
        Assert.Equal(2, step.Uploads.Length);
        Assert.Equal(step.State.Resources[0].Textures, step.State.Resources[1].Textures);
        Assert.Equal(0xA0000000u, step.Memory.FileStaging!.ReadUInt32(0x1DCA328 + 48 + 16));
        Assert.All(step.Memory.FileStaging.ReadSnapshot(0x1DCA328 + 16, 4).Known, value => Assert.False(value));
        step.State.Validate(step.Memory);
    }

    [Fact]
    public void Borrowed_input_retains_the_original_slice_and_historical_reader_with_checked_bounds()
    {
        ImmutableArray<byte> bytes = Enumerable.Range(0, 12000).Select(index => (byte)(index % 251)).ToImmutableArray();
        NativeFieldResourceOwner owner = NativeFieldResourceOwner.Borrow(bytes, 17, 8192);
        Assert.True(owner.Storage.TryBorrowResource(0, 8192, out ImmutableArray<byte> retained, out int offset));
        Assert.Equal(bytes, retained);
        Assert.Equal(17, offset);
        Assert.Equal(0, owner.Storage.MaterializedPageCount);
        using NativeBattleStagingReadWindow history = owner.Storage.OpenReadWindow(0, 8192, maximumRetainedBytes: 0);
        using (NativeFieldRegistrationMemory memory = new(NativeBattleFileStaging.Unknown(), NativeBattleFileStaging.Address, 0x1DCD6E4))
        {
            NativeFieldResourceCursor cursor = new(owner, 0);
            memory.Write8(cursor + 5000, 0xFE);
            Assert.Throws<InvalidDataException>(() => memory.Read32(cursor + 8190));
            memory.Finish();
        }
        Assert.Equal(bytes[5017], history.ReadByte(5000));
        Assert.Equal((byte)0xFE, owner.Storage.ReadByte(5000));
        Assert.Equal((byte)(5017 % 251), bytes[5017]);
        Assert.Throws<InvalidDataException>(() => NativeFieldResourceOwner.Borrow(bytes, 10000, 3000));
    }

    [Fact]
    public void Invalid_selected_source_rolls_back_registration_and_owned_coordinate_metadata_is_checked()
    {
        NativeFieldRegistrationResources source = Fixture(NativeFieldRuntimeProfile.FF8EnExecutableHash, false, 0);
        NativeFieldRegistrationStep registered = Register(source);
        string before = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(registered.Memory));
        NativeFieldOneModelEntry entry = source.Directory[0] with { RawTextureWords = [(uint)source.One.Data.Length + 4] };
        Assert.Throws<InvalidDataException>(() => NativeFieldRegistrationMachine.Register(NativeFieldRegistrationEntry.BattleReturn, source with { Directory = [entry] }, [new(0, 0, 0, 1)], registered.Memory));
        Assert.Equal(before, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(registered.Memory)));
        Assert.Throws<InvalidDataException>(() => (registered.State with { RelocatedSourceAddress = registered.State.PreModelAddress, RelocatedSourceLength = 4 }).Validate(registered.Memory));
        NativeFieldRegisteredResource resource = registered.State.Resources[0];
        Assert.Throws<InvalidDataException>(() => (registered.State with { Resources = [resource with { Input = resource.Input! with { AnimationSourceHash = null! } }] }).Validate(registered.Memory));
        Assert.Throws<InvalidDataException>(() => (registered.State with { Resources = [resource with { GeometryAddress = registered.State.PreModelAddress }] }).Validate(registered.Memory));
    }

    private static NativeFieldRegistrationStep Register(NativeFieldRegistrationResources source) => NativeFieldRegistrationMachine.Register(NativeFieldRegistrationEntry.BattleReturn, source, [new(0, 0, 0, 1)], NativeBattleRetainedMemory.FromColdProcess(source.ExecutableHash));
    private static NativeFieldResourceOwner Owner(NativeBattleRetainedMemory memory) => memory.FileStaging!.ReadResourcePointer(0x1DCB0F0)!.FieldStorage!.Resource!;
    private static ushort Read16(NativeBattleFileStaging storage, uint offset) => BinaryPrimitives.ReadUInt16LittleEndian(storage.Read(offset, 2));

    private static NativeFieldRegistrationResources Fixture(string image, bool mainCharacter, int padding, bool repeatedTexture = false)
    {
        NativeFieldRegistrationResources template = NativeFieldResourceOwnerTests.Fixture(image);
        byte[] model = new byte[292 + 546];
        uint[] counts = [1, 1, 24, 1, 1, 1, 0, 1];
        uint[] offsets = [64, 128, 136, 160, 224, 256, 264, 0];
        for (int index = 0; index < counts.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(model.AsSpan(index * 4), counts[index]);
            BinaryPrimitives.WriteUInt32LittleEndian(model.AsSpan(32 + index * 4), offsets[index]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(model.AsSpan(64 + 8), 123);
        BinaryPrimitives.WriteUInt32LittleEndian(model.AsSpan(64 + 52), 0x12345678);
        BinaryPrimitives.WriteInt16LittleEndian(model.AsSpan(128), -16);
        BinaryPrimitives.WriteInt16LittleEndian(model.AsSpan(130), 32);
        BinaryPrimitives.WriteInt16LittleEndian(model.AsSpan(132), -48);
        template.One.Data.AsSpan(4 + 64, 24).CopyTo(model.AsSpan(136));
        BinaryPrimitives.WriteUInt32LittleEndian(model.AsSpan(160), 0x25010607);
        BinaryPrimitives.WriteInt16LittleEndian(model.AsSpan(160 + 8), 9);
        model[160 + 29] = 4;
        BinaryPrimitives.WriteUInt16LittleEndian(model.AsSpan(160 + 46), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(model.AsSpan(160 + 48), 512);
        BinaryPrimitives.WriteUInt16LittleEndian(model.AsSpan(224 + 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(model.AsSpan(224 + 14), 0x1234);
        BinaryPrimitives.WriteUInt16LittleEndian(model.AsSpan(256 + 6), 0x2222);
        template.One.Data.AsSpan(4 + 88, 28).CopyTo(model.AsSpan(264));
        template.One.Data.AsSpan(4 + 116, 546).CopyTo(model.AsSpan(292));
        BinaryPrimitives.WriteUInt16LittleEndian(model.AsSpan(292 + 20), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(model.AsSpan(292 + 544), 0x1234);
        int geometryOffset = padding + (mainCharacter ? 16 : 0);
        int texture = geometryOffset + 292;
        NativeFieldOneModelEntry entry;
        NativeFieldModelSource one;
        ImmutableArray<NativeFieldModelSource> mains;
        if (mainCharacter)
        {
            byte[] data = new byte[geometryOffset + model.Length];
            model.CopyTo(data.AsSpan(geometryOffset));
            BinaryPrimitives.WriteUInt32LittleEndian(data, (uint)texture);
            int sentinel = 4;
            if (repeatedTexture)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), (uint)texture);
                sentinel = 8;
            }
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(sentinel), uint.MaxValue);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(sentinel + 4), (uint)geometryOffset);
            byte[] animation = new byte[4 + 28];
            BinaryPrimitives.WriteUInt32LittleEndian(animation, 1);
            model.AsSpan(264, 28).CopyTo(animation.AsSpan(4));
            one = Source("chara.one", animation);
            mains = [Source("chara/n000.mch", data)];
            entry = template.Directory[0] with { Size = 28, Flag = 0xD0100000, ModelOffset = 0, Textures = [], RawTextureWords = [] };
        }
        else
        {
            byte[] data = new byte[4 + geometryOffset + model.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(data, 1);
            model.CopyTo(data.AsSpan(4 + geometryOffset));
            one = Source("chara.one", data);
            mains = [];
            entry = template.Directory[0] with { Size = data.Length - 4, ModelOffset = geometryOffset, Textures = repeatedTexture ? [texture, texture] : [texture], RawTextureWords = repeatedTexture ? [(uint)texture, (uint)texture] : [(uint)texture] };
        }
        NativeFieldRegistrationLoaderInputs loader = template.Loader!;
        if (repeatedTexture)
        {
            loader = loader with { Colors = Source("sample.pcb", [110, 48, 48, 48, 0, 136, 128, 128]), PaletteCoordinates = [0x00F00200, 0x00F10200], ImageCoordinates = [0x008003C0, 0x008003C2] };
        }
        return template with { One = one, Directory = [entry], MainCharacters = mains, Loader = loader };
    }

    private static NativeFieldModelSource Source(string member, byte[] bytes) => new(member, Convert.ToHexString(SHA256.HashData(bytes)), bytes.ToImmutableArray());
}
