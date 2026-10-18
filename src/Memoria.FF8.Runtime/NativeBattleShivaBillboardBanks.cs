using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaLateSpriteDraw(NativeBattleModuleDraw Sprite, NativeBattleActionMatrix Matrix);

public static class NativeBattleShivaBillboardBanks
{
    public static NativeBattleShivaEarlyTaskStep Advance5C62F0(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleActionMatrix viewWorld, NativeBattleModuleResource sprite, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleShivaLateSpriteDraw, NativeBattleShivaEarlyTaskFrame> drawSprite) => Advance(frame, slot, graphicsFlags, viewWorld, sprite, drawSprite, 0);
    public static NativeBattleShivaEarlyTaskStep Advance5C6BB0(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleActionMatrix viewWorld, NativeBattleModuleResource sprite, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleShivaLateSpriteDraw, NativeBattleShivaEarlyTaskFrame> drawSprite) => Advance(frame, slot, graphicsFlags, viewWorld, sprite, drawSprite, 1);
    public static NativeBattleShivaEarlyTaskStep Advance5C71B0(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleActionMatrix viewWorld, NativeBattleModuleResource sprite, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleShivaLateSpriteDraw, NativeBattleShivaEarlyTaskFrame> drawSprite) => Advance(frame, slot, graphicsFlags, viewWorld, sprite, drawSprite, 2);
    public static NativeBattleShivaEarlyTaskStep Advance5C7CA0(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleActionMatrix viewWorld, NativeBattleModuleResource sprite, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleShivaLateSpriteDraw, NativeBattleShivaEarlyTaskFrame> drawSprite) => Advance(frame, slot, graphicsFlags, viewWorld, sprite, drawSprite, 3);

