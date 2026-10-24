using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect140Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect140OwnedModelTests
{
    private const uint Main = 0x20DFAB8;
    private const uint Model = Main + 0x64;
    private const uint Directory = Main + 0x100;
    private const uint Source = 0x11B537C;

    [Fact]
    public void Initialize_writes_original_fields_and_actual_descriptor_aliases_only()
    {
        NativeBattleEffect140Resources resources = Resources();
        NativeBattleEffect140Frame frame = NativeBattleEffect140ControllerTests.Frame(0);
        frame = WriteBytes(frame, Model, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA7, 0x9C).ToArray()), 0x9C);
        frame = NativeBattleEffect140OwnedModel.Initialize6EC060(frame, Model, Directory, resources);
        Assert.Equal((ushort)2, Word(frame, Model));
        Assert.Equal(0xA7A7A7A7u, Dword(frame, Model + 4));
        Assert.Equal(0x808080u, Dword(frame, Model + 0x28));
        Assert.Equal(uint.MaxValue, Dword(frame, Model + 0x7C));
        Assert.Equal(Directory, Dword(frame, Model + 0x64));
        Assert.Equal(Directory, Dword(frame, Model + 0x68));
        Assert.Equal(new uint[] { Source + 24, Source + 88, Source + 96, Source + 110 }, Enumerable.Range(0, 4).Select(index => Dword(frame, Directory + (uint)(index * 4))));
        Assert.Equal(new byte[] { 0, 0 }, ReadBytes(frame, Model + 0x60, 2).Read(0, 2));
        Assert.Equal((byte)0xA7, ReadBytes(frame, Model + 0x62, 1).Read(0, 1)[0]);
    }

    [Fact]
    public void Select_and_regular_step_use_task_D2_D4_cursors_and_current_staged_pose_after_JSON()
    {
        NativeBattleEffect140Resources resources = Resources();
        NativeBattleEffect140Frame frame = Initialized(resources);
        frame = NativeBattleEffect140OwnedModel.Select6574D0(frame, Model, 0, resources);
        Assert.Equal((ushort)3, Word(frame, Main + 0xD2));
        Assert.Equal((ushort)3, Word(frame, Main + 0xD4));
        Assert.Equal((byte)1, ReadBytes(frame, Main + 0xD6, 1).Read(0, 1)[0]);
        frame = frame with { Staging = frame.Staging!.Write(Source + 24 + 8, new byte[] { 123, 0, 56, 254, 21, 3 }) };
        frame = frame with
        {
            Staging = JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(frame.Staging))!,
            Effect = JsonSerializer.Deserialize<NativeBattleOriginalEffectState>(JsonSerializer.Serialize(frame.Effect))!
        };
        NativeBattleEffect140Frame advanced = NativeBattleEffect140OwnedModel.Advance6FBDB0(frame, Model, resources);
        NativeBattleEffect140OwnedModelSnapshot snapshot = NativeBattleEffect140OwnedModel.ReadCurrent(advanced, Model, resources);
        Assert.Equal(new NativeBattleVector(123, -456, 789), snapshot.Pose.Translation);
        Assert.Equal((ushort)5, Word(advanced, Main + 0xD2));
        Assert.Equal((ushort)6, Word(advanced, Main + 0xD4));
        Assert.Equal((byte)2, ReadBytes(advanced, Main + 0xD6, 1).Read(0, 1)[0]);
        NativeBattleEffect140Frame restarted = NativeBattleEffect140OwnedModel.Advance6FBDB0(advanced, Model, resources);
        Assert.Equal((byte)1, ReadBytes(restarted, Main + 0xD6, 1).Read(0, 1)[0]);
        Assert.Equal(default(NativeBattleVector), NativeBattleEffect140OwnedModel.ReadCurrent(restarted, Model, resources).Pose.Translation);
    }

    [Fact]
    public void Snapshot_preserves_current_warm_bone_matrix_and_unknown_padding_without_redecode()
    {
        NativeBattleEffect140Resources resources = Resources();
        NativeBattleEffect140Frame frame = NativeBattleEffect140OwnedModel.Select6574D0(Initialized(resources), Model, 0, resources);
        uint matrix = Source + 24 + 32;
        NativeBattleEffectArena current = ReadBytes(frame, matrix, 32).Write(0, BitConverter.GetBytes((short)1234)).Write(20, BitConverter.GetBytes(7654321)).MarkUnknown(18, 2);
        frame = WriteBytes(frame, matrix, current, 32);
        NativeBattleEffect140OwnedModelSnapshot snapshot = NativeBattleEffect140OwnedModel.ReadCurrent(frame, Model, resources);
        Assert.Equal((short)1234, BinaryPrimitives.ReadInt16LittleEndian(snapshot.Bones[0].Bytes.AsSpan()));
        Assert.Equal(7654321, BinaryPrimitives.ReadInt32LittleEndian(snapshot.Bones[0].Bytes.AsSpan().Slice(20)));
        Assert.False(snapshot.Bones[0].Known[18]);
        Assert.False(snapshot.Bones[0].Known[19]);
        Assert.Empty(snapshot.Geometry);
        Assert.Equal(current.Read(0, 18), ReadBytes(frame, matrix, 18).Read(0, 18));
    }

    [Fact]
    public void Current_source_and_descriptor_must_be_observed_not_rehydrated()
    {
        NativeBattleEffect140Resources resources = Resources();
        NativeBattleEffect140Frame frame = Initialized(resources);
        frame = WriteBytes(frame, Directory, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(Source + 25)), 4);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect140OwnedModel.ReadCurrent(frame, Model, resources));
        frame = Initialized(resources) with { Staging = NativeBattleFileStaging.Unknown().Rebase(0x1100000) };
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect140OwnedModel.ReadCurrent(frame, Model, resources));
    }

    private static NativeBattleEffect140Frame Initialized(NativeBattleEffect140Resources resources)
    {
        NativeBattleEffect140Frame frame = NativeBattleEffect140ControllerTests.Frame(0);
        frame = frame with { Staging = frame.Staging!.Write(Source, resources.Model.Bytes.AsSpan()) };
        return NativeBattleEffect140OwnedModel.Initialize6EC060(frame, Model, Directory, resources);
    }

    private static NativeBattleEffect140Resources Resources()
    {
        byte[] bytes = new byte[110];
        int[] header = [4, 24, 88, 96, 110, 110];
        for (int index = 0; index < header.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(index * 4), header[index]);
        }
        bytes[24] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 1024);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), -1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(96), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(100), 8);
        bytes[104] = 2;
        NativeBattlePoseSource pose = new(bytes.AsSpan(24, 64).ToArray().ToImmutableArray(), bytes.AsSpan(96, 14).ToArray().ToImmutableArray());
        NativeBattleEffectOwnedModelResource model = new(Source, bytes.ToImmutableArray(), pose, []);
        return new(null!, model, null!, [], ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, null!, ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
    }

    private static ushort Word(NativeBattleEffect140Frame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(frame, address, 2).Read(0, 2));
    private static uint Dword(NativeBattleEffect140Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, address, 4).Read(0, 4));
}
