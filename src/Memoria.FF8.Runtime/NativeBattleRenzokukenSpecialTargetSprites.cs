using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenSpecialController
{
    public static NativeBattleRenzokuken160ChildStep AdvanceTargetSprites(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleSearchSpriteResource sprite, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll, int bone, int emissionLastFrame, int emissionCount, int speedRange, int speedBase, int? secondBone = null, bool random335 = false, int scaleRange = 1536, int scaleBase = 2048)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = unchecked((short)task.ReadWord(12));
        NativeBattleRenzokukenSpecialState state = ReadState(frame);
        byte[] bank = state.ExtraStorage.ToArray();
        bool advance = (inputs.GraphicsFlags & 0x201) == 0;
        if (advance && counter <= emissionLastFrame)
        {
            Func<byte, int, short, NativeBattleVector> read = inputs.ReadBone ?? throw new NotSupportedException("Native target sprites require their original body-bone endpoints.");
            byte target = checked((byte)state.Core.FirstTarget);
            NativeBattleVector end;
            (int X, int Y, int Z) direction = default;
            if (random335)
            {
                end = read(target, bone, 4096);
                end = end with { Y = unchecked((short)(end.Y - 600)), Z = unchecked((short)(end.Z - 550)) };
            }
            else
            {
                NativeBattleVector first = read(target, bone, 0);
                end = read(target, bone, 4096);
                direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(end.X - first.X, end.Y - first.Y, end.Z - first.Z);
                if (secondBone is int other)
                {
                    NativeBattleVector secondStart = read(target, other, 0);
                    end = read(target, other, 4096);
                    var secondDirection = NativeBattleActionGeometryRules.NormalizeVector56BC50(end.X - secondStart.X, end.Y - secondStart.Y, end.Z - secondStart.Z);
                    direction = NativeBattleActionGeometryRules.NormalizeVector56BC50((direction.X + secondDirection.X) >> 1, (direction.Y + secondDirection.Y) >> 1, (direction.Z + secondDirection.Z) >> 1);
                }
            }
            for (int emission = 0; emission < emissionCount; emission++)
            {
                int offset = TargetFree(bank);
                if (offset == bank.Length)
                {
                    break;
                }
                BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(offset), 2);
                GroupPut(bank, offset + 4, 0);
                GroupPut(bank, offset + 6, Rand() % scaleRange + scaleBase);
                GroupPut(bank, offset + 8, end.X);
                GroupPut(bank, offset + 10, end.Y);
                GroupPut(bank, offset + 12, end.Z);
                frame = SetExtraPaddingUnknown(frame, offset / 32, true);
                if (random335)
                {
                    direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(Rand() % 1536 - 768, -2304 - Rand() % 768, Rand() % 768 + 2048);
                }
                int speed = Rand() % speedRange + speedBase;
                GroupPut(bank, offset + 16, unchecked(direction.X * speed) >> 12);
                GroupPut(bank, offset + 18, unchecked(direction.Y * speed) >> 12);
                GroupPut(bank, offset + 20, unchecked(direction.Z * speed) >> 12);
                if (inputs.CapturePresentation)
                {
                    NativeBattleRenzokukenVisualLifetimes.Allocated(frame.Effect, 3, offset / 32);
                }
            }
        }
        int alive = 0;
        for (int offset = 0; offset < bank.Length; offset += 32)
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(bank.AsSpan(offset)) != 2)
            {
                continue;
            }
            NativeBattleRenzokuken160Sprite current = new(sprite, unchecked((ushort)GroupWord(bank, offset + 4)), new(GroupWord(bank, offset + 8), GroupWord(bank, offset + 10), GroupWord(bank, offset + 12)), GroupWord(bank, offset + 6), null, 0) { PilotIdentity = inputs.CapturePresentation ? NativeBattleRenzokukenVisualLifetimes.Particle(frame.Effect, 3, offset / 32, sprite.Address) : null };
            NativeBattleRenzokuken160SpritePackets drawn = NativeBattleRenzokukenSpecialSpriteDrawing.Draw64DD70(current, view, projection, cameraRoll);
            frame = inputs.Execute(frame, new NativeBattleRenzokukenSpecialSpriteDraw(drawn.Packets));
            if (!advance)
            {
                continue;
            }
            if (drawn.AnimationEnded)
            {
                BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(offset), 0);
                continue;
            }
            GroupPut(bank, offset + 4, GroupWord(bank, offset + 4) + 1);
            short scale = GroupWord(bank, offset + 6);
            GroupPut(bank, offset + 6, scale + (scale >> 4));
            for (int axis = 0; axis < 3; axis++)
            {
                short speed = GroupWord(bank, offset + 16 + axis * 2);
                GroupPut(bank, offset + 8 + axis * 2, GroupWord(bank, offset + 8 + axis * 2) + speed);
                GroupPut(bank, offset + 16 + axis * 2, speed - (speed >> 4));
            }
            alive++;
        }
        frame = Store(frame, ReadState(frame) with { ExtraStorage = bank.ToImmutableArray() });
        if (!advance)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool.WriteSlot(slot, task), alive == 0 ? 2 : 0);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    private static int TargetFree(byte[] bytes)
    {
        int offset = 0;
        while (offset < bytes.Length && BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset)) != 0)
        {
            offset += 32;
        }
        return offset;
    }
}
