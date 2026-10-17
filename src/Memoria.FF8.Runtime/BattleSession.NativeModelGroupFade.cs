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
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native50F190 requires its actual decoded body skeleton.");
        ImmutableArray<NativeBattleActionMatrix> matrices = deformed ?? NativeBattlePoseMatrices.ModelGroupMatrices50F190(model, source, view);
        ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new NotSupportedException("Native50F190 requires its actual original geometry directory.");
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native50F190 requires its observed linked geometry presence.");
        bool shared = !linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native50F190 requires its original shared-skeleton resource shape."));
        uint mask = 1u << group;
        for (int meshIndex = 0; meshIndex < geometry.Length; meshIndex++)
        {
            if ((mask & (1u << meshIndex)) == 0)
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
            graphics = AppendNativePrimitivePackets(graphics, NativeBattleRawGeometryPackets.Emit5099D0([mesh], matrices, projection, color, 1, null, 17, 4, binding));
        }
        return graphics;
    }
}
