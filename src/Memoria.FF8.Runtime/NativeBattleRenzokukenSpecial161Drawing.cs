using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenSpecialController
{
    public static NativeBattleRenzokuken160ChildStep Advance5FEA70(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = unchecked((short)task.ReadWord(12));
        short scale = unchecked((short)task.ReadWord(28));
        NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0)), scale, 4096, scale) with { X = unchecked((short)task.ReadWord(16)), Y = unchecked((short)task.ReadWord(18)), Z = unchecked((short)task.ReadWord(20)) };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, world);
        NativeBattleModuleMeshContext context = counter >= 4 ? new(195, 0, 682 * (counter - 4), null, null, null, 17, 2) : new(3, null, null, null, null, null, 17, 2);
        NativeBattleGfGeometryBatch packets = NativeBattleModulePackets.EmitBatch572200(resources.Meshes[2].Bytes, 0, default, matrix, projection, context, colors);
        frame = inputs.Execute(frame, new NativeBattleRenzokuken160MeshDraw(packets));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        short speed = unchecked((short)task.ReadWord(30));
        task = task.WriteWord(28, unchecked((ushort)(scale + speed))).WriteWord(12, unchecked((ushort)(counter + 1))).WriteWord(30, unchecked((ushort)(speed - speed / 3)));
        return new(frame, pool.WriteSlot(slot, task), task.ReadWord(12) >= 10 ? 2 : 0);
    }

    public static NativeBattleRenzokuken160Frame AdvanceParticles5FEC60(NativeBattleRenzokuken160Frame frame, uint graphicsFlags, Func<NativeBattleRenzokuken160Frame, NativeBattleRenzokuken160Sprite, NativeBattleRenzokuken160SpriteStep> draw)
    {
        NativeBattleRenzokukenSpecialState state = ReadState(frame);
        if (state.ExtraSprites.Length != 2)
        {
            throw new NotSupportedException("Native5FEC60 requires its actual target sprite and common sprite3 resources.");
        }
        byte[][] banks = [state.Core.Sparks.ToArray(), state.Core.Debris.ToArray(), state.TargetParticles.ToArray(), state.BurstParticles.ToArray(), state.Core.Spirals.ToArray()];
        NativeBattleSearchSpriteResource[] sprites = [state.Resources.ParticleSprites[0], state.Resources.ParticleSprites[1], state.ExtraSprites[0], state.ExtraSprites[1], state.Resources.ParticleSprites[2]];
        bool advance = (graphicsFlags & 0x201) == 0;
        for (int bank = 0; bank < banks.Length; bank++)
        {
            byte[] bytes = banks[bank];
            int stride = bank switch
            {
                0 => 12,
                4 => 16,
                _ => 20
            };
            for (int offset = 0; offset < bytes.Length; offset += stride)
            {
                short counter = Get161(bytes, offset);
                if (counter < 0)
                {
                    continue;
                }
                short scale = Get161(bytes, offset + 2);
                uint? color = bank == 4 ? state.Resources.SpiralColors[Get161(bytes, offset + 12)] : null;
                NativeBattleVector position = new(Get161(bytes, offset + 4), Get161(bytes, offset + 6), Get161(bytes, offset + 8));
                NativeBattleRenzokuken160SpriteStep rendered = draw(frame, new(sprites[bank], unchecked((ushort)counter), position, scale, color, bank == 4 ? (ushort)4 : (ushort)0));
                frame = rendered.Frame;
                if (!advance)
                {
                    continue;
                }
                Put161(bytes, offset, counter + 1);
                if (rendered.AnimationEnded)
                {
                    Put161(bytes, offset, -1);
                    continue;
                }
                if (bank is 1 or 2 or 3)
                {
                    Put161(bytes, offset + 2, bank switch
                    {
                        1 => scale - (scale >> 5),
                        2 => scale + (Get161(banks[1], offset + 2) >> 5),
                        _ => scale + (scale >> 7)
                    });
                    Put161(bytes, offset + 4, Get161(bytes, offset + 4) + Get161(bytes, offset + 12));
                    if (bank != 3)
                    {
                        Put161(bytes, offset + 6, Get161(bytes, offset + 6) + Get161(bytes, offset + 14) + (bank == 2 ? Get161(bytes, offset + 18) : 0));
                    }
                    Put161(bytes, offset + 8, Get161(bytes, offset + 8) + Get161(bytes, offset + 16));
                    for (int velocity = offset + 12; velocity <= offset + 16; velocity += 2)
                    {
                        if (bank == 3 && velocity == offset + 14)
                        {
                            continue;
                        }
                        short value = Get161(bytes, velocity);
                        Put161(bytes, velocity, value - (value >> (bank == 2 ? 4 : 3)));
                    }
                    if (bank == 2 && Get161(bytes, offset + 6) > 0)
                    {
                        Put161(bytes, offset, -1);
                    }
                }
                if (bank == 4)
                {
                    Put161(bytes, offset + 6, Get161(bytes, offset + 6) + Get161(bytes, offset + 10));
                    int next = Get161(bytes, offset + 12) + 1;
                    Put161(bytes, offset + 12, next >= 24 ? 0 : next);
                }
            }
        }
        state = ReadState(frame);
        return Store(frame, state with { Core = state.Core with { Sparks = banks[0].ToImmutableArray(), Debris = banks[1].ToImmutableArray(), Spirals = banks[4].ToImmutableArray() }, TargetParticles = banks[2].ToImmutableArray(), BurstParticles = banks[3].ToImmutableArray() });
    }
}
