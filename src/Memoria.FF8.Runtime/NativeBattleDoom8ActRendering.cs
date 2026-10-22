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
        NativeBattleActContext92PreparationStep prepared = NativeBattleActContext92Preparation.Prepare(frame.Memory, frame.Staging, NativeBattleDoom8Controller.Read(frame, address), camera, retained92, compositionTemporary32, MorphAddress);
        return new(frame with { Memory = prepared.Memory, Staging = prepared.Staging }, prepared.Context92);
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

    private static NativeBattleFileStaging RequireStaging(NativeBattleDoom8Frame frame) => frame.Staging ?? throw new NotSupportedException("Doom ACT drawing requires its physical temporary memory.");
}
