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
                case Ff7BattlePrimaryLaserCannonBeamDraw laserCannonBeam:
                    DrawLaserCannonBeam(renderer, laserCannonBeam, view);
                    break;
                case Ff7BattlePrimaryLaserCannonImpactDraw laserCannonImpact:
                    DrawLaserCannonImpact(renderer, laserCannonImpact, view);
                    break;
                case Ff7BattleTailLaserMeshDraw tailLaserMesh:
                    DrawTailLaserMesh(renderer, tailLaserMesh, view);
                    break;
                case Ff7BattleTailLaserSpriteDraw tailLaserSprite:
                    DrawTailLaserSprite(renderer, tailLaserSprite.Sprite, view);
                    break;
                case Ff7BattlePrimaryBigBomberMeshDraw bomberMesh:
                    DrawBigBomberMesh(renderer, bomberMesh, view);
                    break;
                case Ff7BattlePrimaryBigBomberSpriteDraw bomberSprite:
                    DrawBigBomberSprite(renderer, bomberSprite, view);
                    break;
                case Ff7BattlePrimaryBeamGunMeshDraw shot:
                    DrawBeamGunMesh(renderer, shot, view);
                    break;
                case Ff7BattlePrimaryBeamGunSpriteDraw flash:
                    DrawBeamGunSprite(renderer, flash, view);
                    break;
                case Ff7BattlePrimaryBubbleSpriteDraw bubble:
                    DrawBubbleSprite(renderer, bubble, view);
                    break;
                case Ff7BattlePrimaryBubbleRibbonDraw ribbon:
                    DrawBubbleRibbon(renderer, ribbon, view);
                    break;
                case Ff7BattlePrimaryBubbleCloudDraw cloud:
                    DrawBubbleCloud(renderer, cloud, view);
                    break;
                case Ff7BattlePrimaryEnergyBallMeshDraw energyBallMesh:
                    DrawEnergyBallMesh(renderer, energyBallMesh, view);
                    break;
                case Ff7BattlePrimaryEnergyBallSpriteDraw energyBallSprite:
                    DrawEnergyBallSprite(renderer, energyBallSprite, view);
                    break;
                case Ff7BattlePrimaryWaveMeshDraw waveMesh:
                    DrawWaveMesh(renderer, waveMesh, view);
                    break;
                case Ff7BattlePrimaryWaveSpriteDraw waveSprite:
                    DrawWaveSprite(renderer, waveSprite, view);
                    break;
                case Ff7BattlePrimaryRocketDraw rocket:
                    DrawRockets(renderer, rocket.State, view);
                    break;
                case Ff7BattlePrimarySmogSpriteDraw smogSprite:
                    DrawSmog(renderer, new(smogSprite.Frame, [], [smogSprite.Sprite]), view);
                    break;
                case Ff7BattlePrimarySmogCloudDraw smogCloud:
                    DrawSmog(renderer, new(smogCloud.Frame + 12, [new(smogCloud.Target, Cloud: smogCloud.Position)], []), view);
                    break;
                case Ff7BattlePrimarySmokeShotDraw smokeShot:
                    DrawSmokeShot(renderer, new(smokeShot.Frame, [], [smokeShot.Sprite]), view);
                    break;
                case Ff7BattlePrimaryBigShotDraw bigShot:
                    DrawBigShot(renderer, bigShot.State, view);
                    break;
                case Ff7BattlePrimaryBeatRushDraw beatRush:
                    DrawBeatRush(renderer, beatRush.State, view);
                    break;
                case Ff7BattlePrimaryBraverDraw braver:
                    DrawBraver(renderer, braver.State, view);
                    break;
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
                case Ff7BattlePrimaryFireDraw fire:
                    DrawFireSprite(renderer, fire.Sprite, view);
                    break;
                case Ff7BattlePrimaryIceDraw ice:
                    DrawIceCrystal(renderer, ice.Crystal, ice.Frame, view);
                    break;
                default:
                    throw new NotSupportedException("FFVII primary packet has no admitted renderer.");
            }
        }
    }
}
