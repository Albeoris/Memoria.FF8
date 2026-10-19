using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState SubmitNativeModelGroup50F190(NativeBattleGraphicsState graphics, int index, byte group, byte counter, uint sourceColor, ImmutableArray<NativeBattleActionMatrix>? deformed = null)
    {
        NativeBattleGraphicsModel model = graphics.Models[index];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native50F190 requires its prior published camera matrix.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native50F190 requires its retained camera view matrix.");
        NativeBattleSummonColors rules = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native50F190 requires its authenticated GTE color interpolation constants.");
        uint color = (rules.Interpolate(sourceColor, (counter << 12) / 15, 0) & 0xFFFFFFu) | 0x02000000u;
        PublishNativeModelRendererColor(0);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native50F190 requires its actual decoded body skeleton.");
        ImmutableArray<NativeBattleActionMatrix> matrices;
        if (deformed is { } deformation)
        {
            matrices = deformation;
        }
        else
        {
            model = NativeBattlePoseMatrices.ComposeGeometryMatrices5095B0(model, source, view);
            graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
            matrices = NativeBattleBodyBoneMatrices.Require(model, source);
        }
        ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new NotSupportedException("Native50F190 requires its actual original geometry directory.");
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native50F190 requires its observed linked geometry presence.");
        bool shared = !linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native50F190 requires its original shared-skeleton resource shape."));
        uint mask = 1u << group;
        bool draw = ResetAndEnterNativeModelRenderer5099D0(color);
        for (int meshIndex = 0; meshIndex < geometry.Length; meshIndex++)
        {
            if (!draw || (mask & (1u << meshIndex)) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = geometry[meshIndex];
            NativeBattleModelTextureBinding binding = model.BodyTextureSlots ?? throw new NotSupportedException("Native50F190 requires its executed body material relocation.");
            if (shared && meshIndex == 1)
            {
                if (geometry.Length < 2 || linked.Length != 1)
                {
                    throw new NotSupportedException("Native50F190 shared-skeleton resource requires its original single weapon object.");
                }
                mesh = linked[0];
                binding = model.LinkedTextureSlots ?? throw new NotSupportedException("Native50F190 requires its executed weapon material relocation.");
            }
            graphics = AppendNativeModelRendererPackets(graphics, NativeBattleRawGeometryPackets.Emit5099D0([mesh], matrices, projection, color, 1, null, 17, 4, binding), true);
        }
        if (deformed is null)
        {
            graphics = graphics with { Models = graphics.Models.SetItem(index, NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[index], source)) };
        }
        return graphics;
    }
}
