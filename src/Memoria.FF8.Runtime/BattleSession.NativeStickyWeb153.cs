using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeStickyWeb153(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleStickyWeb153Resources resources = effect.Binding.StickyWeb153Resources ?? throw new NotSupportedException("Native Sticky Web requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleStickyWeb153State state = memory.StickyWeb153 ?? throw new NotSupportedException("Native Sticky Web requires its retained process banks.");
        byte[] globals = state.Globals.ToArray();
        for (int particle = 0; particle < 100; particle++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(globals.AsSpan(0x88 + particle * 24), 0);
        }
        NativeBattleEffectTextureState textures = memory.Textures!.Register5099A0(resources.Camera);
        NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
        NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Sticky Web requires its original TIM allocation.");
        (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
        NativeEffectMemory = memory with { Textures = textures, StickyWeb153 = state with { Globals = globals.ToImmutableArray() } };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x243A550, 0x243A540, 16, 1);
        (root, _) = root.Append(0x60AF50);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x243AFC0, 0x243AFD0, 36, 100);
        (children, _) = children.Append(0x6096F0);
        return graphics with { OriginalEffect = effect with { Pools = [root, children], ReturnedDescriptor = root.DescriptorAddress } };
    }

    private NativeBattleGraphicsState AdvanceNativeStickyWeb153(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleStickyWeb153Resources resources = effect.Binding.StickyWeb153Resources!;
        NativeBattleStickyWeb153State state = NativeEffectMemory!.StickyWeb153 ?? throw new NotSupportedException("Native Sticky Web requires its retained process banks.");
        byte[] data = state.Globals.ToArray();
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Sticky Web requires its CRT random stream.");
        NativeBattleEffectTaskPool root = effect.Pools[0];
        NativeBattleEffectTaskPool children = effect.Pools[1];
        NativeBattleModulePoolTask rootTask = root.Slots[0];
        int cursor = (rootTask.ReadWord(12) & 1) == 0 ? 0 : 0x8000;
        bool paused = (graphics.Flags & 0x201) != 0;
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        NativeBattleScriptedGraphicsMemory registers = NativeEffectMemory.ScriptedGraphics ?? throw new NotSupportedException("Native Sticky Web requires its actual GTE registers.");
        short zsf3 = registers.Zsf3 ?? throw new NotSupportedException("Native Sticky Web requires ZSF3.");
        short zsf4 = registers.Zsf4 ?? throw new NotSupportedException("Native Sticky Web requires ZSF4.");
        short dqa = registers.Dqa;
        int dqb = registers.Dqb;
        int caster = effect.Parameters!.Actor78;
        int target = Enumerable.Range(0, 7).First(actor => (effect.Parameters.TargetMask7A & (1 << actor)) != 0);
        NativeBattleEffectTaskStep advanced = children.Advance((pool, slot) =>
        {
            children = pool;
            NativeBattleModulePoolTask task = pool.Slots[slot];
            int result = task.Callback switch
            {
                0x6096F0 => Master(ref task),
                0x609930 => Thread(ref task),
                0x609DF0 => Model(ref task, false),
                0x60AE10 => Model(ref task, true),
                0x60A990 => Particles(ref task, 1),
                0x60AC10 => Particles(ref task, 2),
                _ => throw new NotSupportedException("Native Sticky Web selected an unrelated task callback.")
            };
            task = task with { Next = children.Slots[slot].Next };
            return new(children with { Slots = children.Slots.SetItem(slot, task) }, result);
        });
        children = advanced.Pool;
        rootTask = rootTask.WriteWord(12, unchecked((ushort)(rootTask.ReadWord(12) + 1)));
        root = root with { Slots = root.Slots.SetItem(0, rootTask) };
        if (advanced.Survivors == 0)
        {
            root = root.Advance((pool, _) => new(pool, 2)).Pool;
        }
        NativeEffectMemory = NativeEffectMemory! with { StickyWeb153 = state with { Globals = data.ToImmutableArray() } };
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = [root, children], ReturnedDescriptor = advanced.Survivors == 0 ? 0u : root.DescriptorAddress } };

        int Master(ref NativeBattleModulePoolTask task)
        {
            NativeBattleVector mouth = ReadNativeGraphicsBone(graphics, caster, 0xE, 4096);
            Store(0x1920, mouth with { Y = unchecked((short)(mouth.Y - 100)) });
            if (paused)
            {
                return 0;
            }
            int frame = Word(task, 12);
            if (frame == 0)
            {
                NativeBattleModuleAnchor anchor = NativeBattlePoseMatrices.ReadAnchor(graphics.Models[target], NativeGraphicsPoseResource(graphics, target).PoseSource ?? throw new NotSupportedException("Native Sticky Web requires its target pose."));
                NativeBattleVector position = new(anchor.X, anchor.Y, anchor.Z);
                Put(0x190E, anchor.W);
                NativeBattleEffectLocalBounds bounds = graphics.Models[target].GeometryBounds52 ?? throw new NotSupportedException("Native Sticky Web requires its target geometry bounds.");
                Store(0x1908, position with { Y = unchecked((short)((bounds.Minimum.Y + bounds.Maximum.Y) >> 1)) });
            }
            if (frame == 1)
            {
                PublishNativeModuleSound(new(resources.Sound, 0, null) { Pan = 128 });
            }
            if (frame == 10 || frame == 48)
            {
                Add(frame == 10 ? 0x60A990u : 0x60AC10u);
            }
            if (frame == 13)
            {
                int slot = Add(0x609930);
                children = children with { Slots = children.Slots.SetItem(slot, children.Slots[slot].WriteWord(16, 0).WriteWord(20, 0x380).WriteWord(18, unchecked((ushort)(Vector(0x1908).Y + 750))).WriteWord(22, unchecked((ushort)-60))) };
                for (int ring = 0; ring < 5; ring++)
                {
                    Dword(0x1930 + ring * 20, 0);
                }
                SpawnModel(0x609DF0, 0xC00, null);
            }
            if (frame == 50)
            {
                int yaw = Roll() % 4096;
                int scale = (Roll() % 0x500 + 0x980) / 4;
                SpawnModel(0x60AE10, scale, yaw);
            }
            if (frame == 55)
            {
                int first = nativeActionPackets.Headers[effect.Parameters.HeaderIndex7C].FirstContactIndex ?? throw new InvalidDataException("Native Sticky Web requires its actual first contact.");
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(first)] });
                graphics = graphics with { PendingContacts = pending };
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return frame + 1 > 60 ? 2 : 0;
        }

        void SpawnModel(uint callback, int scale, int? suppliedYaw)
        {
            int slot = Add(callback);
            NativeBattleVector point = Vector(0x1908);
            NativeBattleModulePoolTask model = children.Slots[slot].WriteWord(16, unchecked((ushort)point.X)).WriteWord(18, unchecked((ushort)point.Y)).WriteWord(20, unchecked((ushort)point.Z)).WriteWord(22, unchecked((ushort)ReadWord(0x190E))).WriteWord(24, (ushort)(suppliedYaw ?? Roll() % 4096)).WriteWord(28, unchecked((ushort)scale));
            if (callback == 0x60AE10)
            {
                model = model.WriteWord(30, unchecked((ushort)scale));
            }
            children = children with { Slots = children.Slots.SetItem(slot, model) };
        }

        int Model(ref NativeBattleModulePoolTask task, bool burst)
        {
            int frame = Word(task, 12);
            int scale = Word(task, 28);
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CE30(new(0, Word(task, 24), 0));
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale, scale, scale) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
            matrix = NativeBattleGfMatrices.Compose56C2F0(view, matrix);
            NativeBattleGfGeometryBatch batch;
            if (!burst && frame < 30)
            {
                int height = unchecked((Vector(0x1928).Y - Word(task, 18)) * (0x1000000 / scale)) >> 12;
                batch = NativeBattleStickyWeb153Drawing.Clipped(resources, matrix, projection, height, zsf3, zsf4, dqa, dqb);
            }
            else
            {
                bool fade = frame >= (burst ? 6 : 37);
                batch = NativeBattleModulePackets.EmitBatch572200((burst ? resources.Burst : resources.Web).Bytes, 0, default, matrix, projection, new(fade ? 0xF3u : burst ? 0x33u : 0u, 0, fade ? (frame - (burst ? 6 : 37)) << 9 : 0, null, null, 0, 17, 2), colors);
            }
            Append(batch);
            if (paused)
            {
                return 0;
            }
            if (burst)
            {
                int speed = Word(task, 30);
                task = task.WriteWord(28, unchecked((ushort)(scale + speed))).WriteWord(30, unchecked((ushort)(speed - speed / 7)));
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return frame + 1 >= (burst ? 14 : 45) ? 2 : 0;
        }

        int Particles(ref NativeBattleModulePoolTask task, int type)
        {
            int alive = 0;
            for (int particle = 0; particle < 100; particle++)
            {
                int offset = 0x88 + particle * 24;
                if ((ReadDword(offset) & type) == 0)
                {
                    continue;
                }
                int frame = ReadWord(offset + 4);
                int scale = ReadWord(offset + 6);
                NativeBattleVector position = Vector(offset + 8);
                NativeBattleModuleSpriteFrame sprite = resources.Sprite.SpriteFrame(frame);
                NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resources.Sprite, SpriteFrame = sprite, Phase = unchecked((ushort)frame), Anchor = new(position.X, position.Y, position.Z, 0), Scale = scale, DepthOffset = -(scale >> 4), Direction = 4096, SpriteFlags = 8 };
                Append(NativeBattleMeltdownPacketConversion.FromSprite(NativeBattleModulePackets.EmitSprite571DC0(draw, projection, NativeCamera.Roll, 17, 2)));
                if (paused)
                {
                    continue;
                }
                Put(offset + 4, frame + 1);
                if (sprite.NextOffset < 0)
                {
                    Dword(offset, 0);
                    continue;
                }
                Put(offset + 10, position.Y + ReadWord(offset + 18));
                if (type == 1)
                {
                    Put(offset + 8, position.X + ReadWord(offset + 16));
                    Put(offset + 12, position.Z + ReadWord(offset + 20));
                }
                else
                {
                    int speed = ReadWord(offset + 18);
                    Put(offset + 18, speed - (speed >> 4));
                }
                alive++;
            }
            if (paused)
            {
                return 0;
            }
            int counter = Word(task, 12);
            if (counter >= 0 && counter <= (type == 1 ? 22 : 8))
            {
                for (int birth = 0; birth < (type == 1 ? 1 : 8); birth++)
                {
                    int free = Enumerable.Range(0, 100).FirstOrDefault(index => ReadDword(0x88 + index * 24) == 0, -1);
                    if (free < 0)
                    {
                        break;
                    }
                    int offset = 0x88 + free * 24;
                    Dword(offset, type);
                    Put(offset + 4, 0);
                    Put(offset + 6, Roll() % (type == 1 ? 0x500 : 0x900) + (type == 1 ? 0xB00 : 0xD00));
                    NativeBattleVector center = Vector(type == 1 ? 0x1920 : 0x1908);
                    Store(offset + 8, center);
                    data.AsSpan((type == 1 ? 0x1920 : 0x1908) + 6, 2).CopyTo(data.AsSpan(offset + 14));
                    for (int axis = 0; axis < 3; axis++)
                    {
                        Put(offset + 8 + axis * 2, ReadWord(offset + 8 + axis * 2) + Roll() % (type == 1 ? 50 : 1000) - (type == 1 ? 25 : 500));
                    }
                    if (type == 1)
                    {
                        NativeBattleVector targetPoint = Vector(0x1908);
                        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(targetPoint.X - center.X, 0, targetPoint.Z - center.Z);
                        int speed = Roll() % 5 + 5;
                        Put(offset + 16, speed * direction.X >> 12);
                        Put(offset + 18, (speed * direction.Y >> 12) + Roll() % 20 - 10);
                        Put(offset + 20, speed * direction.Z >> 12);
                    }
                    else
                    {
                        Put(offset + 18, -10 - Roll() % 60);
                    }
                }
            }
            task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
            return counter + 1 >= 4 && alive == 0 ? 2 : 0;
        }

        int Thread(ref NativeBattleModulePoolTask task)
        {
            return AdvanceNativeStickyWebThread(ref task, graphics, data, view, projection, colors, zsf3, zsf4, paused, Append);
        }
        int Add(uint callback)
        {
            (children, int slot) = children.Append(callback);
            children = children with { Slots = children.Slots.SetItem(slot, children.Slots[slot].WriteWord(12, 0)) };
            return slot;
        }
        void Append(NativeBattleGfGeometryBatch batch, int tail = 0)
        {
            NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(state.Packets, "243BE6C", cursor, 0x10000, graphics.PrimitiveFrame!.Pending, batch, tail);
            state = state with { Packets = written.Arena };
            cursor = written.Cursor;
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
        short ReadWord(int offset) => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset));
        int ReadDword(int offset) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
        void Put(int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset), unchecked((short)value));
        void Dword(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), value);
        NativeBattleVector Vector(int offset) => new(ReadWord(offset), ReadWord(offset + 2), ReadWord(offset + 4));
        void Store(int offset, NativeBattleVector value)
        {
            Put(offset, value.X);
            Put(offset + 2, value.Y);
            Put(offset + 4, value.Z);
        }
        int Roll()
        {
            random = random.Draw(out ushort value);
            return value;
        }
        static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    }
}
