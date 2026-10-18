using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaModelMeshDraw(NativeBattleStaticMeshResource Resource, NativeBattleActionMatrix Matrix, NativeBattleModuleMeshContext Context);
public sealed record NativeBattleShivaModelBillboardDraw(NativeBattleStaticMeshResource Resource, NativeBattleModuleAnchor Anchor, int Scale, int DepthOffset, NativeBattleModuleMeshContext Context);

public static class NativeBattleShivaModelParticles
{
    public static NativeBattleShivaEarlyTaskStep Advance5C5950(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleActionMatrix entryWorld, byte boneCount, NativeBattleModuleResource sprite, Func<NativeBattleShivaEarlyTaskFrame, int, int, NativeBattleVector> sampleBone502170, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleModuleDraw, NativeBattleShivaEarlyTaskFrame> drawSprite)
    {
        ValidateTask(frame, slot, 0x5C5950);
        BankMemory bank = new(frame.Memory.Arena ?? throw new NotSupportedException("Native Shiva model particles require their current arena."));
        bool paused = (graphicsFlags & 0x201) != 0;
        int alive = 0;
        for (int index = 0; index < 150; index++)
        {
            int record = 0x2C000 + index * 24;
            if ((bank.Byte(record) & 1) == 0)
            {
                continue;
            }
            ArgumentNullException.ThrowIfNull(drawSprite);
            if (sprite.Address != 0xD208B0)
            {
                throw new InvalidDataException("Native Shiva model particles require the originalD208B0 sprite.");
            }
            ushort phase = bank.Uword(record + 4);
            NativeBattleModuleSpriteFrame selected = sprite.ProjectedFrame571C80(unchecked((short)phase));
            int size = bank.Word(record + 6);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = sprite, SpriteFrame = selected, Phase = phase, Anchor = new(bank.Word(record + 8), bank.Word(record + 10), bank.Word(record + 12), 0), Scale = size, DepthOffset = -(size >> 4), Direction = 4096, SpriteFlags = 0 };
            frame = drawSprite(frame with { Memory = frame.Memory with { Arena = bank.Arena } }, draw);
            bank.Arena = frame.Memory.Arena ?? throw new InvalidDataException("Native Shiva model sprite drawing lost its arena.");
            if (paused)
            {
                continue;
            }
            bank.PutWord(record + 4, phase + 1);
            if (selected.NextOffset < 0)
            {
                bank.PutDword(record, 0);
            }
            else
            {
                bank.PutWord(record + 6, size - (size >> 5));
                alive++;
            }
        }
        if (paused)
        {
            return new(frame with { Memory = frame.Memory with { Arena = bank.Arena } }, 0);
        }
        int counter = unchecked((short)frame.Pool.Slots[slot].ReadWord(0x0C));
        if (counter is >= 0 and <= 41)
        {
            for (int emitted = 0; emitted < 4; emitted++)
            {
                int record = bank.FirstFree();
                if (record < 0)
                {
                    break;
                }
                if (boneCount == 0)
                {
                    throw new NotSupportedException("Native Shiva model particles require the actual nonzero skeleton bone count.");
                }
                ArgumentNullException.ThrowIfNull(sampleBone502170);
                bank.PutDword(record, 1);
                bank.PutWord(record + 4, 0);
                bank.PutWord(record + 6, Draw(ref frame) % 1536 + 384);
                int fraction = Draw(ref frame) % 4096;
                int bone = Draw(ref frame) % boneCount;
                NativeBattleVector source = sampleBone502170(frame with { Memory = frame.Memory with { Arena = bank.Arena } }, bone, fraction);
                NativeBattleVector position = NativeBattleGfMatrices.RotateShort56C4F0(entryWorld, source);
                bank.PutWord(record + 8, position.X + entryWorld.X + Draw(ref frame) % 1900 - 950);
                bank.PutWord(record + 10, position.Y + entryWorld.Y + Draw(ref frame) % 1900 - 950);
                bank.PutWord(record + 12, position.Z + entryWorld.Z + Draw(ref frame) % 1900 - 950);
            }
        }
        return Finish(frame, slot, bank.Arena, 4, alive);
    }

    public static NativeBattleShivaEarlyTaskFrame Initialize5C5BB0(NativeBattleShivaEarlyTaskFrame frame)
    {
        ValidatePool(frame.Pool);
        NativeBattleEffectTaskPool pool = frame.Pool;
        (pool, int slot) = pool.Append(0x5C5BE0);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0x0C, 0).WriteWord(0x1C, 35).WriteWord(0x1E, 35).WriteWord(0x22, 1228);
        return frame with { Pool = pool with { Slots = pool.Slots.SetItem(slot, task) } };
    }

    public static NativeBattleShivaEarlyTaskStep Advance5C5BE0(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleActionMatrix entryWorld, NativeBattleStaticMeshResource mesh, Func<NativeBattleShivaEarlyTaskFrame, int, int, NativeBattleVector> sampleBone502170, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleShivaModelBillboardDraw, NativeBattleShivaEarlyTaskFrame> drawMesh)
    {
        ValidateTask(frame, slot, 0x5C5BE0);
        NativeBattleShivaProcess process = frame.Memory.Shiva ?? throw new NotSupportedException("Native Shiva anchor growth requires its two retained physical anchors.");
        NativeBattleModulePoolTask task = frame.Pool.Slots[slot];
        int counter = unchecked((short)task.ReadWord(0x0C));
        if (counter < 47)
        {
            ArgumentNullException.ThrowIfNull(sampleBone502170);
            NativeBattleVector first = sampleBone502170(frame, 26, 6144);
            NativeBattleVector second = sampleBone502170(frame, 32, 6144);
            NativeBattleShivaRetainedVector local = process.LocalAnchor22BCFC8 with { X = (short)((first.X + second.X) / 2), Y = (short)((first.Y + second.Y) / 2), Z = (short)((first.Z + second.Z) / 2) };
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(entryWorld, new(local.X, local.Y, local.Z));
            NativeBattleShivaRetainedVector world = process.WorldAnchor22BCFC0 with { X = unchecked((short)(rotated.X + entryWorld.X)), Y = unchecked((short)(rotated.Y + entryWorld.Y)), Z = unchecked((short)(rotated.Z + entryWorld.Z)) };
            process = process with { LocalAnchor22BCFC8 = local, WorldAnchor22BCFC0 = world };
            frame = frame with { Memory = frame.Memory with { Shiva = process } };
        }
        ArgumentNullException.ThrowIfNull(drawMesh);
        if (mesh.Address != 0xD31A74)
        {
            throw new InvalidDataException("Native Shiva anchor growth requires its originalD31A74 mesh.");
        }
        int scale = unchecked((short)task.ReadWord(0x1C));
        NativeBattleModuleMeshContext context = new(counter < 50 ? 0x33u : 0xF3u, 0, counter < 50 ? null : (counter - 50) * 682, null, null, null, 17, 2);
        NativeBattleShivaRetainedVector anchor = process.WorldAnchor22BCFC0;
        frame = drawMesh(frame, new(mesh, new(anchor.X, anchor.Y, anchor.Z, anchor.W), scale, -(scale >> 3), context));
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(frame, 0);
        }
        task = frame.Pool.Slots[slot];
        if (counter < 36)
        {
            task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + task.ReadWord(0x1E))));
        }
        if (counter >= 46)
        {
            int speed = unchecked((short)task.ReadWord(0x22));
            task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + speed))).WriteWord(0x22, unchecked((ushort)(speed - speed / 8)));
        }
        frame = frame with { Pool = frame.Pool with { Slots = frame.Pool.Slots.SetItem(slot, task) } };
        return Finish(frame, slot, frame.Memory.Arena, 56, 0);
    }

    public static NativeBattleShivaEarlyTaskStep Advance5C5D90(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleActionMatrix currentView, NativeBattleStaticMeshResource mesh, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleShivaModelMeshDraw, NativeBattleShivaEarlyTaskFrame> drawMesh)
    {
        return AdvanceRing(frame, slot, graphicsFlags, currentView, mesh, drawMesh, false);
    }

    public static NativeBattleShivaEarlyTaskStep Advance5C6040(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleActionMatrix viewWorld, NativeBattleStaticMeshResource mesh, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleShivaModelMeshDraw, NativeBattleShivaEarlyTaskFrame> drawMesh)
    {
        return AdvanceRing(frame, slot, graphicsFlags, viewWorld, mesh, drawMesh, true);
    }

    private static NativeBattleShivaEarlyTaskStep AdvanceRing(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleActionMatrix view, NativeBattleStaticMeshResource mesh, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleShivaModelMeshDraw, NativeBattleShivaEarlyTaskFrame> drawMesh, bool forward)
    {
        ValidateTask(frame, slot, forward ? 0x5C6040u : 0x5C5D90u);
        NativeBattleShivaProcess process = frame.Memory.Shiva ?? throw new NotSupportedException("Native Shiva model rings require their retained local and world anchors.");
        BankMemory bank = new(frame.Memory.Arena ?? throw new NotSupportedException("Native Shiva model rings require their current particle arena."));
        NativeBattleShivaRetainedVector world = process.WorldAnchor22BCFC0;
        var point = forward ? (X: 0, Y: 0, Z: 0) : NativeBattleGfMatrices.RotateLong56C600(view, world.X, world.Y, world.Z);
        bool paused = (graphicsFlags & 0x201) != 0;
        byte flag = forward ? (byte)4 : (byte)2;
        int alive = 0;
        for (int index = 0; index < 150; index++)
        {
            int record = 0x2C000 + index * 24;
            if ((bank.Byte(record) & flag) == 0)
            {
                continue;
            }
            ArgumentNullException.ThrowIfNull(drawMesh);
            if (mesh.Address != (forward ? 0xD309A4u : 0xD3048Cu))
            {
                throw new InvalidDataException("Native Shiva model ring requires its original mesh.");
            }
            int phase = bank.Word(record + 4);
            int scale = bank.Word(record + 6);
            NativeBattleActionMatrix matrix;
            if (forward)
            {
                NativeBattleShivaRetainedVector local = process.LocalAnchor22BCFC8;
                matrix = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, bank.Word(record + 18), bank.Word(record + 20))), scale, scale, scale) with { X = local.X, Y = local.Y, Z = local.Z };
                matrix = NativeBattlePoseMatrices.Compose56C2F0(view, matrix);
            }
            else
            {
                matrix = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CD50(new(0, 0, bank.Word(record + 20))), scale, 1280, 0) with { X = unchecked(point.X + view.X), Y = unchecked(point.Y + view.Y), Z = unchecked(point.Z + view.Z) };
            }
            uint flags = forward && phase >= 6 ? 0x33u : 0xF3u;
            int? cue = forward && phase >= 6 ? null : phase < 6 ? 4096 - phase * 682 : (phase - 6) * 682;
            NativeBattleModuleMeshContext context = new(flags, 0, cue, null, null, null, 17, 2);
            frame = drawMesh(frame with { Memory = frame.Memory with { Arena = bank.Arena } }, new(mesh, matrix, context));
            bank.Arena = frame.Memory.Arena ?? throw new InvalidDataException("Native Shiva ring drawing lost its arena.");
            if (paused)
            {
                continue;
            }
            int next = unchecked((short)(phase + 1));
            bank.PutWord(record + 4, next);
            if (next >= (forward ? 10 : 12))
            {
                bank.PutDword(record, 0);
                continue;
            }
            if (forward)
            {
                int speed = bank.Word(record + 22);
                bank.PutWord(record + 6, scale - speed);
                bank.PutWord(record + 22, speed - speed / 6);
            }
            else
            {
                bank.PutWord(record + 20, bank.Word(record + 20) + bank.Word(record + 22));
            }
            alive++;
        }
        if (paused)
        {
            return new(frame with { Memory = frame.Memory with { Arena = bank.Arena } }, 0);
        }
        int counter = unchecked((short)frame.Pool.Slots[slot].ReadWord(0x0C));
        bool spawn = forward ? counter is >= 0 and <= 16 && counter % 4 == 1 : counter is >= 0 and <= 23;
        int free = spawn ? bank.FirstFree() : -1;
        if (free >= 0)
        {
            bank.PutDword(free, flag);
            bank.PutWord(free + 4, 0);
            int scale = Draw(ref frame) % (forward ? 2560 : 304) + (forward ? 6144 : 320);
            bank.PutWord(free + 6, scale);
            if (forward)
            {
                bank.PutWord(free + 18, Draw(ref frame) % 4096);
                bank.PutWord(free + 20, Draw(ref frame) % 2048);
                bank.PutWord(free + 22, unchecked((short)scale) / 5);
            }
            else
            {
                int angle = Draw(ref frame) % 4096;
                int speed = Draw(ref frame) % 55 + 20;
                bank.PutWord(free + 20, angle);
                bank.PutWord(free + 22, (angle & 1) != 0 ? -speed : speed);
            }
        }
        return Finish(frame, slot, bank.Arena, forward ? 10 : 12, alive);
    }

    private static NativeBattleShivaEarlyTaskStep Finish(NativeBattleShivaEarlyTaskFrame frame, int slot, NativeBattleEffectArena? arena, int threshold, int alive)
    {
        NativeBattleModulePoolTask task = frame.Pool.Slots[slot].WriteWord(0x0C, unchecked((ushort)(frame.Pool.Slots[slot].ReadWord(0x0C) + 1)));
        frame = frame with { Memory = frame.Memory with { Arena = arena }, Pool = frame.Pool with { Slots = frame.Pool.Slots.SetItem(slot, task) } };
        return new(frame, unchecked((short)task.ReadWord(0x0C)) >= threshold && alive == 0 ? 2 : 0);
    }

    private static ushort Draw(ref NativeBattleShivaEarlyTaskFrame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }

    private static void ValidateTask(NativeBattleShivaEarlyTaskFrame frame, int slot, uint callback)
    {
        ValidatePool(frame.Pool);
        if ((uint)slot >= frame.Pool.Slots.Length || frame.Pool.Slots[slot].Callback != callback || (frame.Pool.Slots[slot].Flags & 1) == 0)
        {
            throw new InvalidDataException("Native Shiva model particle callback requires its allocated original task.");
        }
    }

    private static void ValidatePool(NativeBattleEffectTaskPool pool)
    {
        pool.Validate();
        if (pool.DescriptorAddress != 0x22BC198 || pool.BankAddress != 0x22BC1A8 || pool.Stride != 0x24 || pool.Slots.Length != 100)
        {
            throw new InvalidDataException("Native Shiva model particles require the original child bank.");
        }
    }

    private sealed class BankMemory(NativeBattleEffectArena arena)
    {
        public NativeBattleEffectArena Arena { get; set; } = arena;
        public byte Byte(int address) => Arena.Read(address, 1)[0];
        public ushort Uword(int address) => BinaryPrimitives.ReadUInt16LittleEndian(Arena.Read(address, 2));
        public short Word(int address) => unchecked((short)Uword(address));

        public int FirstFree()
        {
            for (int index = 0; index < 150; index++)
            {
                int record = 0x2C000 + index * 24;
                if (BinaryPrimitives.ReadUInt32LittleEndian(Arena.Read(record, 4)) == 0)
                {
                    return record;
                }
            }
            return -1;
        }

        public void PutWord(int address, int value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
            Arena = Arena.Write(address, bytes);
        }

        public void PutDword(int address, uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            Arena = Arena.Write(address, bytes);
        }
    }
}
