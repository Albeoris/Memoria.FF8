using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public abstract record NativeBattleDuel216Operation;
public sealed record NativeBattleDuel216PrepareMain(int Slot) : NativeBattleDuel216Operation;
public sealed record NativeBattleDuel216NormalizeBillboard : NativeBattleDuel216Operation;
public sealed record NativeBattleDuel216StageIntensity(int Current, int Previous) : NativeBattleDuel216Operation;
public sealed record NativeBattleDuel216RestoreScene : NativeBattleDuel216Operation;
public sealed record NativeBattleDuel216File(NativeBattleEffectFileRequest Request) : NativeBattleDuel216Operation;
public sealed record NativeBattleDuel216Inputs(Func<NativeBattleDuel216Frame, NativeBattleDuel216Operation, NativeBattleDuel216Frame> Execute);

public static class NativeBattleDuel216Controller
{
    public const uint Root = 0x25088C8;
    public const uint Main = 0x25088B8;
    public const uint Secondary = 0x25088A8;
    public const uint Particles = 0x2508898;
    public const uint Small = 0x2508888;
    public const uint Arena = 0x20DFAB8;

    public static NativeBattleDuel216Frame Enter695A10(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleFileStaging staging, NativeBattleDuel216Resources resources, NativeCrtRandomState random, int minimumTargetIndex)
    {
        if (effect.Binding is not { Effect: 216, Entry: 0x6959E0, Initializer: 0x6959F0 } || effect.Parameters is not { } parameters || (uint)minimumTargetIndex >= 7)
        {
            throw new InvalidDataException("Native Duel216 requires its original entered binding, parameters and501FF0 target.");
        }
        resources.Validate();
        NativeBattleDuel216State state = memory.Duel216 ?? throw new NotSupportedException("Native Duel216 requires its retained process owner.");
        state.Validate();
        if (!state.GlobalsInitialized)
        {
            staging = staging.Write(0x2508870, resources.ColdGlobals.AsSpan());
        }
        NativeBattleEffectResourcePointer tim = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x2508870) ?? throw new NotSupportedException("Native Duel216 requires its actual initializer texture pointer.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Duel216 requires its actual texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native Duel216 requires its retained arena."), tim.ArenaOffset);
        staging = Dword(Dword(Dword(Dword(Dword(staging, 0x2508870, checked(Arena + (uint)tim.ArenaOffset)), 0x250890C, Arena), 0x250887C, 0x1D99A78), 0x2508880, checked(0x1D972C0u + (uint)parameters.Actor78 * 156)), 0x2508884, checked(0x1D972C0u + (uint)minimumTargetIndex * 156));
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(Root, 0x25088D8, 16, 1);
        (root, int slot) = root.Append(0x695AB0);
        root = root with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteDword(12, 0)) };
        staging = staging.MarkUnknown(Root, 32);
        return new(memory with { Duel216 = new(true, parameters, resources), Textures = textures }, effect with { Pools = [root], ReturnedDescriptor = Root }, staging, random);
    }

    public static NativeBattleDuel216Frame Advance695AB0(NativeBattleDuel216Frame frame, NativeBattleDuel216Inputs inputs, Func<NativeBattleDuel216Frame, NativeBattleEffectTaskPool, int, NativeBattleDuel216Step> advanceChild)
    {
        int rootIndex = Index(frame, Root);
        NativeBattleEffectTaskStep rootStep = frame.Effect.Pools[rootIndex].Advance((root, slot) =>
        {
            NativeBattleModulePoolTask task = root.Slots[slot];
            if (task.Callback != 0x695AB0)
            {
                throw new NotSupportedException("Native Duel216 requires its original root callback.");
            }
            bool second = task.ReadByte(15) != 0;
            int cursor = second ? 12696 : 119192;
            int end = second ? 119192 : 225688;
            task = task.WriteByte(15, second ? (byte)0 : (byte)1);
            frame = frame with { Staging = Dword(Dword(Dword(frame.Staging, 0x2508874, checked(Arena + (uint)cursor)), 0x2508878, checked(Arena + (uint)end)), 0x25088E8, 0), Effect = frame.Effect with { DrawArenaOffset = cursor } };
            PublishRoot();
            if (task.ReadWord(12) == 1)
            {
                if (task.ReadByte(14) == 0)
                {
                    task = task.WriteByte(14, 1);
                    PublishRoot();
                    Initialize(Main, 0, 6128, 1);
                    Initialize(Secondary, 6128, 260, 8);
                    Initialize(Particles, 8208, 36, 64);
                    Initialize(Small, 10512, 28, 32);
                }
                int mainIndex = Index(frame, Main);
                NativeBattleEffectTaskPool main = frame.Effect.Pools[mainIndex];
                (main, int child) = main.Append(0x6960E0);
                main = main with { Slots = main.Slots.SetItem(child, main.Slots[child].WriteBytes(12, new byte[1529 * 4])) };
                frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(mainIndex, main) } };
                Execute(new NativeBattleDuel216PrepareMain(child));
            }
            Execute(new NativeBattleDuel216NormalizeBillboard());
            int survivors = 1;
            if (task.ReadByte(14) != 0)
            {
                survivors = Advance(Main);
                _ = Advance(Secondary);
                _ = Advance(Particles);
                _ = Advance(Small);
            }
            int intensity = unchecked((int)ReadDword(frame.Staging, 0x25088E8));
            int previous = unchecked((int)ReadDword(frame.Staging, 0x25088EC));
            if (intensity != previous)
            {
                Execute(new NativeBattleDuel216StageIntensity(intensity, previous));
                frame = frame with { Staging = Dword(frame.Staging, 0x25088EC, unchecked((uint)intensity)) };
            }
            int result = 0;
            if (task.ReadByte(14) == 0 || survivors != 0)
            {
                task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
            }
            else
            {
                NativeBattleEffectIoAdvance advanced = NativeBattleEffectIo.Advance534210(frame.Memory);
                frame = frame with { Memory = advanced.Memory };
                if (advanced.Request is { } request)
                {
                    Execute(new NativeBattleDuel216File(request));
                }
                NativeBattleEffectIoPoll io = NativeBattleEffectIo.Select5342C0(frame.Memory, 1);
                frame = frame with { Memory = io.Memory };
                if (io.Request is { } selected)
                {
                    Execute(new NativeBattleDuel216File(selected));
                }
                if (!io.Busy)
                {
                    Execute(new NativeBattleDuel216RestoreScene());
                    result = 2;
                }
            }
            return new(root with { Slots = root.Slots.SetItem(slot, task) }, result);

            void PublishRoot()
            {
                root = root with { Slots = root.Slots.SetItem(slot, task) };
                frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(Index(frame, Root), root) } };
            }
            void Execute(NativeBattleDuel216Operation operation)
            {
                PublishRoot();
                frame = inputs.Execute(frame, operation);
                root = frame.Effect.Pools[Index(frame, Root)];
                task = root.Slots[slot];
            }
            void Initialize(uint descriptor, int offset, ushort stride, ushort count)
            {
                NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(descriptor, checked(Arena + (uint)offset), stride, count);
                if (frame.Effect.Pools.Any(candidate => candidate.DescriptorAddress == descriptor))
                {
                    throw new InvalidDataException("Native Duel216 initialization found an existing descriptor owner.");
                }
                frame = frame with { Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [pool]), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) }, Staging = frame.Staging.MarkUnknown(descriptor, 16) };
            }
            int Advance(uint descriptor)
            {
                int index = Index(frame, descriptor);
                NativeBattleEffectTaskStep advanced = frame.Effect.Pools[index].Advance((pool, childSlot) =>
                {
                    frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
                    NativeBattleDuel216Step next = advanceChild(frame, pool, childSlot);
                    frame = next.Frame;
                    return new(next.Pool, next.Result);
                });
                frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, advanced.Pool) } };
                return advanced.Survivors;
            }
        });
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(Index(frame, Root), rootStep.Pool), ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : Root } };
        if (rootStep.Survivors == 0)
        {
            foreach (uint descriptor in new uint[] { Main, Secondary, Particles, Small })
            {
                NativeBattleEffectTaskTransfer detached = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, descriptor);
                frame = frame with { Memory = detached.Memory, Effect = detached.Effect };
            }
        }
        return frame;
    }

    public static int Index(NativeBattleDuel216Frame frame, uint descriptor)
    {
        int[] matches = Enumerable.Range(0, frame.Effect.Pools.Length).Where(index => frame.Effect.Pools[index].DescriptorAddress == descriptor).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidDataException("Native Duel216 requires one physical descriptor owner.");
    }

    private static uint ReadDword(NativeBattleFileStaging staging, uint address) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4));
    private static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value) => staging.Write(address, BitConverter.GetBytes(value));
}
