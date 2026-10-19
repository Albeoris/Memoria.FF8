using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public abstract record NativeBattleNoMercy200Operation;
public sealed record NativeBattleNoMercy200Camera : NativeBattleNoMercy200Operation;
public sealed record NativeBattleNoMercy200Bind : NativeBattleNoMercy200Operation;
public sealed record NativeBattleNoMercy200Sequence(int Actor, ushort Sequence) : NativeBattleNoMercy200Operation;
public sealed record NativeBattleNoMercy200TargetSequence(ushort Sequence) : NativeBattleNoMercy200Operation;
public sealed record NativeBattleNoMercy200Spawn(uint Callback) : NativeBattleNoMercy200Operation;
public sealed record NativeBattleNoMercy200Sound(uint Descriptor) : NativeBattleNoMercy200Operation;
public sealed record NativeBattleNoMercy200Read(short File, int ArenaOffset) : NativeBattleNoMercy200Operation;
public sealed record NativeBattleNoMercy200Upload(int ArenaOffset) : NativeBattleNoMercy200Operation;
public sealed record NativeBattleNoMercy200Stage(int Intensity) : NativeBattleNoMercy200Operation;
public sealed record NativeBattleNoMercy200Flash : NativeBattleNoMercy200Operation;
public sealed record NativeBattleNoMercy200Contacts : NativeBattleNoMercy200Operation;
public sealed record NativeBattleNoMercy200Reload(uint CompletionAddress) : NativeBattleNoMercy200Operation;
public sealed record NativeBattleNoMercy200Inputs(uint GraphicsFlags, NativeBattleActionMatrix View, Func<int> ReadIoResult, Func<NativeBattleNoMercy200Frame, NativeBattleNoMercy200Operation, NativeBattleNoMercy200Frame> Execute);

/// <summary>639380's signed175-tick scene and63E680/63E6F0's single physical callback queue.</summary>
public static partial class NativeBattleNoMercy200Controller
{
    public const uint Root = 0x24F8D80;
    public const uint Children = 0x24F9990;

