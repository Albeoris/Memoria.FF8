using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState DrawNativeBreak117Body(NativeBattleGraphicsState graphics, int index, int level, ImmutableArray<byte> dither, NativeBattlePilotVisualIdentity identity)
    {
        NativeBattleGraphicsModel model = graphics.Models[index];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Break requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Break requires its retained view matrix.");
        graphics = SubmitNativeShadowPackets5088A0(graphics, model, projection, 4112, false);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native Break requires its actual body skeleton.");
        model = NativeBattlePoseMatrices.ComposeGeometryMatrices5095B0(model, source, view);
        graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(model, source);
        ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new NotSupportedException("Native Break requires its actual body geometry.");
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native Break requires its observed extra geometry presence.");
        bool shared = !linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native Break requires its actual linked skeleton ownership."));
        uint mask = model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native Break requires its actual mesh mask.");
        uint color = model.Color40 ?? throw new NotSupportedException("Native Break requires its retained model color.");
        NativeBattleModelTextureBinding bodyBinding = model.BodyTextureSlots ?? throw new NotSupportedException("Native Break requires its body material relocation.");
        byte ditherIndex = 0;
        if (shared && (geometry.Length < 2 || linked.Length != 1))
        {
            throw new NotSupportedException("Native Break shared-skeleton geometry requires its actual weapon object and body directory slot.");
        }
        for (int meshIndex = 0; meshIndex < geometry.Length; meshIndex++)
        {
            if ((mask & (1u << (meshIndex & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = shared && meshIndex == 1 ? linked[0] : geometry[meshIndex];
            NativeBattleModelTextureBinding binding = shared && meshIndex == 1 ? model.LinkedTextureSlots ?? throw new NotSupportedException("Native Break requires its weapon material relocation.") : bodyBinding;
            Submit(mesh, matrices, resource.GeometryRelocated507550 ? null : binding, meshIndex);
        }
        if (!shared && !linked.IsEmpty)
        {
            ImmutableArray<NativeBattleActionMatrix> linkedMatrices = NativeBattlePoseMatrices.LinkedGeometryMatrices502D40(model, resource.LinkedPoseSource ?? throw new NotSupportedException("Native Break requires its extra skeleton."), view);
            NativeBattleModelTextureBinding binding = model.LinkedTextureSlots ?? throw new NotSupportedException("Native Break requires its extra material relocation.");
            for (int meshIndex = 0; meshIndex < linked.Length; meshIndex++)
            {
                Submit(linked[meshIndex], linkedMatrices, binding, 0x10000 + meshIndex);
            }
        }
        graphics = graphics with { Models = graphics.Models.SetItem(index, NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[index], source)) };
        return graphics;

        void Submit(NativeBattleRawMesh mesh, ImmutableArray<NativeBattleActionMatrix> bones, NativeBattleModelTextureBinding? binding, int visualMesh)
        {
            NativeBattleBreak117BodyResult batch = NativeBattleBreak117Body.Draw([mesh], bones, projection, color, 1, dither, level, binding, ditherIndex, identity with { Primitive = unchecked(0x40000000 + visualMesh) });
            ditherIndex = batch.DitherIndex;
            int capacity = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native Break requires its current physical projection capacity.")) * 8);
            if (batch.Projected.Length > capacity)
            {
                throw new InvalidDataException("Native Break actor projection exceeds its original CPU scratch owner.");
            }
            uint scratch = checked(0x196B018u - (uint)capacity);
            NativeFileStaging = (NativeFileStaging ?? throw new NotSupportedException("Native Break requires its current physical projection memory.")).Write(scratch, batch.Projected.AsSpan());
            graphics = AppendNativePrimitivePackets(graphics, batch.Primitives);
        }
    }

}
