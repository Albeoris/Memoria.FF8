using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCurse47ActRendering
{
    public static NativeBattleCurse47ActPreparation PrepareDraw(NativeBattleCurse47Frame frame, uint address, NativeBattlePackedMatrix32 camera, NativeBattleEffectArena retained92, NativeBattleEffectArena compositionTemporary32)
    {
        NativeBattleActContext92PreparationStep prepared = NativeBattleActContext92Preparation.Prepare(frame.Memory, frame.Staging, NativeBattleCurse47Controller.Read(frame, address), camera, retained92, compositionTemporary32, MorphAddress);
        return new(frame with { Memory = prepared.Memory, Staging = prepared.Staging }, prepared.Context92);
    }

    public static NativeBattleCurse47Frame EmitNode(NativeBattleCurse47Frame frame, NativeBattleGfAnimationSource source, NativeBattleEffectArena context92, NativeBattleGfAnimationNode node, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, Func<NativeBattleCurse47Frame, NativeBattleGfGeometryBatch, NativeBattleCurse47Frame> deliverCommon, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (!Visible(node))
        {
            return frame;
        }
        (byte[] bytes, bool[] known) = context92.ReadSnapshot(0, 32);
        NativeBattleActionMatrix matrix = new NativeBattlePackedMatrix32(bytes.ToImmutableArray(), known.ToImmutableArray()).ReadMatrix();
        NativeBattleActMeshContext root = new(matrix, unchecked((short)BinaryPrimitives.ReadUInt16LittleEndian(context92.Read(0x58, 2))));
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 104);
        NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 104), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 104), 0, 104);
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Curse ACT drawing requires its actual COMMON packet process.");
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Curse ACT drawing requires its actual projection controls.");
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Curse ACT drawing requires its geometry controls."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Curse ACT drawing requires its geometry data registers."));
        NativeBattleActMeshDraw drawn = DrawNode(source, node, root, temporary, frame.State.ReadView(MorphAddress, MorphLength), registers, process.ModelRendererRegisters ?? throw new NotSupportedException("Curse ACT drawing requires its retained FAR controls."), projection, controls.Dqa, controls.Dqb, controls.Zsf3, controls.Zsf4, constants, pilotIdentity: pilotIdentity);
        frame = frame with
        {
            Memory = frame.Memory with { GeometryRegisters = drawn.Registers.Geometry, ScriptedColorRegisters = drawn.Registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = drawn.FarControls } },
            State = frame.State.WriteView(MorphAddress, drawn.MorphScratch, MorphLength),
            Staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary104, 104)
        };
        frame = deliverCommon(frame, drawn.Batch);
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 104) };
    }

    private static NativeBattleFileStaging RequireStaging(NativeBattleCurse47Frame frame) => frame.Staging ?? throw new NotSupportedException("Curse ACT drawing requires its physical temporary memory.");
}
