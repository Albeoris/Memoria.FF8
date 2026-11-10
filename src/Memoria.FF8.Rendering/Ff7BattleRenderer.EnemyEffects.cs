using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private Ff7BattleBillboardRenderer? smokeShot;
    private Ff7BattleBillboardRenderer[]? smog;

    private void DrawSmokeShot(ITriangleRenderer renderer, Ff7BattleSmokeShotState effect, CameraView view)
    {
        smokeShot ??= LoadBillboard("ff7/data/battle/special/kemu1/kemu1.s", ["threat.tex"], Ff7BattleSmokeShotEffect.SpriteFrameCount, _ => 0);
        foreach (Ff7BattleSmokeShotSprite sprite in effect.Sprites)
        {
            smokeShot.Draw(renderer, effect.Frame - sprite.StartedAt, view.Transform(sprite.Position.Position), view.Width, view.Height, view.Focal, scale: 2);
        }
    }

    private void DrawSmog(ITriangleRenderer renderer, Ff7BattleSmogState effect, CameraView view)
    {
        if (smog is null)
        {
            const string path = "ff7/data/battle/special/sumi/";
            smog =
            [
                LoadBillboard("sumi.s", ["sumi_a00.tex"], 1, _ => 0),
                LoadBillboard(path + "hit1.s", ["sumi_a00.tex"], 1, _ => 0),
                LoadBillboard(path + "hit21.s", ["sumi_a01.tex"], 8, _ => 0),
                LoadBillboard(path + "hit22.s", ["sumi_a01.tex"], 8, _ => 0),
                LoadBillboard("hit23.s", ["sumi_a01.tex"], 8, _ => 0),
                LoadBillboard("hit24.s", ["sumi_a01.tex"], 8, _ => 0)
            ];
        }
        for (int index = 0; index < effect.Targets.Count; index++)
        {
            int age = effect.Frame - index * 4 - 12;
            if (age is >= 0 and < 30 && effect.Targets[index].Cloud is { } cloud)
            {
                smog[1].Draw(renderer, 0, view.Transform(cloud.Position), view.Width, view.Height, view.Focal, Ff7BattleSmogEffect.CloudSize(age) * 400 / 4096f);
            }
        }
        foreach (Ff7BattleSmogSprite sprite in effect.Sprites)
        {
            int source = sprite.Velocity is null ? sprite.Variant + 2 : 0;
            int frame = sprite.Velocity is null ? effect.Frame - sprite.StartedAt : 0;
            smog[source].Draw(renderer, frame, view.Transform(sprite.Position.Position), view.Width, view.Height, view.Focal, sprite.Scale / 4096f);
        }
    }
}
