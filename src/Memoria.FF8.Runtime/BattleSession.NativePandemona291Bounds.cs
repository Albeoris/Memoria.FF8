using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleEffectMemory Memory, NativeBattlePandemona291Bounds Bounds) CaptureNativePandemona291Bounds(NativeBattleGraphicsState graphics, NativeBattleEffectMemory memory, int actorIndex)
    {
        NativeBattleGraphicsModel actor = graphics.ActorModels.Get(actorIndex);
        if (ReadNativeForeignModel(graphics, actorIndex) is { } foreign)
        {
            NativeBattleCameraProjection hostProjection = NativeCamera?.Projection ?? throw new NotSupportedException("Native actor bounds require their live projection registers.");
            NativeBattleScriptedGraphicsMemory hostControls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native actor bounds require their live depth-cue controls.");
            NativeBattleScriptedColorRegisters hostRegisters = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native actor bounds require their canonical packed V0 owner.");
            NativeBattleActionMatrix groundBasis = foreign.World with { Y = 0 };
            var captured = NativeBattleForeignModelDrawing.VerticalBounds(foreign, point =>
            {
                byte[] input = hostRegisters.NormalInput?.ToArray() ?? new byte[8];
                bool[] known = hostRegisters.NormalInputKnown?.ToArray() ?? Enumerable.Repeat(hostRegisters.NormalInput is not null, 8).ToArray();
                BinaryPrimitives.WriteInt16LittleEndian(input, point.X);
                BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(2), point.Y);
                BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(4), point.Z);
                Array.Fill(known, true, 0, 6);
                NativeBattleModuleDepthCueVertex projected = NativeBattleModuleProjection.ProjectWithCue45FAA0(groundBasis, point, hostProjection, hostControls.Dqa, hostControls.Dqb);
                hostRegisters = hostRegisters with { NormalInput = input.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray(), Mac = [null, null, null], Ir = [null, projected.Ir2, null], Flags = projected.Projected.Flags };
                return projected.Ir2;
            });
            return (graphics, memory with { ScriptedColorRegisters = hostRegisters }, new(captured.Maximum, captured.Minimum));
        }
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actorIndex);
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new NotSupportedException("Native Pandemona requires its actual actor pose source.");
        NativeBattleActionMatrix world = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native Pandemona bounds require the current actor world matrix.");
        NativeBattleModelTransform transform = actor.Transform ?? throw new NotSupportedException("Native Pandemona bounds require the current signed actor position.");
        NativeBattleActionMatrix ground = world with { Y = 0 };
        actor = actor with { GeometryWorldMatrix = ground };
        actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, ground);
        actor = actor with { GeometryWorldMatrix = world with { Y = transform.Position28.Y } };
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(actorIndex, actor) };
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(actor, pose);
        ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new NotSupportedException("Native Pandemona bounds require the actual body geometry directory.");
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native Pandemona bounds require the actual linked geometry presence.");
        bool shared = !linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native Pandemona bounds require their executed shared skeleton layout."));
        if (shared)
        {
            if (geometry.Length < 2 || linked.Length != 1)
            {
                throw new NotSupportedException("Native Pandemona bounds require the actual linked directory slot.");
            }
            geometry = geometry.SetItem(1, linked[0]);
        }
        uint mask = actor.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native Pandemona bounds require the current actor object mask.");
        byte modelId = actor.ModelId4 ?? throw new NotSupportedException("Native Pandemona bounds require the current actor model identifier.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Pandemona bounds require their live projection registers.");
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native Pandemona bounds require their live depth-cue controls.");
        NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Pandemona bounds require the canonical packed V0 owner.");
        NativeBattlePandemona291Bounds bounds = NativeBattlePandemona291ActorBounds.Capture6ED770(geometry, mask, modelId, vertex =>
        {
            if (vertex.Bone >= matrices.Length)
            {
                throw new InvalidDataException("Native Pandemona vertex has no current actor bone matrix.");
            }
            byte[] input = registers.NormalInput?.ToArray() ?? new byte[8];
            bool[] known = registers.NormalInputKnown?.ToArray() ?? Enumerable.Repeat(registers.NormalInput is not null, 8).ToArray();
            BinaryPrimitives.WriteInt16LittleEndian(input, vertex.Position.X);
            BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(2), vertex.Position.Y);
            BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(4), vertex.Position.Z);
            Array.Fill(known, true, 0, 6);
            NativeBattleModuleDepthCueVertex projected = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrices[vertex.Bone], vertex.Position, projection, controls.Dqa, controls.Dqb);
            registers = registers with { NormalInput = input.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray(), Mac = [null, null, null], Ir = [null, projected.Ir2, null], Flags = projected.Projected.Flags };
            return projected.Ir2;
        });
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(actorIndex, NativeBattleBodyBoneMatrices.Publish508C90(graphics.ActorModels.Get(actorIndex), pose)) };
        return (graphics, memory with { ScriptedColorRegisters = registers }, bounds);
    }
}
