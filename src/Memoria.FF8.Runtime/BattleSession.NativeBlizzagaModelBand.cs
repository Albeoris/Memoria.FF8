using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, short Minimum, short Maximum) DrawNativeBlizzagaModelBand(NativeBattleGraphicsState graphics, int index, short low, short high)
    {
        NativeBattleGraphicsModel model = graphics.Models[index];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Blizzaga requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Blizzaga requires its retained view matrix.");
        graphics = SubmitNativeShadowPackets5088A0(graphics, model, projection, 4112, false);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native Blizzaga requires its actual body skeleton.");
        model = NativeBattlePoseMatrices.ComposeGeometryMatrices5095B0(model, source, view);
        graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(model, source);
        ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new NotSupportedException("Native Blizzaga requires its actual body geometry.");
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native Blizzaga requires its observed extra geometry presence.");
        bool shared = !linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native Blizzaga requires its actual linked skeleton ownership."));
        uint mask = model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native Blizzaga requires its actual mesh mask.");
        uint color = model.Color40 ?? throw new NotSupportedException("Native Blizzaga requires its retained model color.");
        NativeBattleModelTextureBinding bodyBinding = model.BodyTextureSlots ?? throw new NotSupportedException("Native Blizzaga requires its body material relocation.");
        short minimum = 32767;
        short maximum = -32767;
        if (shared && (geometry.Length < 2 || linked.Length != 1))
        {
            throw new NotSupportedException("Native Blizzaga shared-skeleton geometry requires its actual weapon object and body directory slot.");
        }
        for (int meshIndex = 0; meshIndex < geometry.Length; meshIndex++)
        {
            if ((mask & (1u << (meshIndex & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = shared && meshIndex == 1 ? linked[0] : geometry[meshIndex];
            NativeBattleModelTextureBinding binding = shared && meshIndex == 1 ? model.LinkedTextureSlots ?? throw new NotSupportedException("Native Blizzaga requires its weapon material relocation.") : bodyBinding;
            Submit(mesh, matrices, resource.GeometryRelocated507550 ? null : binding);
        }
        if (!shared && !linked.IsEmpty)
        {
            ImmutableArray<NativeBattleActionMatrix> linkedMatrices = NativeBattlePoseMatrices.LinkedGeometryMatrices502D40(model, resource.LinkedPoseSource ?? throw new NotSupportedException("Native Blizzaga requires its extra skeleton."), view);
            NativeBattleModelTextureBinding binding = model.LinkedTextureSlots ?? throw new NotSupportedException("Native Blizzaga requires its extra material relocation.");
            foreach (NativeBattleRawMesh mesh in linked)
            {
                Submit(mesh, linkedMatrices, binding);
            }
        }
        graphics = graphics with { Models = graphics.Models.SetItem(index, NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[index], source)) };
        return (graphics, minimum, maximum);

        void Submit(NativeBattleRawMesh mesh, ImmutableArray<NativeBattleActionMatrix> bones, NativeBattleModelTextureBinding? binding)
        {
            NativeBattleBlizzagaModelBandResult batch = NativeBattleBlizzagaModelBand.Emit6D8D80([mesh], bones, projection, color, 1, low, high, binding, minimum, maximum);
            minimum = batch.Minimum;
            maximum = batch.Maximum;
            graphics = AppendNativePrimitivePackets(graphics, batch.Primitives);
        }
    }

    private static NativeBattleGraphicsState SetNativeBlizzagaModelFlags(NativeBattleGraphicsState graphics, int index, bool enabled, bool band)
    {
        NativeBattleGraphicsModel model = graphics.Models[index];
        ImmutableArray<NativeBattleGraphicsModel> models = graphics.Models.SetItem(index, model with { Flags0 = (ushort)((model.Flags0 & ~4) | (enabled ? 4 : 0)) });
        foreach (int linked in NativeBattleModelRings.Walk(models, index))
        {
            NativeBattleGraphicsModel current = models[linked];
            models = models.SetItem(linked, current with { Flags0 = (ushort)((current.Flags0 & ~8) | (band ? 8 : 0)) });
        }
        return graphics with { Models = models };
    }
}
