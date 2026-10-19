using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattlePandemona291Frame Frame, NativeBattleGraphicsState Graphics) AdvanceNativePandemona291Targets(NativeBattlePandemona291Frame frame, uint address, int age, NativeBattleVector? anchor, NativeBattleGraphicsState graphics, bool drawOnly = false)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona target motion requires its actual records.");
        int start = NativeBattleWaterExecutionFields.ArenaOffset(address, 0x1030);
        NativeBattlePandemona291State state = frame.Memory.Pandemona291 ?? throw new NotSupportedException("Native Pandemona target motion requires its current module state.");
        bool paused = (state.Pause2556258 ?? throw new NotSupportedException("Native Pandemona target motion requires its actual pause DWORD.")) != 0;
        int ground = state.Ground25562E0 ?? throw new NotSupportedException("Native Pandemona target motion requires its current ground coordinate.");
        ImmutableArray<byte> trigonometry = frame.Effect.Binding.Pandemona291Resources!.TrailTrigonometry;
        if (!drawOnly && !paused && age is >= 224 and < 254)
        {
            int elapsed = age - 224;
            for (int index = 0; index < Count(); index++)
            {
                int row = start + 0x14 + index * 32;
                int actor = Actor(row);
                NativeBattleGraphicsModel model = graphics.Models[actor];
                NativeBattleModelTransform transform = RequireTransform(model);
                NativeBattleVector position = transform.Position28;
                position = position with { Z = unchecked((short)(position.Z + elapsed)) };
                if (elapsed >= 15 + index * 2)
                {
                    position = position with { Z = unchecked((short)(position.Z + (elapsed << 5))), Y = unchecked((short)(position.Y - (elapsed << 3))) };
                }
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Transform = transform with { Position28 = position } }) };
            }
        }
        if (!drawOnly && !paused && age is >= 254 and < 284)
        {
            NativeBattleVector origin = anchor ?? throw new NotSupportedException("Native Pandemona target attraction requires its actual current model anchor.");
            int elapsed = age - 254;
            int phase = unchecked((int)((uint)(unchecked(elapsed << 12)) / 20));
            for (int index = 0; index < Count(); index++)
            {
                int row = start + 0x14 + index * 32;
                int actor = Actor(row);
                NativeBattleGraphicsModel model = graphics.Models[actor];
                NativeBattleModelTransform transform = RequireTransform(model);
                phase = unchecked(phase - 256);
                if (phase > 4352)
                {
                    arena = arena.Write(row + 0x1D, [0]);
                    continue;
                }
                NativeBattleVector initial = new(Word(row + 0xC), unchecked((short)(Word(row + 0xE) - 1000)), Word(row + 0x10));
                NativeBattleVector destination = origin with { Y = unchecked((short)(origin.Y - ((Word(row + 4) + Word(row + 6)) / 2))) };
                Store(row + 0x16, Word(row + 0x16) + ((index & 1) != 0 ? 512 : -512));
                Store(row + 0x14, Word(row + 0x14) + ((index & 1) != 0 ? elapsed << 3 : -(elapsed << 3)));
                NativeBattleVector position = NativeBattleQuezacotlRibbonMath.Blend(initial, destination, unchecked(4096 - phase), phase);
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Transform = transform with { Position28 = position } }) };
                phase >>= 2;
                phase = unchecked(4096 - NativeBattleCameraMath.Sine(phase));
                if (phase < 0)
                {
                    phase = 0;
                }
                int product = unchecked(Word(row + 8) * phase);
                Store(row + 0xA, unchecked(product / 4096 - phase + 4096));
            }
        }
        if (!drawOnly && !paused && age is >= 335 and < 377)
        {
            NativeBattleVector origin = anchor ?? throw new NotSupportedException("Native Pandemona target restoration requires its actual model anchor.");
            int elapsed = age - 335;
            int phase = unchecked(elapsed << 12) >>> 5;
            for (int index = 0; index < Count(); index++)
            {
                int row = start + 0x14 + index * 32;
                int actor = Actor(row);
                NativeBattleGraphicsModel model = graphics.Models[actor];
                NativeBattleModelTransform transform = RequireTransform(model);
                phase = unchecked(phase - 256);
                if (phase < 0)
                {
                    continue;
                }
                Store(row + 0x16, Word(row + 0x16) + ((index & 1) != 0 ? 512 : -512));
                Store(row + 0x14, Word(row + 0x14) + ((index & 1) != 0 ? elapsed << 3 : -(elapsed << 3)));
                if (phase >= 3072)
                {
                    NativeBattleVector position = transform.Position28 with { Y = unchecked((short)(transform.Position28.Y - 512)) };
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Transform = transform with { Position28 = position } }) };
                    Store(row + 0xA, 4096);
                    continue;
                }
                NativeBattleVector destination = new(unchecked((short)(Word(row + 0xC) >> 1)), Word(row + 0xE), Word(row + 0x10));
                NativeBattleVector initial = origin with { Y = unchecked((short)(origin.Y - ((Word(row + 4) + Word(row + 6)) / 2))) };
                NativeBattleVector moved = NativeBattleQuezacotlRibbonMath.Blend(initial, destination, phase, unchecked(4096 - phase));
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Transform = transform with { Position28 = moved } }) };
                phase = NativeBattleCameraMath.Cosine(phase >> 2);
                int product = unchecked(Word(row + 8) * unchecked(4096 - phase));
                arena = arena.Write(row + 0x1D, [1]);
                Store(row + 0xA, unchecked(product / 4096 + phase));
            }
        }
        if (age is >= 377 and < 417)
        {
            int elapsed = age - 377;
            if (elapsed == 0)
            {
                for (int index = 0; index < Count(); index++)
                {
                    int row = start + 0x14 + index * 32;
                    int actor = Actor(row);
                    NativeBattleGraphicsModel model = graphics.Models[actor];
                    NativeBattleModelTransform transform = RequireTransform(model);
                    NativeBattleVector position = new(Word(row + 0xC), Word(row + 0xE), Word(row + 0x10));
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Transform = transform with { Position28 = position, Vertical34 = Word(row + 0x12) } }) };
                    Store(row + 0x14, model.Rotation12 ?? throw new NotSupportedException("Native Pandemona restoration requires the current actor rotation X."));
                    Store(row + 0x16, model.Rotation14 ?? throw new NotSupportedException("Native Pandemona restoration requires the current actor rotation Y."));
                    Store(row + 0x18, model.Rotation16 ?? throw new NotSupportedException("Native Pandemona restoration requires the current actor rotation Z."));
                    if (model.RotationPadding18 is ushort padding)
                    {
                        arena = arena.Write(row + 0x1A, BitConverter.GetBytes(padding));
                    }
                    else
                    {
                        arena = arena.MarkUnknown(row + 0x1A, 2);
                    }
                    Store(row + 0x18, (index & 1) != 0 ? 256 : -256);
                }
            }
            if (!paused)
            {
                for (int index = 0; index < Count(); index++)
                {
                    int row = start + 0x14 + index * 32;
                    int actor = Actor(row);
                    NativeBattleGraphicsModel model = graphics.Models[actor];
                    NativeBattleModelTransform transform = RequireTransform(model);
                    Store(row + 0x16, Word(row + 0x16) + ((index & 1) != 0 ? 256 : -256));
                    int multiplier = (model.ModelId4 ?? throw new NotSupportedException("Native Pandemona restoration requires its current model ID.")) == 142 ? 480 : 350;
                    short y = unchecked((short)(unchecked(-elapsed * multiplier) - (index << 11)));
                    short doubled = unchecked((short)(y << 1));
                    short radial = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(trigonometry.AsSpan().Slice((doubled & 0xFFE) * 2, 2)) >> 2));
                    int angle = unchecked(doubled + (elapsed << 7)) & 0xFFF;
                    short x = unchecked((short)(unchecked(Trig(angle, false) * radial) >> 12));
                    short z = unchecked((short)(ground + (unchecked(Trig(angle, true) * radial) >> 12)));
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Transform = transform with { Position28 = new(x, y, z) } }) };
                }
            }
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        if (drawOnly && age >= 224)
        {
            for (int index = 0; index < Count(); index++)
            {
                int row = start + 0x14 + index * 32;
                if (arena.Read(row + 0x1C, 1)[0] != 0)
                {
                    continue;
                }
                int actor = Actor(row);
                NativeBattleGraphicsModel model = graphics.Models[actor];
                if (arena.Read(row + 0x1D, 1)[0] != 0 && (model.Flags0 & 2) != 0)
                {
                    short center = unchecked((short)((Word(row + 4) + Word(row + 6)) >> 1));
                    short factor = Word(row + 0xA);
                    NativeBattleVector angles = new(Word(row + 0x14), Word(row + 0x16), Word(row + 0x18));
                    NativeBattleVector position = RequireTransform(model).Position28;
                    NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native Pandemona target draw requires its current world matrix.");
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { GeometryWorldMatrix = world with { X = position.X, Y = position.Y, Z = position.Z } }) };
                    IEnumerable<int> rows = arena.Read(row + 0x1E, 1)[0] != 0 ? NativeBattleModelRings.Walk(graphics.Models, actor) : new[] { actor };
                    foreach (int selected in rows)
                    {
                        if ((graphics.Models[selected].Flags0 & 2) != 0 || selected == actor)
                        {
                            var drawn = DrawNativePandemona291Actor(frame, selected, angles, new(0, center, 0), new(factor, factor, factor), graphics);
                            frame = drawn.Frame;
                            graphics = drawn.Graphics;
                        }
                    }
                }
                bool ring = arena.Read(row + 0x1E, 1)[0] != 0;
                IEnumerable<int> hidden = ring ? NativeBattleModelRings.Walk(graphics.Models, actor) : new[] { actor };
                foreach (int selected in hidden)
                {
                    NativeBattleGraphicsModel target = graphics.Models[selected];
                    if (!ring || (target.Flags0 & 2) != 0)
                    {
                        graphics = graphics with { Models = graphics.Models.SetItem(selected, target with { Flags0 = (ushort)(target.Flags0 | 4) }) };
                    }
                }
            }
        }
        return (frame, graphics);

        int Count() => nativeActionPackets.Headers[frame.Effect.Parameters!.HeaderIndex7C].ReadByte(0x10);
        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
        void Store(int offset, int value) => arena = arena.Write(offset, BitConverter.GetBytes(unchecked((ushort)value)));
        int Actor(int row)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(row, 4));
            uint relative = unchecked(pointer - 0x1D972C0);
            if (relative % 156 != 0 || relative / 156 >= graphics.Models.Length)
            {
                throw new NotSupportedException("Native Pandemona target record requires its actual physical actor pointer.");
            }
            return checked((int)(relative / 156));
        }
        static NativeBattleModelTransform RequireTransform(NativeBattleGraphicsModel model) => model.Transform ?? throw new NotSupportedException("Native Pandemona target motion requires its retained signed position.");
        short Trig(int angle, bool cosine) => BinaryPrimitives.ReadInt16LittleEndian(trigonometry.AsSpan().Slice((angle & 0xFFF) * 4 + (cosine ? 2 : 0), 2));
    }
}
