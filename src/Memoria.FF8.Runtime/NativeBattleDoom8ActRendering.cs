using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDoom8ActRendering
{
    public const uint MorphAddress = 0x275F5C8;
    public const int MorphLength = 848;
    private static readonly NativeBattleActMeshRules Rules = new(MorphAddress, MorphLength, true, true) { AlternateEulerFromHeader = true, UvFlagsFromHeader = false };

    public static NativeBattleDoom8ActPreparation Prepare8C59E0(NativeBattleDoom8Frame frame, uint address, NativeBattlePackedMatrix32 camera, NativeBattleEffectArena retained92, NativeBattleEffectArena compositionTemporary32)
    {
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, address);
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleEffectArena context = WriteMatrix(retained92, identity);
        foreach ((char axis, int offset) in new[] { ('Y', 0x62), ('X', 0x60), ('Z', 0x64) })
        {
            short angle = unchecked((short)task.ReadWord(offset));
            if (angle == 0)
            {
                continue;
            }
            (byte[] bytes, bool[] known) = context.ReadSnapshot(0, 32);
            NativeBattleStatefulMatrixRotationStep rotated = NativeBattleStatefulMatrixRotation.Rotate(frame.Memory, RequireStaging(frame), new(bytes.ToImmutableArray(), known.ToImmutableArray()), axis, angle);
            frame = frame with { Memory = rotated.Memory, Staging = rotated.Staging };
            context = WriteMatrix(context, rotated.Matrix.ReadMatrix());
        }
        (byte[] localBytes, bool[] localKnown) = context.ReadSnapshot(0, 32);
        NativeBattleActionMatrix local = new NativeBattlePackedMatrix32(localBytes.ToImmutableArray(), localKnown.ToImmutableArray()).ReadMatrix();
        local = NativeBattlePoseMatrices.Scale56BEF0(local, unchecked((int)task.ReadDword(0x50)), unchecked((int)task.ReadDword(0x54)), unchecked((int)task.ReadDword(0x58))) with { X = unchecked((short)task.ReadWord(0x1C)), Y = unchecked((short)task.ReadWord(0x1E)), Z = unchecked((short)task.ReadWord(0x20)) };
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(camera.ReadMatrix(), local);
        compositionTemporary32 = WriteMatrix(compositionTemporary32, composed);
        context = NativeBattleEffectNodeContextMemory.Copy(compositionTemporary32, 0, context, 0, 32);
        context = Dword(context, 0x38, task.ReadDword(0x70));
        context = Dword(context, 0x44, task.ReadDword(0x7C));
        context = Dword(context, 0x48, MorphAddress);
        foreach ((int destination, int source) in new[] { (0x4C, 0x8C), (0x4E, 0x8E), (0x50, 0x90), (0x52, 0x92), (0x54, 0x86), (0x56, 0x80), (0x58, 0x88) })
        {
            context = Word(context, destination, task.ReadWord(source));
        }
        return new(frame, context);
    }

    public static NativeBattleDoom8Frame DrawNode8C5B10(NativeBattleDoom8Frame frame, NativeBattleGfAnimationSource source, NativeBattleEffectArena context92, NativeBattleGfAnimationNode node, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, Func<NativeBattleDoom8Frame, NativeBattleGfGeometryBatch, NativeBattleDoom8Frame> deliver)
    {
        if (!NativeBattleActMeshRendering.Visible(node))
        {
            return frame;
        }
        (byte[] matrixBytes, bool[] matrixKnown) = context92.ReadSnapshot(0, 32);
        NativeBattleActionMatrix matrix = new NativeBattlePackedMatrix32(matrixBytes.ToImmutableArray(), matrixKnown.ToImmutableArray()).ReadMatrix();
        NativeBattleActMeshContext root = new(matrix, unchecked((short)BinaryPrimitives.ReadUInt16LittleEndian(context92.Read(0x58, 2))));
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 104);
        NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 104), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 104), 0, 104);
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Doom ACT drawing requires its packet process.");
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Doom ACT drawing requires its projection controls.");
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Doom ACT drawing requires its geometry controls."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Doom ACT drawing requires its geometry data registers."));
        NativeBattleActMeshDraw drawn = NativeBattleActMeshRendering.DrawNode(source, node, root, temporary, frame.State.ReadView(MorphAddress, MorphLength), registers, process.ModelRendererRegisters ?? throw new NotSupportedException("Doom ACT drawing requires its retained FAR controls."), projection, controls.Dqa, controls.Dqb, controls.Zsf3, controls.Zsf4, constants, Rules);
        frame = frame with
        {
            Memory = frame.Memory with { GeometryRegisters = drawn.Registers.Geometry, ScriptedColorRegisters = drawn.Registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = drawn.FarControls } },
            State = frame.State.WriteView(MorphAddress, drawn.MorphScratch, MorphLength),
            Staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary104, 104)
        };
        frame = deliver(frame, drawn.Batch);
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 104) };
    }

    private static NativeBattleEffectArena WriteMatrix(NativeBattleEffectArena arena, NativeBattleActionMatrix matrix)
    {
        for (int lane = 0; lane < 9; lane++)
        {
            arena = Word(arena, lane * 2, unchecked((ushort)matrix.Rotation[lane]));
        }
        return Dword(Dword(Dword(arena, 20, unchecked((uint)matrix.X)), 24, unchecked((uint)matrix.Y)), 28, unchecked((uint)matrix.Z));
    }

    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }

    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }

    private static NativeBattleFileStaging RequireStaging(NativeBattleDoom8Frame frame) => frame.Staging ?? throw new NotSupportedException("Doom ACT drawing requires its physical temporary memory.");
}
