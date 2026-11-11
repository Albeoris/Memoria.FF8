using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private void DrawPrimaryEffects(ITriangleRenderer renderer, Ff7BattlePrimaryState state, CameraView view)
    {
        foreach (Ff7BattlePrimaryDraw draw in state.Draws)
        {
            switch (draw)
            {
                case Ff7BattlePrimaryScopeMeshDraw mesh:
                    DrawSearchScopeMesh(renderer, mesh.Source, mesh.Basis, mesh.Mesh, view);
                    break;
                case Ff7BattlePrimaryScopeSpriteDraw sprite:
                    DrawSearchScopeSprite(renderer, sprite.Position, sprite.Stage, sprite.Frame, view);
                    break;
                case Ff7BattlePrimarySilkLineDraw line:
                    DrawSilkLine(renderer, line, view);
                    break;
                case Ff7BattlePrimarySilkSpriteDraw silkSprite:
                    DrawSilkSprite(renderer, silkSprite, view);
                    break;
                case Ff7BattlePrimaryCureDraw cure:
                    DrawCureParticle(renderer, cure.Kind, cure.Particle, view);
                    break;
                case Ff7BattlePrimaryBoltStrikeDraw boltStrike:
                    DrawBoltStrike(renderer, boltStrike.Strike, boltStrike.Frame, view);
                    break;
                case Ff7BattlePrimaryBoltSparkDraw boltSpark:
                    DrawBoltSpark(renderer, boltSpark.Spark, view);
                    break;
                case Ff7BattlePrimaryDestroyedSpriteDraw destroyedSprite:
                    DrawDestroyedSprite(renderer, destroyedSprite.Sprite, destroyedSprite.Frame, view);
                    break;
                case Ff7BattlePrimaryDestroyedPulseDraw pulse:
                    DrawDestroyedPulse(renderer, pulse.Blue, view);
                    break;
                default:
                    throw new NotSupportedException("FFVII primary packet has no admitted renderer.");
            }
        }
    }
}
