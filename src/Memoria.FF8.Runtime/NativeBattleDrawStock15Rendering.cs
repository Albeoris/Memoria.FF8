using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDrawStock15Matrix(NativeBattleActionMatrix Matrix, bool Rotation, bool Translation) : NativeBattleDrawStock15ParticleOperation
{
    public ushort? Padding18 { get; init; }
    public int? TemporaryMatrixOffset { get; init; }
}
public sealed record NativeBattleDrawStock15Projection(NativeBattleActionMatrix Matrix, NativeBattleCameraProjection Projection, ImmutableArray<NativeBattleVector> Points) : NativeBattleDrawStock15ParticleOperation
{
    public bool FinalCue { get; init; } = true;
    public ImmutableArray<short?> Ws { get; init; }
    public int? TemporaryVectorOffset { get; init; }
}
public sealed record NativeBattleDrawStock15RibbonStep(NativeBattleGfGeometryBatch Batch, ImmutableArray<NativeBattleDrawStock15ParticleOperation> Operations);
public sealed record NativeBattleDrawStock15SpriteComposition(NativeBattleModuleDraw Draw, NativeBattleActionMatrix Setup, NativeBattleActionMatrix Main, NativeBattleModulePacketBatch Batch);

public static class NativeBattleDrawStock15Rendering
{
    public static NativeBattleDrawStock15SpriteComposition ComposeSprite(NativeBattleModulePoolTask task, NativeBattleDrawStock15ParticleResources resources, NativeBattleActionMatrix camera, NativeBattleCameraProjection projection, short cameraRoll)
    {
        resources.Validate();
        bool head = task.Callback == 0x8B5C70;
        if (!head && task.Callback != 0x8B6440 || task.ReadDword(0x4C) != (head ? 0x162B520u : 0x162B730u))
        {
            throw new InvalidDataException("Draw Stock sprite requires its original task and source identity.");
        }
        NativeBattleModuleResource resource = resources.Resource(task.ReadDword(0x4C));
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            Resource = resource,
            SpriteFrame = resource.ProjectedFrame571C80(task.ReadWord(0x50)),
            Phase = task.ReadWord(0x50),
            Anchor = new(Word(task, 0x1C), Word(task, 0x1E), Word(task, 0x20), Word(task, 0x22)),
            Scale = 4096,
            Direction = 4096,
            DepthOffset = head ? Word(task, 0x54) : 0,
            SpriteFlags = 0
        };
        NativeBattleActionMatrix setup;
        if (head)
        {
            setup = NativeBattlePoseMatrices.ModuleSpriteCamera(draw with { SpriteFlags = 0x200 }, camera, 0);
        }
        else
        {
            NativeBattleActionMatrix world = NativeBattleCuragaMatrices.Identity8DD770();
            world = NativeBattleCuragaMatrices.RotateY8DD8A0(world, Word(task, 0x42));
            world = NativeBattleCuragaMatrices.RotateZ8DD960(world, Word(task, 0x44));
            world = NativeBattleCuragaMatrices.RotateX8DD7E0(world, Word(task, 0x40));
            draw = draw with { SpriteWorldRotation = world.Rotation };
            setup = NativeBattleGfMatrices.Compose56C2F0(camera, world with { X = Word(task, 0x1C), Y = Word(task, 0x1E), Z = Word(task, 0x20) });
        }
        NativeBattleActionMatrix main = NativeBattlePoseMatrices.ModuleSpriteCamera(draw, camera, cameraRoll);
        return new(draw, setup, main, NativeBattleModulePackets.EmitSprite571DC0(draw, projection, cameraRoll, 17, 2, main));
    }

    public static NativeBattleDrawStock15RibbonStep Ribbon8B5F30(NativeBattleModulePoolTask task, NativeBattleActionMatrix camera, NativeBattleCameraProjection projection, ushort? cameraPadding = null)
    {
        if (task.Callback != 0x8B5C70)
        {
            throw new InvalidDataException("Draw Stock ribbon requires its original trajectory task.");
        }
        return RibbonTrajectory(task, camera, projection, cameraPadding);
    }

    internal static NativeBattleDrawStock15RibbonStep RibbonTrajectory(NativeBattleModulePoolTask task, NativeBattleActionMatrix camera, NativeBattleCameraProjection projection, ushort? cameraPadding)
    {
        int first = Word(task, 0x128);
        int last = Word(task, 0x12A);
        if (last == 0 || first >= 10)
        {
            return new(new([], 0), []);
        }
        if (first < 0 || last > 10 || first > last)
        {
            throw new NotSupportedException("Draw Stock ribbon leaves its actual eleven-row history.");
        }
        ImmutableArray<NativeBattleDrawStock15ParticleOperation>.Builder operations = ImmutableArray.CreateBuilder<NativeBattleDrawStock15ParticleOperation>();
        NativeBattleVector[] points = new NativeBattleVector[22];
        byte[] colors = new byte[11];
        byte intensity = 0;
        NativeBattleActionMatrix basis = NativeBattleDrawStock15Particles.ReadRotation(task, 0x60);
        for (int row = first; row <= last; row++)
        {
            colors[row] = intensity;
            intensity = (byte)Math.Min(intensity + 32, 128);
            short angle = (short)((task.ReadWord(0x126) + 288 * row) & 0xFFF);
            operations.Add(new NativeBattleDrawStock15Rotate(basis, 'Z', angle) { Padding18 = NativeBattleDrawStock15Particles.ReadPadding(task, 0x72) });
            NativeBattleActionMatrix rotated = NativeBattleCuragaMatrices.RotateZ8DD960(basis, angle);
            for (int edge = 0; edge < 2; edge++)
            {
                NativeBattleVector local = new(0, edge == 0 ? (short)-128 : (short)128, 0);
                operations.Add(new NativeBattleDrawStock15Transform(rotated, local));
                NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(rotated, local);
                int start = 0x90 + row * 8;
                points[row * 2 + edge] = new(unchecked((short)(Word(task, start) + offset.X)), unchecked((short)(Word(task, start + 2) + offset.Y)), unchecked((short)(Word(task, start + 4) + offset.Z)));
            }
        }
        operations.Add(new NativeBattleDrawStock15Matrix(camera, true, true) { Padding18 = cameraPadding });
        ImmutableArray<NativeBattleVector> projectedPoints = points.Skip(first * 2).Take((last - first + 1) * 2).ToImmutableArray();
        operations.Add(new NativeBattleDrawStock15Projection(camera, projection, projectedPoints));
        NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[22];
        for (int index = first * 2; index <= last * 2 + 1; index++)
        {
            vertices[index] = NativeBattleModuleProjection.Project45FE10(camera, points[index], projection);
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        for (int row = first; row < last; row++)
        {
            int index = row * 2;
            Add(index, index + 2, index + 1, colors[row], colors[row + 1], colors[row], false);
            Add(index + 1, index + 2, index + 3, colors[row], colors[row + 1], colors[row + 1], true);
        }
        return new(new(attempts.ToImmutable(), attempts.Count * 40), operations.ToImmutable());

        void Add(int a, int b, int c, byte ca, byte cb, byte cc, bool second)
        {
            byte[] bytes = new byte[40];
            NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 40).ToArray();
            Dword(0, 0x09000000);
            Byte(7, 0x36);
            Word(0x0E, 0x3D94);
            Word(0x1A, 0xB9);
            Dword(8, vertices[a].Screen);
            Dword(20, vertices[b].Screen);
            Dword(32, vertices[c].Screen);
            for (int component = 0; component < 3; component++)
            {
                Byte(4 + component, ca);
                Byte(16 + component, cb);
                Byte(28 + component, cc);
            }
            Byte(12, 0x60);
            Byte(13, second ? (byte)0x3F : (byte)0);
            Byte(24, 0x7F);
            Byte(25, 0);
            Byte(36, second ? (byte)0x7F : (byte)0x60);
            Byte(37, 0x3F);
            int bucket = RibbonBucket(vertices[a].Depth, vertices[b].Depth, vertices[c].Depth);
            ImmutableArray<NativeBattleGfPacketByteState> known = mask.ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(bucket, Enumerable.Range(0, 10).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(word * 4))).ToImmutableArray(), []) { PhysicalWriteMask = known };
            attempts.Add(new(attempts.Count * 40, bytes.ToImmutableArray(), known, primitive));

            void Byte(int offset, byte value)
            {
                bytes[offset] = value;
                mask[offset] = NativeBattleGfPacketByteState.Known;
            }
            void Word(int offset, ushort value)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
                mask[offset] = mask[offset + 1] = NativeBattleGfPacketByteState.Known;
            }
            void Dword(int offset, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
                for (int index = 0; index < 4; index++)
                {
                    mask[offset + index] = NativeBattleGfPacketByteState.Known;
                }
            }
        }
    }

    public static int RibbonBucket(int first, int second, int third)
    {
        int mean = unchecked(first + second + third) / 3;
        return unchecked(17 + ((mean / 4) >> 2));
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