    public static NativeBattleNoMercy200Frame AdvanceRoot(NativeBattleNoMercy200Frame frame, NativeBattleNoMercy200Inputs inputs, Func<NativeBattleNoMercy200Frame, NativeBattleEffectTaskPool, int, NativeBattleNoMercy200ChildStep> advanceChild)
    {
        NativeBattleEffectTaskPool main = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == Root);
        NativeBattleEffectTaskPool tasks = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == Children);
        NativeBattleEffectTaskStep advanced = main.Advance((root, slot) =>
        {
            NativeBattleModulePoolTask task = root.Slots[slot];
            if (task.Callback == 0x63E6F0)
            {
                if (task.ReadWord(12) == 1)
                {
                    frame = frame with { Effect = frame.Effect with { Pools = [root, tasks] } };
                    frame = inputs.Execute(frame, new NativeBattleNoMercy200Reload(checked(root.BankAddress + (uint)(slot * root.Stride) + 14)));
                    root = frame.Effect.Pools[0];
                    task = root.Slots[slot];
                }
                bool complete = task.ReadWord(14) != 0;
                task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
                return new(root with { Slots = root.Slots.SetItem(slot, task) }, complete ? 2 : 0);
            }
            if (task.Callback != 0x63E680)
            {
                throw new NotSupportedException("Native No Mercy root has an unsupported callback.");
            }
            frame = frame with { Effect = frame.Effect with { DrawArenaOffset = (task.ReadByte(12) & 1) != 0 ? 57344 : 0, Pools = [root, tasks] } };
            frame = frame with { Staging = WriteDword(frame.Staging, 0x24FBD64, checked(0x20DFAB8u + (uint)frame.Effect.DrawArenaOffset!.Value)) };
            NativeBattleEffectTaskStep children = tasks.Advance((pool, childSlot) =>
            {
                frame = frame with { Effect = frame.Effect with { Pools = [root, pool] } };
                NativeBattleNoMercy200ChildStep child = advanceChild(frame, pool, childSlot);
                frame = child.Frame;
                return new(child.Pool, child.Result);
            });
            tasks = children.Pool;
            if (children.Survivors == 0)
            {
                (root, int ending) = root.Append(0x63E6F0);
                root = root with { Slots = root.Slots.SetItem(ending, root.Slots[ending].WriteWord(12, 0).WriteWord(14, 0)) };
            }
            task = root.Slots[slot].WriteWord(12, unchecked((ushort)(root.Slots[slot].ReadWord(12) + 1)));
            return new(root with { Slots = root.Slots.SetItem(slot, task) }, children.Survivors == 0 ? 2 : 0);
        });
        return frame with { Effect = frame.Effect with { Pools = [advanced.Pool, tasks], ReturnedDescriptor = advanced.Survivors == 0 ? 0u : Root } };
    }

    public static NativeBattleNoMercy200ChildStep Advance639380(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleNoMercy200Inputs inputs)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x639380)
        {
            throw new InvalidDataException("Native639380 requires its actual scene callback.");
        }
        NativeBattleActionMatrix world = ReadMatrix(frame.Staging, 0x24F8D90);
        frame = frame with { Staging = StoreMatrix(frame.Staging, 0x24F9930, NativeBattlePoseMatrices.Compose56C2F0(inputs.View, world)) };
        if ((inputs.GraphicsFlags & 0x201) != 0 && ((inputs.GraphicsFlags & 1) != 0 || inputs.ReadIoResult() < 0))
        {
            return new(frame, pool, 0);
        }
        short tick = unchecked((short)task.ReadWord(12));
        int actor = ReadDword(frame.Staging, 0x24FBCE0);
        if (tick == 0)
        {
            Execute(new NativeBattleNoMercy200Camera());
        }
        switch (tick)
        {
            case 3:
                Execute(new NativeBattleNoMercy200Bind());
                Execute(new NativeBattleNoMercy200Sequence(actor, 4));
                break;
            case 4:
                Execute(new NativeBattleNoMercy200Spawn(0x639780));
                break;
            case 6:
                Execute(new NativeBattleNoMercy200Sound(Resources().Dword(0xE33D30)));
                Execute(new NativeBattleNoMercy200TargetSequence(5));
                break;
            case 10:
                Execute(new NativeBattleNoMercy200Sequence(actor, 5));
                break;
            case 21:
                Execute(new NativeBattleNoMercy200Sequence(actor, 1));
                Execute(new NativeBattleNoMercy200Spawn(0x63A220));
                break;
            case 31:
                Execute(new NativeBattleNoMercy200Read(562, 114688));
                break;
            case 35:
                Execute(new NativeBattleNoMercy200Sound(Resources().Dword(0xE33D34)));
                break;
            case 45:
            case 61:
            case 122:
                if (inputs.ReadIoResult() < 0)
                {
                    return new(frame, pool, 0);
                }
                Execute(new NativeBattleNoMercy200Upload(114688));
                break;
            case 46:
                Execute(new NativeBattleNoMercy200Read(563, 114688));
                break;
            case 64:
                Execute(new NativeBattleNoMercy200Sequence(actor, 2));
                break;
            case 65:
                Execute(new NativeBattleNoMercy200Spawn(0x63ACB0));
                break;
            case 72:
                Execute(new NativeBattleNoMercy200Spawn(0x63AE80));
                break;
            case 80:
                Execute(new NativeBattleNoMercy200Spawn(0x63B080));
                break;
            case 84:
                Execute(new NativeBattleNoMercy200Spawn(0x63CBD0));
                break;
            case 90:
                Execute(new NativeBattleNoMercy200Spawn(0x63CEF0));
                break;
            case 114:
                Execute(new NativeBattleNoMercy200Sequence(actor, 3));
                Execute(new NativeBattleNoMercy200Read(564, 114688));
                break;
            case 119:
                Execute(new NativeBattleNoMercy200Flash());
                Execute(new NativeBattleNoMercy200Spawn(0x63D1F0));
                break;
            case 120:
                Execute(new NativeBattleNoMercy200Spawn(0x63D370));
                break;
            case 128:
                Execute(new NativeBattleNoMercy200Spawn(0x63DAF0));
                break;
            case 139:
                Execute(new NativeBattleNoMercy200Sequence(actor, 6));
                Execute(new NativeBattleNoMercy200Spawn(0x63DD10));
                break;
            case 141:
                Execute(new NativeBattleNoMercy200Contacts());
                break;
        }
        if (tick <= 8)
        {
            Execute(new NativeBattleNoMercy200Stage(tick << 8));
        }
        else if (tick >= 166)
        {
            Execute(new NativeBattleNoMercy200Stage((174 - tick) << 8));
        }
        task = pool.Slots[slot].WriteWord(12, unchecked((ushort)(pool.Slots[slot].ReadWord(12) + 1)));
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        bool completed = unchecked((short)task.ReadWord(12)) > 174;
        if (completed)
        {
            Execute(new NativeBattleNoMercy200Stage(0));
        }
        return new(frame, pool, completed ? 2 : 0);

        NativeBattleNoMercy200Resources Resources() => frame.Memory.NoMercy200?.Resources ?? throw new NotSupportedException("Native639380 requires resources retained by its actual entry.");
        void Execute(NativeBattleNoMercy200Operation operation)
        {
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(current => current.DescriptorAddress == pool.DescriptorAddress ? pool : current).ToImmutableArray() } };
            frame = inputs.Execute(frame, operation);
            pool = frame.Effect.Pools.Single(current => current.DescriptorAddress == pool.DescriptorAddress);
        }
    }

    private static int ReadDword(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadInt32LittleEndian(staging.Read(address, 4));

    private static NativeBattleActionMatrix ReadMatrix(NativeBattleFileStaging staging, uint address) => new(Enumerable.Range(0, 9).Select(lane => BinaryPrimitives.ReadInt16LittleEndian(staging.Read(address + (uint)(lane * 2), 2))).ToImmutableArray(), ReadDword(staging, address + 20), ReadDword(staging, address + 24), ReadDword(staging, address + 28), 4096);

    private static NativeBattleFileStaging StoreMatrix(NativeBattleFileStaging staging, uint address, NativeBattleActionMatrix matrix)
    {
        byte[] rotation = new byte[18];
        for (int lane = 0; lane < 9; lane++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rotation.AsSpan(lane * 2), matrix.Rotation[lane]);
        }
        byte[] translation = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(translation, matrix.X);
        BinaryPrimitives.WriteInt32LittleEndian(translation.AsSpan(4), matrix.Y);
        BinaryPrimitives.WriteInt32LittleEndian(translation.AsSpan(8), matrix.Z);
        return staging.Write(address, rotation).Write(address + 20, translation);
    }
}
