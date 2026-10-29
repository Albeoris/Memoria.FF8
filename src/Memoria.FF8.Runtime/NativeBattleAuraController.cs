using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleAuraController
{
    public const uint RootDescriptor = 0x25219B8;

    public static NativeBattleAuraFrame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleAuraResources resources, NativeCrtRandomState random)
    {
        if (effect.Binding is not { Effect: 111, Entry: 0x6CD920 } || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native Aura requires its selected entry and action context.");
        }
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Aura requires its current texture queue.");
            NativeBattleEffectInitializerAsset texture = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Aura requires its original TIM allocation.");
            (textures, _) = textures.EnqueueTim505E30(new(texture.Name, texture.Data));
            textures = textures.Register5099A0(resources.Animation);
            memory = memory with { Textures = textures };
        }
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, 0x2521980, 20, 1);
        (root, _) = root.Append(0x6CD9C0);
        return new(memory, effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null }, random);
    }

    public static NativeBattleAuraFrame Advance(NativeBattleAuraFrame frame, NativeBattleAuraResources resources, NativeBattleAuraInputs inputs)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        NativeBattleAuraFrame current = frame;
        NativeBattleEffectTaskPool[] pools = frame.Effect.Pools.ToArray();
        if (pools.Length is not (1 or 3) || pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Native Aura lost its initialized root and private target banks.");
        }
        NativeBattleModulePoolTask root = pools[0].Slots[0];
        current = current with { Effect = current.Effect with { DrawArenaOffset = root.ReadDword(16) != 0 ? 0xE28 : 0x18E28 } };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Aura lost its original action context.");
        if (root.ReadWord(12) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                root = root.WriteByte(15, 1);
                pools = [pools[0], NativeBattleEffectTaskPool.Initialize(0x25219A8, 0x20DFAB8, 0x360, 3), NativeBattleEffectTaskPool.Initialize(0x2521998, 0x20DFAB8 + 0xA20, 0x20, 32)];
            }
            int action = root.ReadByte(14);
            if (action <= inputs.Packets.Headers[parameters.HeaderIndex7C].ReadByte(17))
            {
                int first = inputs.Packets.Headers[parameters.HeaderIndex7C + action].FirstContactIndex ?? throw new NotSupportedException("Native Aura requires its action's actual first target.");
                byte model = inputs.Packets.ReadContact(first).Primary.ActorIndex;
                uint pointer = checked(0x1D972C0u + model * 156u);
                if (pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.ReadDword(16) == pointer && Word(task, 12) <= 28))
                {
                    root = root.WriteWord(12, 0);
                }
                else if (Available(1))
                {
                    (pools[1], int slot) = pools[1].Append(0x6CDB60);
                    NativeBattleModulePoolTask task = pools[1].Slots[slot].ClearPayload(0x354);
                    task = task.WriteWord(14, checked((ushort)action)).WriteDword(16, pointer);
                    NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (8), 0x34C);
                    task = StoreWorkspace(task, workspace.Initialize7016B0(resources.Layout, 0x34C));
                    Store(1, slot, task);
                    root = root.WriteByte(14, unchecked((byte)(action + 1)));
                }
            }
        }
        int targets = -1;
        int flames = -1;
        if (root.ReadByte(15) != 0)
        {
            NativeBattleEffectTaskStep targetStep = pools[1].Advance((pool, slot) =>
            {
                pools[1] = pool;
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int age = Word(task, 12);
                int actor = NativeBattleStatusMagicController.ModelIndex(task.ReadDword(16));
                NativeBattleGraphicsModel model = inputs.ReadModel(actor);
                NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Aura requires target size and height WORD owners.");
                if (age >= 0)
                {
                    short scale = unchecked((short)(long)Math.Sqrt(unchecked(transform.Scale38 << 12)));
                    NativeBattleVector feet = Bone(actor);
                    NativeBattleActionMatrix local = new([scale, 0, 0, 0, scale, 0, 0, 0, scale], feet.X, transform.Angle36, feet.Z, 4096);
                    NativeBattleActionMatrix placement = NativeBattleGfMatrices.Compose56C2F0(inputs.Projection.View ?? throw new NotSupportedException("Native Aura requires its current camera matrix."), local);
                    NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (8), 0x34C);
                    NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(111, task);
                    NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.Layout, 0, (owner, node) =>
                    {
                        current = NativeBattleAuraDrawing.Part(current, resources, node, placement, inputs, identity);
                        return owner;
                    });
                    task = StoreWorkspace(task, advanced.Workspace);
                }
                if ((uint)(age - 15) < 4)
                {
                    int angle = Roll();
                    int speed = (Roll() & 15) + 16;
                    if ((Roll() & 1) != 0)
                    {
                        speed = -speed;
                    }
                    for (int index = 0; index < 4; index++)
                    {
                        if (Available(2))
                        {
                            (pools[2], int child) = pools[2].Append(0x6CE020);
                            NativeBattleVector position = Bone(actor) with { Y = unchecked((short)(transform.Angle36 - 600)) };
                            NativeBattleModulePoolTask flame = pools[2].Slots[child].ClearPayload(20);
                            flame = flame.WriteWord(16, unchecked((ushort)position.X)).WriteWord(18, unchecked((ushort)position.Y)).WriteWord(20, unchecked((ushort)position.Z)).WriteWord(22, checked((ushort)((Roll() & 1023) + 800))).WriteWord(24, unchecked((ushort)angle)).WriteWord(26, unchecked((ushort)speed)).WriteWord(28, checked((ushort)((Roll() & 2047) + 2048)));
                            Store(2, child, flame);
                        }
                        angle += 1024;
                    }
                }
                int glow = age - 28;
                if ((uint)glow < 6)
                {
                    if (glow == 0)
                    {
                        current = inputs.Execute(current, new NativeBattleAuraFade());
                    }
                    NativeBattleVector position = Bone(actor) with { Y = unchecked((short)(transform.Angle36 - 600)) };
                    NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resources.Glow, SpriteFrame = resources.Glow.SpriteFrame(glow), Phase = checked((ushort)glow), Anchor = new(position.X, position.Y, position.Z, 0), Scale = 4096, Direction = 4096, DepthOffset = -256, SpriteFlags = 0, SpriteColor = 0x808080, PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(111, task, 0x10000) };
                    current = inputs.Execute(current, new NativeBattleAuraSprite(draw));
                }
                if (age == 0)
                {
                    NativeBattleModuleAnchor anchor = NativeBattlePoseMatrices.ReadAnchor(model, inputs.ReadPoseSource(actor));
                    current = inputs.Execute(current, new NativeBattleAuraSound(new(resources.Sound, 0x101, anchor)));
                }
                if (age == 29)
                {
                    NativeBattleActionHeaderSlot header = inputs.Packets.Headers[parameters.HeaderIndex7C + task.ReadWord(14)];
                    int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Aura result requires its actual action target list.");
                    for (int index = 0; index < header.ReadByte(16); index++)
                    {
                        current = inputs.Execute(current, new NativeBattleAuraContact(first + index));
                    }
                }
                bool complete = age >= 34;
                if (!complete)
                {
                    task = task.WriteWord(12, unchecked((ushort)(age + 1)));
                }
                Store(1, slot, task);
                return new(pools[1], complete ? 2 : 0);
            });
            pools[1] = targetStep.Pool;
            targets = targetStep.Survivors;
            NativeBattleEffectTaskStep flameStep = pools[2].Advance((pool, slot) =>
            {
                NativeBattleModulePoolTask task = pool.Slots[slot];
                current = NativeBattleAuraDrawing.PublishFar(current, 0);
                current = inputs.Execute(current, new NativeBattleAuraPackets(NativeBattleAuraDrawing.Flame(task, resources, inputs.Projection, inputs.Colors, NativeBattlePilotVisualLifetimes.Identity(111, task))));
                bool complete = Word(task, 12) >= 20;
                if (!complete)
                {
                    task = task.WriteWord(24, unchecked((ushort)(task.ReadWord(24) + task.ReadWord(26)))).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
                }
                return new(pool.WriteSlot(slot, task), complete ? 2 : 0);
            });
            pools[2] = flameStep.Pool;
            flames = flameStep.Survivors;
        }
        bool finished = root.ReadByte(15) != 0 && targets == 0 && flames == 0;
        if (!finished)
        {
            root = root.WriteWord(12, (ushort)((root.ReadWord(12) + 1) % 10));
        }
        Store(0, 0, root);
        if (finished)
        {
            pools[0] = pools[0].Advance((pool, _) => new(pool, 2)).Pool;
        }
        return current with { Effect = current.Effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = finished ? 0u : RootDescriptor } };

        bool Available(int index) => pools[index].Slots.Any(task => (task.Flags & 1) == 0);
        void Store(int index, int slot, NativeBattleModulePoolTask task) => pools[index] = pools[index].WriteSlot(slot, task);
        NativeBattleVector Bone(int actor) => NativeBattlePoseMatrices.ReadBone(inputs.ReadModel(actor), inputs.ReadPoseSource(actor), 0xF1, 0);
        ushort Roll()
        {
            NativeCrtRandomState random = current.Random.Draw(out ushort value);
            current = current with { Random = random };
            return value;
        }
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));

    private static NativeBattleModulePoolTask StoreWorkspace(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace)
    {
        return workspace.CopyTo(task, 12 + (8), retainUnknown: true);
    }
}
