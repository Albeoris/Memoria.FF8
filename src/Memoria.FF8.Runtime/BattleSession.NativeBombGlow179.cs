using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeBombGlow179(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleBombGlow179Resources resources = effect.Binding.BombGlow179Resources ?? throw new NotSupportedException("Native Bomb glow requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleBombGlow179State state = memory.BombGlow179 ?? throw new NotSupportedException("Native Bomb glow requires its retained executable particle bank.");
        byte[] particles = state.Particles.ToArray();
        for (int particle = 0; particle < 100; particle++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(particles.AsSpan(particle * 24), 0);
        }
        NativeBattleEffectTextureState textures = memory.Textures!.Register5099A0(resources.Camera);
        NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
        NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Bomb glow requires its original TIM.");
        (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
        NativeEffectMemory = memory with { Textures = textures, BombGlow179 = state with { Particles = particles.ToImmutableArray() } };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2309570, 0x2309560, 16, 1);
        (root, _) = root.Append(0x5CED70);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x2309EE0, 0x2309EF0, 36, 100);
        (children, _) = children.Append(0x5CDFF0);
        return graphics with { OriginalEffect = effect with { Pools = [root, children], ReturnedDescriptor = root.DescriptorAddress } };
    }

    private NativeBattleGraphicsState AdvanceNativeBombGlow179(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleBombGlow179Resources resources = effect.Binding.BombGlow179Resources!;
        NativeBattleBombGlow179State state = NativeEffectMemory!.BombGlow179 ?? throw new NotSupportedException("Native Bomb glow requires its retained particle bank.");
        byte[] data = state.Particles.ToArray();
        NativeBattleEffectTaskPool roots = effect.Pools[0];
        NativeBattleEffectTaskPool children = effect.Pools[1];
        NativeBattleModulePoolTask root = roots.Slots[0];
        int cursor = (root.ReadWord(12) & 1) == 0 ? 0 : 0x8000;
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Bomb glow requires its CRT random stream.");
        bool paused = (graphics.Flags & 0x201) != 0;
        int actor = effect.Parameters!.Actor78;
        NativeBattleEffectTaskStep advanced = children.Advance((pool, slot) =>
        {
            children = pool;
            NativeBattleModulePoolTask task = pool.Slots[slot];
            int result = task.Callback switch
            {
                0x5CDFF0 => Director(ref task),
                0x5CE0F0 => Glow(ref task),
                0x5CEB10 => Sparks(ref task),
                _ => throw new NotSupportedException("Native Bomb glow selected an unrelated callback.")
            };
            task = task with { Next = children.Slots[slot].Next };
            return new(children with { Slots = children.Slots.SetItem(slot, task) }, result);
        });
        children = advanced.Pool;
        root = root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)));
        roots = roots with { Slots = roots.Slots.SetItem(0, root) };
        if (advanced.Survivors == 0)
        {
            roots = roots.Advance((pool, _) => new(pool, 2)).Pool;
        }
        NativeEffectMemory = NativeEffectMemory! with { BombGlow179 = state with { Particles = data.ToImmutableArray() } };
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = [roots, children], ReturnedDescriptor = advanced.Survivors == 0 ? 0u : roots.DescriptorAddress } };

        int Director(ref NativeBattleModulePoolTask task)
        {
            if (paused)
            {
                return 0;
            }
            int frame = Word(task, 12);
            if (frame == 1)
            {
                int glow = Add(0x5CE0F0);
                children = children with { Slots = children.Slots.SetItem(glow, children.Slots[glow].WriteWord(26, (ushort)(graphics.Models[actor].Flags0 & 4))) };
                Add(0x5CEB10);
                NativeBattleModuleAnchor anchor = NativeBattlePoseMatrices.ReadAnchor(graphics.Models[actor], NativeGraphicsPoseResource(graphics, actor).PoseSource!);
                PublishNativeModuleSound(resources.Sound, 0, anchor);
            }
            if (frame == 35)
            {
                int first = nativeActionPackets.Headers[effect.Parameters.HeaderIndex7C].FirstContactIndex ?? throw new InvalidDataException("Native Bomb glow requires its actual first contact.");
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(first)] });
                graphics = graphics with { PendingContacts = pending };
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return frame + 1 > 40 ? 2 : 0;
        }
        int Glow(ref NativeBattleModulePoolTask task)
        {
            int frame = Word(task, 12);
            if (frame < 30)
            {
                int fade = frame < 4 ? (4 - frame) << 10 : frame >= 22 ? (frame - 22) << 9 : 0;
                uint color = definition.NativeGraphics!.Rules.SummonColors!.Interpolate(resources.Colors[frame % 8], fade, 0);
                graphics = graphics with { Models = graphics.Models.SetItem(actor, graphics.Models[actor] with { Flags0 = (ushort)(graphics.Models[actor].Flags0 | 4) }) };
                graphics = DrawNativeBombActor(graphics, actor, color, frame * 72, true);
            }
            if (paused)
            {
                return 0;
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            if (frame + 1 <= 30)
            {
                return 0;
            }
            graphics = graphics with { Models = graphics.Models.SetItem(actor, graphics.Models[actor] with { Flags0 = (ushort)((graphics.Models[actor].Flags0 & 0xFFFB) | task.ReadWord(26)) }) };
            return 2;
        }
        int Sparks(ref NativeBattleModulePoolTask task)
        {
            int alive = 0;
            for (int particle = 0; particle < 100; particle++)
            {
                int offset = particle * 24;
                if ((Read32(offset) & 1) == 0)
                {
                    continue;
                }
                NativeBattleModuleSpriteFrame sprite = resources.Spark.SpriteFrame(Read(offset + 4));
                short scale = Read(offset + 6);
                NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resources.Spark, SpriteFrame = sprite, Phase = unchecked((ushort)Read(offset + 4)), Anchor = new(Read(offset + 8), Read(offset + 10), Read(offset + 12), 0), Scale = unchecked((ushort)scale), DepthOffset = -(scale >> 4), Direction = Read(offset + 16), SpriteYScale = Read(offset + 18), SpriteFlags = 2 };
                NativeBattleGfGeometryBatch batch = NativeBattleMeltdownPacketConversion.FromSprite(NativeBattleModulePackets.EmitSprite571DC0(draw, projection, NativeCamera.Roll, 17, 2));
                NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(state.Packets, "230AD0C", cursor, 0x10000, graphics.PrimitiveFrame!.Pending, batch);
                cursor = written.Cursor;
                state = state with { Packets = written.Arena };
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
                if (paused)
                {
                    continue;
                }
                Put(offset + 4, Read(offset + 4) + 1);
                if (sprite.NextOffset < 0)
                {
                    Put32(offset, 0);
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
            int frame = Word(task, 12);
            if (frame is >= 0 and <= 22)
            {
                int bones = NativeGraphicsPoseResource(graphics, actor).PoseSource!.BoneCount;
                for (int birth = 0; birth < 3; birth++)
                {
                    int free = Enumerable.Range(0, 100).FirstOrDefault(index => Read32(index * 24) == 0, -1);
                    if (free < 0)
                    {
                        break;
                    }
                    int offset = free * 24;
                    Put32(offset, 1);
                    Put(offset + 4, 0);
                    Put(offset + 6, Roll() % 0x6A0 + unchecked((ushort)(frame + 64)) * 8);
                    short fraction = (short)(Roll() % 4096);
                    byte bone = checked((byte)(Roll() % bones));
                    NativeBattleVector point = ReadNativeGraphicsBone(graphics, actor, bone, fraction);
                    Put(offset + 8, point.X + Roll() % 440 - 220);
                    Put(offset + 10, point.Y + Roll() % 440 - 220);
                    Put(offset + 12, point.Z + Roll() % 440 - 220);
                    Put(offset + 16, (Read(offset + 8) & 1) != 0 ? 4096 : -4096);
                    Put(offset + 18, (Read(offset + 10) & 1) != 0 ? 4096 : -4096);
                }
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return frame + 1 >= 4 && alive == 0 ? 2 : 0;
        }
        int Add(uint callback)
        {
            (children, int slot) = children.Append(callback);
            children = children with { Slots = children.Slots.SetItem(slot, children.Slots[slot].WriteWord(12, 0)) };
            return slot;
        }
        int Roll()
        {
            random = random.Draw(out ushort value);
            return value;
        }
        short Read(int offset) => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset));
        int Read32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
        void Put(int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset), unchecked((short)value));
        void Put32(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), value);
        static short Word(NativeBattleModulePoolTask owner, int offset) => unchecked((short)owner.ReadWord(offset));
    }
}
