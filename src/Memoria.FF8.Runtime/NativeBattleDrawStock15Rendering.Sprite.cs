using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDrawStock15Billboard(NativeBattleModuleAnchor Anchor, short Scale, short Depth) : NativeBattleDrawStock15ParticleOperation;
public sealed record NativeBattleDrawStock15MatrixProduct(NativeBattleActionMatrix Left, NativeBattleActionMatrix Right) : NativeBattleDrawStock15ParticleOperation
{
    public ushort? Padding18 { get; init; }
    public int? TemporaryLeftMatrixOffset { get; init; }
}
public sealed record NativeBattleDrawStock15LongTransform(NativeBattleActionMatrix Matrix, NativeBattleVector Point) : NativeBattleDrawStock15ParticleOperation
{
    public bool LoadRotation { get; init; }
    public ushort? Padding18 { get; init; }
    public int? TemporaryMatrixOffset { get; init; }
}
public sealed record NativeBattleDrawStock15AverageDepth(ImmutableArray<ushort> Depths) : NativeBattleDrawStock15ParticleOperation;
public sealed record NativeBattleDrawStock15SpriteTemporaryResult(NativeBattleFileStaging Staging, NativeBattleModulePacketBatch Batch, ImmutableArray<NativeBattleDrawStock15ParticleOperation> Operations);

public static partial class NativeBattleDrawStock15SpriteRendering
{
    public static ImmutableArray<NativeBattleDrawStock15ParticleOperation> Preparation(NativeBattleModulePoolTask task, NativeBattleActionMatrix camera, ushort? cameraPadding = null)
    {
        bool head = task.Callback switch
        {
            0x8B5C70 => true,
            0x8B6440 => false,
            _ => throw new InvalidDataException("Draw Stock sprite preparation requires its actual callback.")
        };
        return PreparationCore(task, head, camera, cameraPadding);
    }

    internal static ImmutableArray<NativeBattleDrawStock15ParticleOperation> PreparationCore(NativeBattleModulePoolTask task, bool head, NativeBattleActionMatrix camera, ushort? cameraPadding)
    {
        if (head)
        {
            return [new NativeBattleDrawStock15Billboard(new(Word(task, 0x1C), Word(task, 0x1E), Word(task, 0x20), Word(task, 0x22)), 4096, Word(task, 0x54))];
        }
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        ImmutableArray<NativeBattleDrawStock15ParticleOperation>.Builder operations = ImmutableArray.CreateBuilder<NativeBattleDrawStock15ParticleOperation>();
        Rotate('Y', Word(task, 0x42));
        Rotate('Z', Word(task, 0x44));
        Rotate('X', Word(task, 0x40));
        matrix = matrix with { X = Word(task, 0x1C), Y = Word(task, 0x1E), Z = Word(task, 0x20) };
        operations.Add(new NativeBattleDrawStock15Matrix(NativeBattleGfMatrices.Compose56C2F0(camera, matrix), true, true));
        return operations.ToImmutable();

        void Rotate(char axis, short angle)
        {
            operations.Add(new NativeBattleDrawStock15Rotate(matrix, axis, angle));
            matrix = axis switch
            {
                'X' => NativeBattleCuragaMatrices.RotateX8DD7E0(matrix, angle),
                'Y' => NativeBattleCuragaMatrices.RotateY8DD8A0(matrix, angle),
                _ => NativeBattleCuragaMatrices.RotateZ8DD960(matrix, angle)
            };
        }
    }

    public static NativeBattleDrawStock15SpriteTemporaryResult Emit180(NativeBattleFileStaging staging, uint temporary, NativeBattleModulePoolTask task, NativeBattleDrawStock15ParticleResources resources, NativeBattleActionMatrix camera, NativeBattleCameraProjection projection, short cameraRoll, NativeBattlePackedMatrix32? capturedMatrix = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleDrawStock15SpriteComposition composed = NativeBattleDrawStock15Rendering.ComposeSprite(task, resources, camera, projection, cameraRoll);
        return EmitPrepared180(staging, temporary, task, composed, projection, cameraRoll, capturedMatrix, pilotIdentity);
    }

