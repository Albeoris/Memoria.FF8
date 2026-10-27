using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStatusCureItemResources(ushort Effect, NativeBattleGfAnimationSource Layout, uint Sound);
public sealed record NativeBattleStatusCureItemExecution([property: JsonRequired] int ArenaOffset, [property: JsonRequired] uint CasterModelPointer)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleActionMatrix? Billboard { get; init; }
}
public sealed record NativeBattleStatusCureItemFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleStatusCureItemExecution Execution);
public sealed record NativeBattleStatusCureItemStep(NativeBattleStatusCureItemFrame Frame, ImmutableArray<int> Contacts, ImmutableArray<NativeBattleModuleSound> Sounds);
public sealed record NativeBattleStatusCureItemInputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix View, Func<int, NativeBattleVector> ReadTargetAnchor, Func<int, short> ReadModelY, Func<NativeBattleStatusCureItemFrame, NativeBattleGfAnimationNode, NativeBattleVector, NativeBattleStatusCureItemFrame> Draw);

public sealed partial record NativeBattleOriginalEffectState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleStatusCureItemExecution? StatusCureItem { get; init; }
}

public static class NativeBattleStatusCureItemController
{
    public static NativeBattleStatusCureItemFrame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleStatusCureItemResources resources, int arenaOffset)
    {
        NativeBattleStatusCureItemProfile profile = RequireResources(effect, resources);
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native status-cure item requires its original50AFC0 parameters.");
        int textureOffset = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == profile.TextureGlobal)?.ArenaOffset ?? throw new NotSupportedException("Native status-cure item requires its executed initializer TIM publication.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native status-cure item requires its retained texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native status-cure item requires its current571870 arena."), textureOffset);
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(profile.MainDescriptor, profile.MainBank, 20, 1);
        (roots, int slot) = roots.Append(profile.RootCallback);
        roots = roots.WriteSlot(slot, roots.Slots[slot].WriteDword(12, 0).WriteDword(16, 0));
        NativeBattleStatusCureItemExecution execution = new(arenaOffset, checked(0x1D972C0u + (uint)parameters.Actor78 * 156));
        return new(memory with { Textures = textures }, effect with { Pools = [roots], ReturnedDescriptor = profile.MainDescriptor, StatusCureItem = execution, DrawArenaOffset = null }, execution);
    }

    public static NativeBattleStatusCureItemStep Advance(NativeBattleStatusCureItemFrame frame, NativeBattleStatusCureItemResources resources, NativeBattleStatusCureItemInputs inputs)
    {
        NativeBattleStatusCureItemProfile profile = RequireResources(frame.Effect, resources);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return new(frame, [], []);
        }
        if (frame.Effect.Parameters is not { } parameters || frame.Effect.Pools.Length is < 1 or > 2)
        {
            throw new InvalidDataException("Native status-cure item requires its entered root and optional actual child pool.");
        }
        NativeBattleEffectTaskPool roots = frame.Effect.Pools[0];
        if (roots.DescriptorAddress != profile.MainDescriptor || roots.BankAddress != profile.MainBank || roots.Stride != 20 || roots.Slots.Length != 1)
        {
            throw new InvalidDataException("Native status-cure item root pool differs from its physical contract.");
        }
        NativeBattleEffectTaskPool? children = frame.Effect.Pools.Length == 2 ? frame.Effect.Pools[1] : null;
        ImmutableArray<int>.Builder contacts = ImmutableArray.CreateBuilder<int>();
        ImmutableArray<NativeBattleModuleSound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleModuleSound>();
        NativeBattleEffectTaskStep rootStep = roots.Advance((pool, slot) =>
        {
            NativeBattleModulePoolTask root = pool.Slots[slot];
            if (root.Callback != profile.RootCallback)
            {
                throw new NotSupportedException("Native status-cure item root requires its own callback.");
            }
            int drawOffset = root.ReadDword(16) != 0 ? profile.PrimaryBufferOffset : profile.SecondaryBufferOffset;
            frame = frame with { Effect = frame.Effect with { DrawArenaOffset = checked(frame.Execution.ArenaOffset + drawOffset) } };
            root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
            if (root.ReadWord(12) == 1 && root.ReadByte(15) == 0)
            {
                root = root.WriteByte(15, 1);
                children = NativeBattleEffectTaskPool.Initialize(profile.ChildDescriptor, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)frame.Execution.ArenaOffset), profile.ChildStride, 1);
                NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native status-cure item requires its current child-bank bytes.");
                NativeBattleModulePoolTask retainedChild = children.Slots[0].WriteRetainedBytes(12, arena.Read(checked(frame.Execution.ArenaOffset + 12), profile.ChildStride - 12));
                children = children.WriteSlot(0, retainedChild);
                (children, int childSlot) = children.Append(profile.ChildCallback);
                NativeBattleActionHeaderSlot header = inputs.Packets.Headers[parameters.HeaderIndex7C];
                int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native status-cure item requires its actual first contact pointer.");
                int target = inputs.Packets.ReadContact(contact).Primary.ActorIndex;
                NativeBattleVector anchor = inputs.ReadTargetAnchor(target);
                NativeBattleModulePoolTask child = StoreAnchor(children.Slots[childSlot], anchor);
                if (profile.Effect != 293)
                {
                    child = child.WriteWord(18, unchecked((ushort)inputs.ReadModelY(target)));
                }
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(child, 12 + (8), profile.WorkspaceBytes);
                workspace = workspace.Initialize7016B0(resources.Layout, profile.WorkspaceBytes);
                children = children.WriteSlot(childSlot, StoreWorkspace(child, workspace));
                frame = frame with { Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [children]) };
                sounds.Add(new(resources.Sound, 0, null) { Pan = 128, RawPan = 128 });
            }
            int survivors = 1;
            if (root.ReadByte(15) != 0)
            {
                if (profile.Effect == 295)
                {
                    NativeBattleStatusCureItemExecution execution = frame.Execution with { Billboard = NativeBattleGfMatrices.Normalize67AAE0(inputs.View) };
                    frame = frame with { Execution = execution, Effect = frame.Effect with { StatusCureItem = execution } };
                }
                if (children is null || children.DescriptorAddress != profile.ChildDescriptor || children.Stride != profile.ChildStride || children.Slots.Length != 1)
                {
                    throw new NotSupportedException("Native status-cure item lost its physical child workspace owner.");
                }
                NativeBattleEffectTaskStep childStep = children.Advance((childPool, childSlot) =>
                {
                    NativeBattleModulePoolTask child = childPool.Slots[childSlot];
                    if (child.Callback != profile.ChildCallback)
                    {
                        throw new NotSupportedException("Native status-cure item child requires its own animation callback.");
                    }
                    NativeBattleVector anchor = new(unchecked((short)child.ReadWord(12)), unchecked((short)child.ReadWord(14)), unchecked((short)child.ReadWord(16)));
                    NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(child, 12 + (8), profile.WorkspaceBytes);
                    NativeBattleGfAnimationAdvance advance = workspace.Advance701970(resources.Layout, 0, (current, node) =>
                    {
                        frame = inputs.Draw(frame, node, anchor);
                        return current;
                    });
                    child = StoreWorkspace(child, advance.Workspace);
                    if (advance.Remaining == 0)
                    {
                        int first = inputs.Packets.Headers[parameters.HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Native status-cure item completion lost its actual first contact pointer.");
                        contacts.Add(first);
                    }
                    return new(childPool with { Slots = childPool.Slots.SetItem(childSlot, child) }, advance.Remaining == 0 ? 2 : 0);
                });
                children = childStep.Pool;
                survivors = childStep.Survivors;
            }
            bool complete = root.ReadByte(15) != 0 && survivors == 0;
            if (!complete)
            {
                root = root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)));
            }
            return new(pool with { Slots = pool.Slots.SetItem(slot, root) }, complete ? 2 : 0);
        });
        NativeBattleOriginalEffectState effect = frame.Effect with { Pools = children is null ? [rootStep.Pool] : [rootStep.Pool, children], ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : profile.MainDescriptor };
        return new(frame with { Effect = effect }, contacts.ToImmutable(), sounds.ToImmutable());
    }

    private static NativeBattleStatusCureItemProfile RequireResources(NativeBattleOriginalEffectState effect, NativeBattleStatusCureItemResources resources)
    {
        NativeBattleStatusCureItemProfile profile = NativeBattleStatusCureItemProfile.ForEffect(resources.Effect);
        if (effect.Binding.Effect != profile.Effect || effect.Binding.Entry != profile.Entry || effect.Binding.Initializer != profile.Initializer || resources.Layout.Address != profile.SourceAddress)
        {
            throw new InvalidDataException("Native status-cure item requires its own selected entry and ACT resource.");
        }
        return profile;
    }

    private static NativeBattleModulePoolTask StoreAnchor(NativeBattleModulePoolTask task, NativeBattleVector anchor)
    {
        return task.WriteWord(12, unchecked((ushort)anchor.X)).WriteWord(14, unchecked((ushort)anchor.Y)).WriteWord(16, unchecked((ushort)anchor.Z));
    }

    private static NativeBattleModulePoolTask StoreWorkspace(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace)
    {
        return workspace.CopyTo(task, 12 + (8), retainUnknown: true);
    }
}
