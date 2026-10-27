using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeZombie(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Zombie requires its selected effect.");
        NativeBattleZombieResources resources = effect.Binding.ZombieResources ?? throw new NotSupportedException("Native Zombie requires its original resources.");
        if (effect.Binding is not { Effect: 114, Entry: 0x6C99C0 } || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native Zombie requires its exact original entry and context.");
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Zombie requires current retained memory.");
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Zombie requires its actual texture queue.");
            NativeBattleEffectInitializerAsset texture = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Zombie requires its original loaded TIM.");
            (textures, _) = textures.EnqueueTim505E30(new(texture.Name, texture.Data));
            textures = textures.Register5099A0(resources.Animation);
            NativeEffectMemory = memory with { Textures = textures };
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Zombie requires its current camera.")).RegisterControlResource(textures.Animation);
        }
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2521850, 0x2521828, 0x14, 1);
        (root, _) = root.Append(0x6C9A60);
        return graphics with { OriginalEffect = effect with { Pools = [root], ReturnedDescriptor = 0x2521850, DrawArenaOffset = null } };
    }

    private NativeBattleGraphicsState AdvanceNativeZombie(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Zombie requires its selected effect.");
        NativeBattleZombieResources resources = effect.Binding.ZombieResources ?? throw new NotSupportedException("Native Zombie requires its original resources.");
        if (effect.ReturnedDescriptor == 0)
        {
            return graphics;
        }
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Native Zombie requires its original action context.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Zombie requires its current projection.");
        NativeBattleActionMatrix camera = projection.View ?? throw new NotSupportedException("Native Zombie requires its current camera matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Zombie requires its original color interpolation constants.");
        NativeBattleEffectTaskPool[] pools = effect.Pools.ToArray();
        if (pools.Length is not (1 or 2) || pools[0].DescriptorAddress != 0x2521850)
        {
            throw new InvalidDataException("Native Zombie lost its root/target queue owners.");
        }
        NativeBattleModulePoolTask root = pools[0].Slots[0];
        effect = effect with { DrawArenaOffset = root.ReadDword(16) != 0 ? 0x1600 : 0x19600 };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        if (root.ReadWord(12) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                root = root.WriteByte(15, 1);
                pools = [pools[0], NativeBattleEffectTaskPool.Initialize(0x2521840, 0x20DFAB8, 0x710, 3)];
            }
            int action = root.ReadByte(14);
            if (action <= nativeActionPackets.Headers[parameters.HeaderIndex7C].ReadByte(17))
            {
                if (pools[1].Slots.Any(task => (task.Flags & 1) != 0 && Word(task, 12) <= 40))
                {
                    root = root.WriteWord(12, 0);
                }
                else if (pools[1].Slots.Any(task => (task.Flags & 1) == 0))
                {
                    int first = nativeActionPackets.Headers[parameters.HeaderIndex7C + action].FirstContactIndex ?? throw new NotSupportedException("Native Zombie requires its original first action target.");
                    byte model = nativeActionPackets.ReadContact(first).Primary.ActorIndex;
                    (pools[1], int slot) = pools[1].Append(0x6C9BC0);
                    NativeBattleModulePoolTask task = pools[1].Slots[slot].ClearPayload(0x704);
                    task = task.WriteWord(14, checked((ushort)action)).WriteDword(16, checked(0x1D972C0u + model * 156u));
                    NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(16, 0x6F4));
                    Store(1, slot, Workspace(task, workspace.Initialize7016B0(resources.Layout, 0x6F4)));
                    root = root.WriteByte(14, unchecked((byte)(action + 1)));
                }
            }
        }
        int survivors = -1;
        if (root.ReadByte(15) != 0)
        {
            NativeBattleEffectTaskStep step = pools[1].Advance((pool, slot) =>
            {
                pools[1] = pool;
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int age = Word(task, 12);
                int actor = NativeBattleStatusMagicController.ModelIndex(task.ReadDword(16));
                if (age >= 30)
                {
                    Mark(actor, true);
                }
                NativeBattleGraphicsModel model = graphics.Models[actor];
                NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Zombie requires its actual target skeleton.");
                NativeBattleModuleAnchor anchor = NativeBattlePoseMatrices.ReadAnchor(model, pose);
                task = task.WriteWord(20, unchecked((ushort)anchor.X)).WriteWord(22, unchecked((ushort)anchor.Y)).WriteWord(24, unchecked((ushort)anchor.Z)).WriteWord(26, unchecked((ushort)anchor.W));
                short size = model.Transform?.Scale38 ?? throw new NotSupportedException("Native Zombie requires its current signed target size WORD.");
                int reach = (size >> 1) + 2048;
                int depth = unchecked(-2048 - size) >> 3;
                NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(16, 0x6F4));
                NativeBattleGfAnimationAdvance played = workspace.Advance701970(resources.Layout, 0, (owner, node) =>
                {
                    NativeBattleZombieDraw draw = NativeBattleZombiePart.Draw(NativeEffectMemory ?? throw new NotSupportedException("Native Zombie requires its current magic memory."), NativeFileStaging ?? throw new NotSupportedException("Native Zombie requires its current temporary memory."), resources, node, new(anchor.X, anchor.Y, anchor.Z), reach, depth, projection, colors);
                    NativeEffectMemory = draw.Memory;
                    NativeFileStaging = draw.Staging;
                    Append(draw.Packets);
                    return owner;
                });
                task = Workspace(task, played.Workspace);
                if ((uint)(age - 30) < 30 && (parameters.Flags79 & 2) == 0)
                {
                    (graphics, effect) = DrawNativeZombieOverlay(graphics, actor, age, reach, resources, projection, effect);
                }
                if (age == 0)
                {
                    PublishNativeModuleSound(new(resources.Sound, 0x101, anchor));
                }
                if (age == 54)
                {
                    NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[parameters.HeaderIndex7C + task.ReadWord(14)];
                    int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Zombie result requires its original target list.");
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    for (int index = 0; index < header.ReadByte(16); index++)
                    {
                        graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(first + index)] });
                    }
                    graphics = graphics with { PendingContacts = pending };
                }
                bool finished = age >= 60;
                if (finished)
                {
                    Mark(actor, false);
                }
                else
                {
                    task = task.WriteWord(12, unchecked((ushort)(age + 1)));
                }
                Store(1, slot, task);
                return new(pools[1], finished ? 2 : 0);
            });
            pools[1] = step.Pool;
            survivors = step.Survivors;
        }
        bool complete = root.ReadByte(15) != 0 && survivors == 0;
        if (!complete)
        {
            root = root.WriteWord(12, (ushort)((root.ReadWord(12) + 1) % 10));
        }
        Store(0, 0, root);
        if (complete)
        {
            pools[0] = pools[0].Advance((pool, _) => new(pool, 2)).Pool;
        }
        return graphics with { OriginalEffect = effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = complete ? 0u : 0x2521850u } };

        void Store(int bank, int slot, NativeBattleModulePoolTask task)
        {
            pools[bank] = pools[bank].WriteSlot(slot, task);
        }
        void Mark(int actor, bool marked)
        {
            foreach (int index in NativeBattleModelRings.Walk(graphics.Models, actor))
            {
                NativeBattleGraphicsModel model = graphics.Models[index];
                graphics = graphics with { Models = graphics.Models.SetItem(index, model with { Flags0 = marked ? (ushort)(model.Flags0 | 8) : (ushort)(model.Flags0 & 0xFFF7) }) };
            }
        }
        void Append(NativeBattleGfGeometryBatch packets)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory ?? throw new NotSupportedException("Native Zombie requires its physical magic arena."), effect.DrawArenaOffset ?? throw new NotSupportedException("Native Zombie lost its selected packet cursor."), graphics.PrimitiveFrame!.Pending, packets);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
        static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
        static NativeBattleModulePoolTask Workspace(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace)
        {
            return task.WriteRetainedBytes(12 + (16), workspace.Data.AsSpan());
        }
    }
}