    public static NativeBattleDrawStock15SpriteTemporaryResult EmitPrepared180(NativeBattleFileStaging staging, uint temporary, NativeBattleModulePoolTask task, NativeBattleDrawStock15SpriteComposition composed, NativeBattleCameraProjection projection, short cameraRoll, NativeBattlePackedMatrix32? capturedMatrix = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (composed.Draw.Resource?.Address != task.ReadDword(0x4C) || composed.Draw.Phase != task.ReadWord(0x50))
        {
            throw new InvalidDataException("Native prepared sprite requires its actual task resource and selected phase.");
        }
        staging = Dword(staging, temporary, task.ReadDword(0x4C));
        staging = Word(staging, temporary + 4, task.ReadWord(0x50));
        staging = Word(staging, temporary + 36, 0);
        staging = NativeBattleModuleSpriteTemporary.Begin571C80(staging, temporary, task.ReadDword(0x4C), 0);
        NativeBattleModuleSpriteTemporaryResult emitted = NativeBattleModuleSpriteTemporary.Emit571C80(staging, temporary, composed.Draw with { PilotIdentity = pilotIdentity }, projection, cameraRoll, 17, 2, capturedMatrix);
        ImmutableArray<NativeBattleDrawStock15ParticleOperation>.Builder operations = ImmutableArray.CreateBuilder<NativeBattleDrawStock15ParticleOperation>();
        NativeBattleModuleSpriteFrame frame = composed.Draw.SpriteFrame!;
        bool accepted = false;
        for (int offset = 0; offset < frame.Quads.Length; offset += 20)
        {
            ReadOnlySpan<byte> quad = frame.Quads.AsSpan().Slice(offset, 20);
            NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.ModuleSpriteQuadCamera(composed.Main, quad);
            NativeBattleActionMatrix local = NativeBattlePoseMatrices.ModuleSpriteQuadCamera(NativeBattleCuragaMatrices.Identity8DD770(), quad);
            operations.Add(new NativeBattleDrawStock15MatrixProduct(composed.Main, local) { TemporaryLeftMatrixOffset = 132 });
            operations.Add(new NativeBattleDrawStock15LongTransform(composed.Main, new(unchecked((short)local.X), unchecked((short)local.Y), unchecked((short)local.Z))) { LoadRotation = false, TemporaryMatrixOffset = 132 });
            operations.Add(new NativeBattleDrawStock15Matrix(matrix, true, true) { TemporaryMatrixOffset = 100 });
            short width = (short)(8 * quad[0]);
            short height = (short)(8 * quad[2]);
            ImmutableArray<NativeBattleVector> corners = [new(unchecked((short)-width), unchecked((short)-height), 0), new(width, unchecked((short)-height), 0), new(unchecked((short)-width), height, 0), new(width, height, 0)];
            operations.Add(new NativeBattleDrawStock15Projection(matrix, projection, corners.Take(3).ToImmutableArray()) { FinalCue = false, TemporaryVectorOffset = 68 });
            NativeBattleModuleProjectedVertex[] projected = corners.Select(point => NativeBattleModuleProjection.Project45FE10(matrix, point, projection)).ToArray();
            if (((projected[0].Flags | projected[1].Flags | projected[2].Flags) & 0x60000) != 0)
            {
                continue;
            }
            operations.Add(new NativeBattleDrawStock15Projection(matrix, projection, [corners[3]]) { TemporaryVectorOffset = 92 });
            if (!accepted)
            {
                operations.Add(new NativeBattleDrawStock15AverageDepth(projected.Select(vertex => vertex.Depth).ToImmutableArray()));
            }
            accepted = true;
        }
        return new(emitted.Staging, emitted.Batch, operations.ToImmutable());
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleFileStaging Word(NativeBattleFileStaging staging, uint address, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return staging.Write(address, bytes);
    }
    private static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return staging.Write(address, bytes);
    }
}
