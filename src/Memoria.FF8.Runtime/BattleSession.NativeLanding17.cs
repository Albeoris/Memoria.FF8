using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeLanding17(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleLanding17Resources resources = effect.Binding.Landing17Resources ?? throw new NotSupportedException("Native landing requires its original executable resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        (NativeBattleEffectTextureState textures, _) = memory.Textures!.EnqueueTim505E30(resources.Tim);
        int buffer = memory.Cursor;
        textures = textures.Register5099A0(resources.Camera);
        NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
        NativeEffectMemory = memory with { Textures = textures };
        int caster = effect.Parameters!.Actor78;
        NativeBattleGraphicsModel actor = graphics.Models[caster];
        actor = actor with { Flags0 = (ushort)(actor.Flags0 | 0x1000), Transform = actor.Transform! with { Position28 = actor.Transform!.Position28 with { Y = -5440 } } };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2556FE8, 0x2556FB0, 20, 1);
        (root, int slot) = root.Append(0x700460);
        root = root.WriteSlot(slot, root.Slots[slot].ClearPayload(8));
        return graphics with { Models = graphics.Models.SetItem(caster, actor), OriginalEffect = effect with { Pools = [root], Landing17 = new(buffer), ReturnedDescriptor = 0x2556FE8 } };
    }

    private NativeBattleGraphicsState AdvanceNativeLanding17(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        effect = effect with { PresentationPackets = [] };
        NativeBattleLanding17Resources resources = effect.Binding.Landing17Resources!;
        NativeBattleLanding17State state = effect.Landing17 ?? throw new NotSupportedException("Native landing requires its executed entry state.");
        NativeBattleEffectTaskPool roots = effect.Pools[0];
        NativeBattleModulePoolTask root = roots.Slots[0];
        bool started = root.ReadByte(15) != 0;
        NativeBattleEffectTaskPool? directors = started ? effect.Pools[1] : null;
        NativeBattleEffectTaskPool? particles = started ? effect.Pools[2] : null;
        int cursor = checked(state.BufferOffset + (root.ReadDword(16) != 0 ? 0x1BA4 : 0x11BA4));
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        int caster = effect.Parameters!.Actor78;
        UpdateActor(graphics.Models[caster] with { Flags0 = (ushort)(graphics.Models[caster].Flags0 | 0x1000) });
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native landing requires its shared CRT stream.");
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        if (root.ReadWord(12) == 1)
        {
            if (!started)
            {
                UpdateActor(graphics.Models[caster] with { Flags0 = (ushort)(graphics.Models[caster].Flags0 & 0xFFFB) });
                root = root.WriteByte(15, 1);
                started = true;
                directors = NativeBattleEffectTaskPool.Initialize(0x2556FD8, checked(0x20DFAB8u + (uint)state.BufferOffset + 0x1000), 0x1FC, 1);
                particles = NativeBattleEffectTaskPool.Initialize(0x2556FC8, checked(0x20DFAB8u + (uint)state.BufferOffset), 32, 128);
            }
            (directors, int director) = directors!.Append(0x7005C0);
            if (director >= 0)
            {
                NativeBattleModulePoolTask task = directors.Slots[director].ClearPayload(0x1F0);
                NativeVibrationEnqueue vibration = (NativeVibrationProcess ?? throw new NotSupportedException("Native landing requires its actual vibration process.")).Enqueue(resources.Vibration, 1, 128);
                NativeVibrationProcess = vibration.State;
                task = task.WriteDword(16, unchecked((uint)vibration.Handle));
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (8), 0x1E8);
                task = PublishWorkspace(task, 20, workspace.Initialize7016B0(resources.Animation, 0x1E8));
                directors = directors.WriteSlot(director, task);
            }
        }
        int remainingDirectors = 0;
        int remainingParticles = 0;
        if (started)
        {
            NativeBattleEffectTaskStep directorStep = directors!.Advance((pool, slot) =>
            {
                directors = pool;
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int result = Director(ref task);
                directors = directors.WriteSlot(slot, task);
                return new(directors, result);
            });
            directors = directorStep.Pool;
            remainingDirectors = directorStep.Survivors;
            NativeBattleEffectTaskStep particleStep = particles!.Advance((pool, slot) =>
            {
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int frame = task.ReadWord(12);
                int scale = unchecked((short)task.ReadWord(22));
                NativeBattleVector position = Vector(task, 16);
                NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resources.Spark, SpriteFrame = resources.Spark.SpriteFrame(frame), Anchor = new(position.X, position.Y, position.Z, 0), Scale = scale, DepthOffset = -(scale >> 2), SpriteFlags = 0, PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(17, task) with { ResourceGeneration = resources.Spark.Address } };
                NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, NativeCamera!.Roll, 17, 2);
                AppendSprite(batch);
                NativeBattleVector velocity = Vector(task, 24);
                NativeBattleVector moved = new(unchecked((short)(position.X + velocity.X)), unchecked((short)(position.Y + velocity.Y)), unchecked((short)(position.Z + velocity.Z)));
                task = PutVector(task, 16, moved).WriteWord(12, unchecked((ushort)(frame + 1)));
                return new(pool.WriteSlot(slot, task), frame + 1 >= 13 ? 2 : 0);
            });
            particles = particleStep.Pool;
            remainingParticles = particleStep.Survivors;
        }
        bool complete = started && remainingDirectors == 0 && remainingParticles == 0;
        if (complete)
        {
            UpdateActor(graphics.Models[caster] with { Flags0 = (ushort)(graphics.Models[caster].Flags0 & 0xEFFF) });
            roots = roots.Advance((pool, _) => new(pool, 2)).Pool;
        }
        else
        {
            root = root.WriteWord(12, unchecked((ushort)Math.Min(10, (int)unchecked((short)(root.ReadWord(12) + 1)))));
            roots = roots.WriteSlot(0, root);
        }
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = started ? [roots, directors!, particles!] : [roots], ReturnedDescriptor = complete ? 0u : 0x2556FE8u, DrawArenaOffset = cursor } };

        int Director(ref NativeBattleModulePoolTask task)
        {
            int frame = task.ReadWord(12);
            NativeBattleGraphicsModel actor = graphics.Models[caster];
            if ((frame is 20 or 40) && actor.ModelId4 is < 160)
            {
                uint sound = definition.NativeGraphics!.Rules.ActorSounds1120!.Value[actor.ModelId4.Value * 7];
                PublishNativeModuleSound(new(sound, 0, null) { Pan = 128 });
            }
            if (frame < 85)
            {
                if (frame <= 32)
                {
                    int weight = frame << 7;
                    uint color = actor.Color44!.Value & 0xFF000000;
                    for (int channel = 0; channel < 3; channel++)
                    {
                        int shift = channel * 8;
                        int value = unchecked(NativePrecision53.RoundInteger((byte)(resources.ShadeSource >> shift) * (weight / 4096d)) + NativePrecision53.RoundInteger((byte)(resources.ShadeDestination >> shift) * ((4096 - weight) / 4096d)));
                        color |= (uint)(byte)Math.Clamp(value, short.MinValue, short.MaxValue) << shift;
                    }
                    actor = actor with { Color44 = color };
                }
                actor = actor with { Transform = actor.Transform! with { Position28 = actor.Transform!.Position28 with { Y = unchecked((short)(actor.Transform.Position28.Y + 64)) } } };
                UpdateActor(actor);
            }
            if (frame is >= 80 and < 100)
            {
                int count = (NativeBattleCameraMath.Sine((frame - 80) * 4096 / 40) >> 9) + 4;
                int angle = Rand();
                int step = 4096 / count;
                for (int birth = 0; birth < count; birth++)
                {
                    int slot = SpawnParticle();
                    if (slot >= 0)
                    {
                        NativeBattleModulePoolTask spark = particles!.Slots[slot].ClearPayload(20);
                        short x = unchecked((short)(NativeBattleCameraMath.Cosine(angle) >> 2));
                        short z = unchecked((short)(NativeBattleCameraMath.Sine(angle) >> 2));
                        NativeBattleVector center = actor.Transform!.Position28;
                        spark = PutVector(spark, 16, new(unchecked((short)(x + center.X)), actor.Transform.Angle36, unchecked((short)(z + center.Z))));
                        int factor = Rand() & 0xFFF;
                        factor += 1024 - (factor >> 2);
                        spark = spark.WriteWord(24, unchecked((ushort)(x * factor >> 14))).WriteWord(28, unchecked((ushort)(z * factor >> 14))).WriteWord(22, unchecked((ushort)((Rand() & 0xFFF) + 0x800)));
                        particles = particles.WriteSlot(slot, spark);
                    }
                    angle += step;
                }
            }
            if (frame < 85)
            {
                int phase = frame % 25;
                NativeBattleVector anchorPoint = ReadNativeGraphicsBone(graphics, caster, 29, 1024);
                NativeBattleModuleAnchor anchor = new(anchorPoint.X, anchorPoint.Y, anchorPoint.Z, 0);
                if (phase == 0)
                {
                    PublishNativeModuleSound(resources.FirstSound, 0, anchor);
                }
                for (int birth = 0; birth < 2; birth++)
                {
                    if (unchecked(Rand() * 20) >> 15 <= phase)
                    {
                        continue;
                    }
                    int slot = SpawnParticle();
                    if (slot < 0)
                    {
                        continue;
                    }
                    NativeBattleModulePoolTask spark = particles!.Slots[slot].ClearPayload(20);
                    spark = PutVector(spark, 16, new(anchor.X, anchor.Y, anchor.Z));
                    spark = spark.WriteWord(22, unchecked((ushort)((Rand() & 0x3FF) + 0x800))).WriteWord(24, unchecked((ushort)((Rand() & 63) - 32))).WriteWord(26, unchecked((ushort)((Rand() & 63) - 32))).WriteWord(28, unchecked((ushort)((Rand() & 63) - 32)));
                    particles = particles.WriteSlot(slot, spark);
                }
            }
            if (frame == 85)
            {
                foreach (NativeBattleGraphicsModel party in graphics.Models.Take(3))
                {
                    if ((party.Flags0 & 2) != 0 && (party.Flags8!.Value & 3) == 0)
                    {
                        graphics = SelectNativeHeaderSequence(graphics, checked((byte)party.Index), 4);
                    }
                }
            }
            if (frame >= 50)
            {
                NativeBattleVector position = actor.Transform!.Position28;
                NativeBattleVector origin = new(position.X, actor.Transform.Angle36, position.Z);
                if (frame == 80)
                {
                    PublishNativeModuleSound(resources.SecondSound, 0, new(position.X, position.Y, position.Z, 0));
                }
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (8), 0x1E8);
                NativeBattlePilotVisualIdentity directorIdentity = NativeBattlePilotVisualLifetimes.Identity(17, task) with { ResourceGeneration = resources.Animation.Address };
                NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.Animation, 0, (current, node) =>
                {
                    DrawNode(node, origin, directorIdentity with { Primitive = node.NodeIndex });
                    return current;
                });
                task = PublishWorkspace(task, 20, advanced.Workspace);
            }
            if (frame == 118)
            {
                NativeVibrationProcess = NativeVibrationProcess!.Stop4A2940(unchecked((int)task.ReadDword(16)));
            }
            if (frame >= 122)
            {
                return 2;
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return 0;
        }

        void DrawNode(NativeBattleGfAnimationNode node, NativeBattleVector origin, NativeBattlePilotVisualIdentity visualIdentity)
        {
            if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
            {
                return;
            }
            ImmutableArray<byte> data = resources.Animation.Data;
            int geometry = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(8 + node.GeometryIndex * 4));
            int count = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(geometry + 4));
            ImmutableArray<byte> vertices;
            if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
            {
                int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
                vertices = data.Slice(checked(geometry + 12 + shape * count * 8), checked(count * 8));
            }
            else
            {
                NativeBattleEffectMemory memory = NativeEffectMemory!;
                int scratch = checked(state.BufferOffset + 0x1B9C);
                ImmutableArray<byte> destination = memory.Arena!.Read(scratch, checked(count * 8)).ToImmutableArray();
                vertices = NativeBattleGfMorphGeometry.Morph701390(data, geometry, node.Word40, node.Word42, node.Scalar38, destination);
                NativeEffectMemory = memory with { Arena = memory.Arena.Write(scratch, vertices.AsSpan()) };
            }
            uint flags = resources.NodeFlags[node.NodeIndex];
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler701310(node.Vector16) with { X = unchecked(origin.X + node.Vector8.X), Y = unchecked(origin.Y + node.Vector8.Y), Z = unchecked(origin.Z + node.Vector8.Z) };
            if ((flags & 0x10000000) != 0)
            {
                (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(view, matrix.X, matrix.Y, matrix.Z);
                matrix = matrix with { X = unchecked(x + view.X), Y = unchecked(y + view.Y), Z = unchecked(z + view.Z) };
            }
            else
            {
                matrix = NativeBattleGfMatrices.Compose56C2F0(view, matrix);
            }
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, node.Vector24.X, node.Vector24.Y, node.Vector24.Z);
            flags = (flags & 0xDFFF) | 0x2000;
            if (node.Scalar36 != 0)
            {
                flags |= 0xC0;
            }
            Append(NativeBattleModulePackets.EmitBatch572200(data, geometry, vertices, matrix, projection, new(flags, node.Scalar36 != 0 ? node.Color32 : null, node.Scalar36, null, null, null, 17, 2), colors, pilotIdentity: visualIdentity));
        }

        void Append(NativeBattleGfGeometryBatch batch)
        {
            effect = effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(effect.PresentationPackets, "20DFAB8", 0, cursor, batch) };
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, cursor, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = written.Memory;
            cursor = written.Cursor;
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }

        void AppendSprite(NativeBattleModulePacketBatch batch)
        {
            effect = effect with { PresentationPackets = effect.PresentationPackets.AddRange(batch.Primitives.Select(item => new NativeBattlePrimitiveReference("20DFAB8", 0, checked(cursor + item.ByteOffset)))) };
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, cursor, graphics.PrimitiveFrame!.Pending, batch, NativeBattleEffectGouraudWrites.FullDword);
            NativeEffectMemory = written.Memory;
            cursor = written.Cursor;
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }

        int SpawnParticle()
        {
            if (particles!.Slots.All(task => (task.Flags & 1) != 0))
            {
                return -1;
            }
            (particles, int slot) = particles.Append(0x700B70);
            return slot;
        }

        int Rand()
        {
            random = random.Draw(out ushort value);
            return value;
        }

        void UpdateActor(NativeBattleGraphicsModel actor) => graphics = graphics with { Models = graphics.Models.SetItem(caster, actor) };
        static NativeBattleVector Vector(NativeBattleModulePoolTask task, int offset) => new(unchecked((short)task.ReadWord(offset)), unchecked((short)task.ReadWord(offset + 2)), unchecked((short)task.ReadWord(offset + 4)));
        static NativeBattleModulePoolTask PutVector(NativeBattleModulePoolTask task, int offset, NativeBattleVector value) => task.WriteWord(offset, unchecked((ushort)value.X)).WriteWord(offset + 2, unchecked((ushort)value.Y)).WriteWord(offset + 4, unchecked((ushort)value.Z));
    }
}
