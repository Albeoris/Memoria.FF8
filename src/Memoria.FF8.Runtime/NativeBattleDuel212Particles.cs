namespace Memoria.FF8.Runtime;

public abstract record NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212Sprite(NativeBattleModuleDraw Draw) : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212Mesh(uint Address, NativeBattleActionMatrix Matrix, NativeBattleModuleMeshContext Context, uint TemporaryAddress) : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212Inputs(NativeBattleActionMatrix View, Func<NativeBattleDuel212Frame, NativeBattleDuel212Operation, NativeBattleDuel212Frame> Execute);

public static partial class NativeBattleDuel212Controller
{
    public const uint Root = 0x2510238;
    public const uint Targets = 0x2510228;
    public const uint Fragments = 0x2510218;
    public const uint Sprites = 0x2510208;

    public static NativeBattleDuel212Step AdvanceSprite(NativeBattleDuel212Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleDuel212Inputs inputs)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        uint callback = task.Callback;
        if (callback is not (0x6A3750 or 0x6A3800 or 0x6A38B0))
        {
            throw new InvalidDataException("Native Burning Rave sprite requires its original callback.");
        }
        uint address = callback == 0x6A38B0 ? 0x11823C8u : 0x1182534u;
        NativeBattleSearchSpriteResource sprite = Resources(frame).Sprites.Single(sprite => sprite.Address == address);
        NativeBattleModuleResource resource = new(0, address, 0, 0, sprite.Bytes);
        ushort phase = unchecked((ushort)((int)Resources(frame).Dword(address + 8) - SignedByte(task, 23)));
        int scale = Word(task, 18);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            Resource = resource,
            SpriteFrame = resource.ProjectedFrame571C80(phase),
            Phase = phase,
            Anchor = new(Word(task, 12), Word(task, 14), Word(task, 16), 0),
            Scale = scale,
            DepthOffset = callback == 0x6A3750 ? -137 : -(scale >> 2),
            SpriteFlags = 0
        };
        frame = inputs.Execute(frame, new NativeBattleDuel212Sprite(draw));
        task = task.WriteByte(23, unchecked((byte)(task.ReadByte(23) - 1)));
        bool complete = task.ReadByte(23) == 0;
        if (!complete)
        {
            task = task.WriteWord(14, unchecked((ushort)(Word(task, 14) + 4 * SignedByte(task, 21))));
            task = task.WriteWord(16, unchecked((ushort)(Word(task, 16) + 4 * SignedByte(task, 22))));
            task = task.WriteWord(12, unchecked((ushort)(Word(task, 12) + 4 * SignedByte(task, 20))));
        }
        return new(frame, pool.WriteSlot(slot, task), complete ? 2 : 0);
    }

    public static NativeBattleDuel212Step SpawnSprites(NativeBattleDuel212Frame frame, NativeBattleEffectTaskPool pool, int count, byte lifetime, NativeBattleVector origin, int spread, short bias, sbyte vx, sbyte vy, sbyte vz, int scaleSpread, short scaleBase, uint callback)
    {
        // 6A3960 uses its original post-decrement: operand1 creates two rows.
        do
        {
            if (pool.Slots.All(task => (task.Flags & 1) != 0))
            {
                break;
            }
            (pool, int slot) = pool.Append(callback);
            NativeBattleModulePoolTask task = pool.Slots[slot];
            task = task.WriteWord(12, unchecked((ushort)(origin.X + bias + ((spread * Rand()) >> 15))));
            task = task.WriteWord(14, unchecked((ushort)(origin.Y + bias + ((spread * Rand()) >> 15))));
            task = task.WriteWord(16, unchecked((ushort)(origin.Z + bias + ((spread * Rand()) >> 15))));
            task = task.WriteWord(18, unchecked((ushort)(scaleBase + ((scaleSpread * Rand()) >> 15))));
            task = task.WriteByte(20, unchecked((byte)vx)).WriteByte(21, unchecked((byte)vy)).WriteByte(22, unchecked((byte)vz)).WriteByte(23, lifetime);
            pool = pool.WriteSlot(slot, task);
        }
        while (count-- != 0);
        return new(frame, pool, 0);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    public static NativeBattleDuel212Step AdvanceFragment(NativeBattleDuel212Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleDuel212Inputs inputs)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback is not (0x6A30F0 or 0x6A3630 or 0x6A3280 or 0x6A33C0 or 0x6A33E0))
        {
            throw new InvalidDataException("Native Burning Rave fragment requires its original callback.");
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 152);
        frame = frame with { Staging = allocation.Staging };
        int angle = 16 * SignedByte(task, 29);
        int scale = Word(task, 32);
        short sine = unchecked((short)((scale * NativeBattleCameraMath.Sine(angle)) >> 12));
        short cosine = unchecked((short)((scale * NativeBattleCameraMath.Cosine(angle)) >> 12));
        NativeBattleActionMatrix tilt = new([cosine, 0, unchecked((short)-sine), 0, unchecked((short)scale), 0, sine, 0, cosine], 0, 0, 0, 4096);
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Multiply56C090(NativeBattleGfMatrices.Euler701310(new(unchecked((short)(16 * SignedByte(task, 28))), 0, 0)), tilt);
        NativeBattleActionMatrix world = rotation with { X = Word(task, 12), Y = Word(task, 14), Z = Word(task, 16) };
        uint resourceAddress = task.Callback switch { 0x6A3280 => 0x1184174u, 0x6A33C0 => 0x11843DCu, 0x6A33E0 => 0x1184694u, _ => 0x1183FA4u };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(inputs.View, world);
        NativeBattleFileStaging staging = frame.Staging.Write(allocation.Address + 88, new byte[20]).Write(allocation.Address + 120, new byte[20]);
        for (int index = 0; index < 9; index++)
        {
            staging = staging.Write(allocation.Address + 88 + (uint)index * 2, BitConverter.GetBytes(matrix.Rotation[index])).Write(allocation.Address + 120 + (uint)index * 2, BitConverter.GetBytes(tilt.Rotation[index]));
        }
        staging = staging.Write(allocation.Address + 108, BitConverter.GetBytes(matrix.X)).Write(allocation.Address + 112, BitConverter.GetBytes(matrix.Y)).Write(allocation.Address + 116, BitConverter.GetBytes(matrix.Z));
        staging = staging.Write(allocation.Address, BitConverter.GetBytes(resourceAddress)).Write(allocation.Address + 28, new byte[4]);
        frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleDuel212Mesh(resourceAddress, matrix, new(0, null, null, null, null, null, 17, 2), allocation.Address));
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging, 152) };
        task = task.WriteByte(27, unchecked((byte)(task.ReadByte(27) - 1)));
        bool complete = task.ReadByte(27) == 0;
        if (!complete)
        {
            short vy = Word(task, 20);
            task = task.WriteWord(14, unchecked((ushort)(Word(task, 14) + vy)));
            task = task.WriteWord(16, unchecked((ushort)(Word(task, 16) + Word(task, 22))));
            task = task.WriteWord(12, unchecked((ushort)(Word(task, 12) + Word(task, 18))));
            task = task.WriteByte(28, unchecked((byte)(task.ReadByte(28) + task.ReadByte(30))));
            task = task.WriteByte(29, unchecked((byte)(task.ReadByte(29) + task.ReadByte(31))));
            if (task.Callback == 0x6A30F0 && Word(task, 14) >= 0)
            {
                vy = unchecked((short)(-vy >> 1));
            }
            task = task.WriteWord(20, unchecked((ushort)(vy + SignedByte(task, 25))));
            task = task.WriteWord(18, unchecked((ushort)(Word(task, 18) + SignedByte(task, 24))));
            task = task.WriteWord(22, unchecked((ushort)(Word(task, 22) + SignedByte(task, 26))));
        }
        return new(frame, pool.WriteSlot(slot, task), complete ? 2 : 0);
    }

    private static NativeBattleDuel212Resources Resources(NativeBattleDuel212Frame frame) => frame.Memory.Duel212?.Resources ?? throw new NotSupportedException("Native Burning Rave requires original entered resources.");
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static sbyte SignedByte(NativeBattleModulePoolTask task, int offset) => unchecked((sbyte)task.ReadByte(offset));
}
