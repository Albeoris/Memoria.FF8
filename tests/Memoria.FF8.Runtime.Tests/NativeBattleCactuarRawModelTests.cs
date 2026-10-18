using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCactuarRawModelTests
{
    [Fact]
    public void InitializationWritesOnlyItsActualModelAndControlFieldsAndKeepsNormalSpeedExplicit()
    {
        NativeBattleEffectOwnedModelResource resource = Resource();
        NativeBattleCactuarRawModelState retained = new(Enumerable.Repeat((byte)0xA5, 0x90).ToImmutableArray(), Enumerable.Repeat((byte)0xB6, 0x34).ToImmutableArray(), null, null, null, null);
        NativeBattleCactuarRawModelState initialized = NativeBattleCactuarRawModel.Initialize5A8F40(retained, resource, 0x20DFAB8, 1);
        Assert.Equal((ushort)2, U16(initialized.Model, 0));
        Assert.Equal(0x00808080u, U32(initialized.Model, 0x28));
        Assert.Equal(0xA5A5A5A5u, U32(initialized.Model, 0x2C));
        Assert.Equal(0xA5A5A5A5u, U32(initialized.Model, 0x40));
        Assert.Equal((byte)0xA5, initialized.Model[0x6D]);
        Assert.Equal(NativeBattleCactuarRawModel.ControlAddress + 12, U32(initialized.Model, 0x64));
        Assert.Equal(NativeBattleCactuarRawModel.ControlAddress, U32(initialized.Model, 0x84));
        Assert.Equal(0x20DFAB8u + 24, U32(initialized.Control, 12));
        Assert.Equal(0x20DFAB8u + 100, U32(initialized.Control, 0x30));
        Assert.Equal((ushort)1, U16(initialized.Control, 2));
        Assert.All(initialized.Control.Take(2), value => Assert.Equal((byte)0xB6, value));
        Assert.Equal((byte)0xB6, initialized.Control[0x2F]);
        Assert.Throws<NotSupportedException>(() => NativeBattleCactuarRawModel.Select5A8FB0(initialized, resource, 0));
        NativeBattleCactuarRawModelState restored = JsonSerializer.Deserialize<NativeBattleCactuarRawModelState>(JsonSerializer.Serialize(initialized))!;
        NativeBattleCactuarRawModel.Validate(restored, resource);
        Assert.Equal(JsonSerializer.Serialize(initialized), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void RawBodyUsesSuppliedMatrixWithoutShadowBoundsOrRawStoresAndMaskZeroRetainsUnknownScratch()
    {
        NativeBattleEffectOwnedModelResource resource = Resource();
        NativeBattleCactuarRawModelState initialized = NativeBattleCactuarRawModel.Initialize5A8F40(NativeBattleCactuarRawModelState.FromColdProcess(), resource, 0x20DFAB8, 1);
        byte[] model = initialized.Model.ToArray();
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(model.AsSpan(0x40 + 2 * index), index % 4 == 0 ? (short)4096 : (short)0);
        }
        NativeBattleDecodedPose pose = new(new(0, 0, 0), [new(new(0, 0, 0), new(4096, 4096, 4096))], false);
        initialized = initialized with { Model = model.ToImmutableArray(), DecodedPose = pose };
        NativeBattleActionMatrix parent = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
        NativeBattleCameraProjection projection = new(parent, 216, 160, 108);
        NativeBattleCactuarRawModelDraw draw = NativeBattleCactuarRawModel.Draw5A9010(initialized, resource, parent, projection, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(24, draw.ProjectedBytesWritten);
        Assert.Equal(32, draw.Packets.AllocationBytes);
        Assert.Single(draw.Packets.Attempts);
        Assert.Equal(273, draw.Packets.Attempts[0].Primitive!.Bucket);
        Assert.Equal(initialized.Model.ToArray(), draw.State.Model.ToArray());
        Assert.Equal(initialized.Control.ToArray(), draw.State.Control.ToArray());
        BinaryPrimitives.WriteUInt32LittleEndian(model.AsSpan(0x7C), 0);
        NativeBattleCactuarRawModelState masked = initialized with { Model = model.ToImmutableArray() };
        NativeBattleCactuarRawModelDraw hidden = NativeBattleCactuarRawModel.Draw5A9010(masked, resource, parent, projection, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(0, hidden.Packets.AllocationBytes);
        Assert.Equal(0, hidden.ProjectedBytesWritten);
        Assert.Null(hidden.State.ProjectedScratch);
        model[7] = 1;
        Assert.Throws<NotSupportedException>(() => NativeBattleCactuarRawModel.Draw5A9010(masked with { Model = model.ToImmutableArray() }, resource, parent, projection, NativeBattleGteColorInterpolation.CodeConstants45F270));
    }

    private static NativeBattleEffectOwnedModelResource Resource()
    {
        byte[] bytes = new byte[104];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 4);
        int[] offsets = [24, 88, 96, 100, 104];
        for (int index = 0; index < offsets.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + index * 4), offsets[index]);
        }
        bytes[24] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), -1);
        NativeBattlePoseSource source = new(bytes.AsSpan(24, 64).ToArray().ToImmutableArray(), [1]);
        byte[] face = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(face, 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 2);
        NativeBattleRawMesh mesh = new([new(new(-50, -50, 0), 0), new(new(50, -50, 0), 0), new(new(0, 50, 0), 0)], [new(false, face.ToImmutableArray())]);
        return new(0x1000, bytes.ToImmutableArray(), source, [mesh]);
    }
    private static ushort U16(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset));
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
}
