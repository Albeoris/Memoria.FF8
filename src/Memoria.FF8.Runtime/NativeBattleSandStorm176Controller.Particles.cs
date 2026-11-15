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
                    NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resources.Sand, SpriteFrame = sprite, Phase = unchecked((ushort)frame), Anchor = new(position.X, position.Y, position.Z, 0), Scale = unchecked((ushort)scale), Direction = 4096, DepthOffset = -(scale >> 4), SpriteFlags = 0, PilotIdentity = inputs.CapturePresentation ? NativeBattleSandStorm176VisualLifetimes.Identity(task, checked((int)((particle - 0x23685B8) / 24)), landing, resources.Sand.Address) : null };
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
                if (inputs.CapturePresentation)
                {
                    NativeBattleSandStorm176VisualLifetimes.Allocated(task, checked((int)((particle - 0x23685B8) / 24)));
                }
            }
            if (landing && age == 2)
            {
                int offset = task.ReadExecutionReference<BattleExecutionReference.ContactOffset>(26, 2).Offset ?? throw new InvalidDataException("Sand Storm landing requires its contact offset.");
                BattleContactOccurrence occurrence = inputs.Packets.ReadContact(FirstContact.Advance(offset)).Occurrence ?? throw new InvalidDataException("Sand Storm contact has no occurrence reference.");
                Emit(new NativeBattleSandStorm176Contact(occurrence));
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 >= 4 && alive == 0 ? 2 : 0;
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
            BattleModelOwner actor = task.ReadExecutionReference<BattleExecutionReference.Model>(14, 2).Owner;
            NativeBattleGraphicsModel model = inputs.ReadModel(actor);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Sand Storm lift requires its target's native position.");
            int age = S(task, 12);
            if (age == 0)
            {
                short pitch = model.Rotation12 ?? throw new NotSupportedException("Native Sand Storm requires the target's X rotation.");
                short yaw = model.Rotation14 ?? throw new NotSupportedException("Native Sand Storm requires the target's Y rotation.");
                short roll = model.Rotation16 ?? throw new NotSupportedException("Native Sand Storm requires the target's Z rotation.");
                NativeBattleSavedActorTransform captured = new((ushort)(model.Flags0 & 0x1020), pitch, yaw, roll, model.RotationPadding18, transform.Position28, transform.Vertical34);
                current = current with { Effect = current.Effect with { SavedActorTransforms = current.Effect.SavedActorTransforms.SetItem(actor, captured) } };
            }
            if (age < 32)
            {
                short xAngle = unchecked((short)(S(task, 24) * age / 44));
                short yAngle = unchecked((short)((age << 12) / 32));
                short zAngle = model.Rotation16 ?? throw new NotSupportedException("Native Sand Storm requires its saved Z rotation.");
                NativeBattleVector origin = TaskPosition(task, 16);
                NativeBattleVector savedPosition = ReadSavedActor(actor).Position;
                NativeBattleVector offset = new(unchecked((short)(origin.X - savedPosition.X)), unchecked((short)(origin.Y - savedPosition.Y)), unchecked((short)(origin.Z - savedPosition.Z)));
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
                NativeBattleVector savedPosition = ReadSavedActor(actor).Position;
                model = model with { Transform = transform with { Position28 = new(savedPosition.X, unchecked((short)(transform.Position28.Y - S(task, 32))), savedPosition.Z) } };
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
            NativeBattleSavedActorTransform saved = ReadSavedActor(actor);
            restored = restored with { Flags0 = (ushort)(saved.Flags0 | (restored.Flags0 & 0xEFDF)), Rotation12 = saved.Pitch, Rotation14 = saved.Yaw, Rotation16 = saved.Roll, RotationPadding18 = saved.RotationPadding18, Transform = retained with { Position28 = saved.Position, Vertical34 = saved.Vertical34 } };
            Emit(new NativeBattleSandStorm176Actor(actor, restored));
            int dust = Spawn(0x5D1820);
            BattleExecutionReference.ContactOffset contactOffset = task.ReadExecutionReference<BattleExecutionReference.ContactOffset>(26, 2);
            int offset = contactOffset.Offset ?? throw new InvalidDataException("Sand Storm lift requires its contact offset.");
            NativeBattleModulePoolTask dustTask = children.Slots[dust].WriteWord(12, 0).WriteWord(14, unchecked((ushort)(0x100 << (offset & 31)))).WriteDword(16, PackWords(saved.Position.X, saved.Position.Y)).WriteDword(20, PackWords(saved.Position.Z, saved.Vertical34));
            dustTask.WriteExecutionReference(26, 2, contactOffset, contactOffset.EncodeOriginalWord());
            Put(dust, dustTask);
            Emit(new NativeBattleSandStorm176Sound(resources.LandingSound));
            return 2;
        }

        private NativeBattleSavedActorTransform ReadSavedActor(BattleModelOwner actor)
        {
            return current.Effect.SavedActorTransforms.TryGetValue(actor, out NativeBattleSavedActorTransform? saved) ? saved : throw new NotSupportedException("Native Sand Storm requires its saved actor transform.");
        }

        private static uint PackWords(short low, short high) => unchecked((ushort)low | ((uint)(ushort)high << 16));
    }
}
