using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Original120 and122 root scheduling, with separate complete target callbacks.</summary>
public static class NativeBattleStatusMagicController
{
    public static NativeBattleStatusMagicFrame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleStatusMagicResources resources)
    {
        if (effect.Binding.Entry != resources.Entry || effect.Parameters is null)
        {
            throw new InvalidDataException("Native status entry requires its selected original module and parameter block.");
        }
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native status entry requires its texture queue.");
        NativeBattleEffectInitializerAsset texture = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native status entry requires its actual TIM allocation.");
        (textures, _) = textures.EnqueueTim505E30(new(texture.Name, texture.Data));
        textures = textures.Register5099A0(resources.Animation);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(resources.RootDescriptor, resources.RootBank, 20, 1);
        (root, _) = root.Append(resources.RootCallback);
        return new(memory with { Textures = textures }, effect with { Pools = [root], ReturnedDescriptor = root.DescriptorAddress, DrawArenaOffset = null });
    }

    public static NativeBattleStatusMagicStep Advance(NativeBattleStatusMagicFrame frame, NativeBattleStatusMagicResources resources, NativeBattleStatusMagicInputs inputs)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return new(frame, [], []);
        }
        if (frame.Effect.Binding.Entry != resources.Entry || frame.Effect.Pools.IsDefaultOrEmpty || frame.Effect.Pools[0].DescriptorAddress != resources.RootDescriptor)
        {
            throw new InvalidDataException("Native status callback requires its executed module root.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native status callback lost its parameter block.");
        NativeBattleStatusMagicFrame current = frame;
        NativeBattleEffectTaskPool rootPool = frame.Effect.Pools[0];
        NativeBattleModulePoolTask root = rootPool.Slots[0];
        bool alternate = root.ReadDword(16) != 0;
        current = current with { Effect = current.Effect with { DrawArenaOffset = alternate ? resources.FirstArena : resources.SecondArena } };
        root = root.WriteDword(16, alternate ? 0u : 1u);
        ImmutableArray<int>.Builder contacts = ImmutableArray.CreateBuilder<int>();
        ImmutableArray<NativeBattleModuleSound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleModuleSound>();
        if (resources.Effect == 120)
        {
            current = inputs.Execute(current, new NativeBattleStatusMagicClearFlash());
        }
        NativeBattleEffectTaskPool? targets = frame.Effect.Pools.Length > 1 ? frame.Effect.Pools[1] : null;
        if (root.ReadWord(12) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                root = root.WriteByte(15, 1);
                targets = NativeBattleEffectTaskPool.Initialize(resources.TargetDescriptor, 0x20DFAB8, resources.TargetStride, resources.TargetCapacity);
            }
            int action = root.ReadByte(14);
            int last = inputs.Packets.Headers[parameters.HeaderIndex7C].ReadByte(17);
            if (action <= last)
            {
                if (targets is null)
                {
                    throw new InvalidDataException("Native status root has no initialized target queue.");
                }
                bool available = targets.Slots.Any(task => (task.Flags & 1) == 0);
                if (!available && resources.Effect == 122)
                {
                    throw new InvalidOperationException("Native Silence exhausted its original three-node target pool.");
                }
                if (available)
                {
                    (targets, int slot) = targets.Append(resources.TargetCallback);
                    NativeBattleModulePoolTask target = targets.Slots[slot] with { Payload = ImmutableArray.CreateRange(new byte[resources.TargetStride - 12]) };
                    int first = inputs.Packets.Headers[checked(parameters.HeaderIndex7C + action)].FirstContactIndex ?? throw new NotSupportedException("Native status spawn requires its action's first target.");
                    byte model = inputs.Packets.ReadContact(first).Primary.ActorIndex;
                    target = target.WriteWord(14, checked((ushort)action)).WriteDword(resources.Effect == 120 ? 32 : 16, checked(0x1D972C0u + model * 156u));
                    if (resources.Effect == 120)
                    {
                        target = InitializeWorkspace(target, 36, 0x140, resources.Layouts[0]);
                        target = InitializeWorkspace(target, 0x164, 0x4F4, resources.Layouts[1]);
                    }
                    else
                    {
                        inputs.WriteFlags(model, (ushort)(inputs.ReadModel(model).Flags0 | 0x2000));
                        target = InitializeWorkspace(target, 20, 0x498, resources.Layouts[0]);
                    }
                    targets = targets with { Slots = targets.Slots.SetItem(slot, target) };
                    root = root.WriteByte(14, unchecked((byte)(action + 1)));
                }
            }
        }
        int survivors = -1;
        if (root.ReadByte(15) != 0)
        {
            if (targets is null)
            {
                throw new InvalidDataException("Native status continuation lost its physical target queue.");
            }
            NativeBattleEffectTaskStep step = targets.Advance((pool, slot) =>
            {
                NativeBattleModulePoolTask task = pool.Slots[slot];
                short counter = unchecked((short)task.ReadWord(12));
                int model = ModelIndex(task.ReadDword(resources.Effect == 120 ? 32 : 16));
                if (resources.Effect == 122)
                {
                    if (counter >= 0)
                    {
                        NativeBattleVector anchor = Bone(model);
                        if (counter == 0)
                        {
                            sounds.Add(new(resources.Sound, 1, new(anchor.X, anchor.Y, anchor.Z, 0)));
                        }
                        task = Play(task, 20, 0x498, resources.Layouts[0], anchor, NativeBattleStatusMagicDrawing.SizeDepth(inputs.ReadModel(model)));
                    }
                    if (counter == 50)
                    {
                        Contact(task);
                    }
                    if (counter >= 60)
                    {
                        inputs.WriteFlags(model, (ushort)(inputs.ReadModel(model).Flags0 & ~0x2000));
                        return new(pool with { Slots = pool.Slots.SetItem(slot, task) }, 2);
                    }
                }
                else
                {
                    int caster = parameters.Actor78;
                    if (counter < 10)
                    {
                        task = WriteVector(task, 16, Bone(caster));
                    }
                    NativeBattleVector followed = Vector(task, 16);
                    short facing = inputs.ReadModel(caster).Rotation14 ?? throw new NotSupportedException("Native Petrify requires the caster's actual facingWORD0E.");
                    NativeBattleVector stare = new(unchecked((short)(followed.X + (-NativeBattleCameraMath.Sine(facing) >> 4))), followed.Y, unchecked((short)(followed.Z + (-NativeBattleCameraMath.Cosine(facing) >> 4))));
                    task = Play(task, 36, 0x140, resources.Layouts[0], stare, NativeBattleStatusMagicDrawing.SizeDepth(inputs.ReadModel(caster)));
                    if (counter == 4)
                    {
                        current = inputs.Execute(current, new NativeBattleStatusMagicFade());
                    }
                    if ((uint)(counter - 14) < 100)
                    {
                        if (counter == 14)
                        {
                            task = WriteVector(task, 24, DefaultPosition(model));
                        }
                        task = Play(task, 0x164, 0x4F4, resources.Layouts[1], Vector(task, 24), NativeBattleStatusMagicDrawing.SizeDepth(inputs.ReadModel(model)));
                    }
                    if (counter == 0)
                    {
                        NativeBattleVector position = DefaultPosition(caster);
                        sounds.Add(new(resources.Sound, 0x100, new(position.X, position.Y, position.Z, 0)));
                    }
                    if (counter == 42)
                    {
                        Contact(task);
                    }
                    if (counter > 48)
                    {
                        return new(pool with { Slots = pool.Slots.SetItem(slot, task) }, 2);
                    }
                }
                task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
                return new(pool with { Slots = pool.Slots.SetItem(slot, task) }, 0);
            });
            targets = step.Pool;
            survivors = step.Survivors;
        }
        bool complete = root.ReadByte(15) != 0 && survivors == 0;
        if (!complete)
        {
            root = root.WriteWord(12, (ushort)((root.ReadWord(12) + 1) % 10));
        }
        rootPool = rootPool with { Slots = rootPool.Slots.SetItem(0, root) };
        if (complete)
        {
            rootPool = rootPool.Advance((pool, _) => new(pool, 2)).Pool;
        }
        return new(current with { Effect = current.Effect with { Pools = targets is null ? [rootPool] : [rootPool, targets], ReturnedDescriptor = complete ? 0u : resources.RootDescriptor } }, contacts.ToImmutable(), sounds.ToImmutable());

        NativeBattleVector Bone(int index) => NativeBattlePoseMatrices.ReadBone(inputs.ReadModel(index), inputs.ReadPoseSource(index), 0xF0, 0);

        NativeBattleVector DefaultPosition(int index)
        {
            NativeBattleModuleAnchor anchor = NativeBattlePoseMatrices.ReadAnchor(inputs.ReadModel(index), inputs.ReadPoseSource(index));
            return new(anchor.X, anchor.Y, anchor.Z);
        }

        NativeBattleModulePoolTask Play(NativeBattleModulePoolTask task, int offset, int length, NativeBattleGfAnimationSource source, NativeBattleVector anchor, int depth)
        {
            NativeBattleGfAnimationWorkspace workspace = new(task.Payload.AsSpan().Slice(offset - 12, length).ToArray().ToImmutableArray());
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(source, 0, (owner, node) =>
            {
                current = NativeBattleStatusMagicDrawing.Draw(current, resources, source, node, anchor, depth, inputs);
                return owner;
            });
            return StoreWorkspace(task, offset, step.Workspace);
        }

        void Contact(NativeBattleModulePoolTask task)
        {
            int action = checked(parameters.HeaderIndex7C + task.ReadWord(14));
            NativeBattleActionHeaderSlot header = inputs.Packets.Headers[action];
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Native status result requires its action target list.");
            for (int index = 0; index < header.ReadByte(16); index++)
            {
                int contact = checked(first + index);
                contacts.Add(contact);
                if (inputs.ApplyContact is { } apply)
                {
                    current = apply(current, contact);
                }
            }
        }
    }

    public static int ModelIndex(uint pointer)
    {
        if (pointer < 0x1D972C0 || (pointer - 0x1D972C0) % 156 != 0 || (pointer - 0x1D972C0) / 156 >= 7)
        {
            throw new InvalidDataException("Native status target lost its physical model pointer.");
        }
        return checked((int)((pointer - 0x1D972C0) / 156));
    }

    private static NativeBattleModulePoolTask InitializeWorkspace(NativeBattleModulePoolTask task, int offset, int length, NativeBattleGfAnimationSource source)
    {
        NativeBattleGfAnimationWorkspace workspace = new(task.Payload.AsSpan().Slice(offset - 12, length).ToArray().ToImmutableArray());
        return StoreWorkspace(task, offset, workspace.Initialize7016B0(source, length));
    }

    private static NativeBattleModulePoolTask StoreWorkspace(NativeBattleModulePoolTask task, int offset, NativeBattleGfAnimationWorkspace workspace)
    {
        byte[] bytes = task.Payload.ToArray();
        workspace.Data.AsSpan().CopyTo(bytes.AsSpan(offset - 12));
        return task with { Payload = bytes.ToImmutableArray() };
    }

    private static NativeBattleVector Vector(NativeBattleModulePoolTask task, int offset) => new(unchecked((short)task.ReadWord(offset)), unchecked((short)task.ReadWord(offset + 2)), unchecked((short)task.ReadWord(offset + 4)));

    private static NativeBattleModulePoolTask WriteVector(NativeBattleModulePoolTask task, int offset, NativeBattleVector vector)
    {
        return task.WriteWord(offset, unchecked((ushort)vector.X)).WriteWord(offset + 2, unchecked((ushort)vector.Y)).WriteWord(offset + 4, unchecked((ushort)vector.Z));
    }
}
