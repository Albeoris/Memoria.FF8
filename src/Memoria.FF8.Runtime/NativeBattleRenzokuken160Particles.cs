using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokuken160Controller
{
    /// <summary>600980 draws every live slot before applying its original pause-gated state changes.</summary>
    public static NativeBattleRenzokuken160Frame AdvanceParticles600980(NativeBattleRenzokuken160Frame frame, NativeBattleRenzokuken160Resources resources, uint graphicsFlags, Func<NativeBattleRenzokuken160Frame, NativeBattleRenzokuken160Sprite, NativeBattleRenzokuken160SpriteStep> draw)
    {
        NativeBattleRenzokuken160State state = State(frame);
        if (resources.ParticleSprites.IsDefault || resources.ParticleSprites.Length != 3 || resources.SpiralColors.IsDefault || resources.SpiralColors.Length != 24)
        {
            throw new InvalidDataException("Native600980 requires its three actual sprite programs and24 color DWORDs.");
        }
        byte[][] banks = [state.Sparks.ToArray(), state.Debris.ToArray(), state.Spirals.ToArray()];
        bool advance = (graphicsFlags & 0x201) == 0;
        for (int bank = 0; bank < banks.Length; bank++)
        {
            byte[] bytes = banks[bank];
            int stride = bank switch { 0 => 12, 1 => 20, _ => 16 };
            for (int offset = 0; offset < bytes.Length; offset += stride)
            {
                short counter = Read(bytes, offset);
                if (counter < 0)
                {
                    continue;
                }
                short scale = Read(bytes, offset + 2);
                uint? color = null;
                if (bank == 2)
                {
                    short colorIndex = Read(bytes, offset + 12);
                    if ((uint)colorIndex >= 24)
                    {
                        throw new InvalidDataException("Native600980 spiral color selector exceeds its original24 DWORDs.");
                    }
                    color = resources.SpiralColors[colorIndex];
                }
                NativeBattleVector position = new(Read(bytes, offset + 4), Read(bytes, offset + 6), Read(bytes, offset + 8));
                NativeBattleRenzokuken160SpriteStep rendered = draw(frame, new(resources.ParticleSprites[bank], unchecked((ushort)counter), position, scale, color, bank == 2 ? (ushort)4 : (ushort)0) { PilotIdentity = NativeBattleRenzokukenVisualLifetimes.Particle(frame.Effect, NativeBattleRenzokukenVisualLifetimes.Bank(frame.Effect.Binding.Effect, bank), offset / stride, resources.ParticleSprites[bank].Address) });
                frame = rendered.Frame;
                if (!advance)
                {
                    continue;
                }
                Write(bytes, offset, counter + 1);
                if (rendered.AnimationEnded)
                {
                    Write(bytes, offset, -1);
                    continue;
                }
                if (bank == 1)
                {
                    Write(bytes, offset + 2, scale - (scale >> 5));
                    Write(bytes, offset + 4, Read(bytes, offset + 4) + Read(bytes, offset + 12));
                    Write(bytes, offset + 6, Read(bytes, offset + 6) + Read(bytes, offset + 14));
                    Write(bytes, offset + 8, Read(bytes, offset + 8) + Read(bytes, offset + 16));
                    for (int velocity = offset + 12; velocity <= offset + 16; velocity += 2)
                    {
                        short value = Read(bytes, velocity);
                        Write(bytes, velocity, value - (value >> 3));
                    }
                }
                if (bank == 2)
                {
                    Write(bytes, offset + 6, Read(bytes, offset + 6) + Read(bytes, offset + 10));
                    short nextColor = unchecked((short)(Read(bytes, offset + 12) + 1));
                    Write(bytes, offset + 12, nextColor >= 24 ? 0 : nextColor);
                }
            }
        }
        state = State(frame) with { Sparks = banks[0].ToImmutableArray(), Debris = banks[1].ToImmutableArray(), Spirals = banks[2].ToImmutableArray() };
        return WriteState(frame, state);
    }

    private static short Read(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
    private static void Write(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), unchecked((short)value));
    private static NativeBattleRenzokuken160State State(NativeBattleRenzokuken160Frame frame)
    {
        return frame.Effect.Binding.Effect switch
        {
            160 => frame.Memory.Renzokuken160 ?? throw new NotSupportedException("Native160 requires its retained local module state."),
            161 or >= 334 and <= 337 or >= 339 and <= 343 => NativeBattleRenzokukenSpecialController.ReadState(frame).Core,
            _ => NativeBattleRenzokukenVariantController.ReadState(frame).Core
        };
    }

    private static NativeBattleRenzokuken160Frame WriteState(NativeBattleRenzokuken160Frame frame, NativeBattleRenzokuken160State state)
    {
        return frame.Effect.Binding.Effect switch
        {
            160 => frame with { Memory = frame.Memory with { Renzokuken160 = state } },
            161 or >= 334 and <= 337 or >= 339 and <= 343 => NativeBattleRenzokukenSpecialController.WriteState(frame, state),
            _ => NativeBattleRenzokukenVariantController.WriteState(frame, state)
        };
    }

}
