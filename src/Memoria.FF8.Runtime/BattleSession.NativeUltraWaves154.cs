using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeUltraWaves154(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleUltraWaves154Resources resources = effect.Binding.UltraWaves154Resources ?? throw new NotSupportedException("Native Ultra Waves requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleUltraWaves154State state = memory.UltraWaves154 ?? throw new NotSupportedException("Native Ultra Waves requires its retained process banks.");
        byte[] particles = state.Particles.ToArray();
        for (int particle = 0; particle < 100; particle++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(particles.AsSpan(particle * 24), 0);
        }
        NativeBattleEffectTextureState textures = memory.Textures!.Register5099A0(resources.Camera);
        NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
        NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Ultra Waves requires its original TIM allocation.");
        (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
        NativeEffectMemory = memory with { Textures = textures, UltraWaves154 = state with { Particles = particles.ToImmutableArray() } };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2404260, 0x2404240, 16, 2);
        (root, _) = root.Append(0x609590);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x2404BD0, 0x2404BE0, 36, 100);
        (children, _) = children.Append(0x607CC0);
        return graphics with { OriginalEffect = effect with { Pools = [root, children], ReturnedDescriptor = root.DescriptorAddress } };
    }

    private NativeBattleGraphicsState AdvanceNativeUltraWaves154(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleUltraWaves154Resources resources = effect.Binding.UltraWaves154Resources!;
        NativeBattleUltraWaves154State state = NativeEffectMemory!.UltraWaves154 ?? throw new NotSupportedException("Native Ultra Waves requires its retained process banks.");
        byte[] particles = state.Particles.ToArray();
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Ultra Waves requires its CRT stream.");
        NativeBattleEffectTaskPool roots = effect.Pools[0];
        NativeBattleEffectTaskPool children = effect.Pools[1];
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        bool paused = (graphics.Flags & 0x201) != 0;
        int cursor = 0;
        NativeBattleEffectTaskStep rootStep = roots.Advance((pool, slot) =>
        {
            roots = pool;
            NativeBattleModulePoolTask task = roots.Slots[slot];
            int frame = Word(task, 12);
            int result;
            if (task.Callback == 0x609590)
            {
                cursor = (frame & 1) == 0 ? 0 : 0xC000;
                NativeBattleEffectTaskStep childStep = children.Advance((childPool, childSlot) =>
                {
                    children = childPool;
                    NativeBattleModulePoolTask child = children.Slots[childSlot];
                    int childResult = child.Callback switch
                    {
                        0x607CC0 => Controller(ref child),
                        0x608530 => Emitter(ref child),
                        0x608040 => Warp(ref child),
                        _ => throw new NotSupportedException("Native Ultra Waves selected an unrelated child callback.")
                    };
                    child = child with { Next = children.Slots[childSlot].Next };
                    return new(children with { Slots = children.Slots.SetItem(childSlot, child) }, childResult);
                });
                children = childStep.Pool;
                if (childStep.Survivors == 0)
                {
                    (roots, int wait) = roots.Append(0x6095F0);
                    roots = roots with { Slots = roots.Slots.SetItem(wait, roots.Slots[wait].WriteWord(12, 0).WriteWord(14, 0)) };
                }
                result = childStep.Survivors == 0 ? 2 : 0;
            }
            else if (task.Callback == 0x6095F0)
            {
                if (frame == 1)
                {
                    uint completion = roots.BankAddress + (uint)(slot * roots.Stride + 14);
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(0x24224D4, completion, (address, value) =>
                    {
                        NativeBattleModulePoolTask owner = roots.Slots[slot];
                        owner = owner with { Payload = owner.Payload.SetItem(2, value) };
                        roots = roots with { Slots = roots.Slots.SetItem(slot, owner) };
                    });
                    graphics = graphics with { Auxiliary = auxiliary };
                    task = task with { Payload = task.Payload.SetItem(2, roots.Slots[slot].Payload[2]) };
                }
                result = task.ReadWord(14) != 0 ? 2 : 0;
            }
            else
            {
                throw new NotSupportedException("Native Ultra Waves selected an unrelated root callback.");
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1))) with { Next = roots.Slots[slot].Next };
            return new(roots with { Slots = roots.Slots.SetItem(slot, task) }, result);
        });
        roots = rootStep.Pool;
        NativeEffectMemory = NativeEffectMemory! with { UltraWaves154 = state with { Particles = particles.ToImmutableArray() } };
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = [roots, children], ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : roots.DescriptorAddress } };

        int Controller(ref NativeBattleModulePoolTask task)
        {
            if (paused)
            {
                return 0;
            }
            int frame = Word(task, 12);
            if (frame == 0)
            {
                state = NativeBattleUltraWaves154Grid.Initialize(state);
            }
            if (frame == 1)
            {
                int actor = effect.Parameters!.Actor78;
                NativeBattleActionMatrix world = graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Native Ultra Waves requires its caster world matrix.");
                for (int emitter = 0; emitter < 4; emitter++)
                {
                    int slot = Add(0x608530);
                    NativeBattleVector anchor = ReadNativeGraphicsBone(graphics, actor, 0xF1, 4096);
                    (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(world, resources.EmitterVectors[emitter * 2]);
                    NativeBattleVector offset = new(unchecked((short)x), unchecked((short)y), unchecked((short)z));
                    NativeBattleVector position = new(unchecked((short)(anchor.X + offset.X)), unchecked((short)(anchor.Y + offset.Y)), unchecked((short)(anchor.Z + offset.Z)));
                    (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(world, resources.EmitterVectors[emitter * 2 + 1]);
                    var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(unchecked((short)(anchor.X - position.X + unchecked((short)x))), unchecked((short)(anchor.Y - position.Y + unchecked((short)y))), unchecked((short)(anchor.Z - position.Z + unchecked((short)z))));
                    NativeBattleModulePoolTask child = children.Slots[slot].WriteWord(14, (ushort)(1 << (emitter * 2))).WriteWord(16, unchecked((ushort)position.X)).WriteWord(18, unchecked((ushort)position.Y)).WriteWord(20, unchecked((ushort)position.Z)).WriteWord(24, unchecked((ushort)normalized.X)).WriteWord(26, unchecked((ushort)normalized.Y)).WriteWord(28, unchecked((ushort)normalized.Z)).WriteWord(32, (ushort)(Roll() % 4096)).WriteWord(34, 0);
                    children = children with { Slots = children.Slots.SetItem(slot, child) };
                    task = task.WriteWord(16, unchecked((ushort)anchor.X)).WriteWord(18, unchecked((ushort)anchor.Y)).WriteWord(20, unchecked((ushort)anchor.Z));
                }
                PublishNativeModuleSound(new(resources.Sound, 0, null) { Pan = 128 });
            }
            if (frame == 8)
            {
                Add(0x608040);
            }
            if (frame == 36)
            {
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = nativeActionPackets.ReadHeaderContacts506BA0(effect.Parameters!.HeaderIndex7C) });
                graphics = graphics with { PendingContacts = pending };
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return frame + 1 > 40 ? 2 : 0;
        }

        int Emitter(ref NativeBattleModulePoolTask task)
        {
            return AdvanceNativeUltraWavesEmitter(ref task, particles, resources, view, projection, colors, paused, Append);
        }
        int Warp(ref NativeBattleModulePoolTask task)
        {
            int frame = Word(task, 12);
            if (frame < 35)
            {
                (state, graphics, cursor) = CaptureNativeUltraWaves154(state, graphics, resources, cursor);
                state = state with { Grid = NativeBattleUltraWaves154Grid.Wave(state.Grid, frame) };
                byte buffer = NativeEffectMemory!.PrimitiveProcess!.Buffer80 ?? throw new NotSupportedException("Native Ultra Waves requires its current display buffer.");
                (state, NativeBattleLogicalOrdering ordering) = NativeBattleUltraWaves154Grid.Draw(state, graphics.PrimitiveFrame!.Pending, projection, buffer, resources.WarpColors[frame]);
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = ordering } };
            }
            if (paused)
            {
                return 0;
            }
            if (frame is 0 or 35)
            {
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Ultra Waves requires its four stage groups.");
                NativeStage = stage with { Groups = stage.Groups.Select(group => group with { Flags = frame == 0 ? (byte)(group.Flags & 0xFD) : (byte)(group.Flags | 2) }).ToImmutableArray() };
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return frame + 1 > 35 ? 2 : 0;
        }
        void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(state.Packets, "24224D4", cursor, 0x18000, graphics.PrimitiveFrame!.Pending, batch);
            state = state with { Packets = written.Arena };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            cursor = written.Cursor;
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
        static short Word(NativeBattleModulePoolTask owner, int offset) => unchecked((short)owner.ReadWord(offset));
    }
}
