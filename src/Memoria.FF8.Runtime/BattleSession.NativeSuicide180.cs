using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeSuicide180(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Suicide requires its selected original effect.");
        NativeBattleSuicide180Resources resources = effect.Binding.Suicide180Resources ?? throw new NotSupportedException("Native Suicide requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Suicide requires its process memory.");
        NativeBattleSuicide180State state = memory.Suicide180 ?? throw new NotSupportedException("Native Suicide requires its retained particle bank.");
        int buffer = memory.Cursor;
        if (state.Particles.Length != 2400 || buffer < 0 || buffer > NativeBattleEffectArena.Length - 0x18000)
        {
            throw new InvalidDataException("Native Suicide particle storage or entry packet base is outside its original allocation.");
        }
        byte[] particles = state.Particles.ToArray();
        for (int particle = 0; particle < 100; particle++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(particles.AsSpan(particle * 24), 0);
        }
        NativeBattleEffectTextureState textures = (memory.Textures ?? throw new NotSupportedException("Native Suicide requires its texture queue.")).Register5099A0(resources.Camera);
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Suicide requires its camera owner.")).RegisterControlResource(textures.Animation);
        NativeBattleEffectInitializerAsset texture = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Suicide requires its loaded TIM.");
        (textures, _) = textures.EnqueueTim505E30(new(texture.Name, texture.Data));
        NativeEffectMemory = memory with { Textures = textures, Suicide180 = state with { BufferOffset = buffer, Particles = particles.ToImmutableArray() } };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2307DB0, 0x2307DA0, 16, 1);
        (root, int rootSlot) = root.Append(0x5CDED0);
        root = root with { Slots = root.Slots.SetItem(rootSlot, root.Slots[rootSlot].WriteWord(12, 0)) };
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x2308720, 0x2308730, 36, 100);
        (children, int director) = children.Append(0x5CC900);
        children = children with { Slots = children.Slots.SetItem(director, children.Slots[director].WriteWord(12, 0)) };
        return graphics with { OriginalEffect = effect with { Pools = [root, children], ReturnedDescriptor = root.DescriptorAddress, DrawArenaOffset = buffer } };
    }

    private NativeBattleGraphicsState AdvanceNativeSuicide180(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Suicide requires its selected original effect.");
        NativeBattleSuicide180Resources resources = effect.Binding.Suicide180Resources ?? throw new NotSupportedException("Native Suicide requires its original resources.");
        NativeBattleSuicide180State state = NativeEffectMemory?.Suicide180 ?? throw new NotSupportedException("Native Suicide requires its retained particle bank.");
        byte[] particles = state.Particles.ToArray();
        NativeBattleEffectTaskPool roots = effect.Pools[0];
        NativeBattleEffectTaskPool children = effect.Pools[1];
        NativeBattleModulePoolTask root = roots.Slots[0];
        int cursor = checked(state.BufferOffset + (root.ReadWord(12) & 1) * 0xC000);
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Suicide requires its camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Suicide requires its camera projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Suicide requires its published camera matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Suicide requires its interpolation constants.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Suicide requires its shared CRT stream.");
        bool paused = (graphics.Flags & 0x201) != 0;
        int actor = effect.Parameters!.Actor78;
        NativeBattleEffectTaskStep advanced = children.Advance((pool, slot) =>
        {
            children = pool;
            NativeBattleModulePoolTask task = pool.Slots[slot];
            int result = task.Callback switch
            {
                0x5CC900 => Director(ref task),
                0x5CCB60 => Glow(ref task),
                0x5CD1C0 => Particles(ref task, 1),
                0x5CD560 => Ring(ref task, false),
                0x5CD6C0 => Ring(ref task, true),
                0x5CD800 => Particles(ref task, 2),
                0x5CDAC0 => Particles(ref task, 4),
                0x5CDC80 => Particles(ref task, 8),
                _ => throw new NotSupportedException("Native Suicide selected an unrelated private callback.")
            };
            task = task with { Next = children.Slots[slot].Next };
            children = children with { Slots = children.Slots.SetItem(slot, task) };
            return new(children, result);
        });
        children = advanced.Pool;
        root = root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)));
        roots = roots with { Slots = roots.Slots.SetItem(0, root) };
        if (advanced.Survivors == 0)
        {
            roots = roots.Advance((pool, _) => new(pool, 2)).Pool;
        }
        NativeEffectMemory = NativeEffectMemory! with { Suicide180 = state with { Particles = particles.ToImmutableArray() } };
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = [roots, children], ReturnedDescriptor = advanced.Survivors == 0 ? 0u : 0x2307DB0u, DrawArenaOffset = cursor } };

        int Director(ref NativeBattleModulePoolTask task)
        {
            if (paused)
            {
                return 0;
            }
            int age = Word(task, 12);
            if (age == 16)
            {
                Spawn(0x5CCB60);
            }
            if (age == 15)
            {
                NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
                task = task.WriteWord(16, unchecked((ushort)anchor.X)).WriteWord(18, unchecked((ushort)anchor.Y)).WriteWord(20, unchecked((ushort)anchor.Z)).WriteWord(22, unchecked((ushort)anchor.W));
                SpawnAt(0x5CD1C0, task);
            }
            if (age == 28)
            {
                int first = SpawnAt(0x5CD560, task);
                NativeBattleModulePoolTask ring = children.Slots[first].WriteWord(14, 0).WriteWord(24, (ushort)(Roll() % 4096));
                int speed = Roll() % 110 + 60;
                PutTask(first, ring.WriteWord(26, (ushort)speed).WriteWord(28, 460).WriteWord(30, 460));
                int second = SpawnAt(0x5CD560, task);
                ring = children.Slots[second].WriteWord(14, 0).WriteWord(24, (ushort)(Roll() % 4096));
                speed = -80 - Roll() % 130;
                PutTask(second, ring.WriteWord(26, unchecked((ushort)speed)).WriteWord(28, 870).WriteWord(30, 870));
            }
            if (age == 27)
            {
                int ring = SpawnAt(0x5CD6C0, task);
                PutTask(ring, children.Slots[ring].WriteWord(24, (ushort)(Roll() % 4096)).WriteWord(28, 1408).WriteWord(30, 1408));
                SpawnAt(0x5CD800, task);
            }
            if (age == 28)
            {
                SpawnAt(0x5CDAC0, task);
            }
            if (age == 1)
            {
                Spawn(0x5CDC80);
            }
            if (age == 38)
            {
                NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters.HeaderIndex7C];
                int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Suicide requires its original first contact.");
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(first)] });
                graphics = graphics with { PendingContacts = pending };
            }
            if (age == 1)
            {
                PublishNativeModuleSound(new(resources.Sound, 0, null) { Pan = 128 });
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 > 50 ? 2 : 0;
        }

        int Glow(ref NativeBattleModulePoolTask task)
        {
            int age = Word(task, 12);
            if (age < 24)
            {
                uint color = colors.Interpolate(0, NativeBattleSuicide180.GlowLevel(age), 0xC0C0C0);
                graphics = graphics with { Models = graphics.Models.SetItem(actor, graphics.Models[actor] with { Flags0 = (ushort)(graphics.Models[actor].Flags0 | 4) }) };
                graphics = DrawNativeBombActor(graphics, actor, color, 0, false);
            }
            if (paused)
            {
                return 0;
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            if (age + 1 <= 24)
            {
                return 0;
            }
            graphics = graphics with { Models = graphics.Models.SetItem(actor, graphics.Models[actor] with { Flags0 = (ushort)(graphics.Models[actor].Flags0 | 0x10) }) };
            return 2;
        }

        int Ring(ref NativeBattleModulePoolTask task, bool second)
        {
            int age = Word(task, 12);
            short scale = Word(task, 28);
            NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, Word(task, 24), 0)), scale, scale, scale) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
            int fadeStart = second ? 10 : 8;
            NativeBattleStaticMeshResource resource = second ? resources.SecondRing : resources.Ring;
            DrawMesh(resource, NativeBattleGfMatrices.Compose56C2F0(view, local), age >= fadeStart ? 0xF3u : 0x33u, age >= fadeStart ? (age - fadeStart) << 9 : 0);
            if (paused)
            {
                return 0;
            }
            task = NativeBattleSuicide180.AdvanceRing(task, second);
            return Word(task, 12) >= (second ? 18 : 16) ? 2 : 0;
        }

        void DrawMesh(NativeBattleStaticMeshResource resource, NativeBattleActionMatrix matrix, uint flags, int fade)
        {
            NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(resource.Bytes, 0, default, matrix, projection, new(flags, 0, fade, null, null, null, 17, 2), colors);
            Append(batch);
        }
        void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, cursor, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = written.Memory;
            cursor = written.Cursor;
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
        int Spawn(uint callback)
        {
            (children, int slot) = children.Append(callback);
            PutTask(slot, children.Slots[slot].WriteWord(12, 0));
            return slot;
        }
        int SpawnAt(uint callback, NativeBattleModulePoolTask parent)
        {
            int slot = Spawn(callback);
            PutTask(slot, children.Slots[slot].WriteDword(16, parent.ReadDword(16)).WriteDword(20, parent.ReadDword(20)));
            return slot;
        }
        void PutTask(int slot, NativeBattleModulePoolTask task) => children = children with { Slots = children.Slots.SetItem(slot, task with { Next = children.Slots[slot].Next }) };
        ushort Roll()
        {
            random = random.Draw(out ushort value);
            return value;
        }
        static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
        short Read(int offset) => BinaryPrimitives.ReadInt16LittleEndian(particles.AsSpan(offset));
        uint Read32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(particles.AsSpan(offset));
        void Put(int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(particles.AsSpan(offset), unchecked((short)value));
        void Put32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(particles.AsSpan(offset), value);

        int Particles(ref NativeBattleModulePoolTask task, int kind)
        {
            int alive = 0;
            uint explosionFlags = 0x33;
            int explosionFade = 0;
            for (int particle = 0; particle < 100; particle++)
            {
                int offset = particle * 24;
                if ((Read32(offset) & (uint)kind) == 0)
                {
                    continue;
                }
                int frame = Read(offset + 4);
                bool ended;
                if (kind == 1)
                {
                    short vx = Read(offset + 16);
                    short vy = Read(offset + 18);
                    short vz = Read(offset + 20);
                    NativeBattleActionMatrix matrix = new(NativeBattleModuleAlignment.DownToVelocity573C40(vx, vy, vz), Read(offset + 8), Read(offset + 10), Read(offset + 12), 4096);
                    matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, 4096, Read(offset + 6), 4096);
                    if (frame >= 2)
                    {
                        explosionFlags |= 0xC0;
                        explosionFade = unchecked((frame - 2) << 10);
                    }
                    DrawMesh(resources.Explosion, NativeBattleGfMatrices.Compose56C2F0(view, matrix), explosionFlags, explosionFade);
                    ended = frame + 1 >= 6;
                }
                else
                {
                    NativeBattleModuleResource resource = kind == 4 ? resources.Smoke : resources.Spark;
                    NativeBattleModuleSpriteFrame sprite = resource.SpriteFrame(frame);
                    short scale = Read(offset + 6);
                    NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = sprite, Phase = unchecked((ushort)frame), Anchor = new(Read(offset + 8), Read(offset + 10), Read(offset + 12), 0), Scale = unchecked((ushort)scale), DepthOffset = -(scale >> 4), Direction = kind == 8 ? Read(offset + 16) : 4096, SpriteYScale = kind == 8 ? Read(offset + 18) : null, SpriteFlags = kind == 8 ? (ushort)2 : (ushort)0 };
                    Append(NativeBattleMeltdownPacketConversion.FromSprite(NativeBattleModulePackets.EmitSprite571DC0(draw, projection, camera.Roll, 17, 2)));
                    ended = sprite.NextOffset < 0;
                }
                if (paused)
                {
                    continue;
                }
                Put(offset + 4, frame + 1);
                if (ended)
                {
                    Put32(offset, 0);
                    continue;
                }
                if (kind is 1 or 2)
                {
                    if (kind == 1)
                    {
                        short scale = Read(offset + 6);
                        Put(offset + 6, scale - (scale >> 4));
                    }
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int position = offset + 8 + axis * 2;
                        int velocity = offset + 16 + axis * 2;
                        Put(position, Read(position) + Read(velocity));
                        Put(velocity, Read(velocity) - (Read(velocity) >> 2));
                    }
                }
                alive++;
            }
            if (paused)
            {
                return 0;
            }
            int age = Word(task, 12);
            int lastBirth = kind switch { 1 => 4, 2 => 7, 4 => 13, 8 => 12, _ => throw new InvalidDataException() };
            int births = kind switch { 1 => 6, 2 => 7, 4 => 2, 8 => 3, _ => throw new InvalidDataException() };
            if (age >= 0 && age <= lastBirth)
            {
                for (int birth = 0; birth < births; birth++)
                {
                    int offset = -1;
                    for (int particle = 0; particle < 100; particle++)
                    {
                        if (Read32(particle * 24) == 0)
                        {
                            offset = particle * 24;
                            break;
                        }
                    }
                    if (offset < 0)
                    {
                        break;
                    }
                    Put32(offset, (uint)kind);
                    Put(offset + 4, 0);
                    if (kind is 1 or 2)
                    {
                        Put(offset + 6, kind == 1 ? Roll() % 1536 + 3584 : Roll() % 4096 + 2048);
                        CopyPosition(offset, task);
                        int x = Roll() % 4096 - 2048;
                        int y = Roll() % 4096 - 2048;
                        int z = Roll() % 4096 - 2048;
                        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                        int distance = kind == 1 ? Roll() % 2000 + 1200 : Roll() % 600 + 400;
                        Put(offset + 8, Read(offset + 8) + (unchecked(distance * direction.X) >> 12));
                        Put(offset + 10, Read(offset + 10) + (unchecked(distance * direction.Y) >> 12));
                        Put(offset + 12, Read(offset + 12) + (unchecked(distance * direction.Z) >> 12));
                        int speed = kind == 1 ? Roll() % 240 + 400 : Roll() % 250 + 290;
                        Put(offset + 16, unchecked((kind == 1 ? -speed : speed) * direction.X) >> 12);
                        Put(offset + 18, unchecked((kind == 1 ? -speed : speed) * direction.Y) >> 12);
                        Put(offset + 20, unchecked((kind == 1 ? -speed : speed) * direction.Z) >> 12);
                    }
                    else if (kind == 4)
                    {
                        Put(offset + 6, Roll() % 3840 + 2560);
                        CopyPosition(offset, task);
                        for (int axis = 0; axis < 3; axis++)
                        {
                            int position = offset + 8 + axis * 2;
                            Put(position, Read(position) + Roll() % 1200 - 600);
                        }
                    }
                    else
                    {
                        Put(offset + 6, Roll() % 1280 + unchecked((ushort)(age + 160)) * 8);
                        short fraction = (short)(Roll() % 4096);
                        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Suicide fuse requires its actual caster bone count.");
                        byte bone = checked((byte)(Roll() % source.BoneCount));
                        NativeBattleVector point = ReadNativeGraphicsBone(graphics, actor, bone, fraction);
                        Put(offset + 8, point.X + Roll() % 200 - 100);
                        Put(offset + 10, point.Y + Roll() % 200 - 100);
                        Put(offset + 12, point.Z + Roll() % 200 - 100);
                        Put(offset + 16, (Read(offset + 8) & 1) != 0 ? 4096 : -4096);
                        Put(offset + 18, (Read(offset + 10) & 1) != 0 ? 4096 : -4096);
                    }
                }
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 >= (kind == 1 ? 6 : 4) && alive == 0 ? 2 : 0;
        }
        void CopyPosition(int offset, NativeBattleModulePoolTask task)
        {
            Put32(offset + 8, task.ReadDword(16));
            Put32(offset + 12, task.ReadDword(20));
        }
    }
}
