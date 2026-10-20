using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeRegen147(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleRegen147Resources resources = effect.Binding.Regen147Resources ?? throw new NotSupportedException("Native Regen requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleRegen147State state = memory.Regen147 ?? throw new NotSupportedException("Native Regen requires its retained process storage.");
        state.Validate();
        NativeBattleEffectArena prepared = state.Storage.MarkUnknown(0xE48, 4).MarkUnknown(0xE50, 4).MarkUnknown(8, 12).MarkUnknown(0x18, 0x20);
        for (int slot = 0; slot < 100; slot++)
        {
            prepared = prepared.MarkUnknown(0x38 + slot * 0x24, 12);
        }
        state = state with { Storage = prepared };
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures!.Register5099A0(resources.Camera);
            NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
            NativeBattleEffectInitializerAsset texture = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Regen requires its original TIM.");
            (textures, _) = textures.EnqueueTim505E30(new(texture.Name, texture.Data));
            memory = memory with { Textures = textures };
        }
        NativeBattleEffectTaskPool roots = RegenPool(state.Storage, 0x2476678, 0x2476668, 0x10, 1);
        (roots, int root) = roots.Append(0x614D10);
        roots = roots with { Slots = roots.Slots.SetItem(root, roots.Slots[root].WriteWord(12, 0)) };
        NativeBattleEffectTaskPool tasks = RegenPool(state.Storage, 0x2476688, 0x2476698, 0x24, 100);
        (tasks, int first) = tasks.Append(0x614350);
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters.HeaderIndex7C];
        int contact = header.FirstContactIndex ?? throw new InvalidDataException("Native Regen requires its first contact.");
        byte actor = nativeActionPackets.ReadContact(contact).Primary.ActorIndex;
        tasks = tasks with { Slots = tasks.Slots.SetItem(first, tasks.Slots[first].WriteWord(12, 0).WriteWord(14, 0).WriteWord(32, actor)) };
        state = state with { Storage = RegenDword(RegenDword(state.Storage, 0, actor), 0xE4C, effect.Parameters.Actor78) };
        NativeEffectMemory = memory with { Regen147 = state };
        return graphics with { OriginalEffect = effect with { Pools = [roots, tasks], ReturnedDescriptor = 0x2476678 } };
    }

    private NativeBattleGraphicsState AdvanceNativeRegen147(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleRegen147Resources resources = effect.Binding.Regen147Resources ?? throw new NotSupportedException("Native Regen requires its original resources.");
        NativeBattleRegen147State state = NativeEffectMemory!.Regen147 ?? throw new NotSupportedException("Native Regen requires its retained process storage.");
        NativeBattleEffectTaskPool roots = effect.Pools[0];
        NativeBattleEffectTaskPool tasks = effect.Pools[1];
        NativeBattleModulePoolTask root = roots.Slots[0];
        if (roots.DescriptorAddress != 0x2476678 || tasks.DescriptorAddress != 0x2476688 || root.Callback != 0x614D10)
        {
            throw new InvalidDataException("Native Regen lost its original task queues.");
        }
        int cursor = (root.ReadByte(12) & 1) == 0 ? 0x3558 : 0xF558;
        bool paused = (graphics.Flags & 0x201) != 0;
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        NativeBattleEffectTaskStep advanced = tasks.Advance((pool, slot) =>
        {
            tasks = pool;
            NativeBattleModulePoolTask node = pool.Slots[slot];
            bool complete = node.Callback switch
            {
                0x614350 => Director(ref node),
                0x614720 => Envelope(ref node),
                0x6148E0 => Leaf(ref node),
                _ => throw new NotSupportedException("Native Regen reached an unrelated callback.")
            };
            node = node with { Next = tasks.Slots[slot].Next };
            tasks = tasks with { Slots = tasks.Slots.SetItem(slot, node) };
            return new(tasks, complete ? 2 : 0);
        });
        tasks = advanced.Pool;
        roots = roots with { Slots = roots.Slots.SetItem(0, root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)))) };
        if (advanced.Survivors == 0)
        {
            roots = roots.Advance((pool, _) => new(pool, 2)).Pool;
        }
        NativeBattleEffectArena storage = state.Storage;
        foreach (NativeBattleEffectTaskPool pool in new[] { roots, tasks })
        {
            for (int slot = 0; slot < pool.Slots.Length; slot++)
            {
                storage = storage.Write(checked((int)(pool.BankAddress - 0x2476660) + slot * pool.Stride + 12), pool.Slots[slot].Payload.AsSpan());
            }
        }
        state = state with { Storage = RegenDword(storage, 0x1B558, unchecked((int)(0x2476660u + (uint)cursor))) };
        NativeEffectMemory = NativeEffectMemory with { Regen147 = state };
        return graphics with { OriginalEffect = effect with { Pools = [roots, tasks], ReturnedDescriptor = advanced.Survivors == 0 ? 0u : 0x2476678u } };

        short S(NativeBattleModulePoolTask node, int offset) => unchecked((short)node.ReadWord(offset));
        NativeBattleModulePoolTask W(NativeBattleModulePoolTask node, int offset, int value) => node.WriteWord(offset, unchecked((ushort)value));
        int Rand()
        {
            nativeCrtRandom = (nativeCrtRandom ?? throw new NotSupportedException("Native Regen requires its CRT random owner.")).Draw(out ushort value);
            return value;
        }
        void Set(int slot, NativeBattleModulePoolTask node) => tasks = tasks with { Slots = tasks.Slots.SetItem(slot, node) };
        (int Slot, NativeBattleModulePoolTask Node) Add(uint callback)
        {
            (tasks, int slot) = tasks.Append(callback);
            return (slot, tasks.Slots[slot]);
        }
        void Emit(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleRegionalPacketWrite result = NativeBattleRegionalPacketArena.Append(state.Storage, "2476660", cursor, 0x1B558, graphics.PrimitiveFrame!.Pending, batch);
            state = state with { Storage = result.Arena };
            cursor = result.Cursor;
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = result.Ordering } };
        }
        void Sprite(NativeBattleVector position, short size, int frame, NativeBattleActionMatrix? matrix = null, ushort flags = 0, uint color = 0x808080)
        {
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resources.Leaf, SpriteFrame = resources.Leaf.ProjectedFrame571C80(unchecked((ushort)frame)), Anchor = new(position.X, position.Y, position.Z, 0), Scale = size, DepthOffset = -(size >> 3), SpriteFlags = flags, SpriteColor = color };
            Emit(NativeBattleMeltdownPacketConversion.FromSprite(NativeBattleModulePackets.EmitSprite571DC0(draw, projection, NativeCamera!.Roll, 17, 2, matrix)));
        }
        bool Director(ref NativeBattleModulePoolTask node)
        {
            if (paused)
            {
                return false;
            }
            int age = S(node, 12);
            int actor = S(node, 32);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if (age == 0)
            {
                NativeBattleVector anchor = ReadNativeDefaultPosition571400(graphics, actor);
                node = WindStore(node, 16, anchor);
                node = W(node, 22, model.Transform!.Angle36);
                int scale = Math.Min(unchecked(model.Transform.Scale38 * 5600 + 5000) >> 12, 2200);
                node = W(node, 24, unchecked(scale << 12) / 1000);
                NativeBattleVector bone = ReadNativeGraphicsBone(graphics, actor, 0xF0, 0x1000);
                node = W(node, 26, (bone.Y - 900) / 24);
            }
            if (age == 1)
            {
                for (int kind = 0; kind < 2; kind++)
                {
                    (int slot, NativeBattleModulePoolTask child) = Add(0x614720);
                    child = WindStore(child, 16, WindVector(node, 16));
                    child = W(W(child, 12, 0), 14, kind);
                    child = W(child, 18, S(node, 22));
                    child = W(child, 22, S(node, 22));
                    child = W(child, 24, Rand() % 4096);
                    int spin = Rand() % 60 + 30;
                    child = W(W(W(W(W(child, 26, spin), 28, 0x1800), 30, kind == 0 ? -0x100 : -0xDB), 32, 0), 34, kind == 0 ? 0x66 : 0x57);
                    Set(slot, child);
                }
            }
            if (age == 2)
            {
                int radius = Math.Min((unchecked(model.Transform!.Scale38 * 4000) >> 12) + 200, 1000);
                int angle = Rand() % 4096;
                for (int leaf = 0; leaf < 8; leaf++)
                {
                    (int slot, NativeBattleModulePoolTask child) = Add(0x6148E0);
                    angle += 512;
                    child = W(child, 12, 0);
                    child = W(W(W(child, 16, S(node, 16)), 18, S(node, 22)), 20, S(node, 20));
                    child = W(child, 24, unchecked(NativeBattleCameraMath.Cosine(angle) * (radius / 2)) >> 12);
                    child = W(child, 26, -30 - Rand() % 70);
                    child = W(child, 28, unchecked(NativeBattleCameraMath.Sine(angle) * (radius / 2)) >> 12);
                    child = W(child, 30, leaf);
                    child = W(child, 32, Rand() % 1024 + 4096);
                    child = W(child, 22, S(node, 14));
                    Set(slot, child);
                    int trail = TrailOffset(S(node, 14), leaf);
                    for (int index = 0; index < 16; index++)
                    {
                        Write(trail + index * 20, -1);
                    }
                }
            }
            if (age == 28)
            {
                int action = S(node, 14);
                int contact = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C + action].FirstContactIndex!.Value;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
                int next = action + 1;
                if (next <= nativeActionPackets.Headers[effect.Parameters.HeaderIndex7C].ReadByte(17))
                {
                    (int slot, NativeBattleModulePoolTask child) = Add(0x614350);
                    int first = nativeActionPackets.Headers[effect.Parameters.HeaderIndex7C + next].FirstContactIndex!.Value;
                    child = W(W(W(child, 12, 0), 14, next), 32, nativeActionPackets.ReadContact(first).Primary.ActorIndex);
                    Set(slot, child);
                }
            }
            if (age == 1)
            {
                NativeBattleVector anchor = ReadNativeDefaultPosition571400(graphics, actor);
                PublishNativeModuleSound(resources.Sound, 0, new(anchor.X, anchor.Y, anchor.Z, 0));
            }
            node = W(node, 12, age + 1);
            return S(node, 12) > 48;
        }
        bool Envelope(ref NativeBattleModulePoolTask node)
        {
            int age = S(node, 12);
            int fade = NativeBattleRegen147Math.EnvelopeFade((short)age, S(node, 14));
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, S(node, 24), 0)) with { X = S(node, 16), Y = S(node, 18), Z = S(node, 20) };
            local = NativeBattlePoseMatrices.Scale56BEF0(local, S(node, 28), S(node, 32), S(node, 28));
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, local);
            Emit(NativeBattleModulePackets.EmitBatch572200(resources.Envelope.Bytes, 0, resources.Envelope.Bytes.Slice(8, resources.Envelope.Bytes.Length - 8), matrix, projection, new(0xF0, 0, fade, null, null, null, 17, 2), colors));
            if (paused)
            {
                return false;
            }
            node = W(node, 28, S(node, 28) + S(node, 30));
            node = W(node, 24, S(node, 24) + S(node, 26));
            int growth = S(node, 34);
            node = W(node, 32, S(node, 32) + growth);
            node = W(node, 34, growth / 12 + growth);
            node = W(node, 12, age + 1);
            return S(node, 12) >= 30;
        }
        int TrailOffset(int action, int leaf) => checked(0xE58 + (leaf + action * 8) * 0x140);
        short Read(int offset) => BinaryPrimitives.ReadInt16LittleEndian(state.Storage.Read(offset, 2));
        void Write(int offset, int value)
        {
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, unchecked((short)value));
            state = state with { Storage = state.Storage.Write(offset, bytes) };
        }
        bool Leaf(ref NativeBattleModulePoolTask node)
        {
            if (S(node, 14) > 0)
            {
                if (!paused)
                {
                    node = W(node, 14, S(node, 14) - 1);
                }
                return false;
            }
            int age = S(node, 12);
            NativeBattleVector position = WindVector(node, 16);
            short size = S(node, 32);
            if (age < 14)
            {
                Sprite(position, size, age >= 8 ? age - 8 : 0);
                NativeBattleActionMatrix shadow = NativeBattleGfMatrices.Euler56CE30(new(1024, 0, 0)) with { X = position.X, Y = 0, Z = position.Z };
                shadow = NativeBattlePoseMatrices.Scale56BEF0(shadow, size * 2, size * 2, size * 2);
                byte grey = unchecked((byte)(0x70 - 6 * age));
                Sprite(position, size, 1, NativeBattleGfMatrices.Compose56C2F0(view, shadow), 4, (uint)(grey * 0x10101));
            }
            int trail = TrailOffset(S(node, 22), S(node, 30));
            int frame = 8;
            for (int index = 0; index < 16; index++)
            {
                int offset = trail + index * 20;
                int counter = Read(offset);
                if (counter < 0)
                {
                    continue;
                }
                if (counter < 10)
                {
                    frame = counter;
                }
                Sprite(new(Read(offset + 4), Read(offset + 6), Read(offset + 8)), Read(offset + 2), frame);
                if (paused)
                {
                    continue;
                }
                Write(offset, counter + 1);
                if (Read(offset) >= 26)
                {
                    Write(offset, -1);
                }
                else if (age < 14)
                {
                    Write(offset + 2, Read(offset + 2) - 16);
                    Write(offset + 6, Read(offset + 6) + Read(offset + 10));
                }
                else
                {
                    Write(offset + 2, Read(offset + 2) - (Read(offset + 2) >> 4));
                    for (int axis = 0; axis < 3; axis++)
                    {
                        Write(offset + 4 + axis * 2, Read(offset + 4 + axis * 2) + Read(offset + 12 + axis * 2));
                        Write(offset + 12 + axis * 2, Read(offset + 12 + axis * 2) + (Read(offset + 12 + axis * 2) >> 6));
                    }
                }
            }
            if (paused)
            {
                return false;
            }
            if (age is >= 2 and <= 11)
            {
                int index = 0;
                while (index < 16 && Read(trail + index * 20) >= 0)
                {
                    index++;
                }
                if (index < 16)
                {
                    int offset = trail + index * 20;
                    Write(offset, 1);
                    Write(offset + 2, Rand() % 1536 + 3328);
                    Write(offset + 4, position.X + Rand() % 200 - 100);
                    Write(offset + 6, position.Y + Rand() % 100 - 50);
                    Write(offset + 8, position.Z + Rand() % 200 - 100);
                    Write(offset + 10, -10 - Rand() % 30);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        Write(offset + 12 + axis * 2, Rand() % 40 - 20);
                    }
                    Write(offset + 18, Rand() % 12 + 12);
                }
            }
            for (int axis = 0; axis < 3; axis++)
            {
                int velocity = S(node, 24 + axis * 2);
                node = W(node, 16 + axis * 2, S(node, 16 + axis * 2) + velocity);
                node = W(node, 24 + axis * 2, axis == 1 ? velocity + (velocity >> 3) : velocity - (velocity >> 2));
            }
            node = W(node, 12, age + 1);
            return S(node, 12) >= 40;
        }
    }

    private static NativeBattleEffectTaskPool RegenPool(NativeBattleEffectArena storage, uint descriptor, uint address, ushort stride, ushort capacity)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(descriptor, address, stride, capacity);
        return pool with { Slots = pool.Slots.Select((node, slot) => node with { Payload = storage.Read(checked((int)(address - 0x2476660) + slot * stride + 12), stride - 12).ToImmutableArray() }).ToImmutableArray() };
    }

    private static NativeBattleEffectArena RegenDword(NativeBattleEffectArena storage, int offset, int value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return storage.Write(offset, bytes);
    }
}
