using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeUltrasonic152(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleUltrasonic152Resources resources = effect.Binding.Ultrasonic152Resources ?? throw new NotSupportedException("Native Ultrasonic Waves requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleUltrasonic152State process = memory.Ultrasonic152 ?? throw new NotSupportedException("Native Ultrasonic Waves requires its retained process fields.");
        byte[] particles = process.Particles.ToArray();
        for (int index = 0; index < 100; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(particles.AsSpan(index * 24), 0);
        }
        NativeBattleEffectTextureState textures = memory.Textures!.Register5099A0(resources.Camera);
        NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
        NativeBattleEffectInitializerAsset texture = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Ultrasonic Waves requires its original TIM allocation.");
        (textures, _) = textures.EnqueueTim505E30(new(texture.Name, texture.Data));
        NativeEffectMemory = memory with { Textures = textures, Ultrasonic152 = process with { Particles = particles.ToImmutableArray(), BufferOffset = memory.Cursor } };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x244BE98, 0x244BE78, 16, 2);
        (root, _) = root.Append(0x60C8F0);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x244C808, 0x244C818, 36, 100);
        (children, _) = children.Append(0x60B070);
        return graphics with { OriginalEffect = effect with { Pools = [root, children], ReturnedDescriptor = 0x244BE98, DrawArenaOffset = null } };
    }

    private NativeBattleGraphicsState AdvanceNativeUltrasonic152(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleUltrasonic152Resources resources = effect.Binding.Ultrasonic152Resources ?? throw new NotSupportedException("Native Ultrasonic Waves requires its original resources.");
        NativeBattleUltrasonic152State process = NativeEffectMemory!.Ultrasonic152 ?? throw new NotSupportedException("Native Ultrasonic Waves requires its retained process fields.");
        byte[] particles = process.Particles.ToArray();
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Ultrasonic Waves requires its shared CRT stream.");
        NativeBattleEffectTaskPool roots = effect.Pools[0];
        NativeBattleEffectTaskPool children = effect.Pools[1];
        if (roots.DescriptorAddress != 0x244BE98 || children.DescriptorAddress != 0x244C808)
        {
            throw new InvalidDataException("Native Ultrasonic Waves lost its original physical task banks.");
        }
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        bool paused = (graphics.Flags & 0x201) != 0;
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new InvalidDataException("Native Ultrasonic Waves requires its first physical contact.");
        int target = nativeActionPackets.ReadContact(first).Primary.ActorIndex;
        NativeBattleEffectTaskStep rootStep = roots.Advance((pool, slot) =>
        {
            roots = pool;
            NativeBattleModulePoolTask task = pool.Slots[slot];
            int result;
            if (task.Callback == 0x60C8F0)
            {
                effect = effect with { DrawArenaOffset = checked(process.BufferOffset + ((task.ReadWord(12) & 1) != 0 ? 0x8000 : 0)) };
                NativeBattleEffectTaskStep advanced = children.Advance((current, childSlot) =>
                {
                    children = current;
                    NativeBattleModulePoolTask child = current.Slots[childSlot];
                    int code = child.Callback switch
                    {
                        0x60B070 => Controller(ref child),
                        0x60B400 => Wave(ref child),
                        0x60B4C0 => Ring(ref child),
                        0x60C3C0 => Burst(ref child, 1, resources.Sprites[1], 8),
                        0x60C580 => Burst(ref child, 2, resources.Sprites[2], 0),
                        0x60C740 => Burst(ref child, 4, resources.Sprites[3], 0),
                        _ => throw new NotSupportedException("Native Ultrasonic Waves selected an unrelated effect callback.")
                    };
                    child = child.UpdateHeader(child.Flags, child.State, children.Slots[childSlot].Next, child.Callback);
                    Put(childSlot, child);
                    return new(children, code);
                });
                children = advanced.Pool;
                if (advanced.Survivors == 0)
                {
                    (roots, int waitSlot) = roots.Append(0x60C960);
                    roots = roots.WriteSlot(waitSlot, roots.Slots[waitSlot].WriteWord(12, 0).WriteWord(14, 0));
                }
                result = advanced.Survivors == 0 ? 2 : 0;
            }
            else if (task.Callback == 0x60C960)
            {
                if (task.ReadWord(12) == 1)
                {
                    uint completion = checked(roots.BankAddress + (uint)(slot * roots.Stride + 14));
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)process.BufferOffset), completion, (address, value) =>
                    {
                        NativeBattleModulePoolTask current = roots.Slots[slot];
                        roots = roots.WriteSlot(slot, current.WriteWord(14, (ushort)((current.ReadWord(14) & 0xFF00) | value)));
                    });
                    graphics = graphics with { Auxiliary = auxiliary };
                    task = task.WriteWord(14, roots.Slots[slot].ReadWord(14));
                }
                result = task.ReadWord(14) != 0 ? 2 : 0;
            }
            else
            {
                throw new NotSupportedException("Native Ultrasonic Waves selected an unrelated root callback.");
            }
            task = (task.UpdateHeader(task.Flags, task.State, roots.Slots[slot].Next, task.Callback)).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
            roots = roots.WriteSlot(slot, task);
            return new(roots, result);
        });
        roots = rootStep.Pool;
        NativeEffectMemory = NativeEffectMemory! with { Ultrasonic152 = process with { Particles = particles.ToImmutableArray() } };
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = [roots, children], ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : 0x244BE98u } };

        int Controller(ref NativeBattleModulePoolTask task)
        {
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(process.Target.X - process.Anchor.X, process.Target.Y - process.Anchor.Y, process.Target.Z - process.Anchor.Z);
            NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, 0, -4096, direction.X, direction.Y, direction.Z);
            process = process with { Rotation = process.Rotation with { Rotation = NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis) } };
            if (paused)
            {
                return 0;
            }
            int counter = SonicWord(task, 12);
            if (counter == 0)
            {
                NativeBattleModuleAnchor selected = ReadNativeModelAnchor(graphics, target);
                process = process with { Anchor = ReadNativeGraphicsBone(graphics, effect.Parameters.Actor78, 0xF0, 4096), Target = new(selected.X, selected.Y, selected.Z) };
            }
            if (counter is >= 1 and <= 14 && counter % 5 == 1)
            {
                NativeBattleModulePoolTask wave = New(0x60B400, out int slot);
                wave = SonicStore(wave.WriteWord(12, 0), 16, new(unchecked((short)(process.Anchor.X + Roll() % 110 - 55)), unchecked((short)(process.Anchor.Y + Roll() % 110 - 55)), unchecked((short)(process.Anchor.Z + Roll() % 110 - 55)))).WriteWord(24, (ushort)(Roll() % 4096)).WriteWord(28, (ushort)(Roll() % 1024 + 2304));
                Put(slot, wave);
            }
            if (counter is >= 5 and <= 20)
            {
                NativeBattleModulePoolTask ring = New(0x60B4C0, out int slot);
                ring = SonicStore(ring.WriteWord(12, 0), 16, process.Anchor).WriteWord(22, (ushort)(Roll() % 4096));
                ring = SonicStore(ring, 24, new((short)((process.Target.X - process.Anchor.X) / 18), (short)((process.Target.Y - process.Anchor.Y) / 18), (short)((process.Target.Z - process.Anchor.Z) / 18))).WriteWord(30, (ushort)(Roll() % 40 + 10)).WriteWord(32, 151).WriteWord(34, 151);
                Put(slot, ring);
            }
            if (counter is 25 or 26)
            {
                uint[] callbacks = counter == 25 ? [0x60C3C0u] : [0x60C580u, 0x60C740u];
                foreach (uint callback in callbacks)
                {
                    NativeBattleModulePoolTask burst = New(callback, out int slot);
                    Put(slot, SonicStore(burst.WriteWord(12, 0), 16, process.Target));
                }
            }
            if (counter == 35)
            {
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(first)] });
            }
            if (counter == 1)
            {
                PublishNativeModuleSound(new(resources.Sound, 0, null) { Pan = 128 });
            }
            task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
            return counter + 1 > 40 ? 2 : 0;
        }

        int Wave(ref NativeBattleModulePoolTask task)
        {
            int frame = SonicWord(task, 12);
            int level = (12 - frame) << 5;
            uint color = unchecked((uint)level * 0x10101u);
            NativeBattleModuleSpriteFrame selected = Sprite(resources.Sprites[0], frame, SonicVector(task, 16), SonicWord(task, 28), -(SonicWord(task, 28) >> 3), frame >= 8 ? (ushort)13 : (ushort)9, SonicWord(task, 24), color, NativeBattlePilotVisualLifetimes.Identity(152, task) with { ResourceGeneration = resources.Sprites[0].Address });
            if (paused)
            {
                return 0;
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return selected.NextOffset < 0 ? 2 : 0;
        }

        int Ring(ref NativeBattleModulePoolTask task)
        {
            int frame = SonicWord(task, 12);
            int scale = SonicWord(task, 32);
            NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CD50(new(0, 0, SonicWord(task, 22))), scale, scale, scale);
            matrix = NativeBattleGfMatrices.Multiply56C090(view, NativeBattleGfMatrices.Multiply56C090(process.Rotation, matrix));
            NativeBattleVector point = SonicVector(task, 16);
            (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(view, point.X, point.Y, point.Z);
            x = unchecked(x + view.X);
            y = unchecked(y + view.Y);
            z = unchecked(z + view.Z);
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
            int depth = -(scale >> 4);
            matrix = matrix with { X = unchecked(x + (depth * direction.X >> 12)), Y = unchecked(y + (depth * direction.Y >> 12)), Z = unchecked(z + (depth * direction.Z >> 12)) };
            NativeBattleGfGeometryBatch batch = NativeBattleUltrasonic152.Ring60BA00(resources.Ring, matrix, projection, resources.Colors[frame % 10], frame, colors, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(152, task) with { ResourceGeneration = 0x60BA00 });
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            if (paused)
            {
                return 0;
            }
            task = SonicStore(task, 16, new(unchecked((short)(point.X + SonicWord(task, 24))), unchecked((short)(point.Y + SonicWord(task, 26))), unchecked((short)(point.Z + SonicWord(task, 28))))).WriteWord(22, unchecked((ushort)(SonicWord(task, 22) + SonicWord(task, 30)))).WriteWord(32, unchecked((ushort)(scale + SonicWord(task, 34)))).WriteWord(12, unchecked((ushort)(frame + 1)));
            return frame + 1 > 20 ? 2 : 0;
        }

        int Burst(ref NativeBattleModulePoolTask task, uint type, NativeBattleModuleResource sprite, ushort mode)
        {
            int alive = 0;
            for (int index = 0; index < 100; index++)
            {
                int offset = index * 24;
                if ((particles[offset] & type) == 0)
                {
                    continue;
                }
                int frame = SleepRawWord(particles, offset + 4);
                int scale = SleepRawWord(particles, offset + 6);
                NativeBattleModuleSpriteFrame selected = Sprite(sprite, frame, SleepRawVector(particles, offset + 8), scale, -(scale >> 4), mode, 0, 0, NativeBattleUltrasonic152VisualLifetimes.Identity(task, index, type));
                if (paused)
                {
                    continue;
                }
                SleepPutWord(particles, offset + 4, unchecked((short)(frame + 1)));
                if (selected.NextOffset < 0)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(particles.AsSpan(offset), 0);
                }
                else
                {
                    alive++;
                }
            }
            if (paused)
            {
                return 0;
            }
            int counter = SonicWord(task, 12);
            if (counter >= 0 && counter <= (type == 1 ? 8 : 13))
            {
                for (int birth = 0; birth < (type == 1 ? 2 : 1); birth++)
                {
                    int free = Enumerable.Range(0, 100).FirstOrDefault(index => BinaryPrimitives.ReadUInt32LittleEndian(particles.AsSpan(index * 24)) == 0, -1);
                    if (free < 0)
                    {
                        break;
                    }
                    int offset = free * 24;
                    BinaryPrimitives.WriteUInt32LittleEndian(particles.AsSpan(offset), type);
                    SleepPutWord(particles, offset + 4, 0);
                    SleepPutWord(particles, offset + 6, (short)(Roll() % (type == 1 ? 1792 : type == 2 ? 1024 : 1280) + (type == 4 ? 1280 : 768)));
                    NativeBattleVector point = SonicVector(task, 16);
                    int span = type == 1 ? 800 : 900;
                    SleepPutVector(particles, offset + 8, new(unchecked((short)(point.X + Roll() % span - span / 2)), unchecked((short)(point.Y + Roll() % span - span / 2)), unchecked((short)(point.Z + Roll() % span - span / 2))));
                    NativeBattleUltrasonic152VisualLifetimes.Allocated(task, free);
                }
            }
            task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
            return counter + 1 >= 4 && alive == 0 ? 2 : 0;
        }

        NativeBattleModuleSpriteFrame Sprite(NativeBattleModuleResource sprite, int frame, NativeBattleVector point, int scale, int depth, ushort flags, short rotation, uint color, NativeBattlePilotVisualIdentity? pilotIdentity = null)
        {
            NativeBattleModuleSpriteFrame selected = sprite.SpriteFrame(frame);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = sprite, SpriteFrame = selected, Phase = unchecked((ushort)frame), Anchor = new(point.X, point.Y, point.Z, 0), Scale = scale, DepthOffset = depth, SpriteFlags = flags, Rotation = rotation, SpriteColor = color, PilotIdentity = pilotIdentity };
            NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, NativeCamera.Roll, 17, 2);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch, NativeBattleEffectGouraudWrites.RgbBytes);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            return selected;
        }
        NativeBattleModulePoolTask New(uint callback, out int slot)
        {
            (children, slot) = children.Append(callback);
            return children.Slots[slot];
        }
        void Put(int slot, NativeBattleModulePoolTask task) => children = children.WriteSlot(slot, task);
        int Roll()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    private bool TryWriteNativeUltrasonic152Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - 0x244BE78u);
        if (delta >= 32 || delta % 16 != 14)
        {
            return false;
        }
        if (graphics.OriginalEffect is { Binding.Effect: 152 } effect)
        {
            updated = graphics with { OriginalEffect = NativeBattleUltrasonic152.WriteCompletionByte(effect, address, value) };
        }
        else if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Effect: 152 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleUltrasonic152.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            throw new NotSupportedException("Native Ultrasonic Waves completion requires its retained physical wait task.");
        }
        return true;
    }

    private static short SonicWord(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleVector SonicVector(NativeBattleModulePoolTask task, int offset) => new(SonicWord(task, offset), SonicWord(task, offset + 2), SonicWord(task, offset + 4));
    private static NativeBattleModulePoolTask SonicStore(NativeBattleModulePoolTask task, int offset, NativeBattleVector value) => task.WriteWord(offset, unchecked((ushort)value.X)).WriteWord(offset + 2, unchecked((ushort)value.Y)).WriteWord(offset + 4, unchecked((ushort)value.Z));
}
