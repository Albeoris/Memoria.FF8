using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBlindResources(NativeBattleEffectHeapAllocation Animation, NativeBattleGfAnimationSource Layout, uint Sound);
public sealed record NativeBattleBlindFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleFileStaging Staging);
public abstract record NativeBattleBlindOperation;
public sealed record NativeBattleBlindPackets(NativeBattleGfGeometryBatch Batch) : NativeBattleBlindOperation;
public sealed record NativeBattleBlindSound(NativeBattleModuleSound Sound) : NativeBattleBlindOperation;
public sealed record NativeBattleBlindContact(int Index) : NativeBattleBlindOperation;
public sealed record NativeBattleBlindFade : NativeBattleBlindOperation;
public sealed record NativeBattleBlindFlash(int Strength) : NativeBattleBlindOperation;
public sealed record NativeBattleBlindInputs(NativeBattleActionPacketsState Packets, NativeBattleCameraProjection Projection, NativeBattleSummonColors Colors, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleBlindFrame, NativeBattleBlindOperation, NativeBattleBlindFrame> Execute);

public static class NativeBattleBlindController
{
    public const uint RootDescriptor = 0x2521530;

    public static NativeBattleBlindFrame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleBlindResources resources, NativeBattleFileStaging staging)
    {
        if (effect.Binding is not { Effect: 121, Entry: 0x6BDE80 } || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native Blind requires its selected entry and action context.");
        }
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Blind requires its current texture queue.");
            NativeBattleEffectInitializerAsset texture = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Blind requires its original TIM allocation.");
            (textures, _) = textures.EnqueueTim505E30(new(texture.Name, texture.Data));
            memory = memory with { Textures = textures.Register5099A0(resources.Animation) };
        }
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, 0x2521508, 0x14, 1);
        (root, _) = root.Append(0x6BDF20);
        return new(memory, effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null }, staging);
    }

    public static NativeBattleBlindFrame Advance(NativeBattleBlindFrame frame, NativeBattleBlindResources resources, NativeBattleBlindInputs inputs)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        NativeBattleEffectTaskPool[] pools = frame.Effect.Pools.ToArray();
        if (pools.Length is not (1 or 2) || pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Native Blind lost its initialized private queues.");
        }
        NativeBattleModulePoolTask root = pools[0].Slots[0];
        NativeBattleBlindFrame current = frame with { Effect = frame.Effect with { DrawArenaOffset = root.ReadDword(16) != 0 ? 0x1B34 : 0x19B34 } };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Blind lost its actual action context.");
        if (root.ReadWord(12) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                root = root.WriteByte(15, 1);
                pools = [pools[0], NativeBattleEffectTaskPool.Initialize(0x2521520, 0x20DFAB8, 0x83C, 3)];
            }
            int action = root.ReadByte(14);
            if (action <= inputs.Packets.Headers[parameters.HeaderIndex7C].ReadByte(17) && pools[1].Slots.Any(task => (task.Flags & 1) == 0))
            {
                int contact = inputs.Packets.Headers[parameters.HeaderIndex7C + action].FirstContactIndex ?? throw new NotSupportedException("Native Blind requires its actual first action target.");
                byte model = inputs.Packets.ReadContact(contact).Primary.ActorIndex;
                (pools[1], int slot) = pools[1].Append(0x6BE080);
                NativeBattleModulePoolTask target = pools[1].Slots[slot].ClearPayload(0x830);
                target = target.WriteWord(14, checked((ushort)action)).WriteDword(24, checked(0x1D972C0u + model * 156u));
                NativeBattleGfAnimationWorkspace workspace = new(target.Payload.Slice(16, 0x820));
                target = StoreWorkspace(target, workspace.Initialize7016B0(resources.Layout, 0x820));
                Store(1, slot, target);
                root = root.WriteByte(14, unchecked((byte)(action + 1)));
            }
        }
        int flash = 0;
        int survivors = -1;
        if (root.ReadByte(15) != 0)
        {
            NativeBattleEffectTaskStep step = pools[1].Advance((pool, slot) =>
            {
                pools[1] = pool;
                NativeBattleModulePoolTask target = pool.Slots[slot];
                int age = Word(target, 12);
                flash = Math.Max(flash, FlashAt(age));
                int actor = NativeBattleStatusMagicController.ModelIndex(target.ReadDword(24));
                if (age < 20)
                {
                    NativeBattleVector position = NativeBattlePoseMatrices.ReadBone(inputs.ReadModel(actor), inputs.ReadPoseSource(actor), 0xF0, 0);
                    target = target.WriteWord(16, unchecked((ushort)position.X)).WriteWord(18, unchecked((ushort)position.Y)).WriteWord(20, unchecked((ushort)position.Z));
                }
                NativeBattleVector anchor = new(Word(target, 16), Word(target, 18), Word(target, 20));
                NativeBattleGfAnimationWorkspace workspace = new(target.Payload.Slice(16, 0x820));
                NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.Layout, 0, (owner, node) =>
                {
                    current = NativeBattleBlindDrawing.Part(current, resources, node, anchor, inputs);
                    return owner;
                });
                target = StoreWorkspace(target, advanced.Workspace);
                if (age == 4)
                {
                    NativeBattleModuleAnchor sound = NativeBattlePoseMatrices.ReadAnchor(inputs.ReadModel(actor), inputs.ReadPoseSource(actor));
                    current = inputs.Execute(current, new NativeBattleBlindSound(new(resources.Sound, 0x101, sound)));
                }
                if (age is 13 or 18 or 21)
                {
                    current = inputs.Execute(current, new NativeBattleBlindFade());
                }
                if (age == 32)
                {
                    NativeBattleActionHeaderSlot header = inputs.Packets.Headers[parameters.HeaderIndex7C + target.ReadWord(14)];
                    int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Blind result requires its original target list.");
                    for (int index = 0; index < header.ReadByte(16); index++)
                    {
                        current = inputs.Execute(current, new NativeBattleBlindContact(first + index));
                    }
                }
                bool complete = advanced.Remaining == 0;
                if (!complete)
                {
                    target = target.WriteWord(12, unchecked((ushort)(age + 1)));
                }
                Store(1, slot, target);
                return new(pools[1], complete ? 2 : 0);
            });
            pools[1] = step.Pool;
            survivors = step.Survivors;
        }
        current = inputs.Execute(current, new NativeBattleBlindFlash(flash));
        bool finished = root.ReadByte(15) != 0 && survivors == 0;
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

        void Store(int index, int slot, NativeBattleModulePoolTask task)
        {
            pools[index] = pools[index].WriteSlot(slot, task);
        }
    }

    public static int FlashAt(int age)
    {
        int elapsed = age - 22;
        return (uint)elapsed < 4 ? elapsed * 625 : (uint)elapsed <= 12 ? (12 - elapsed) * 312 : 0;
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));

    private static NativeBattleModulePoolTask StoreWorkspace(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace)
    {
        return task.WriteRetainedBytes(12 + (16), workspace.Data.AsSpan());
    }
}
