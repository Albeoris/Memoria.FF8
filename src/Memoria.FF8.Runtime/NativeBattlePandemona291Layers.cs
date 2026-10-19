using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePandemona291LayerRequest(uint Source, uint Target, int Center, int Angle, int Morph, int RadialPhase, int RadialAmplitude, int HorizontalScale, int VerticalScale, int TextureKind, byte TexturePhase, int Fade);
public sealed record NativeBattlePandemona291LayerVertex(NativeBattleVector Position, NativeBattleVector Input, ushort Padding);
public sealed record NativeBattlePandemona291PreparedLayer(ImmutableArray<NativeBattlePandemona291LayerVertex> Vertices, NativeBattleActionMatrix Matrix, NativeBattleEsunaMeshContext Context);

public static class NativeBattlePandemona291Layers
{
    public static ImmutableArray<NativeBattlePandemona291LayerRequest> Opening(int center, int age)
    {
        int elapsed = unchecked(age - 24);
        uint scale = unchecked((uint)elapsed << 12) / 14;
        int angle = unchecked(elapsed << 6);
        int first = unchecked((int)scale);
        int twice = unchecked(first * 2);
        int vertical = unchecked(4096 + first);
        int depth = unchecked(center + 2000);
        return [new(0x13A0E58, 0x13A4280, depth, angle, 0, 0, 0, first, vertical, 0, unchecked((byte)(elapsed << 5)), unchecked(4096 - (first >> 1))), new(0x13A4F88, 0x13A5EF0, depth, angle, 0, 0, 0, twice, vertical, 1, unchecked((byte)(elapsed << 5)), unchecked(4096 - (first >> 2))), new(0x13A6508, 0x13A7BF0, depth, angle, 0, 0, 0, twice, vertical, 1, unchecked((byte)(elapsed * 40)), unchecked(4096 - (first >> 2)))];
    }

    public static NativeBattlePandemona291PreparedLayer Prepare6F2A30(NativeBattlePandemona291Animation source, ImmutableArray<byte> target, ImmutableArray<byte> trigonometry, NativeBattleActionMatrix view, NativeBattlePandemona291LayerRequest request)
    {
        if (source.Bytes.IsDefault || source.Bytes.Length < 8 || target.IsDefault || trigonometry.IsDefault || trigonometry.Length != 0x4000)
        {
            throw new InvalidDataException("Native Pandemona layers require their actual source records, target shape and lookup table.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan().Slice(4, 4));
        if (count <= 0 || count > (source.Bytes.Length - 8) / 8 || count > (target.Length - 8) / 8)
        {
            throw new InvalidDataException("Native Pandemona layer vertices leave their actual source or target owner.");
        }
        short horizontal = unchecked((short)request.HorizontalScale);
        int sine = unchecked(Trig(request.Angle, false) * horizontal) >> 12;
        int cosine = unchecked(Trig(request.Angle, true) * horizontal) >> 12;
        NativeBattleActionMatrix rotation = new([unchecked((short)cosine), 0, unchecked((short)-sine), 0, unchecked((short)request.VerticalScale), 0, unchecked((short)sine), 0, unchecked((short)cosine)], 0, 0, 0, 4096);
        ImmutableArray<NativeBattlePandemona291LayerVertex>.Builder vertices = ImmutableArray.CreateBuilder<NativeBattlePandemona291LayerVertex>(count);
        for (int index = 0; index < count; index++)
        {
            int offset = checked(8 + index * 8);
            NativeBattleVector input = new(Blend(offset), Blend(offset + 2), Blend(offset + 4));
            ushort padding = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan().Slice(offset + 6, 2));
            NativeBattleVector rotated = NativeBattleCuragaMatrices.RotateVector45DF80(rotation, input);
            int radial = unchecked(BinaryPrimitives.ReadInt16LittleEndian(trigonometry.AsSpan().Slice((padding & 0xFFE) * 2, 2)) * request.RadialAmplitude) >> 12;
            radial = unchecked((short)radial);
            int phase = unchecked(unchecked((short)padding) + request.RadialPhase) & 0xFFF;
            int dx = unchecked(Trig(phase, false) * radial) >> 12;
            int dz = unchecked(Trig(phase, true) * radial) >> 12;
            NativeBattleVector position = new(unchecked((short)(rotated.X + dx)), rotated.Y, unchecked((short)(rotated.Z + dz)));
            vertices.Add(new(position, input, padding));
        }
        (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(view, 0, 0, request.Center);
        NativeBattleActionMatrix matrix = view with { X = unchecked(view.X + x), Y = unchecked(view.Y + y), Z = unchecked(view.Z + z) };
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleEsunaMeshContext context = new(request.Fade == 0 ? 0x2030u : 0x20F0u, request.Fade == 0 ? null : 0, request.Fade == 0 ? null : request.Fade, 0, 0, 0, full, full, 17, 2);
        context = request.TextureKind switch
        {
            0 => context with { UOffset = (ushort)(request.TexturePhase & 127), WindowAfter = new(0, 0, 128, 256) },
            1 => context with { VOffset = (ushort)(request.TexturePhase & 127), WindowAfter = new(0, 128, 256, 128) },
            _ => throw new NotSupportedException("Native Pandemona layers require their retained alternate texture rectangle operands.")
        };
        return new(vertices.MoveToImmutable(), matrix, context);

        short Blend(int offset)
        {
            ushort a = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan().Slice(offset, 2));
            ushort b = BinaryPrimitives.ReadUInt16LittleEndian(target.AsSpan().Slice(offset, 2));
            short difference = unchecked((short)(b - a));
            int product = NativePrecision53.RoundInteger(difference * (unchecked((short)request.Morph) / 4096d));
            return unchecked((short)(a + Math.Clamp(product, short.MinValue, short.MaxValue)));
        }

        short Trig(int angle, bool cosineValue)
        {
            return BinaryPrimitives.ReadInt16LittleEndian(trigonometry.AsSpan().Slice((angle & 0xFFF) * 4 + (cosineValue ? 2 : 0), 2));
        }
    }
}
