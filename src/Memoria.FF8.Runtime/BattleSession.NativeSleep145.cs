using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeSleep145(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleSleep145Resources resources = effect.Binding.Sleep145Resources ?? throw new NotSupportedException("Native Sleep requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleSleep145State process = memory.Sleep145 ?? throw new NotSupportedException("Native Sleep requires its retained process arrays.");
        byte[] zzz = process.Zzz.ToArray();
        for (int index = 0; index < 40; index++)
        {
            SleepPutWord(zzz, index * 12, -1);
        }
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures!.Register5099A0(resources.Camera);
            NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
            NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Sleep requires its original TIM allocation.");
            (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
            memory = memory with { Textures = textures };
        }
        NativeEffectMemory = memory with { Sleep145 = process with { Zzz = zzz.ToImmutableArray() } };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x24AA828, 0x24AA818, 16, 1);
        (root, _) = root.Append(0x61AA80);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x24AAAC0, 0x24AAAD0, 36, 100);
        (children, int slot) = children.Append(0x6186A0);
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C];
        int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native Sleep requires its first action contact.");
        children = children with { Slots = children.Slots.SetItem(slot, children.Slots[slot].WriteWord(12, 0).WriteWord(14, 0).WriteWord(32, nativeActionPackets.ReadContact(contact).Primary.ActorIndex)) };
        return graphics with { OriginalEffect = effect with { Pools = [root, children], ReturnedDescriptor = 0x24AA828, DrawArenaOffset = null } };
    }

    private NativeBattleGraphicsState AdvanceNativeSleep145(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleSleep145Resources resources = effect.Binding.Sleep145Resources ?? throw new NotSupportedException("Native Sleep requires its original resources.");
        NativeBattleSleep145State process = NativeEffectMemory!.Sleep145 ?? throw new NotSupportedException("Native Sleep requires its retained process arrays.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Sleep requires its shared CRT stream.");
        byte[] zzz = process.Zzz.ToArray();
        byte[] mainTrails = process.MainTrails.ToArray();
        byte[] bubbleTrails = process.BubbleTrails.ToArray();
        NativeBattleEffectTaskPool rootPool = effect.Pools[0];
        NativeBattleEffectTaskPool children = effect.Pools[1];
        NativeBattleModulePoolTask root = rootPool.Slots[0];
        if (root.Callback != 0x61AA80 || rootPool.DescriptorAddress != 0x24AA828 || children.DescriptorAddress != 0x24AAAC0)
        {
            throw new InvalidDataException("Native Sleep lost its original physical task banks.");
        }
        effect = effect with { DrawArenaOffset = (root.ReadWord(12) & 1) != 0 ? 0x8000 : 0 };
        bool paused = (graphics.Flags & 0x201) != 0;
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        NativeBattleEffectTaskStep advanced = children.Advance((pool, slot) =>
        {
            children = pool;
            NativeBattleModulePoolTask task = pool.Slots[slot];
            int result = task.Callback switch
            {
                0x6186A0 => Director(ref task),
                0x619100 => Main(ref task),
                0x618BB0 => Bubble(ref task),
                0x619000 => Particle(ref task),
                _ => throw new NotSupportedException("Native Sleep selected an unrelated task callback.")
            };
            task = task with { Next = children.Slots[slot].Next };
            Put(slot, task);
            return new(children, result);
        });
        children = advanced.Pool;
        for (int index = 0; index < 40; index++)
        {
            int offset = index * 12;
            int frame = SleepRawWord(zzz, offset);
            if (frame < 0)
            {
                continue;
            }
            Sprite(resources.Zzz, frame, SleepRawVector(zzz, offset + 4), SleepRawWord(zzz, offset + 2), 1, unchecked((short)(SleepRawWord(zzz, offset + 10) << 2)));
            if (!paused)
            {
                SleepPutWord(zzz, offset, (short)(frame + 1 >= 16 ? -1 : frame + 1));
            }
        }
        root = root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)));
        rootPool = rootPool with { Slots = rootPool.Slots.SetItem(0, root) };
        if (advanced.Survivors == 0)
        {
            rootPool = rootPool.Advance((pool, _) => new(pool, 2)).Pool;
        }
        NativeEffectMemory = NativeEffectMemory! with { Sleep145 = process with { Zzz = zzz.ToImmutableArray(), MainTrails = mainTrails.ToImmutableArray(), BubbleTrails = bubbleTrails.ToImmutableArray() } };
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = [rootPool, children], ReturnedDescriptor = advanced.Survivors == 0 ? 0u : 0x24AA828u } };

        int Director(ref NativeBattleModulePoolTask task)
        {
            if (paused)
            {
                return 0;
            }
            int counter = SleepWord(task, 12);
            int action = SleepWord(task, 14);
            int target = SleepWord(task, 32);
            if (counter == 0)
            {
                NativeBattleVector point = ReadNativeDefaultPosition571400(graphics, target);
                NativeBattleVector top = ReadNativeGraphicsBone(graphics, target, 0xF0, 0);
                task = SleepStore(task, 16, point with { Y = graphics.Models[target].Transform!.Angle36 }).WriteWord(22, unchecked((ushort)top.Y));
            }
            if (counter == 1)
            {
                int spread = unchecked(graphics.Models[target].Transform!.Scale38 * 5000) >> 12;
                spread = Math.Min(spread, 1400);
                NativeBattleModulePoolTask visual = New(0x619100, out int slot);
                NativeBattleVector point = SleepVector(task, 16) with { Y = unchecked((short)(SleepWord(task, 18) - 200)) };
                visual = SleepStore(visual, 16, point).WriteWord(12, 0).WriteWord(14, (ushort)action).WriteWord(22, unchecked((ushort)((SleepWord(task, 22) - point.Y - 500) / 18))).WriteWord(24, (ushort)(Roll() % 4096)).WriteWord(26, (ushort)(Roll() % 50 + 70)).WriteWord(28, (ushort)(Roll() % 4096));
                visual = visual.WriteWord(30, (ushort)(Roll() % 100 + 400)).WriteWord(32, unchecked((ushort)spread)).WriteWord(34, unchecked((ushort)(spread / 18)));
                Put(slot, visual);
                ClearTrails(mainTrails, action * 36);
            }
            if (counter == 26)
            {
                int angle = Roll() % 4096;
                for (int index = 0; index < 6; index++)
                {
                    NativeBattleVector direction = SleepRotateWord(NativeBattleGfMatrices.Euler56CD50(new(0, unchecked((short)(angle + index * 682)), 0)), new(0, 0, -4096));
                    NativeBattleModulePoolTask bubble = New(0x618BB0, out int slot);
                    bubble = SleepStore(bubble, 16, SleepVector(task, 16) with { Y = unchecked((short)(SleepWord(task, 22) - 500)) }).WriteWord(12, 0).WriteWord(14, (ushort)action).WriteWord(22, (ushort)index);
                    int speed = Roll() % 30 + 100;
                    bubble = bubble.WriteWord(24, unchecked((ushort)(direction.X * speed >> 12))).WriteWord(26, unchecked((ushort)(-130 - Roll() % 40))).WriteWord(28, unchecked((ushort)(direction.Z * speed >> 12))).WriteWord(30, (ushort)(Roll() % 60 + 50)).WriteWord(32, (ushort)(Roll() % 128 + 384)).WriteWord(34, (ushort)(Roll() % 4096));
                    Put(slot, bubble);
                    ClearTrails(bubbleTrails, (action * 6 + index) * 36);
                }
            }
            if (counter is >= 3 and <= 30 && (counter & 1) != 0)
            {
                int free = Enumerable.Range(0, 40).FirstOrDefault(index => SleepRawWord(zzz, index * 12) < 0, -1);
                if (free >= 0)
                {
                    NativeBattleVector point = ReadNativeDefaultPosition571400(graphics, target);
                    int offset = free * 12;
                    SleepPutWord(zzz, offset, 0);
                    SleepPutWord(zzz, offset + 2, (short)(Roll() % 1792 + 2560));
                    SleepPutVector(zzz, offset + 4, new(unchecked((short)(Roll() % 1400 + point.X - 700)), unchecked((short)(Roll() % 1400 + point.Y - 700)), unchecked((short)(Roll() % 1400 + point.Z - 700))));
                    SleepPutWord(zzz, offset + 10, (short)((Roll() % 4) << 8));
                }
            }
            NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C + action];
            if (counter == 30)
            {
                int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native Sleep requires its selected action contact.");
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
            }
            if (counter == 45 && action + 1 <= nativeActionPackets.Headers[effect.Parameters.HeaderIndex7C].ReadByte(17))
            {
                NativeBattleActionHeaderSlot nextHeader = nativeActionPackets.Headers[effect.Parameters.HeaderIndex7C + action + 1];
                int contact = nextHeader.FirstContactIndex ?? throw new NotSupportedException("Native Sleep requires its next action contact.");
                NativeBattleModulePoolTask next = New(0x6186A0, out int slot);
                Put(slot, next.WriteWord(12, 0).WriteWord(14, (ushort)(action + 1)).WriteWord(32, nativeActionPackets.ReadContact(contact).Primary.ActorIndex));
            }
            if (counter == 1)
            {
                NativeBattleVector point = ReadNativeDefaultPosition571400(graphics, target);
                PublishNativeModuleSound(resources.Sound, 0, new(point.X, point.Y, point.Z, 0));
            }
            task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
            return counter + 1 > 60 ? 2 : 0;
        }

        int Main(ref NativeBattleModulePoolTask task)
        {
            int counter = SleepWord(task, 12);
            int action = SleepWord(task, 14);
            int trailOffset = action * 36;
            NativeBattleVector orbit = SleepRotateWord(NativeBattleGfMatrices.Euler56CD50(new(0, SleepWord(task, 28), 0)), new(0, 0, unchecked((short)-SleepWord(task, 32))));
            NativeBattleVector origin = SleepVector(task, 16);
            NativeBattleFullMacVertex point = new(origin.X + orbit.X, origin.Y, origin.Z + orbit.Z, 0);
            NativeBattleVector drawn = new(unchecked((short)point.X), unchecked((short)point.Y), unchecked((short)point.Z));
            int fade = counter < 8 ? (8 - counter) << 9 : 0;
            DrawBubble(point, SleepWord(task, 24), 864, fade, counter < 8, false);
            int flatFade = fade == 0 ? NativeBattleSleep145.GroundFade(origin.Y) : fade;
            DrawBubble(point, SleepWord(task, 24), 864, flatFade, true, true);
            DrawTrails(mainTrails, trailOffset, counter, 864, true, flatFade);
            NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C + action];
            int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native Sleep requires its selected target contact.");
            int target = nativeActionPackets.ReadContact(contact).Primary.ActorIndex;
            if (counter < 26)
            {
                graphics = graphics with { Models = graphics.Models.SetItem(target, graphics.Models[target] with { Flags0 = (ushort)(graphics.Models[target].Flags0 | 4) }) };
                graphics = DrawNativeSleep145Tint(graphics, target, drawn, fade);
            }
            if (paused)
            {
                return 0;
            }
            WriteTrail(mainTrails, trailOffset + counter % 3 * 12, drawn, SleepWord(task, 24));
            if (counter < 18)
            {
                task = task.WriteWord(18, unchecked((ushort)(SleepWord(task, 18) + SleepWord(task, 22)))).WriteWord(32, unchecked((ushort)(SleepWord(task, 32) - SleepWord(task, 34))));
            }
            int spinSpeed = SleepWord(task, 26);
            task = task.WriteWord(24, unchecked((ushort)(SleepWord(task, 24) + spinSpeed))).WriteWord(26, unchecked((ushort)(spinSpeed + (spinSpeed >> 4)))).WriteWord(28, unchecked((ushort)(SleepWord(task, 28) + SleepWord(task, 30))));
            if (counter is >= 4 and <= 20)
            {
                for (int index = 0; index < 3; index++)
                {
                    SpawnParticle(drawn, Roll() % 4 + index, 1792, unchecked((short)fade));
                }
            }
            task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
            if (counter + 1 <= 26)
            {
                return 0;
            }
            if (action >= 0)
            {
                graphics = graphics with { Models = graphics.Models.SetItem(target, graphics.Models[target] with { Flags0 = (ushort)(graphics.Models[target].Flags0 & 0xFFFB) }) };
            }
            return 2;
        }

        int Bubble(ref NativeBattleModulePoolTask task)
        {
            int counter = SleepWord(task, 12);
            int trailOffset = (SleepWord(task, 14) * 6 + SleepWord(task, 22)) * 36;
            NativeBattleVector drawn = SleepVector(task, 16);
            int scale = SleepWord(task, 32);
            DrawBubble(new(drawn.X, drawn.Y, drawn.Z, 0), SleepWord(task, 34), scale, counter >= 14 ? (counter - 14) << 10 : 0, counter >= 14, false);
            DrawTrails(bubbleTrails, trailOffset, counter, scale, false, 0);
            if (paused)
            {
                return 0;
            }
            WriteTrail(bubbleTrails, trailOffset + counter % 3 * 12, drawn, SleepWord(task, 34));
            short vx = SleepWord(task, 24);
            short vy = SleepWord(task, 26);
            short vz = SleepWord(task, 28);
            short ay = SleepWord(task, 30);
            task = task.WriteWord(16, unchecked((ushort)(drawn.X + vx))).WriteWord(24, unchecked((ushort)(vx - (short)(vx >> 6)))).WriteWord(18, unchecked((ushort)(drawn.Y + vy))).WriteWord(26, unchecked((ushort)(vy + ay))).WriteWord(30, unchecked((ushort)(ay + (short)(ay >> 5))));
            if (SleepWord(task, 18) > 0)
            {
                task = task.WriteWord(18, 0).WriteWord(26, unchecked((ushort)(-130 - Roll() % 80))).WriteWord(30, (ushort)(Roll() % 60 + 50)).WriteWord(34, unchecked((ushort)(SleepWord(task, 34) + Roll() % 2048)));
            }
            task = task.WriteWord(20, unchecked((ushort)(drawn.Z + vz))).WriteWord(28, unchecked((ushort)(vz - (short)(vz >> 6))));
            if (counter is >= 2 and <= 18)
            {
                SpawnParticle(drawn, Roll() % 3, 1536, null);
            }
            task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
            return counter + 1 >= 18 ? 2 : 0;
        }

        int Particle(ref NativeBattleModulePoolTask task)
        {
            int delay = SleepWord(task, 14);
            if (delay > 0)
            {
                if (!paused)
                {
                    task = task.WriteWord(14, (ushort)(delay - 1));
                }
                return 0;
            }
            Sprite(resources.Particle, SleepWord(task, 12), SleepVector(task, 16), SleepWord(task, 32), 0, 0);
            if (paused)
            {
                return 0;
            }
            task = task.WriteWord(32, unchecked((ushort)(SleepWord(task, 32) - 48)));
            for (int axis = 0; axis < 3; axis++)
            {
                short velocity = SleepWord(task, 24 + axis * 2);
                task = task.WriteWord(16 + axis * 2, unchecked((ushort)(SleepWord(task, 16 + axis * 2) + velocity))).WriteWord(24 + axis * 2, unchecked((ushort)(velocity - (short)(velocity >> 5))));
            }
            task = task.WriteWord(12, unchecked((ushort)(SleepWord(task, 12) + 1)));
            return SleepWord(task, 12) >= 9 ? 2 : 0;
        }

        void SpawnParticle(NativeBattleVector point, int delay, int scaleRange, short? fourth)
        {
            NativeBattleModulePoolTask particle = New(0x619000, out int slot);
            particle = SleepStore(particle.WriteWord(12, 0).WriteWord(14, (ushort)delay), 16, point);
            if (fourth is short pad)
            {
                particle = particle.WriteWord(22, unchecked((ushort)pad));
            }
            particle = particle.WriteWord(16, unchecked((ushort)(point.X + Roll() % 280 - 140))).WriteWord(18, unchecked((ushort)(point.Y + Roll() % 280 - 93))).WriteWord(20, unchecked((ushort)(point.Z + Roll() % 280 - 140))).WriteWord(24, unchecked((ushort)(Roll() % 100 - 50))).WriteWord(26, unchecked((ushort)(Roll() % 60 - 10))).WriteWord(28, unchecked((ushort)(Roll() % 100 - 50))).WriteWord(32, (ushort)(Roll() % scaleRange + 2560));
            Put(slot, particle);
        }

        void DrawBubble(NativeBattleFullMacVertex point, short angle, int scale, int fade, bool cue, bool ground)
        {
            NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CD50(new(0, angle, 0)), scale, scale, scale) with { X = point.X, Y = point.Y, Z = point.Z };
            if (ground)
            {
                matrix = matrix with { Rotation = matrix.Rotation.Select((value, index) => index % 3 == 1 ? (short)0 : value).ToImmutableArray(), Y = 0 };
            }
            matrix = NativeBattleGfMatrices.Compose56C2F0(view, matrix);
            NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(resources.Bubble, 0, default, matrix, projection, new(cue ? 0xC0u : 0u, 0, fade, null, null, 0, 17, 2), colors);
            Append(batch);
        }

        void DrawTrails(byte[] trails, int offset, int counter, int scale, bool ground, int flatFade)
        {
            int index = counter - 1 < 0 ? 0 : (counter - 1) % 3;
            for (int fade = 1408; fade < 2944; fade += 512)
            {
                int entry = offset + index * 12;
                if (BinaryPrimitives.ReadUInt32LittleEndian(trails.AsSpan(entry)) == 0)
                {
                    break;
                }
                NativeBattleVector point = SleepRawVector(trails, entry + 4);
                DrawBubble(new(point.X, point.Y, point.Z, 0), SleepRawWord(trails, entry + 10), scale, fade, true, false);
                if (ground)
                {
                    DrawBubble(new(point.X, point.Y, point.Z, 0), SleepRawWord(trails, entry + 10), scale, flatFade, true, true);
                }
                index = index == 0 ? 2 : index - 1;
            }
        }

        void Sprite(NativeBattleModuleResource resource, int frame, NativeBattleVector point, int scale, ushort flags, short rotation)
        {
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.SpriteFrame(frame), Phase = unchecked((ushort)frame), Anchor = new(point.X, point.Y, point.Z, 0), Scale = scale, DepthOffset = -(scale >> 3), SpriteFlags = flags, Rotation = rotation };
            NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, NativeCamera.Roll, 17, 2);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch, NativeBattleEffectGouraudWrites.RgbBytes);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
        void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
        NativeBattleModulePoolTask New(uint callback, out int slot)
        {
            (children, slot) = children.Append(callback);
            return children.Slots[slot];
        }
        void Put(int slot, NativeBattleModulePoolTask task) => children = children with { Slots = children.Slots.SetItem(slot, task) };
        int Roll()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    private static void ClearTrails(byte[] trails, int offset)
    {
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(trails.AsSpan(offset + index * 12), 0);
        }
    }
    private static void WriteTrail(byte[] trails, int offset, NativeBattleVector point, short angle)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(trails.AsSpan(offset), 1);
        SleepPutVector(trails, offset + 4, point);
        SleepPutWord(trails, offset + 10, angle);
    }
    private static NativeBattleVector SleepRotateWord(NativeBattleActionMatrix matrix, NativeBattleVector point)
    {
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(matrix, point);
        return new(unchecked((short)x), unchecked((short)y), unchecked((short)z));
    }
    private static short SleepWord(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleVector SleepVector(NativeBattleModulePoolTask task, int offset) => new(SleepWord(task, offset), SleepWord(task, offset + 2), SleepWord(task, offset + 4));
    private static NativeBattleModulePoolTask SleepStore(NativeBattleModulePoolTask task, int offset, NativeBattleVector value) => task.WriteWord(offset, unchecked((ushort)value.X)).WriteWord(offset + 2, unchecked((ushort)value.Y)).WriteWord(offset + 4, unchecked((ushort)value.Z));
    private static short SleepRawWord(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
    private static NativeBattleVector SleepRawVector(byte[] bytes, int offset) => new(SleepRawWord(bytes, offset), SleepRawWord(bytes, offset + 2), SleepRawWord(bytes, offset + 4));
    private static void SleepPutWord(byte[] bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
    private static void SleepPutVector(byte[] bytes, int offset, NativeBattleVector value)
    {
        SleepPutWord(bytes, offset, value.X);
        SleepPutWord(bytes, offset + 2, value.Y);
        SleepPutWord(bytes, offset + 4, value.Z);
    }
}
