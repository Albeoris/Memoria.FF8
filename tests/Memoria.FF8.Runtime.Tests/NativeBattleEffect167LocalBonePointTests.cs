using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect167LocalBonePointTests
{
    [Fact]
    public void Disabled_model_copies_only_position_without_reading_bones_or_temporary_memory()
    {
        NativeBattleGraphicsModel model = new(0, 0) { Transform = new(new(0, 0, 0), new(123, -456, 789), 12, 0, 700) };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleEffect167LocalBonePointStep result = NativeBattleEffect167LocalBonePoint.Query(memory, staging, model, null!, int.MaxValue, int.MinValue, Output());
        Assert.Equal(-1, result.Result);
        Assert.Same(memory, result.Memory);
        Assert.Same(staging, result.Staging);
        Assert.Equal(new short[] { 123, -456, 789 }, Coordinates(result.Output8));
        Assert.Equal(Output().Read(6, 2), result.Output8.Read(6, 2));
    }

    [Theory]
    [InlineData(1, 2048, 1000)]
    [InlineData(1, -2048, -1000)]
    [InlineData(1, 0, 0)]
    [InlineData(0, 4096, 0)]
    [InlineData(241, 0, 2000)]
    public void Enabled_query_uses_current_local_bone_full_MAC_and_Z_distance(int selector, int fraction, int distance)
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel model = Model(source);
        Assert.Null(model.GeometryWorldMatrix);
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffect167LocalBonePointStep result = NativeBattleEffect167LocalBonePoint.Query(memory, Staging(), model, source, selector, fraction, Output());
        Assert.Equal(0, result.Result);
        int[] expected = [70000, -70000, 32768 + distance];
        Assert.Equal(expected.Select(value => unchecked((short)value)), Coordinates(result.Output8));
        Assert.Equal(expected.Select(value => (int?)value), result.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(Output().Read(6, 2), result.Output8.Read(6, 2));
        Assert.Equal(memory.GeometryRegisters!.AnchorScratch8, result.Memory.GeometryRegisters!.AnchorScratch8);
        Assert.Equal(memory.BoneMatrixAnchor2795BC0, result.Memory.BoneMatrixAnchor2795BC0);
        Assert.False(result.Memory.ScriptedColorRegisters.NormalInputKnown!.Value[6]);
        Assert.False(result.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
    }

    [Fact]
    public void Actual52_byte_temporary_retains_matrix_and_gaps_and_survives_independent_JSON()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel model = Model(source);
        NativeBattleFileStaging staging = Staging();
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffect167LocalBonePointStep result = NativeBattleEffect167LocalBonePoint.Query(memory, staging, model, source, 1, 2048, Output());
        uint address = NativeBattleTemporaryMemory.Address;
        AssertRange(staging, result.Staging, address, 32);
        AssertRange(staging, result.Staging, address + 44, 4);
        for (int axis = 0; axis < 3; axis++)
        {
            Assert.Equal(result.Memory.ScriptedColorRegisters!.Mac[axis], BinaryPrimitives.ReadInt32LittleEndian(result.Staging.Read(address + 32u + (uint)(axis * 4), 4)));
        }
        Assert.Equal(result.Memory.ScriptedColorRegisters!.Flags, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging.Read(address + 48, 4)));
        Assert.Equal(address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        NativeBattleEffectMemory coldMemory = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        NativeBattleFileStaging coldStaging = JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(staging))!;
        NativeBattleGraphicsModel coldModel = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(model))!;
        NativeBattleEffect167LocalBonePointStep cold = NativeBattleEffect167LocalBonePoint.Query(coldMemory, coldStaging, coldModel, source, 1, 2048, Output());
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(cold));
    }

    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
    private static NativeBattleEffectArena Output() => NativeBattleEffectArena.Clear().Write(0, new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 });
    private static short[] Coordinates(NativeBattleEffectArena point) => Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt16LittleEndian(point.Read(axis * 2, 2))).ToArray();

    private static NativeBattleFileStaging Staging() => NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, Enumerable.Range(0, 52).Select(index => (byte)(0x80 + index)).ToArray()).MarkUnknown(NativeBattleTemporaryMemory.Address + 19, 1).MarkUnknown(NativeBattleTemporaryMemory.Address + 45, 1);

    private static NativeBattlePoseSource Source()
    {
        byte[] skeleton = new byte[112];
        skeleton[0] = 2;
        skeleton[5] = 0x81;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(18), 3000);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(64), 0);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(66), 2000);
        return new(skeleton.ToImmutableArray(), [1]);
    }

    private static NativeBattleGraphicsModel Model(NativeBattlePoseSource source)
    {
        NativeBattleGraphicsModel model = new(0, 3) { SequenceState = new() { DecodedPose = new(new(0, 0, 0), [new(new(0, 0, 0), new(1024, 1024, 1024)), new(new(0, 0, 0), new(1024, 1024, 1024))], false) } };
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, source);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { X = 70000, Y = -70000, Z = 32768 };
        return model with { CurrentBodyBoneMatrices = model.CurrentBodyBoneMatrices! with { Matrices = [matrix, matrix] } };
    }

    private static void AssertRange(NativeBattleFileStaging expected, NativeBattleFileStaging actual, uint address, int count)
    {
        (byte[] expectedBytes, bool[] expectedKnown) = expected.ReadSnapshot(address, count);
        (byte[] actualBytes, bool[] actualKnown) = actual.ReadSnapshot(address, count);
        Assert.Equal(expectedBytes, actualBytes);
        Assert.Equal(expectedKnown, actualKnown);
    }
}
