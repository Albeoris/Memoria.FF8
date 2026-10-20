using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed partial class NativeUltima149Runner
    {
        private NativeBattleUltima149Particle Particle(bool a, int index) => new((a ? state.ParticlesA : state.ParticlesB).Slice(index * 24, 24));
        private void Particle(bool a, int index, NativeBattleUltima149Particle particle)
        {
            byte[] bytes = (a ? state.ParticlesA : state.ParticlesB).ToArray();
            particle.Bytes.AsSpan().CopyTo(bytes.AsSpan(index * 24, 24));
            state = a ? state with { ParticlesA = bytes.ToImmutableArray() } : state with { ParticlesB = bytes.ToImmutableArray() };
        }
        private int Free(bool a)
        {
            for (int index = 0; index < 100; index++)
            {
                if (Particle(a, index).Type == 0)
                {
                    return index;
                }
            }
            return -1;
        }
        private NativeBattleVector Normalize(int x, int y, int z)
        {
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
            return new(unchecked((short)direction.X), unchecked((short)direction.Y), unchecked((short)direction.Z));
        }
        private NativeBattleActionMatrix Align(NativeBattleVector first, NativeBattleVector second)
        {
            NativeBattleModuleAxisAngle angle = NativeBattleModuleAlignment.Between571480(first.X, first.Y, first.Z, second.X, second.Y, second.Z);
            return new(NativeBattleModuleAlignment.Rotation5714F0(angle.Angle, angle.Axis), 0, 0, 0, 4096);
        }
        private static NativeBattleActionMatrix Place(NativeBattleActionMatrix matrix, NativeBattleVector point) => matrix with { X = point.X, Y = point.Y, Z = point.Z };
        private uint BeginSprite(NativeBattleModuleResource resource, ushort flags)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(owner.NativeFileStaging ?? throw new NotSupportedException("Native Ultima sprites require their retained temporary memory."), 180);
            owner.NativeFileStaging = NativeBattleModuleSpriteTemporary.Begin571C80(allocation.Staging, allocation.Address, resource.Address, flags);
            if (flags == 32)
            {
                owner.NativeFileStaging = owner.NativeFileStaging.Write(allocation.Address + 33, [0]);
            }
            return allocation.Address;
        }
        private NativeBattleModuleSpriteFrame Sprite(uint address, NativeBattleModuleResource resource, int frame, NativeBattleVector position, int scale, ushort flags)
        {
            NativeBattleModuleSpriteFrame selected = resource.ProjectedFrame571C80(frame);
            ImmutableArray<byte> palettes = flags == 32 ? resource.Bytes.Slice(4, 4).SetItem(1, 0) : [];
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = selected, Phase = unchecked((ushort)frame), Anchor = new(position.X, position.Y, position.Z, 0), Scale = scale, DepthOffset = -(scale >> 4), SpriteFlags = flags, SpritePaletteOffsets = palettes };
            NativeBattleModuleSpriteTemporaryResult rendered = NativeBattleModuleSpriteTemporary.Emit571C80(owner.NativeFileStaging!, address, draw, Projection, owner.NativeCamera!.Roll, 17, 2);
            owner.NativeFileStaging = rendered.Staging;
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(Memory, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, rendered.Batch, NativeBattleEffectGouraudWrites.FullDword);
            Memory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            return selected;
        }
        private void EndSprite() => owner.NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(owner.NativeFileStaging!, 180);

        private int Orbit(ref NativeBattleModulePoolTask task)
        {
            int alive = 0;
            uint context = BeginSprite(resources.Orbit, 0);
            NativeBattleVector center = Position(task);
            for (int index = 0; index < 100; index++)
            {
                NativeBattleUltima149Particle particle = Particle(false, index);
                if ((particle.Type & 1) == 0)
                {
                    continue;
                }
                int angle = particle.Velocity.X;
                NativeBattleVector point = particle.Position;
                NativeBattleVector position = new(unchecked((short)((unchecked(NativeBattleCameraMath.Cosine(angle) * point.X) >> 12) + center.X)), unchecked((short)((unchecked(NativeBattleCameraMath.Sine(2 * angle) * point.Z) >> 12) + point.Y)), unchecked((short)((unchecked(NativeBattleCameraMath.Sine(angle) * point.X) >> 12) + center.Z)));
                NativeBattleModuleSpriteFrame selected = Sprite(context, resources.Orbit, particle.Frame / 3, position, particle.Scale, 0);
                if (!DrawOnly)
                {
                    particle = particle.Word(4, particle.Frame + 1);
                    if (selected.NextOffset < 0)
                    {
                        particle = particle.SetType(0);
                    }
                    else
                    {
                        particle = NativeBattleUltima149Motion.Move(particle, 1);
                        alive++;
                    }
                    Particle(false, index, particle);
                }
            }
            EndSprite();
            if (DrawOnly)
            {
                return 0;
            }
            int age = Word(task, 12);
            if (age is >= 0 and <= 20)
            {
                for (int birth = 0; birth < 2; birth++)
                {
                    int slot = Free(false);
                    if (slot < 0)
                    {
                        break;
                    }
                    NativeBattleUltima149Particle particle = Particle(false, slot).SetType(1).Word(4, 0).Word(6, Random() % 1280 + 768).Word(8, Random() % 5000 + 900).Word(10, -300 - Random() % 2000).Word(12, Random() % 300 + 30).Word(16, Random() % 4096).Word(18, 0).Word(20, Random() % 30 + 10);
                    Particle(false, slot, particle);
                }
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 >= 24 && alive == 0 ? 2 : 0;
        }

        private int Streaks(ref NativeBattleModulePoolTask task)
        {
            int alive = 0;
            uint flags = 0x33;
            int? fade = null;
            for (int index = 0; index < 100; index++)
            {
                NativeBattleUltima149Particle particle = Particle(false, index);
                if ((particle.Type & 2) == 0)
                {
                    continue;
                }
                NativeBattleVector direction = Normalize(particle.Velocity.X, particle.Velocity.Y, particle.Velocity.Z);
                NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(Place(Align(new(0, -4096, 0), direction), particle.Position), 4096, particle.Scale, 4096);
                if (particle.Frame >= 4)
                {
                    flags |= 0xC0;
                    fade = (particle.Frame - 4) << 10;
                }
                Mesh(resources.Streak, NativeBattleGfMatrices.Compose56C2F0(View, matrix), flags, 0, fade);
                if (!DrawOnly)
                {
                    particle = particle.Word(4, particle.Frame + 1);
                    if (particle.Frame < 8)
                    {
                        particle = NativeBattleUltima149Motion.Move(particle, 2);
                        alive++;
                    }
                    else
                    {
                        particle = particle.SetType(0);
                    }
                    Particle(false, index, particle);
                }
            }
            if (DrawOnly)
            {
                return 0;
            }
            int age = Word(task, 12);
            if (age is >= 0 and <= 70)
            {
                for (int birth = 0; birth < age / 16 + 1; birth++)
                {
                    int slot = Free(false);
                    if (slot < 0)
                    {
                        break;
                    }
                    NativeBattleUltima149Particle particle = Particle(false, slot).SetType(2).Word(4, 0).Word(6, Random() % 3584 + 4096).SetVector(8, Position(task)).Word(14, Word(task, 22));
                    NativeBattleVector direction = Normalize(Random() % 4096 - 2048, Random() % 4096 - 2048, Random() % 4096 - 2048);
                    int radius = Random() % 5000 + 2000;
                    NativeBattleVector center = particle.Position;
                    particle = particle.SetVector(8, new(unchecked((short)(center.X + (unchecked(radius * direction.X) >> 12))), unchecked((short)(center.Y + (unchecked(radius * direction.Y) >> 12))), unchecked((short)(center.Z + (unchecked(radius * direction.Z) >> 12)))));
                    int speed = Random() % 210 + 120;
                    particle = particle.SetVector(16, new(unchecked((short)(unchecked(-unchecked(speed * direction.X)) >> 12)), unchecked((short)(unchecked(-unchecked(speed * direction.Y)) >> 12)), unchecked((short)(unchecked(-unchecked(speed * direction.Z)) >> 12))));
                    Particle(false, slot, particle);
                }
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 >= 8 && alive == 0 ? 2 : 0;
        }

        private int Shards(ref NativeBattleModulePoolTask task)
        {
            int alive = 0;
            NativeBattleVector center = Position(task);
            NativeBattleVector view = Normalize(resources.ReferencePoint.X - center.X, resources.ReferencePoint.Y - center.Y, resources.ReferencePoint.Z - center.Z);
            for (int index = 0; index < 100; index++)
            {
                NativeBattleUltima149Particle particle = Particle(false, index);
                if ((particle.Type & 4) == 0)
                {
                    continue;
                }
                NativeBattleActionMatrix rotation = new(NativeBattleModuleAlignment.Rotation5714F0(particle.Signed(14), view), 0, 0, 0, 4096);
                rotation = NativeBattleGfMatrices.Multiply56C090(rotation, Align(new(0, -4096, 0), particle.Position));
                NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(Place(rotation, center), particle.Scale, particle.Scale, particle.Scale);
                int frame = particle.Frame;
                int? fade = frame < 6 ? 4096 - frame * 682 : frame >= 10 ? (frame - 10) * 682 : null;
                Mesh(resources.Shard, NativeBattleGfMatrices.Compose56C2F0(View, matrix), fade is null ? 0x33u : 0xF3u, 0, fade);
                if (!DrawOnly)
                {
                    particle = particle.Word(4, frame + 1);
                    if (particle.Frame < 16)
                    {
                        particle = NativeBattleUltima149Motion.Move(particle, 4);
                        alive++;
                    }
                    else
                    {
                        particle = particle.SetType(0);
                    }
                    Particle(false, index, particle);
                }
            }
            if (DrawOnly)
            {
                return 0;
            }
            int age = Word(task, 12);
            if (age is >= 0 and <= 40)
            {
                int slot = Free(false);
                if (slot >= 0)
                {
                    NativeBattleUltima149Particle particle = Particle(false, slot).SetType(4).Word(4, 0).Word(6, Random() % 1024 + 1280);
                    NativeBattleVector direction = Normalize(Random() % 4096 - 2048, Random() % 4096 - 2048, Random() % 512 - 256);
                    particle = particle.SetVector(8, NativeBattleGfMatrices.RotateShort56C4F0(Align(new(0, 0, -4096), view), direction)).Word(14, Random() % 4096);
                    int spin = Random() % 30 + 10;
                    particle = particle.Word(22, (particle.Signed(14) & 1) != 0 ? -spin : spin);
                    Particle(false, slot, particle);
                }
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 >= 16 && alive == 0 ? 2 : 0;
        }

        private int Dust(ref NativeBattleModulePoolTask task) => ExpandingSprites(ref task, false);
        private int Sparks(ref NativeBattleModulePoolTask task) => ExpandingSprites(ref task, true);
        private int ExpandingSprites(ref NativeBattleModulePoolTask task, bool sparks)
        {
            int alive = 0;
            uint type = sparks ? 1u : 8u;
            NativeBattleModuleResource resource = sparks ? resources.Spark : resources.Dust;
            ushort flags = sparks ? (ushort)32 : (ushort)0;
            uint context = BeginSprite(resource, flags);
            for (int index = 0; index < 100; index++)
            {
                NativeBattleUltima149Particle particle = Particle(sparks, index);
                if ((particle.Type & type) == 0)
                {
                    continue;
                }
                NativeBattleModuleSpriteFrame selected = Sprite(context, resource, sparks ? particle.Frame << 1 : particle.Frame, particle.Position, particle.Scale, flags);
                if (!DrawOnly)
                {
                    particle = particle.Word(4, particle.Frame + 1);
                    bool finished = sparks ? particle.Frame >= 16 : selected.NextOffset < 0;
                    if (finished)
                    {
                        particle = particle.SetType(0);
                    }
                    else
                    {
                        particle = NativeBattleUltima149Motion.Move(particle, sparks ? 16u : 8u);
                        alive++;
                    }
                    Particle(sparks, index, particle);
                }
            }
            EndSprite();
            if (DrawOnly)
            {
                return 0;
            }
            int age = Word(task, 12);
            if (age >= 0 && age <= (sparks ? 51 : 50))
            {
                int radius = unchecked(state.RingScale + (state.RingScale << 10)) >> 12;
                int count = sparks ? 7 : age / 14 + 5;
                for (int birth = 0; birth < count; birth++)
                {
                    int slot = Free(sparks);
                    if (slot < 0)
                    {
                        break;
                    }
                    NativeBattleUltima149Particle particle = Particle(sparks, slot).SetType(type).Word(4, 0).Word(6, Random() % 2048 + (sparks ? unchecked((ushort)(age + 640)) << 2 : unchecked((ushort)(age + 64)) << 5));
                    int angle = Random() % 4096;
                    int cosine = NativeBattleCameraMath.Cosine(angle);
                    int sine = NativeBattleCameraMath.Sine(angle);
                    int reach = sparks ? Random() % 600 + radius - 150 : Random() % 400 + radius - 200;
                    NativeBattleVector center = Position(task);
                    particle = particle.SetVector(8, new(unchecked((short)(center.X + (unchecked(cosine * reach) >> 12))), center.Y, unchecked((short)(center.Z + (unchecked(sine * reach) >> 12))))).Word(14, Word(task, 22));
                    int speed = sparks ? Random() % 360 + 310 : Random() % 100 + 80;
                    short vx = unchecked((short)(unchecked(cosine * speed) >> 12));
                    short vy = unchecked((short)(sparks ? -160 - Random() % 170 : -50 - Random() % 80));
                    short vz = unchecked((short)(unchecked(sine * speed) >> 12));
                    particle = particle.SetVector(16, new(vx, vy, vz));
                    Particle(sparks, slot, particle);
                }
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 >= 16 && alive == 0 ? 2 : 0;
        }
    }
}