    public static NativeBattleShivaEarlyTaskFrame Initialize5C7180(NativeBattleShivaEarlyTaskFrame frame)
    {
        ValidatePool(frame.Pool);
        (NativeBattleEffectTaskPool pool, int slot) = frame.Pool.Append(0x5C71B0);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0x0C, 0).WriteWord(0x10, 0).WriteWord(0x12, 0).WriteWord(0x14, unchecked((ushort)-2500));
        return frame with { Pool = pool with { Slots = pool.Slots.SetItem(slot, task) } };
    }

    public static NativeBattleModulePacketBatch Emit571C80(NativeBattleShivaLateSpriteDraw draw, NativeBattleCameraProjection projection, short cameraRoll)
    {
        NativeBattleActionMatrix cameraOrigin = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], draw.Matrix.X, draw.Matrix.Y, draw.Matrix.Z, 4096);
        NativeBattleModuleDraw sprite = draw.Sprite with { Anchor = new(0, 0, 0, 0), DepthOffset = 0, SpriteWorldRotation = draw.Matrix.Rotation };
        return NativeBattleModulePackets.EmitSprite571DC0(sprite, projection with { View = cameraOrigin }, cameraRoll, 17, 2);
    }

    private static NativeBattleShivaEarlyTaskStep Advance(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleActionMatrix viewWorld, NativeBattleModuleResource sprite, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleShivaLateSpriteDraw, NativeBattleShivaEarlyTaskFrame> drawSprite, int kind)
    {
        ValidatePool(frame.Pool);
        uint callback = kind switch { 0 => 0x5C62F0u, 1 => 0x5C6BB0u, 2 => 0x5C71B0u, _ => 0x5C7CA0u };
        if ((uint)slot >= frame.Pool.Slots.Length || frame.Pool.Slots[slot].Callback != callback || (frame.Pool.Slots[slot].Flags & 1) == 0)
        {
            throw new InvalidDataException("Native Shiva billboard bank requires its allocated original worker.");
        }
        NativeBattleModulePoolTask task = frame.Pool.Slots[slot];
        NativeBattleActionMatrix local;
        if (kind == 0)
        {
            NativeBattleShivaRetainedVector anchor = (frame.Memory.Shiva ?? throw new NotSupportedException("Native Shiva circular particles require their retained local anchor.")).LocalAnchor22BCFC8;
            local = NativeBattleGfMatrices.Euler56CE30(new(0, -128, 512)) with { X = anchor.X, Y = anchor.Y, Z = anchor.Z };
        }
        else if (kind == 3)
        {
            local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = 0, Y = 700, Z = -7000 };
        }
        else
        {
            local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, kind == 1 ? Word(task, 0x18) : (short)0)) with { X = Word(task, 0x10), Y = Word(task, 0x12), Z = Word(task, 0x14) };
        }
        NativeBattleActionMatrix light = NativeBattlePoseMatrices.Compose56C2F0(viewWorld, local);
        BankMemory bank = new(frame.Memory.Arena ?? throw new NotSupportedException("Native Shiva billboard particles require their current physical bank."));
        bool paused = (graphicsFlags & 0x201) != 0;
        byte flag = kind switch { 0 => (byte)8, 1 => (byte)1, 2 => (byte)2, _ => (byte)4 };
        uint resource = kind < 2 ? 0xD20E10u : kind == 2 ? 0xD20AD0u : 0xD208B0u;
        ushort spriteFlags = kind == 1 ? (ushort)0x208 : kind == 3 ? (ushort)0 : (ushort)8;
        int alive = 0;
        for (int index = 0; index < 150; index++)
        {
            int record = 0x2C000 + index * 24;
            if ((bank.Byte(record) & flag) == 0)
            {
                continue;
            }
            ArgumentNullException.ThrowIfNull(drawSprite);
            if (sprite.Address != resource)
            {
                throw new InvalidDataException("Native Shiva billboard sprite differs from its actual original resource.");
            }
            NativeBattleVector point;
            if (kind == 0)
            {
                int angle = bank.Word(record + 12);
                int radius = bank.Word(record + 8);
                point = new(unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 12)), bank.Word(record + 10), unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12)));
            }
            else
            {
                point = new(bank.Word(record + 8), bank.Word(record + 10), bank.Word(record + 12));
            }
            int phase = bank.Word(record + 4);
            int selectedPhase = kind == 1 ? Math.Max(phase, 8) : phase;
            short size = bank.Word(record + 6);
            NativeBattleModuleSpriteFrame selected = sprite.ProjectedFrame571C80(selectedPhase);
            NativeBattleActionMatrix matrix;
            (frame, matrix) = Billboard(frame, light, point, size, kind != 3);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = sprite, SpriteFrame = selected, Phase = unchecked((ushort)selectedPhase), Anchor = new(0, 0, 0, 0), Scale = size, DepthOffset = 0, SpriteWorldRotation = matrix.Rotation, SpriteFlags = spriteFlags };
            frame = drawSprite(frame with { Memory = frame.Memory with { Arena = bank.Arena } }, new(draw, matrix));
            bank.Arena = frame.Memory.Arena ?? throw new InvalidDataException("Native Shiva billboard drawing lost its current arena.");
            if (paused)
            {
                continue;
            }
            int next = unchecked((short)(phase + 1));
            bank.PutWord(record + 4, next);
            if (selected.NextOffset < 0)
            {
                bank.PutDword(record, 0);
                continue;
            }
            if (kind == 0)
            {
                if (next <= 8)
                {
                    bank.PutWord(record + 8, bank.Word(record + 8) - bank.Word(record + 16));
                }
                int angular = bank.Word(record + 20);
                bank.PutWord(record + 12, bank.Word(record + 12) + angular);
                bank.PutWord(record + 20, angular + (angular >> 3));
                bank.PutWord(record + 10, bank.Word(record + 10) - bank.Word(record + 18));
            }
            else if (kind is 1 or 2)
            {
                for (int coordinate = 0; coordinate < 3; coordinate++)
                {
                    int velocity = bank.Word(record + 16 + coordinate * 2);
                    bank.PutWord(record + 8 + coordinate * 2, bank.Word(record + 8 + coordinate * 2) + velocity);
                    int updated = kind == 2 && coordinate == 2 ? velocity + (velocity >> 4) : velocity - (velocity >> (kind == 1 ? 4 : 3));
                    bank.PutWord(record + 16 + coordinate * 2, updated);
                }
            }
            alive++;
        }
        if (paused)
        {
            return new(frame with { Memory = frame.Memory with { Arena = bank.Arena } }, 0);
        }
        int counter = Word(frame.Pool.Slots[slot], 0x0C);
        if (kind == 1)
        {
            task = frame.Pool.Slots[slot];
            int speed = Word(task, 0x1A);
            task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) - task.ReadWord(0x1A)))).WriteWord(0x1A, unchecked((ushort)(speed - (speed >> 4))));
            frame = frame with { Pool = frame.Pool with { Slots = frame.Pool.Slots.SetItem(slot, task) } };
        }
        int end = kind switch { 0 => 16, 1 => 22, 2 => 18, _ => 26 };
        if (counter >= 0 && counter <= end)
        {
            int maximum = kind switch { 0 => 4, 1 => 3, 2 => 5, _ => 3 };
            for (int emitted = 0; emitted < maximum; emitted++)
            {
                int record = bank.FirstFree();
                if (record < 0)
                {
                    break;
                }
                bank.PutDword(record, flag);
                bank.PutWord(record + 4, 0);
                if (kind == 0)
                {
                    bank.PutWord(record + 6, Draw(ref frame) % 384 + 512);
                    bank.PutWord(record + 12, Draw(ref frame) % 4096);
                    int radius = Draw(ref frame) % 600 + 700;
                    bank.PutWord(record + 8, radius);
                    int y = Draw(ref frame) % 800 - 400;
                    bank.PutWord(record + 10, y);
                    bank.PutWord(record + 16, (radius - 600) / 8);
                    bank.PutWord(record + 20, Draw(ref frame) % 90 + 40);
                    bank.PutWord(record + 18, y / (Draw(ref frame) % 10 + 14));
                }
                else if (kind == 3)
                {
                    bank.PutWord(record + 6, Draw(ref frame) % 3584 + 768);
                    bank.PutWord(record + 8, Draw(ref frame) % 8000 - 4000);
                    bank.PutWord(record + 10, -300 - Draw(ref frame) % 3500);
                    bank.PutWord(record + 12, Draw(ref frame) % 8000 - 4000);
                }
                else
                {
                    bank.PutWord(record + 6, Draw(ref frame) % (kind == 1 ? 864 : 3584) + (kind == 1 ? 384 : 1536));
                    int range = kind == 1 ? 160 : 400;
                    bank.PutWord(record + 8, Draw(ref frame) % range - range / 2);
                    bank.PutWord(record + 10, kind == 1 ? Draw(ref frame) % 160 - 80 : -(Draw(ref frame) % 100));
                    bank.PutWord(record + 12, kind == 1 ? -(Draw(ref frame) % 80) : Draw(ref frame) % 400 - 200);
                    int dx = Draw(ref frame) % (kind == 1 ? 2048 : 8192) - (kind == 1 ? 1024 : 4096);
                    int dy = kind == 1 ? Draw(ref frame) % 2048 - 1024 : -(Draw(ref frame) % 2048);
                    int dz = -(kind == 1 ? 1536 : 512) - Draw(ref frame) % (kind == 1 ? 2560 : 1024);
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, dy, dz);
                    int speed = Draw(ref frame) % (kind == 1 ? 400 : 150) + (kind == 1 ? 230 : 130);
                    bank.PutWord(record + 16, unchecked(direction.X * speed) >> 12);
                    bank.PutWord(record + 18, unchecked(direction.Y * speed) >> 12);
                    bank.PutWord(record + 20, unchecked(direction.Z * speed) >> 12);
                }
            }
        }
        task = frame.Pool.Slots[slot].WriteWord(0x0C, unchecked((ushort)(frame.Pool.Slots[slot].ReadWord(0x0C) + 1)));
        frame = frame with { Memory = frame.Memory with { Arena = bank.Arena }, Pool = frame.Pool with { Slots = frame.Pool.Slots.SetItem(slot, task) } };
        return new(frame, Word(task, 0x0C) >= 4 && alive == 0 ? 2 : 0);
    }

    private static (NativeBattleShivaEarlyTaskFrame Frame, NativeBattleActionMatrix Matrix) Billboard(NativeBattleShivaEarlyTaskFrame frame, NativeBattleActionMatrix light, NativeBattleVector point, short size, bool retained)
    {
        NativeBattleFullMacVertex transformed = NativeBattleGfMatrices.TransformMac460840(light, point);
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(transformed.X, transformed.Y, transformed.Z);
        int offset = -(size >> 3);
        int x = unchecked(transformed.X + (direction.X * offset >> 12));
        int y = unchecked(transformed.Y + (direction.Y * offset >> 12));
        int z = unchecked(transformed.Z + (direction.Z * offset >> 12));
        short[] rotation = new short[9];
        if (retained)
        {
            NativeBattleShivaProcess process = frame.Memory.Shiva ?? throw new NotSupportedException("Native Shiva billboard particles require their actual retained matrix owner.");
            if (process.BillboardMatrix22BD0E8.IsDefault || process.BillboardMatrix22BD0E8.Length != 32)
            {
                throw new InvalidDataException("Native Shiva billboard matrix requires its complete physical32-byte owner.");
            }
            byte[] bytes = process.BillboardMatrix22BD0E8.ToArray();
            for (int index = 0; index < rotation.Length; index++)
            {
                rotation[index] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2));
            }
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), x);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), y);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), z);
            frame = frame with { Memory = frame.Memory with { Shiva = process with { BillboardMatrix22BD0E8 = bytes.ToImmutableArray() } } };
        }
        rotation[0] = size;
        rotation[1] = 0;
        rotation[4] = size;
        rotation[5] = 0;
        rotation[8] = size;
        return (frame, new(rotation.ToImmutableArray(), x, y, z, 4096));
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));

    private static ushort Draw(ref NativeBattleShivaEarlyTaskFrame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }

    private static void ValidatePool(NativeBattleEffectTaskPool pool)
    {
        pool.Validate();
        if (pool.DescriptorAddress != 0x22BC198 || pool.BankAddress != 0x22BC1A8 || pool.Stride != 0x24 || pool.Slots.Length != 100)
        {
            throw new InvalidDataException("Native Shiva billboard workers require their original physical child bank.");
        }
    }

    private sealed class BankMemory(NativeBattleEffectArena arena)
    {
        public NativeBattleEffectArena Arena { get; set; } = arena;
        public byte Byte(int address) => Arena.Read(address, 1)[0];
        public short Word(int address) => BinaryPrimitives.ReadInt16LittleEndian(Arena.Read(address, 2));

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
