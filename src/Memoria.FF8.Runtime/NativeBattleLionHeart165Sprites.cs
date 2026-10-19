using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLionHeart165Sprite(NativeBattleModulePacketBatch Batch, bool Common) : NativeBattleLionHeart165Operation;

public static partial class NativeBattleLionHeart165Controller
{
    public static NativeBattleLionHeart165ChildStep AdvanceSpriteParticles(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleLionHeart165Inputs inputs, NativeBattleSearchSpriteResource sprite, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        uint callback = task.Callback;
        uint spriteAddress = callback switch
        {
            0x5EB070 or 0x5EB410 or 0x5EFCE0 => 0xD7B1C8,
            0x5EBBD0 => 0xD7B4D4,
            0x5EC550 or 0x5EE240 => 0xD7AFE0,
            0x5EC7D0 or 0x5EE980 => 0xD7B340,
            _ => throw new NotSupportedException("Native Lion Heart sprite callback is unsupported.")
        };
        if (sprite.Address != spriteAddress)
        {
            throw new InvalidDataException("Native Lion Heart sprite callback requires its actual resource directory.");
        }
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        short counter = Word(task, 12);
        int marker = Word(task, 14);
        NativeBattleEffectArena? arena = frame.Memory.Arena;
        int localLength = callback is 0x5EB410 or 0x5EBBD0 ? 80 : 72;
        NativeBattleTemporaryAllocation localAllocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, localLength);
        uint temporary = localAllocation.Address;
        frame = frame with { Staging = localAllocation.Staging };
        uint bank = callback == 0x5EE980 ? unchecked((uint)ReadBankDword(frame.Staging, 0x23C5680)) : 0x23C34C0u;
        if (callback == 0x5EE980 && bank != 0x20DFAB8)
        {
            throw new NotSupportedException("Native Lion Heart late particle bank requires its actual571B50 address.");
        }
        int capacity = callback == 0x5EE980 ? 500 : 200;
        bool spawn = callback switch
        {
            0x5EB070 => counter <= 48 && counter % 6 == 1,
            0x5EB410 => counter <= 13,
            0x5EBBD0 => counter <= 50,
            0x5EC550 or 0x5EC7D0 => counter <= 0,
            0x5EE240 => counter <= 4,
            0x5EE980 => counter <= 24,
            _ => counter <= 3
        };
        if (!paused && spawn)
        {
            if (callback is 0x5EC550 or 0x5EC7D0 or 0x5EE240)
            {
                frame = frame with { Staging = CopyTaskToStaging(frame.Staging, temporary, task, 16, 8) };
            }
            int requested = callback == 0x5EB070 ? 2 : callback == 0x5EB410 ? 1 : callback is 0x5EBBD0 or 0x5EC550 ? 3 : callback == 0x5EC7D0 ? 12 : callback == 0x5EE240 ? 6 : callback == 0x5EE980 ? 20 : 8;
            for (int index = 0; index < requested; index++)
            {
                uint row = FreeParticle();
                if (row == 0)
                {
                    break;
                }
                NativeBattleFileStaging staging = frame.Staging;
                NativeBattleVector bone = default;
                if (callback == 0x5EB410)
                {
                    byte actor = unchecked((byte)ReadBankDword(staging, 0x23C6870));
                    NativeBattleVector first = inputs.ReadBone(actor, 19, 3584);
                    NativeBattleVector second = inputs.ReadBone(actor, 19, 3584);
                    staging = Point(Point(staging, temporary, first), temporary + 8, second);
                    bone = unchecked((ushort)first.Y) > unchecked((ushort)second.Y) ? second : first;
                    if (unchecked((ushort)first.Y) > unchecked((ushort)second.Y))
                    {
                        staging = CopyPhysical(staging, temporary + 8, temporary, 8);
                    }
                    frame = frame with { Staging = staging };
                    if (bone.Y <= -50)
                    {
                        break;
                    }
                }
                staging = SetBank(WriteBankDword(frame.Staging, row, unchecked((uint)marker)), row + 4, 0);
                int scale = callback is 0x5EB070 or 0x5EB410 ? Rand() % 576 + 896 : callback == 0x5EBBD0 ? Rand() % 384 + 512 : callback == 0x5EC550 ? Rand() % 2560 + 1536 : callback == 0x5EC7D0 ? Rand() % 1664 + 896 : callback == 0x5EE240 ? Rand() % 4096 + 3584 : callback == 0x5EE980 ? Rand() % 1024 + 512 : Rand() % 2560 + 2048;
                staging = SetBank(staging, row + 6, scale);
                if (callback is 0x5EB070 or 0x5EB410)
                {
                    if (callback == 0x5EB070)
                    {
                        bone = inputs.ReadBone(unchecked((byte)ReadBankDword(staging, 0x23C6870)), (byte)(19 + index), 4096);
                    }
                    staging = Point(staging, row + 8, bone);
                    for (uint axis = 0; axis < 6; axis += 2)
                    {
                        staging = SetBank(staging, row + 8 + axis, ReadBankWord(staging, row + 8 + axis) + Rand() % 160 - 80);
                    }
                }
                else if (callback == 0x5EBBD0)
                {
                    int angle = Rand() % 4096;
                    int radius = Rand() % 600 + 700;
                    staging = SetBank(SetBank(staging, row + 8, radius), row + 12, angle);
                    int height = Rand() % 1200 - 600;
                    staging = SetBank(SetBank(staging, row + 10, height), row + 16, (radius - 300) / 16);
                    staging = SetBank(staging, row + 20, Rand() % 90 + 40);
                    staging = SetBank(staging, row + 18, height / (Rand() % 10 + 10));
                }
                else if (callback is 0x5EC550 or 0x5EE240)
                {
                    staging = CopyTaskToStaging(staging, row + 8, task, 16, 8);
                    int range = callback == 0x5EC550 ? 600 : 2000;
                    for (uint axis = 0; axis < 6; axis += 2)
                    {
                        staging = SetBank(staging, row + 8 + axis, ReadBankWord(staging, row + 8 + axis) + Rand() % range - range / 2);
                    }
                }
                else if (callback == 0x5EC7D0)
                {
                    staging = CopyTaskToStaging(staging, row + 8, task, 16, 8);
                    int dx = Rand() % 8192 - 4096;
                    int dy = Rand() % 8192 - 4096;
                    int dz = Rand() % 8192 - 4096;
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, dy, dz);
                    staging = LongVector(staging, temporary + 40, direction.X, direction.Y, direction.Z);
                    int offset = Rand() % 300 + 150;
                    staging = SetBank(SetBank(SetBank(staging, row + 8, ReadBankWord(staging, row + 8) + ((offset * direction.X) >> 12)), row + 10, ReadBankWord(staging, row + 10) + ((offset * direction.Y) >> 12)), row + 12, ReadBankWord(staging, row + 12) + ((offset * direction.Z) >> 12));
                    int speed = Rand() % 350 + 150;
                    staging = SetBank(SetBank(SetBank(staging, row + 16, (speed * direction.X) >> 12), row + 18, (speed * direction.Y) >> 12), row + 20, (speed * direction.Z) >> 12);
                }
                else
                {
                    int angle = Rand() % 4096;
                    int dx = NativeBattleCameraMath.Sine(angle);
                    int dz = NativeBattleCameraMath.Cosine(angle);
                    staging = WriteBankDword(WriteBankDword(staging, temporary + 40, unchecked((uint)dx)), temporary + 48, unchecked((uint)dz));
                    int radius = callback == 0x5EE980 ? ReadBankWord(staging, 0x23C6936) - Rand() % 100 : Rand() % 200 + 400;
                    staging = SetBank(staging, row + 8, (radius * dx) >> 12);
                    staging = SetBank(staging, row + 10, callback == 0x5EE980 ? Rand() % 200 - 100 : -(Rand() % 50));
                    staging = SetBank(staging, row + 12, (radius * dz) >> 12);
                    if (callback == 0x5EFCE0)
                    {
                        int speed = Rand() % 280 + 120;
                        staging = SetBank(SetBank(staging, row + 16, (speed * dx) >> 12), row + 20, (speed * dz) >> 12);
                    }
                }
                frame = frame with { Staging = staging };
            }
        }
        NativeBattleTemporaryAllocation context = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 180);
        ushort flags = callback is 0x5EB070 or 0x5EB410 ? (ushort)0 : (ushort)8;
        NativeBattleFileStaging current = NativeBattleModuleSpriteTemporary.Begin571C80(context.Staging, context.Address, sprite.Address, flags);
        NativeBattleActionMatrix parent = view;
        int matrixOffset = localLength == 80 ? 16 : 8;
        if (callback is 0x5EBBD0 or 0x5EE980 or 0x5EFCE0)
        {
            NativeBattleVector angles = callback == 0x5EBBD0 ? new(0, -128, 512) : callback == 0x5EE980 ? new(256, 0, 256) : new(0, 0, 0);
            current = Point(current, temporary, angles);
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(angles) with { X = Word(task, 16), Y = callback == 0x5EFCE0 ? 0 : Word(task, 18), Z = Word(task, 20) };
            parent = NativeBattlePoseMatrices.Compose56C2F0(view, local);
            if (callback == 0x5EE980)
            {
                parent = parent with { Rotation = local.Rotation };
            }
        }
        current = Forget(StoreMatrix(current, temporary + (uint)matrixOffset, parent), temporary + (uint)matrixOffset + 18, 2);
        frame = frame with { Staging = current };
        NativeBattleModuleResource resource = new(0, sprite.Address, 0, 0, sprite.Bytes);
        int alive = 0;
        for (int index = 0; index < capacity; index++)
        {
            uint row = bank + (uint)index * 24;
            if (ReadBankDword(frame.Staging, row) != marker)
            {
                continue;
            }
            short age = ReadBankWord(frame.Staging, row + 4);
            short scale = ReadBankWord(frame.Staging, row + 6);
            NativeBattleVector point = new(ReadBankWord(frame.Staging, row + 8), ReadBankWord(frame.Staging, row + 10), ReadBankWord(frame.Staging, row + 12));
            if (callback == 0x5EBBD0)
            {
                point = point with { X = unchecked((short)((point.X * NativeBattleCameraMath.Cosine(point.Z)) >> 12)), Z = unchecked((short)((point.X * NativeBattleCameraMath.Sine(point.Z)) >> 12)) };
                frame = frame with { Staging = Point(frame.Staging, temporary + 8, point) };
            }
            ushort phase = unchecked((ushort)(callback == 0x5EBBD0 ? age >> 1 : age));
            NativeBattleModuleSpriteFrame animation = resource.ProjectedFrame571C80(phase);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = animation, Phase = phase, Anchor = new(point.X, point.Y, point.Z, 0), Scale = scale, DepthOffset = callback is 0x5EBBD0 or 0x5EE980 ? 0 : -(scale >> 3), SpriteFlags = flags };
            NativeBattleActionMatrix billboard = NativeBattlePoseMatrices.ModuleSpriteCamera(draw, parent, cameraRoll);
            if (callback is not (0x5EBBD0 or 0x5EE980))
            {
                NativeBattleModuleDraw unshifted = draw with { DepthOffset = 0 };
                NativeBattleActionMatrix origin = NativeBattlePoseMatrices.ModuleSpriteCamera(unshifted, parent, cameraRoll);
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(origin.X, origin.Y, origin.Z);
                uint directionOffset = temporary + (uint)(localLength == 80 ? 48 : 40);
                uint pointOffset = temporary + (uint)(localLength == 80 ? 64 : 56);
                frame = frame with { Staging = LongVector(LongVector(frame.Staging, directionOffset, direction.X, direction.Y, direction.Z), pointOffset, billboard.X, billboard.Y, billboard.Z) };
            }
            NativeBattleModuleSpriteTemporaryResult emitted = NativeBattleModuleSpriteTemporary.Emit571C80(frame.Staging, context.Address, draw, projection with { View = parent }, cameraRoll, 17, 2);
            frame = inputs.Execute(frame with { Staging = emitted.Staging, Memory = frame.Memory with { Arena = arena } }, new NativeBattleLionHeart165Sprite(emitted.Batch, callback == 0x5EBBD0));
            arena = frame.Memory.Arena;
            if (paused)
            {
                continue;
            }
            current = frame.Staging;
            if (animation.NextOffset < 0)
            {
                frame = frame with { Staging = WriteBankDword(current, row, 0) };
                continue;
            }
            current = SetBank(current, row + 4, age + 1);
            if (callback == 0x5EBBD0)
            {
                if (unchecked((short)(age + 1)) <= 16)
                {
                    current = SetBank(current, row + 8, ReadBankWord(current, row + 8) - ReadBankWord(current, row + 16));
                }
                int angular = ReadBankWord(current, row + 20);
                current = SetBank(SetBank(SetBank(current, row + 12, ReadBankWord(current, row + 12) + angular), row + 20, angular + (angular >> 3)), row + 10, ReadBankWord(current, row + 10) - ReadBankWord(current, row + 18));
            }
            else if (callback is 0x5EC7D0 or 0x5EFCE0)
            {
                for (uint axis = 0; axis < 6; axis += 2)
                {
                    if (callback == 0x5EFCE0 && axis == 2)
                    {
                        continue;
                    }
                    int speed = ReadBankWord(current, row + 16 + axis);
                    current = SetBank(SetBank(current, row + 8 + axis, ReadBankWord(current, row + 8 + axis) + speed), row + 16 + axis, speed - (speed >> 3));
                }
            }
            frame = frame with { Staging = current };
            alive++;
        }
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(NativeBattleTemporaryMemory.Release5082D0(frame.Staging, 180), localLength) };
        if (paused)
        {
            return new(frame with { Memory = frame.Memory with { Arena = arena } }, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        bool released = callback == 0x5EB070 ? Word(task, 12) >= 58 : callback == 0x5EB410 ? task.ReadWord(12) > 13 : callback == 0x5EBBD0 ? Word(task, 12) >= 44 : alive == 0;
        if (released && callback is 0x5EB070 or 0x5EBBD0)
        {
            current = frame.Staging;
            for (int index = 0; index < capacity; index++)
            {
                uint row = bank + (uint)index * 24;
                if (ReadBankDword(current, row) == marker)
                {
                    current = WriteBankDword(current, row, 0);
                }
            }
            frame = frame with { Staging = current };
        }
        return new(frame with { Memory = frame.Memory with { Arena = arena } }, pool with { Slots = pool.Slots.SetItem(slot, task) }, released ? 2 : 0);

        bool InArena(uint address) => address >= 0x20DFAB8 && (ulong)address - 0x20DFAB8 < NativeBattleEffectArena.Length;
        short ReadBankWord(NativeBattleFileStaging staging, uint address) => InArena(address) ? BinaryPrimitives.ReadInt16LittleEndian((arena ?? throw new NotSupportedException("Native Lion Heart late particles require their retained resource arena.")).Read(checked((int)(address - 0x20DFAB8)), 2)) : ReadWord(staging, address);
        int ReadBankDword(NativeBattleFileStaging staging, uint address) => InArena(address) ? BinaryPrimitives.ReadInt32LittleEndian((arena ?? throw new NotSupportedException("Native Lion Heart late particles require their retained resource arena.")).Read(checked((int)(address - 0x20DFAB8)), 4)) : ReadDword(staging, address);
        NativeBattleFileStaging SetBank(NativeBattleFileStaging staging, uint address, int value)
        {
            if (!InArena(address))
            {
                return Set(staging, address, value);
            }
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
            arena = (arena ?? throw new NotSupportedException("Native Lion Heart late particles require their retained resource arena.")).Write(checked((int)(address - 0x20DFAB8)), bytes);
            return staging;
        }
        NativeBattleFileStaging WriteBankDword(NativeBattleFileStaging staging, uint address, uint value)
        {
            if (!InArena(address))
            {
                return WriteDword(staging, address, value);
            }
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            arena = (arena ?? throw new NotSupportedException("Native Lion Heart late particles require their retained resource arena.")).Write(checked((int)(address - 0x20DFAB8)), bytes);
            return staging;
        }
        uint FreeParticle()
        {
            for (int index = 0; index < capacity; index++)
            {
                uint row = bank + (uint)index * 24;
                if (ReadBankDword(frame.Staging, row) == 0)
                {
                    return row;
                }
            }
            return 0;
        }
        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

}
