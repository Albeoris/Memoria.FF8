using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicDetached66(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleCinematicResources resources, ImmutableArray<NativeBattleEffectInitializerAsset> assets, NativeBattleGraphicsState graphics)
    {
        NativeBattleCinematicDetached66Services services = new((current, selector) =>
        {
            uint address = NativeBattleWaterShapeSelection.ResolveCinematic(current, resources, selector).Selected;
            for (int index = 0; index < assets.Length; index++)
            {
                NativeBattleEffectInitializerAsset asset = assets[index];
                uint start = CoreDword(current, 0x2798A68 + (uint)(4 * index));
                if (address >= start && address - start < asset.Data.Length)
                {
                    return new(current, address, checked(asset.Data.Length - (int)(address - start)));
                }
            }
            throw new NotSupportedException("Native detached geometry requires its current complete source owner.");
        }, (current, selector) =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            uint address = NativeBattleWaterMatrixCommands.ResolveB65520(current, bank, selector);
            byte[] raw = current.Core.Read(address, 32);
            NativeBattleActionMatrix basis = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(index * 2))).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(20)), BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(24)), BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(28)), 4096);
            NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native detached geometry requires its current camera owner.");
            NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native detached geometry requires its current projection registers.");
            short focal = BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(18));
            if (focal == 0)
            {
                focal = camera.FocalLength ?? throw new NotSupportedException("Native detached geometry requires its current global focal WORD.");
            }
            NativeCamera = camera with { Projection = projection with { FocalLength = unchecked((ushort)focal) } };
            NativeBattleCinematicScaledBasisResult prepared = NativeBattleCinematicScaledBasis.Prepare(current, resourceLength);
            return new(prepared.Frame, address, basis, prepared.Matrix);
        }, (current, matrix, vertices, kind) =>
        {
            NativeBattleScriptedGraphicsMemory controls = RequireWaterMemory().ScriptedGraphics ?? throw new NotSupportedException("Native detached geometry requires its current depth-cue controls.");
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native detached geometry requires its current projection registers.");
            ImmutableArray<NativeBattleModuleProjectedVertex>.Builder projected = ImmutableArray.CreateBuilder<NativeBattleModuleProjectedVertex>(vertices.Length);
            for (int index = 0; index < vertices.Length; index++)
            {
                bool single = kind == NativeBattleCinematicDetached66ProjectionKind.Baseline || index == 3;
                projected.Add(single ? NativeBattleModuleProjection.Project45FAA0(matrix, vertices[index], projection, controls.Dqa, controls.Dqb) : NativeBattleModuleProjection.Project45FE10(matrix, vertices[index], projection));
            }
            ImmutableArray<NativeBattleModuleProjectedVertex> result = projected.MoveToImmutable();
            uint flags = result.IsEmpty ? 0 : result[^1].Flags;
            if (kind == NativeBattleCinematicDetached66ProjectionKind.Triangle)
            {
                uint early = (result[0].Flags | result[1].Flags) & 0x66000;
                flags |= early | (early != 0 ? 0x80000000u : 0);
            }
            return new(current, result, result.IsEmpty ? 0 : result[^1].Depth >> 2, flags) { PilotWorld = vertices.Select(vertex => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, vertex), projection)).ToImmutableArray() };
        }, (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        NativeBattleWaterSlotBank visualBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        NativeBattlePilotVisualIdentity identity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, frame, visualBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, visualBank));
        return (NativeBattleCinematicDetached66.Draw(frame, services, identity), graphics);
    }
}
