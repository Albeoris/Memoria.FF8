using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class TextAndInitialDataTests
{
    [Fact]
    public void TextRetainsNativeGlyphNamesPagesAndWaitOperands()
    {
        byte[] source = [0x21, 0xFE, 0xD9, 0xEA, 0xF3, 0xE7, 0xE8, 0xFE, 0xDD, 255, 255, 255];
        IReadOnlyList<FieldTextToken> tokens = FieldText.Read(source);
        Assert.Equal("A", tokens[0].Text);
        Assert.Equal(0x21, tokens[0].Code);
        Assert.Equal(FieldTextTokenKind.Color, tokens[1].Kind);
        Assert.Equal(7, tokens[1].Argument);
        Assert.Equal(FieldTextTokenKind.CharacterName, tokens[2].Kind);
        Assert.Equal(FieldTextTokenKind.PartyName, tokens[3].Kind);
        Assert.Equal(FieldTextTokenKind.NewPage, tokens[5].Kind);
        Assert.Equal(65535, tokens[6].Argument);
        Assert.Equal(11, FieldText.FindTerminatedLength(source));
        Assert.Throws<InvalidDataException>(() => FieldText.Read([0xFE, 0xDD, 1]));
        Assert.Throws<InvalidDataException>(() => FieldText.Read([0xFE, 0x01]));
        Assert.Throws<InvalidDataException>(() => FieldText.FindTerminatedLength([0xFE, 0xE2, 1]));
    }

    [Fact]
    public void InitialDataPreservesEmptySlotsSentinelVitalsAndRawCharacterState()
    {
        byte[] data = new byte[2876];
        data[0] = 0;
        data[1] = 6;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(44), 314);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(56), ushort.MaxValue);
        data[0x4A4] = 0;
        data[0x4A5] = 255;
        data[0x4A6] = 255;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x4A8), (ushort)((7 << 9) | 12));
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x728), 0x00123456);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0xB28), 240);
        KernelInitialData initial = KernelInitialData.Read(data);
        Assert.Equal(data, initial.NativeData.ToArray());
        Assert.Equal(314, initial.Characters[0].CurrentHp);
        Assert.Equal(65535, initial.Characters[0].RecordedMaximumHp);
        Assert.Equal(132, initial.Characters[0].Raw.Length);
        Assert.Equal(12, initial.Inventory[0].ItemId);
        Assert.Equal(7, initial.Inventory[0].Quantity);
        Assert.Equal(0x56, initial.Materia[0].MateriaId);
        Assert.Equal(0x1234u, initial.Materia[0].AbilityPoints);
        Assert.Equal(240u, initial.Gil);
        Assert.Throws<InvalidDataException>(() => KernelInitialData.Read(data.AsSpan(0, 2875)));
        data[0x4A4] = 9;
        Assert.Throws<InvalidDataException>(() => KernelInitialData.Read(data));
    }

    [Fact]
    public void TimUsesBoundedDimensionsAndPreservesAdvisoryDeclaredSize()
    {
        byte[] tim = new byte[22];
        BinaryPrimitives.WriteUInt32LittleEndian(tim, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(8), 13);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(16), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(18), 1);
        tim[20] = 0x21;
        tim[21] = 0x43;
        TimTexture texture = TimTexture.Read(tim);
        Assert.Equal(13u, texture.DeclaredImageSize);
        Assert.Equal(4, texture.Width);
        Assert.Equal(3, texture.GetPixel(2, 0));
        Assert.Throws<InvalidDataException>(() => TimTexture.Read(tim, maximumPixels: 3));
        Assert.Throws<InvalidDataException>(() => TimTexture.Read(tim.AsSpan(0, 21)));
    }

    [Fact]
    public void RenderStateUsesNativeOffsetsAndRetainsUnknownWords()
    {
        byte[] raw = new byte[100];
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(8), 0x1234);
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(12), 0x4321);
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(68), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(92), 127);
        PcRenderState state = PcRenderState.Read(raw);
        Assert.Equal(0x1234u, state.Options);
        Assert.Equal(0x4321u, state.Features);
        Assert.Equal(2u, state.BlendMode);
        Assert.Equal(127u, state.VertexAlpha);
        Assert.Equal(raw, state.Raw.ToArray());
        Assert.Throws<InvalidDataException>(() => PcRenderState.Read(new byte[99]));
    }
}
