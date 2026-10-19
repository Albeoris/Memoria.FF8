namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSandStorm176Controller
{
    private sealed partial class Execution
    {
        private int Sand(ref NativeBattleModulePoolTask task, bool landing)
        {
            int alive = 0;
            int mask = S(task, 14);
            for (uint particle = 0x23685B8; particle < 0x23693C8; particle += 24)
            {
                if ((D(particle) & unchecked((uint)mask)) == 0)
                {
                    continue;
                }
                int frame = W(particle + 4);
                NativeBattleModuleSpriteFrame sprite = resources.Sand.SpriteFrame(frame);
                bool ended = sprite.NextOffset < 0;
                {
                    short scale = W(particle + 6);
                    NativeBattleVector position = Position(particle + 8);
                    NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resources.Sand, SpriteFrame = sprite, Phase = unchecked((ushort)frame), Anchor = new(position.X, position.Y, position.Z, 0), Scale = unchecked((ushort)scale), Direction = 4096, DepthOffset = -(scale >> 4), SpriteFlags = 0 };
                    Emit(new NativeBattleSandStorm176Sprite(NativeBattleModulePackets.EmitSprite571DC0(draw, inputs.Projection, inputs.CameraRoll, 17, 2)));
                }
                if (inputs.DrawOnly)
                {
                    continue;
                }
                W(particle + 4, frame + 1);
                if (ended)
                {
                    D(particle, 0);
                    continue;
                }
                W(particle + 8, W(particle + 8) + W(particle + 16));
                W(particle + 10, W(particle + 10) + W(particle + 18));
                W(particle + 12, W(particle + 12) + W(particle + 20));
                W(particle + 16, W(particle + 16) - (W(particle + 16) >> 3));
                W(particle + 18, W(particle + 18) + (W(particle + 18) >> 4));
                W(particle + 20, W(particle + 20) - (W(particle + 20) >> 3));
                alive++;
            }
            if (inputs.DrawOnly)
            {
                return 0;
            }
            int age = S(task, 12);
            int births = landing ? age is >= 0 and <= 2 ? 6 : 0 : age is >= 0 and <= 80 ? (age >> 5) + 1 : 0;
            for (int birth = 0; birth < births; birth++)
            {
                uint particle = 0x23685B8;
                while (particle < 0x23693C8 && D(particle) != 0)
                {
                    particle += 24;
                }
                if (particle == 0x23693C8)
                {
                    break;
                }
                D(particle, unchecked((uint)mask));
                W(particle + 4, 0);
                W(particle + 6, landing ? Roll() % 1280 + 1024 : Roll() % 5120 + unchecked((short)(age + 768)) * 2);
                uint point = mask == 1 ? 0x23685A8u : 0x23685B0u;
                D(particle + 8, landing ? task.ReadDword(16) : D(point));
                D(particle + 12, landing ? task.ReadDword(20) : D(point + 4));
                int angle = Roll() % 4096;
                int sine = NativeBattleCameraMath.Sine(angle);
                int cosine = NativeBattleCameraMath.Cosine(angle);
                if (!landing)
                {
                    int radius = Roll() % 300 + W(point + 6);
                    W(particle + 8, W(particle + 8) + unchecked((short)(unchecked(cosine * radius) >> 12)));
                    W(particle + 12, W(particle + 12) + unchecked((short)(unchecked(sine * radius) >> 12)));
                }
                int speed = Roll() % 50 + 20;
                W(particle + 16, unchecked(cosine * speed) >> 12);
                if (!landing)
                {
                    W(particle + 18, -10 - Roll() % 10);
                }
                W(particle + 20, unchecked(sine * speed) >> 12);
            }
            if (landing && age == 2)
            {
                Emit(new NativeBattleSandStorm176Contact(FirstContact + S(task, 26)));
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 >= 4 && alive == 0 ? 2 : 0;
        }

        private ushort? Padding(uint address)
        {
            (byte[] bytes, bool[] known) = State.Module.ReadSnapshot(checked((int)(address - ModuleBase)), 2);
            return known.All(value => value) ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes) : null;
        }

        private int Lift(ref NativeBattleModulePoolTask task)
        {
            int delay = S(task, 30);
            if (delay > 0)
            {
                if (!inputs.DrawOnly)
                {
                    task = task.WriteWord(30, unchecked((ushort)(delay - 1)));
                }
                return 0;
            }
            int actor = S(task, 14);
            NativeBattleGraphicsModel model = inputs.ReadModel(actor);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Sand Storm lift requires its target's native position.");
            uint saved = 0x2374208u + (uint)(actor * 20);
            int age = S(task, 12);
            if (age == 0)
            {
                W(saved, model.Flags0 & 0x1020);
                W(saved + 4, model.Rotation12 ?? throw new NotSupportedException("Native Sand Storm requires the target's X rotation."));
                W(saved + 6, model.Rotation14 ?? throw new NotSupportedException("Native Sand Storm requires the target's Y rotation."));
                W(saved + 8, model.Rotation16 ?? throw new NotSupportedException("Native Sand Storm requires the target's Z rotation."));
                if (model.RotationPadding18 is ushort padding)
                {
                    W(saved + 10, padding);
                }
                else
                {
                    SetModule(State.Module.MarkUnknown(checked((int)(saved + 10 - ModuleBase)), 2));
                }
                W(saved + 12, transform.Position28.X);
                W(saved + 14, transform.Position28.Y);
                W(saved + 16, transform.Position28.Z);
                W(saved + 18);
            }
            if (age < 32)
            {
                short xAngle = unchecked((short)(S(task, 24) * age / 44));
                short yAngle = unchecked((short)((age << 12) / 32));
                short zAngle = model.Rotation16 ?? throw new NotSupportedException("Native Sand Storm requires its saved Z rotation.");
                NativeBattleVector origin = TaskPosition(task, 16);
                NativeBattleVector offset = new(unchecked((short)(origin.X - W(saved + 12))), unchecked((short)(origin.Y - W(saved + 14))), unchecked((short)(origin.Z - W(saved + 16))));
                NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleGfMatrices.Euler56CE30(new(xAngle, yAngle, zAngle)), offset);
                uint point = S(task, 28) != 0 ? 0x23685B0u : 0x23685A8u;
                NativeBattleVector position = new(unchecked((short)(origin.X - rotated.X + unchecked((short)(unchecked((W(point) - origin.X) * yAngle) >> 12)))), unchecked((short)(origin.Y - rotated.Y + S(task, 32))), unchecked((short)(origin.Z - rotated.Z + unchecked((short)(unchecked((W(point + 4) - origin.Z) * yAngle) >> 12)))));
                model = model with { Flags0 = (ushort)(model.Flags0 | 0x1020), Rotation12 = xAngle, Rotation14 = yAngle, Transform = transform with { Position28 = position } };
            }
            else if (age < 44)
            {
                if (age == 32)
                {
                    task = task.WriteWord(32, unchecked((ushort)(S(task, 32) / 8)));
                }
            }
            else
            {
                model = model with { Transform = transform with { Position28 = new(W(saved + 12), unchecked((short)(transform.Position28.Y - S(task, 32))), W(saved + 16)) } };
            }
            Emit(new NativeBattleSandStorm176Actor(actor, model));
            if (inputs.DrawOnly)
            {
                return 0;
            }
            if (age < 32)
            {
                int height = unchecked((short)(S(task, 32) + S(task, 34)));
                task = task.WriteWord(32, unchecked((ushort)Math.Max(height, -10000))).WriteWord(34, unchecked((ushort)(S(task, 34) / 12 + S(task, 34))));
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            if (age + 1 < 52)
            {
                return 0;
            }
            NativeBattleGraphicsModel restored = inputs.ReadModel(actor);
            NativeBattleModelTransform retained = restored.Transform ?? throw new NotSupportedException("Native Sand Storm lost its lifted target position.");
            restored = restored with { Flags0 = (ushort)(unchecked((ushort)W(saved)) | (restored.Flags0 & 0xEFDF)), Rotation12 = W(saved + 4), Rotation14 = W(saved + 6), Rotation16 = W(saved + 8), RotationPadding18 = Padding(saved + 10), Transform = retained with { Position28 = new(W(saved + 12), W(saved + 14), W(saved + 16)), Vertical34 = W(saved + 18) } };
            Emit(new NativeBattleSandStorm176Actor(actor, restored));
            int dust = Spawn(0x5D1820);
            Put(dust, children.Slots[dust].WriteWord(12, 0).WriteWord(14, unchecked((ushort)(0x100 << (S(task, 26) & 31)))).WriteDword(16, D(saved + 12)).WriteDword(20, D(saved + 16)).WriteWord(26, task.ReadWord(26)));
            Emit(new NativeBattleSandStorm176Sound(resources.LandingSound));
            return 2;
        }
    }
}
