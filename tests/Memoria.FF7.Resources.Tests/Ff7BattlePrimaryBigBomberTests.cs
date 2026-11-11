using System.Text.Json;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7BattlePrimaryBigBomberTests
{
    [Fact]
    public void OriginalBigBomberPreservesBothMuzzlesAndGroundBurstsAcrossRestore()
    {
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleBigBomber(new(), 4, 1);
        Ff7BattlePrimaryState? cold = null;
        int random = 0;
        int coldRandom = 0;
        int muzzle = 0;
        int debris = 0;
        int meshes = 0;
        int peak = 0;
        List<(int Frame, ushort Sound, int Actor)> sounds = [];
        Ff7BattlePrimaryContext context = new((_, _, _) => throw new InvalidOperationException(), _ => default, _ => 0, () => ++random, JointPoint: (slot, selector) => slot == 4 ? new(100 + 20 * selector, -650, -50) : new(900, -800, 1800));
        for (int frame = 0; frame < 45; frame++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            Assert.Equal(frame == 35 ? 1 : 0, state.ImpactTargets);
            Ff7BattlePrimaryBigBomberSpriteDraw[] sprites = state.Draws.OfType<Ff7BattlePrimaryBigBomberSpriteDraw>().ToArray();
            Ff7BattlePrimaryBigBomberMeshDraw[] models = state.Draws.OfType<Ff7BattlePrimaryBigBomberMeshDraw>().ToArray();
            muzzle += sprites.Count(sprite => sprite.Sprite == 7);
            debris += sprites.Count(sprite => sprite.Sprite != 7);
            meshes += models.Length;
            peak = Math.Max(peak, state.Slots.Count(slot => slot is not null));
            sounds.AddRange(state.PositionedSounds.Select(sound => (frame, sound.Sound, sound.Actor)));
            if (frame == 0)
            {
                Assert.Equal(new Ff7BattleBraverPoint[] { new(320, -650, -50), new(340, -650, -50) }, sprites.Select(sprite => sprite.Position));
            }
            if (frame == 20)
            {
                Assert.Equal(new Ff7BattleBraverPoint(1266, 0, 2167), sprites[0].Position);
                Assert.Equal(new Ff7BattleBraverPoint[] { new(1465, 0, 2365), new(334, 0, 1234) }, models.Where(model => model.Model == 0).Select(model => model.Position));
                Assert.Equal((1536, 4096, (byte)255), (models[4].Width, models[4].Height, models[4].Weight));
            }
            if (cold is not null)
            {
                cold = Ff7BattlePrimaryEffects.Advance(cold, context with { NextCrt = () => ++coldRandom });
                Assert.Equal(random, coldRandom);
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(cold));
            }
            if (frame == 23)
            {
                cold = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
                coldRandom = random;
            }
        }
        Assert.Equal((16, 960, 360, 298, 61), (muzzle, debris, meshes, random, peak));
        Assert.Equal(new (int, ushort, int)[] { (0, 20, 4), (20, 250, 0) }, sounds);
        Assert.False(state.HasCallbacks);
    }
}
