using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSleepingGas168;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeSleepingGas168(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleSleepingGas168Resources resources = effect.Binding.SleepingGas168Resources ?? throw new NotSupportedException("Native Sleeping Gas requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleSleepingGas168State process = memory.SleepingGas168 ?? throw new NotSupportedException("Native Sleeping Gas requires its retained process state.");
        byte[] target = process.TargetSparkles.ToArray();
        byte[] caster = process.CasterSparkles.ToArray();
        for (int index = 0; index < 100; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(target.AsSpan(index * 24), 0);
        }
        for (int index = 0; index < 50; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(caster.AsSpan(index * 24), 0);
        }
        NativeBattleEffectTextureState textures = memory.Textures!.Register5099A0(resources.Camera);
        NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
        NativeBattleEffectInitializerAsset texture = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Sleeping Gas requires its original TIM allocation.");
        (textures, _) = textures.EnqueueTim505E30(new(texture.Name, texture.Data));
        NativeEffectMemory = memory with { Textures = textures, SleepingGas168 = process with { TargetSparkles = target.ToImmutableArray(), CasterSparkles = caster.ToImmutableArray() } };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x23B5C68, 0x23B5C58, 16, 1);
        (root, _) = root.Append(0x5E29A0);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23B6A88, 0x23B6A98, 36, 100);
        (children, _) = children.Append(0x5E1820);
        return graphics with { OriginalEffect = effect with { Pools = [root, children], ReturnedDescriptor = root.DescriptorAddress, DrawArenaOffset = null } };
    }

    private NativeBattleGraphicsState AdvanceNativeSleepingGas168(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect! with { PresentationPackets = [] };
        NativeBattleSleepingGas168Resources resources = effect.Binding.SleepingGas168Resources ?? throw new NotSupportedException("Native Sleeping Gas requires its original resources.");
        NativeBattleSleepingGas168State process = NativeEffectMemory!.SleepingGas168 ?? throw new NotSupportedException("Native Sleeping Gas requires its retained process arrays.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Sleeping Gas requires its shared CRT random stream.");
        byte[] clouds = process.Clouds.ToArray();
        byte[] offsets = process.Offsets.ToArray();
        byte[] vertices = process.Vertices.ToArray();
        byte[] targetSparkles = process.TargetSparkles.ToArray();
        byte[] casterSparkles = process.CasterSparkles.ToArray();
        NativeBattleEffectTaskPool rootPool = effect.Pools[0];
        NativeBattleEffectTaskPool children = effect.Pools[1];
        NativeBattleModulePoolTask root = rootPool.Slots[0];
        if (root.Callback != 0x5E29A0 || rootPool.DescriptorAddress != 0x23B5C68 || children.DescriptorAddress != 0x23B6A88)
        {
            throw new InvalidDataException("Native Sleeping Gas lost its original physical task banks.");
        }
        effect = effect with { DrawArenaOffset = (root.ReadWord(12) & 1) != 0 ? 0x14000 : 0 };
        bool paused = (graphics.Flags & 0x201) != 0;
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C];
        int firstContact = header.FirstContactIndex ?? throw new InvalidDataException("Native Sleeping Gas requires its original target contacts.");
        NativeBattleEffectTaskStep advanced = children.Advance((pool, slot) =>
        {
            children = pool;
            NativeBattleModulePoolTask task = pool.Slots[slot];
            int result = task.Callback switch
            {
                0x5E1820 => Director(ref task),
                0x5E1D20 => Gas(ref task),
                0x5E2430 => Sparkles(ref task, false),
                0x5E2720 => Sparkles(ref task, true),
                _ => throw new NotSupportedException("Native Sleeping Gas selected an unrelated task callback.")
            };
            task = task.UpdateHeader(task.Flags, task.State, children.Slots[slot].Next, task.Callback);
            return new(children.WriteSlot(slot, task), result);
        });
        children = advanced.Pool;
        root = root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)));
        rootPool = rootPool.WriteSlot(0, root);
        if (advanced.Survivors == 0)
        {
            rootPool = rootPool.Advance((pool, _) => new(pool, 2)).Pool;
        }
        NativeEffectMemory = NativeEffectMemory! with { SleepingGas168 = process with { Clouds = clouds.ToImmutableArray(), Offsets = offsets.ToImmutableArray(), Vertices = vertices.ToImmutableArray(), TargetSparkles = targetSparkles.ToImmutableArray(), CasterSparkles = casterSparkles.ToImmutableArray() } };
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = [rootPool, children], ReturnedDescriptor = advanced.Survivors == 0 ? 0u : 0x23B5C68u } };

        int Director(ref NativeBattleModulePoolTask task)
        {
            if (paused)
            {
                return 0;
            }
            int counter = TaskWord(task, 12);
            if (counter == 0)
            {
                NativeBattleVector anchor = ReadNativeGraphicsBone(graphics, effect.Parameters!.Actor78, 0xF0, 4096);
                anchor = anchor with { Y = unchecked((short)(anchor.Y - 160)) };
                NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (effect.Parameters.TargetMask7A & (1 << actor)) != 0).Select(actor => graphics.Models[actor].Transform!.Position28).ToArray());
                int x = center.X - anchor.X;
                int y = center.Y - anchor.Y;
                int z = center.Z - anchor.Z;
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                int length = unchecked(((int)Math.Sqrt(NormalizeSquare(x, y, z)) * 13) << 8) >> 12;
                center = new(unchecked((short)(anchor.X + (length * direction.X >> 12))), unchecked((short)(anchor.Y + (length * direction.Y >> 12))), unchecked((short)(anchor.Z + (length * direction.Z >> 12))));
                process = process with { Anchor = anchor, Center = center, PairOrder = SortPairs(resources.Model) };
            }
            if (counter is >= 6 and <= 11)
            {
                for (int birth = 0; birth < 2; birth++)
                {
                    (children, int slot) = children.Append(0x5E1D20);
                    NativeBattleModulePoolTask gas = children.Slots[slot];
                    int cloud = TaskWord(task, 14);
                    NativeBattleVector point = new(unchecked((short)(process.Center.X + Roll() % 5000 - 2500)), unchecked((short)(-200 - Roll() % 1700)), unchecked((short)(process.Center.Z + Roll() % 2500 - 1250)));
                    gas = TaskStore(gas, 16, point).WriteWord(12, 0).WriteWord(14, (ushort)cloud).WriteWord(0x1C, (ushort)(Roll() % 3328 + 1280));
                    int start = cloud * 28;
                    BinaryPrimitives.WriteInt32LittleEndian(clouds.AsSpan(start), 2);
                    BinaryPrimitives.WriteInt32LittleEndian(clouds.AsSpan(start + 4), 2);
                    PutWord(clouds, start + 8, (short)(Roll() % 4096));
                    PutWord(clouds, start + 10, (short)(Roll() % 160 + 150));
                    PutWord(clouds, start + 14, 0);
                    PutWord(clouds, start + 16, (short)(Roll() % 20 + 5));
                    PutWord(clouds, start + 18, 2048);
                    PutWord(clouds, start + 20, (short)((Roll() % 1024 + 1600) / 61));
                    PutWord(clouds, start + 24, (short)(Roll() % 600 + 900));
                    Put(slot, gas);
                    task = task.WriteWord(14, unchecked((ushort)(cloud + 1)));
                }
            }
            if (counter == 23)
            {
                for (int index = 0; index < header.ReadByte(16); index++)
                {
                    int actor = nativeActionPackets.ReadContact(firstContact + index).Primary.ActorIndex;
                    (children, int slot) = children.Append(0x5E2430);
                    NativeBattleVector point = ReadNativeGraphicsBone(graphics, actor, 0xF1, 0);
                    point = point with { Y = (graphics.Models[actor].GeometryBounds52 ?? throw new NotSupportedException("Native Sleeping Gas requires its actual modelWORD3C bounds value.")).Maximum.Y };
                    Put(slot, TaskStore(children.Slots[slot], 16, point).WriteWord(12, 0).WriteWord(14, unchecked((ushort)(1 << index))));
                }
            }
            if (counter == 4)
            {
                (children, int slot) = children.Append(0x5E2720);
                Put(slot, children.Slots[slot].WriteWord(12, 0));
            }
            if (counter == 60)
            {
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = Enumerable.Range(0, header.ReadByte(16)).Select(index => nativeActionPackets.ReadContact(firstContact + index)).ToImmutableArray() });
                graphics = graphics with { PendingContacts = pending };
            }
            if (counter is >= 4 and <= 12)
            {
                Flash((counter - 4) * 192, 0x302060);
            }
            else if (counter >= 62)
            {
                Flash((70 - counter) * 192, 0x302060);
            }
            if (counter == 1)
            {
                PublishNativeModuleSound(new(resources.Sound, 0, null) { Pan = 128 });
            }
            task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
            if (counter < 70)
            {
                return 0;
            }
            Flash(0, 0);
            return 2;
        }

        int Gas(ref NativeBattleModulePoolTask task)
        {
            int cloud = TaskWord(task, 14);
            Span<byte> record = clouds.AsSpan(cloud * 28, 28);
            Span<byte> wave = offsets.AsSpan(cloud * 61 * 8, 61 * 8);
            Span<byte> output = vertices.AsSpan(cloud * 122 * 8, 122 * 8);
            AdvanceWave(record, wave);
            BuildShape(record, wave, output, process.PairOrder, resources.Model, paused);
            NativeBattleVector point = TaskVector(task, 16);
            int x = point.X - process.Anchor.X;
            int y = point.Y - process.Anchor.Y;
            int z = point.Z - process.Anchor.Z;
            int distance = (int)Math.Sqrt(NormalizeSquare(x, y, z));
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
            NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(resources.AxisX, resources.AxisY, resources.AxisZ, direction.X, direction.Y, direction.Z);
            NativeBattleActionMatrix matrix = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), process.Anchor.X, process.Anchor.Y, process.Anchor.Z, 4096);
            short width = TaskWord(task, 0x1C);
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, width, width + (width >> 1), unchecked(distance << 12) / 1600);
            matrix = NativeBattleGfMatrices.Compose56C2F0(view, matrix);
            Append(DrawCloud(resources.Model, output.ToArray().ToImmutableArray(), matrix, projection, TaskWord(task, 12), colors, NativeBattlePilotVisualLifetimes.Identity(168, task, cloud) with { ResourceGeneration = 0xD6EDE4 }));
            if (paused)
            {
                return 0;
            }
            task = task.WriteWord(12, unchecked((ushort)(TaskWord(task, 12) + 1)));
            return TaskWord(task, 12) >= 60 ? 2 : 0;
        }

        int Sparkles(ref NativeBattleModulePoolTask task, bool caster)
        {
            byte[] entries = caster ? casterSparkles : targetSparkles;
            int capacity = caster ? 50 : 100;
            int mask = caster ? 1 : TaskWord(task, 14);
            int counter = TaskWord(task, 12);
            int active = 0;
            for (int index = 0; index < capacity; index++)
            {
                int offset = index * 24;
                if ((BinaryPrimitives.ReadInt32LittleEndian(entries.AsSpan(offset)) & mask) == 0)
                {
                    continue;
                }
                ushort flags = caster ? (ushort)8 : (ushort)0;
                uint color = 0;
                if (!caster && Word(entries, offset + 14) != 0)
                {
                    short fadeLength = Word(entries, offset + 22);
                    if (counter < fadeLength)
                    {
                        uint value = unchecked((uint)(128 / fadeLength * counter));
                        color = value | value << 8 | value << 16;
                        flags = 4;
                    }
                    else
                    {
                        PutWord(entries, offset + 14, 0);
                    }
                }
                int frame = Word(entries, offset + 4);
                NativeBattleModuleSpriteFrame sprite = resources.Sprite.SpriteFrame(frame);
                short scale = Word(entries, offset + 6);
                NativeBattleVector position = EntryVector(entries, offset + 8);
                NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resources.Sprite, SpriteFrame = sprite, Phase = (ushort)frame, Anchor = new(position.X, position.Y, position.Z, 0), Scale = scale, DepthOffset = -(scale >> (caster ? 3 : 4)), Direction = 4096, SpriteFlags = flags, SpriteColor = color, PilotIdentity = NativeBattleCloudBombVisualLifetimes.Identity(168, task, (caster ? 100 : 0) + index, resources.Sprite.Address) };
                NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, NativeCamera.Roll, 17, 2);
                effect = effect with { PresentationPackets = effect.PresentationPackets.AddRange(batch.Primitives.Select(item => new NativeBattlePrimitiveReference("20DFAB8", 0, checked(effect.DrawArenaOffset!.Value + item.ByteOffset)))) };
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch, NativeBattleEffectGouraudWrites.FullDword);
                NativeEffectMemory = written.Memory;
                effect = effect with { DrawArenaOffset = written.Cursor };
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
                if (paused)
                {
                    continue;
                }
                PutWord(entries, offset + 4, unchecked((short)(frame + 1)));
                if (sprite.NextOffset < 0)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(entries.AsSpan(offset), 0);
                    continue;
                }
                PutWord(entries, offset + 8, unchecked((short)(position.X + Word(entries, offset + 16))));
                PutWord(entries, offset + 10, unchecked((short)(position.Y + (caster ? -1 : Word(entries, offset + 18)))));
                PutWord(entries, offset + 12, unchecked((short)(position.Z + Word(entries, offset + 20))));
                if (!caster)
                {
                    short velocity = Word(entries, offset + 18);
                    PutWord(entries, offset + 18, unchecked((short)(velocity - (short)(velocity >> 5))));
                }
                active++;
            }
            if (paused)
            {
                return 0;
            }
            bool birth = caster ? counter is >= 0 and <= 50 : counter is >= 2 and <= 34;
            if (birth)
            {
                NativeBattleVector point = TaskVector(task, 16);
                int dx;
                int dy;
                int dz;
                if (caster)
                {
                    point = ReadNativeGraphicsBone(graphics, effect.Parameters!.Actor78, 0xF0, 4096);
                    point = point with { Y = unchecked((short)(point.Y - 160)) };
                    task = TaskStore(task, 16, point);
                    dx = process.Center.X - point.X;
                    dy = process.Center.Y - point.Y;
                    dz = process.Center.Z - point.Z;
                }
                else
                {
                    dx = point.X - process.Anchor.X;
                    dy = point.Y - process.Anchor.Y;
                    dz = point.Z - process.Anchor.Z;
                }
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, dy, dz);
                int free = Enumerable.Range(0, capacity).FirstOrDefault(index => BinaryPrimitives.ReadInt32LittleEndian(entries.AsSpan(index * 24)) == 0, -1);
                if (free >= 0)
                {
                    int offset = free * 24;
                    BinaryPrimitives.WriteInt32LittleEndian(entries.AsSpan(offset), mask);
                    NativeBattleCloudBombVisualLifetimes.Allocated(task, (caster ? 100 : 0) + free);
                    PutWord(entries, offset + 4, 0);
                    PutWord(entries, offset + 6, (short)(Roll() % (caster ? 2048 : 2560) + 1536));
                    if (caster)
                    {
                        EntryStore(entries, offset + 8, point);
                        PutWord(entries, offset + 14, TaskWord(task, 22));
                        PutWord(entries, offset + 8, unchecked((short)(point.X + Roll() % 100 - 50)));
                        PutWord(entries, offset + 10, unchecked((short)(point.Y + Roll() % 100 - 50)));
                        PutWord(entries, offset + 12, unchecked((short)(point.Z + Roll() % 100 - 50)));
                    }
                    else
                    {
                        PutWord(entries, offset + 8, unchecked((short)(Roll() % 1000 + point.X - 500)));
                        PutWord(entries, offset + 10, unchecked((short)(point.Y - Roll() % 300)));
                        PutWord(entries, offset + 12, unchecked((short)(Roll() % 1000 + point.Z - 500)));
                        PutWord(entries, offset + 18, unchecked((short)(-50 - Roll() % 70)));
                        PutWord(entries, offset + 14, counter < 16 ? (short)1 : (short)0);
                        if (counter < 16)
                        {
                            PutWord(entries, offset + 22, (short)(16 - counter));
                        }
                    }
                    int speed = Roll() % (caster ? 25 : 20) + (caster ? 15 : 20);
                    PutWord(entries, offset + 16, unchecked((short)(speed * direction.X >> 12)));
                    PutWord(entries, offset + 20, unchecked((short)(speed * direction.Z >> 12)));
                }
            }
            task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
            return TaskWord(task, 12) >= 4 && active == 0 ? 2 : 0;
        }

        void Append(NativeBattleGfGeometryBatch batch)
        {
            effect = effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(effect.PresentationPackets, "20DFAB8", 0, effect.DrawArenaOffset!.Value, batch) };
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
        void Flash(int depth, uint color)
        {
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Sleeping Gas requires its current stage color rows.");
            NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)depth), ColorFlags = color }).ToImmutableArray() };
        }
        int Roll()
        {
            random = random.Draw(out ushort value);
            return value;
        }
        void Put(int slot, NativeBattleModulePoolTask task) => children = children.WriteSlot(slot, task);
    }

    private static short TaskWord(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleVector TaskVector(NativeBattleModulePoolTask task, int offset) => new(TaskWord(task, offset), TaskWord(task, offset + 2), TaskWord(task, offset + 4));
    private static NativeBattleModulePoolTask TaskStore(NativeBattleModulePoolTask task, int offset, NativeBattleVector point) => task.WriteWord(offset, unchecked((ushort)point.X)).WriteWord(offset + 2, unchecked((ushort)point.Y)).WriteWord(offset + 4, unchecked((ushort)point.Z));
    private static NativeBattleVector EntryVector(byte[] bytes, int offset) => new(Word(bytes, offset), Word(bytes, offset + 2), Word(bytes, offset + 4));
    private static void EntryStore(byte[] bytes, int offset, NativeBattleVector point)
    {
        PutWord(bytes, offset, point.X);
        PutWord(bytes, offset + 2, point.Y);
        PutWord(bytes, offset + 4, point.Z);
    }
}
