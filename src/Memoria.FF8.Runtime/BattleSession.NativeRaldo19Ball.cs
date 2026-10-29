using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleRaldo19Frame Frame) DrawNativeRaldo19Ball(NativeBattleGraphicsState graphics, NativeBattleRaldo19Frame current, int index, NativeBattlePilotVisualIdentity identity)
    {
        NativeBattleRaldo19BallParameters ball = NativeBattleRaldo19BallRendering.Read(current.State.BallC4);
        if (ball.Actor != index || BinaryPrimitives.ReadUInt32LittleEndian(current.State.Descriptor48.Read(4, 4)) != NativeBattleRaldo19BallRendering.VertexAddress)
        {
            throw new InvalidDataException("Raldo ball requires its retained descriptor and selected actor.");
        }
        NativeBattleGraphicsModel actor = graphics.Models[index];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Raldo ball requires its current original body rig.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Raldo ball requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Raldo ball requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Raldo ball requires its actual view matrix.");
        NativeBattleScriptedGraphicsMemory controls = current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Raldo ball requires its projection controls.");
        NativeBattleSummonColors constants = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Raldo ball requires its geometry constants.");
        NativeBattleGeometryRegisterStep registers = new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Raldo ball requires its retained geometry registers."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Raldo ball requires its retained color registers."));
        PublishBones();
        NativeBattleRaldo19BallPreparation prepared = NativeBattleRaldo19BallRendering.Prepare(current.State.BallC4, actor.GeometryWorldMatrix ?? throw new NotSupportedException("Raldo ball requires its actual model matrix."), null, view, camera.CanonicalViewPadding is { } cameraPadding ? unchecked((short)cameraPadding) : null, registers);
        registers = prepared.Registers;
        current = current with { State = current.State with { BallC4 = prepared.BallC4 } };
        Shadow();
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(actor, source);
        NativeBattleDecodedPose pose = NativeBattlePoseMatrices.ReadDecodedPose(actor.SequenceState!, source);
        Draw(DiablosActorGeometry(resource, actor), bones, false, true, bone => !pose.HasScale && Parent(source, bone) < 0 ? (short)0 : null);
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Raldo ball requires its actual linked-object presence.");
        if (!linked.IsEmpty && resource.LinkedGeometryUsesBodySkeleton != true)
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Raldo ball requires its linked original rig.");
            NativeBattleDecodedPose linkedPose = NativeBattlePoseMatrices.ReadLinkedDecodedPose(actor.SequenceState!, linkedSource);
            Draw(linked, NativeBattlePoseMatrices.StageBoneMatrices(linkedSource, linkedPose), true, resource.GeometryRelocated507550, bone => !linkedPose.HasScale && Parent(linkedSource, bone) < 0 ? (short)0 : null);
        }
        PublishBones();
        current = current with { Memory = current.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        return (graphics with { Models = graphics.Models.SetItem(index, actor) }, current);

        void Draw(ImmutableArray<NativeBattleRawMesh> meshes, ImmutableArray<NativeBattleActionMatrix> matrices, bool isLinked, bool relocated, Func<int, short?> padding)
        {
            ImmutableArray<NativeBattleRawMesh>.Builder selected = ImmutableArray.CreateBuilder<NativeBattleRawMesh>(meshes.Length);
            for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
            {
                NativeBattleRawMesh mesh = meshes[meshIndex];
                NativeBattleModelTextureBinding texture = isLinked || resource.LinkedGeometryUsesBodySkeleton == true && meshIndex == 1 ? actor.LinkedTextureSlots! : actor.BodyTextureSlots!;
                selected.Add(relocated ? mesh : mesh with { Polygons = mesh.Polygons.Select(polygon => polygon with { Bytes = NativeBattleRawGeometryPackets.Relocate507550(polygon, texture) }).ToImmutableArray() });
            }
            (uint address, NativeBattleEffectArena temporary) = Allocate(0xA8);
            NativeBattleRaldo19BallDraw drawn = NativeBattleRaldo19BallRendering.DrawMeshes(current.State.BallC4, temporary, current.State.VertexScratch, selected.MoveToImmutable(), matrices, padding, actor.SequenceState!.MeshMask124, actor.Color40 ?? throw new NotSupportedException("Raldo ball requires its current model color."), projection, controls.Dqa, controls.Dqb, registers, current.Memory.ScriptedLighting ?? throw new NotSupportedException("Raldo ball requires its retained lighting controls."), constants, identity with { Primitive = isLinked ? 1 : 0 });
            Release(address, drawn.TemporaryA8, 0xA8);
            registers = drawn.Registers;
            current = current with { State = current.State with { VertexScratch = drawn.VertexScratch }, Memory = current.Memory with { ScriptedLighting = drawn.Lighting } };
            Append(drawn.Batch);
        }

        void Shadow()
        {
            NativeBattleGraphicsDefinition graphicsDefinition = definition.NativeGraphics!;
            NativeBattleShadowDrawStep drawn = NativeBattleShadowDrawing.Prepare5088A0(NativeFileStaging ?? throw new NotSupportedException("Raldo shadow requires its actual temporary memory."), registers, actor, projection, controls, graphicsDefinition.Rules.ShadowSource ?? throw new NotSupportedException("Raldo shadow requires its original template."), graphicsDefinition.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Raldo shadow requires its actual primitive capabilities."), 4112, 16, identity with { Primitive = 0x5088A0 });
            NativeFileStaging = drawn.Staging;
            registers = drawn.Registers;
            Append(drawn.Batch);
            Release(drawn.TemporaryAddress, drawn.Temporary78, 0x78);
        }

        void PublishBones()
        {
            (uint address, NativeBattleEffectArena temporary) = Allocate(0x58);
            NativeBattleBodyBonePreparation preparedBones = NativeBattleBodyBoneTemporary.Prepare508C90(actor, source, temporary);
            actor = preparedBones.Model;
            temporary = preparedBones.Temporary58;
            Release(address, temporary, 0x58);
        }

        void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Raldo ball requires its actual ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Raldo ball requires its common packet cursor."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            current = current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }

        (uint Address, NativeBattleEffectArena Arena) Allocate(int length)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Raldo requires its retained temporary memory."), length);
            NativeFileStaging = allocation.Staging;
            NativeBattleEffectArena physical = NativeBattleTemporaryMemory.ReadView(NativeFileStaging, allocation.Address, length);
            return (allocation.Address, NativeBattleEffectNodeContextMemory.Copy(physical, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length));
        }

        void Release(uint address, NativeBattleEffectArena arena, int length)
        {
            NativeFileStaging = NativeBattleTemporaryMemory.PublishView(NativeFileStaging!, address, arena, length);
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(NativeFileStaging, length);
        }

        static short Parent(NativeBattlePoseSource pose, int bone) => BinaryPrimitives.ReadInt16LittleEndian(pose.Skeleton.AsSpan().Slice(16 + bone * 48));
    }
}
