using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public abstract record NativeBattleDuel215Operation;
public sealed record NativeBattleDuel215PrepareScene(int Slot) : NativeBattleDuel215Operation;
public sealed record NativeBattleDuel215RestoreScene : NativeBattleDuel215Operation;
public sealed record NativeBattleDuel215File(NativeBattleEffectFileRequest Request) : NativeBattleDuel215Operation;
public sealed record NativeBattleDuel215Inputs(Func<NativeBattleDuel215Frame, NativeBattleDuel215Operation, NativeBattleDuel215Frame> Execute);

public static partial class NativeBattleDuel215Controller
{
    public const uint Root = 0x2508968;
    public const uint Scene = 0x2508958;
    public const uint Particles = 0x2508948;
    public const uint Arena = 0x20DFAB8;

    public static NativeBattleDuel215Frame Enter69AB90(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleFileStaging staging, NativeBattleDuel215Resources resources, NativeCrtRandomState random)
    {
        if (effect.Binding is not { Effect: 215, Entry: 0x69AB60, Initializer: 0x69AB70 } || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native Duel215 requires its original entered binding and parameters.");
        }
        resources.Validate();
        NativeBattleDuel215State state = memory.Duel215 ?? throw new NotSupportedException("Native Duel215 requires its actual retained process owner.");
        state.Validate();
        if (!state.GlobalsInitialized)
        {
            staging = staging.Write(0x2508938, resources.ColdGlobals.AsSpan());
        }
        NativeBattleEffectResourcePointer initialTim = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x2508938) ?? throw new NotSupportedException("Native Duel215 requires its actual initializer texture pointer.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Duel215 requires its actual texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native Duel215 requires its retained resource arena."), initialTim.ArenaOffset);
        staging = Dword(staging, 0x2508938, checked(Arena + (uint)initialTim.ArenaOffset));
        staging = Dword(Dword(Dword(staging, 0x25089A8, Arena), 0x250893C, 0x1D99A78), 0x2508940, checked(0x1D972C0u + (uint)parameters.Actor78 * 156));
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(Root, 0x2508978, 16, 1);
        (root, int slot) = root.Append(0x69AC00);
        root = root with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteDword(12, 0)) };
        return new(memory with { Duel215 = new(true, parameters, resources), Textures = textures }, effect with { Pools = [root], ReturnedDescriptor = Root }, staging, random);
    }

    public static NativeBattleDuel215Frame Advance69AC00(NativeBattleDuel215Frame frame, NativeBattleDuel215Inputs inputs, Func<NativeBattleDuel215Frame, NativeBattleEffectTaskPool, int, NativeBattleDuel215Step> advanceChild)
    {
        NativeBattleEffectTaskPool root = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == Root);
        NativeBattleEffectTaskPool? scene = frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == Scene);
        NativeBattleEffectTaskPool? particles = frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == Particles);
        NativeBattleEffectTaskStep step = root.Advance((current, slot) =>
        {
            NativeBattleModulePoolTask task = current.Slots[slot];
            if (task.Callback != 0x69AC00)
            {
                throw new NotSupportedException("Native Duel215 requires its actual root callback.");
            }
            bool second = task.ReadByte(15) != 0;
            int cursor = second ? 66840 : 140568;
            int end = second ? 140568 : 214296;
            frame = frame with { Staging = Dword(Dword(frame.Staging, 0x2508988, checked(Arena + (uint)cursor)), 0x250898C, checked(Arena + (uint)end)), Effect = frame.Effect with { DrawArenaOffset = cursor } };
            task = task.WriteByte(15, second ? (byte)0 : (byte)1);
            if (task.ReadWord(12) == 1)
            {
                if (task.ReadByte(14) == 0)
                {
                    task = task.WriteByte(14, 1);
                    scene = NativeBattleEffectTaskPool.Initialize(Scene, Arena, 5120, 1);
                    particles = NativeBattleEffectTaskPool.Initialize(Particles, Arena + 5120, 660, 36);
                }
                if (scene is null)
                {
                    throw new InvalidDataException("Native Duel215 lost its initialized scene pool.");
                }
                (scene, int child) = scene.Append(0x69B070);
                scene = scene with { Slots = scene.Slots.SetItem(child, scene.Slots[child].WriteBytes(12, new byte[1277 * 4])) };
                Publish(current with { Slots = current.Slots.SetItem(slot, task) });
                frame = inputs.Execute(frame, new NativeBattleDuel215PrepareScene(child));
                Refresh();
            }
            int survivors = 1;
            if (task.ReadByte(14) != 0)
            {
                scene = Advance(scene ?? throw new InvalidDataException("Native Duel215 scene pool is missing."), out survivors);
                particles = Advance(particles ?? throw new InvalidDataException("Native Duel215 particle pool is missing."), out _);
                NativeBattleEffectIoAdvance io = NativeBattleEffectIo.Advance534210(frame.Memory);
                frame = frame with { Memory = io.Memory };
                if (io.Request is { } request)
                {
                    Publish(current with { Slots = current.Slots.SetItem(slot, task) });
                    frame = inputs.Execute(frame, new NativeBattleDuel215File(request));
                    Refresh();
                }
            }
            int result = 0;
            if (task.ReadByte(14) == 0 || survivors != 0)
            {
                short counter = unchecked((short)task.ReadWord(12));
                task = task.WriteWord(12, unchecked((ushort)(counter > 256 ? 256 : counter + 1)));
            }
            else
            {
                NativeBattleEffectIoPoll io = NativeBattleEffectIo.Select5342C0(frame.Memory, 2);
                frame = frame with { Memory = io.Memory };
                if (io.Request is { } request)
                {
                    Publish(current with { Slots = current.Slots.SetItem(slot, task) });
                    frame = inputs.Execute(frame, new NativeBattleDuel215File(request));
                    Refresh();
                }
                if (!io.Busy)
                {
                    Publish(current with { Slots = current.Slots.SetItem(slot, task) });
                    frame = inputs.Execute(frame, new NativeBattleDuel215RestoreScene());
                    Refresh();
                    result = 2;
                }
            }
            return new(current with { Slots = current.Slots.SetItem(slot, task) }, result);

            NativeBattleEffectTaskPool Advance(NativeBattleEffectTaskPool pool, out int count)
            {
                NativeBattleEffectTaskStep advanced = pool.Advance((active, childSlot) =>
                {
                    if (pool.DescriptorAddress == Scene)
                    {
                        scene = active;
                    }
                    else
                    {
                        particles = active;
                    }
                    Publish(current with { Slots = current.Slots.SetItem(slot, task) });
                    NativeBattleDuel215Step child = advanceChild(frame, active, childSlot);
                    frame = child.Frame;
                    Refresh();
                    return new(child.Pool, child.Result);
                });
                count = advanced.Survivors;
                return advanced.Pool;
            }
        });
        Publish(step.Pool);
        return frame with { Effect = frame.Effect with { ReturnedDescriptor = step.Survivors == 0 ? 0u : Root } };

        void Publish(NativeBattleEffectTaskPool activeRoot)
        {
            frame = frame with { Effect = frame.Effect with { Pools = new[] { activeRoot, scene, particles }.Where(pool => pool is not null).Select(pool => pool!).ToImmutableArray() } };
        }
        void Refresh()
        {
            scene = frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == Scene);
            particles = frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == Particles);
        }
    }

    private static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value) => staging.Write(address, BitConverter.GetBytes(value));
}
