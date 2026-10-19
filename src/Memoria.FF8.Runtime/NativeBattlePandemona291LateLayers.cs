using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePandemona291LateLayerRequest(uint Source, int Angle, int RadialPhase, int RadialAmplitude, int HorizontalScale, int VerticalScale, int TextureKind, byte TexturePhase, int HeightBias);
internal sealed record NativeBattlePandemona291LateLayer(NativeBattleActionMatrix Matrix, ImmutableArray<byte> Vertices, NativeBattleEffectMeshWindow Window);

public static class NativeBattlePandemona291LateLayers
{
    internal static NativeBattlePandemona291LateLayer Prepare6F1C10(NativeBattlePandemona291Animation source, ImmutableArray<byte> trigonometry, NativeBattleActionMatrix parent, NativeBattleActionMatrix view, NativeBattlePandemona291LateLayerRequest request, Action<ImmutableArray<byte>>? observeInput = null)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan().Slice(4));
        byte[] vertices = new byte[checked(count * 8)];
        int phase = request.Angle & 0xFFF;
        short horizontal = unchecked((short)request.HorizontalScale);
        short cosine = unchecked((short)(unchecked(Trig(phase, true) * horizontal) >> 12));
        short sine = unchecked((short)(unchecked(Trig(phase, false) * horizontal) >> 12));
        NativeBattleActionMatrix rotation = new([cosine, sine, 0, unchecked((short)-sine), cosine, 0, 0, 0, unchecked((short)request.VerticalScale)], 0, 0, 0, 4096);
        for (int index = 0; index < count; index++)
        {
            int inputOffset = checked(8 + index * 8);
            ImmutableArray<byte> input = source.Bytes.AsSpan().Slice(inputOffset, 8).ToArray().ToImmutableArray();
            observeInput?.Invoke(input);
            NativeBattleVector raw = new(BinaryPrimitives.ReadInt16LittleEndian(input.AsSpan()), BinaryPrimitives.ReadInt16LittleEndian(input.AsSpan().Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(input.AsSpan().Slice(4)));
            NativeBattleVector transformed = NativeBattleCuragaMatrices.RotateVector45DF80(rotation, raw);
            short sourcePhase = BinaryPrimitives.ReadInt16LittleEndian(input.AsSpan().Slice(6));
            int radial = unchecked(BinaryPrimitives.ReadInt16LittleEndian(trigonometry.AsSpan().Slice((sourcePhase & 0xFFE) * 2, 2)) * request.RadialAmplitude) >> 12;
            radial = unchecked((short)radial);
            int angle = unchecked(sourcePhase + request.RadialPhase) & 0xFFF;
            short x = unchecked((short)(transformed.X + (unchecked(Trig(angle, false) * radial) >> 12)));
            short y = unchecked((short)(transformed.Y + (unchecked(Trig(angle, true) * radial) >> 12)));
            int height = unchecked((short)(sourcePhase + request.HeightBias));
            if (height < 0)
            {
                height = 0;
            }
            else if (height > 4096)
            {
                height = height > 8192 ? unchecked(12288 - height) : 4096;
            }
            int output = index * 8;
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(output), x);
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(output + 2), y);
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(output + 4), transformed.Z);
            BinaryPrimitives.WriteUInt16LittleEndian(vertices.AsSpan(output + 6), unchecked((ushort)(4096 - height)));
        }
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleEffectMeshWindow window = request.TextureKind switch
        {
            0 => new((ushort)(request.TexturePhase & 127), 0, full, new(0, 0, 128, 256)),
            1 => new(0, (ushort)(request.TexturePhase & 127), full, new(0, 128, 256, 128)),
            2 => new(0, 0, full, full),
            _ => throw new NotSupportedException("Native Pandemona late layer requires its reached texture selector contract.")
        };
        return new(NativeBattleGfMatrices.Compose56C2F0(view, parent), vertices.ToImmutableArray(), window);

        short Trig(int angle, bool cosine) => BinaryPrimitives.ReadInt16LittleEndian(trigonometry.AsSpan().Slice((angle & 0xFFF) * 4 + (cosine ? 2 : 0), 2));
    }

    internal static NativeBattleGfGeometryBatch Emit6F1E10(ImmutableArray<byte> geometry, NativeBattlePandemona291LateLayer prepared, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, short zsf4, Func<int, NativeBattleVector> readSelected, Func<int, short> readFactor, Action<int>? observeInput = null, Action<uint>? observeFlags = null, Func<NativeBattleVector, NativeBattleModuleProjectedVertex>? projectFourth = null)
    {
        int corner = -1;
        return NativeBattleEffectMeshPackets.EmitBatch(geometry, prepared.Matrix, projection, 0x2000, offset =>
        {
            corner++;
            if (corner is 0 or 3)
            {
                observeInput?.Invoke(offset);
            }
            return readSelected(offset);
        }, (group, face, project, offset) =>
        {
            corner = -1;
            if (group is not (3 or 7))
            {
                throw new NotSupportedException("Native Pandemona late renderer requires its actual FT4/GT4 resource groups.");
            }
            int selected = BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(4)) * 4;
            short factor = readFactor(selected);
            NativeBattleEffectMeshWindow window = group == 3 ? prepared.Window with { V = 0 } : prepared.Window;
            NativeBattleEffectMeshPacketContext context = new(factor == 0 ? 0u : 0xC0u, 0, factor, null, null, 0, false, null, 17, 2, window);
            NativeBattleGfGeometryBatch batch = NativeBattleEffectMeshPackets.EmitTextured(face, project, true, group == 7, context, colors, offset, NativeBattleEffectMeshClip.OutsideScreen);
            NativeBattleGeometryPrimitive? polygon = batch.Attempts.Select(attempt => attempt.Primitive).FirstOrDefault(primitive => primitive is not null && primitive.VertexPayload4.Length == 4 && primitive.Words.Length > 3);
            if (polygon is null)
            {
                return batch;
            }
            int average = unchecked((int)(unchecked((uint)polygon.VertexPayload4.Sum() * (uint)(int)zsf4) >> 12));
            int bucket = unchecked(17 + (average >> 2));
            return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = primitive with { Bucket = bucket } } : attempt).ToImmutableArray() };
        }, observeFlags, projectFourth);
    }
}
