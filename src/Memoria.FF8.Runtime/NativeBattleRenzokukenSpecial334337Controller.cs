using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRenzokukenSpecialActorPosition(short X, short Z) : NativeBattleRenzokuken160Operation;
public sealed record NativeBattleRenzokukenSpecialCameraSet(short Y) : NativeBattleRenzokuken160Operation;

public static partial class NativeBattleRenzokukenSpecialController
{
    public static NativeBattleRenzokuken160ChildStep Advance334337(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, Func<byte, NativeBattleModuleAnchor> readAnchor)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        ushort effect = frame.Effect.Binding.Effect;
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = unchecked((short)task.ReadWord(12));
        NativeBattleRenzokuken160State state = ReadState(frame).Core;
        if (counter == 0)
        {
            ushort flags = inputs.ReadModel(state.Actor).Flags0;
            task = task.WriteWord(14, (ushort)(flags & 0x800));
            Execute(new NativeBattleRenzokuken160ActorFlags((ushort)(flags | 0x800)));
        }
        if (counter == 1)
        {
            Execute(new NativeBattleRenzokuken160ActorResource(resources.ActorResource, state.InitialControl ? (byte)2 : (byte)1));
            Execute(new NativeBattleRenzokukenSpecialTargetResource(resources.ActorResource, (byte)state.FirstTarget, 0, 1));
        }
        if (counter == 2)
        {
            if (inputs.AbSegments.IsDefaultOrEmpty)
            {
                throw new NotSupportedException("Native5FD230 requires the current reached504BB0 unsigned AB byte bank.");
            }
            int duration = inputs.AbSegments.Sum(value => (int)value);
            state = ReadState(frame).Core with { Duration = duration, AbSegments = inputs.AbSegments };
            byte[] history = state.TrailHistory.ToArray();
            for (int offset = 0; offset < history.Length; offset += 20)
            {
                BinaryPrimitives.WriteInt32LittleEndian(history.AsSpan(offset), 0);
            }
            frame = WriteState(frame, state with { TrailHistory = history.ToImmutableArray() });
            Append(effect switch
            {
                334 => 0x588260,
                335 => 0x586230,
                336 => 0x583B90,
                337 => 0x581510,
                _ => throw new InvalidDataException()
            }, child => child.WriteWord(12, 0));
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            Append(effect switch
            {
                334 => 0x5887E0,
                335 => 0x5867B0,
                336 => 0x584110,
                337 => 0x581A90,
                _ => throw new InvalidDataException()
            }, child => child.WriteWord(12, 0).WriteWord(14, (ushort)(value % 24)));
            Append(effect switch
            {
                334 => 0x588910,
                335 => 0x5868E0,
                336 => 0x584240,
                337 => 0x581BC0,
                _ => throw new InvalidDataException()
            }, child => child.WriteWord(12, 0));
        }
        state = ReadState(frame).Core;
        if (state.InitialControl && counter == state.Duration)
        {
            Execute(new NativeBattleRenzokuken160SharedTask(effect switch
            {
                334 => 0x587CA0,
                335 => 0x585C70,
                336 => 0x5835D0,
                337 => 0x580F50,
                _ => throw new InvalidDataException()
            }, 0, state.Actor));
        }
        switch (effect)
        {
            case 334:
                if (counter == 57)
                {
                    Append(0x588FC0, child => child.WriteWord(12, 0));
                }
                if (counter == 101)
                {
                    int child = Stored(0x5892B0);
                    Position(child, state.Origin, state.OriginPadding);
                    Set(child, 24, Rand() % 4096);
                    Scale(child, (Rand() % 768 + 2560) / 3);
                    Append(0x589400, item => item.WriteWord(12, 0));
                }
                if (counter == 142)
                {
                    int child = New(0x589710);
                    Position(child, state.Origin, state.OriginPadding);
                    Scale(child, (Rand() % 2048 + 9216) / 3);
                }
                break;
            case 335:
                if (counter == 13)
                {
                    NativeBattleModuleAnchor anchor = readAnchor((byte)state.FirstTarget);
                    Execute(new NativeBattleRenzokukenSpecialActorPosition(anchor.X, unchecked((short)(anchor.Z + 3850))));
                }
                if (counter > 13 && counter == state.Duration - 5)
                {
                    NativeBattleModelTransform original = inputs.ReadModel(state.Actor).Transform ?? throw new NotSupportedException("Native5857D0 requires its original actor position20.");
                    Execute(new NativeBattleRenzokukenSpecialActorPosition(original.Initial20.X, original.Initial20.Z));
                }
                if (counter == 40)
                {
                    Position(New(0x586F90), state.Origin, state.OriginPadding);
                }
                if (counter == 59)
                {
                    int child = Stored(0x5872C0);
                    Scale(child, (Rand() % 1536 + 4096) / 3);
                    Capture(child);
                }
                break;
            case 336:
                if (counter == 60)
                {
                    int child = New(0x5848F0);
                    Position(child, state.Origin, state.OriginPadding);
                    Scale(child, (Rand() % 2048 + 5632) / 3);
                }
                if (counter == 91)
                {
                    New(0x584A30);
                }
                if (counter == 105)
                {
                    int child = New(0x584DE0);
                    Position(child, state.Origin, state.OriginPadding);
                    Scale(child, (Rand() % 2048 + 8192) / 3);
                }
                if (counter == 120)
                {
                    NativeBattleVector anchor = TargetBone();
                    Position(New(0x584F20), anchor, null);
                    int child = New(0x585220);
                    Position(child, anchor, null);
                    Scale(child, (Rand() % 2560 + 7680) / 3);
                    New(0x585360);
                }
                break;
            case 337:
                if (counter == 70)
                {
                    int child = New(0x582270);
                    Position(child, state.Origin, state.OriginPadding);
                    Scale(child, (Rand() % 1536 + 5632) / 3);
                }
                if (counter == 71)
                {
                    New(0x5823B0);
                }
                if (counter == 84)
                {
                    int child = Stored(0x5826C0);
                    Scale(child, (Rand() % 1024 + 3072) / 3);
                    Capture(child);
                }
                if (counter == 89)
                {
                    NativeBattleVector anchor = TargetBone();
                    Position(New(0x582850), anchor, null);
                    int child = New(0x582B50);
                    Position(child, anchor, null);
                    Scale(child, (Rand() % 2048 + 6144) / 3);
                    New(0x582C90);
                }
                break;
            default:
                throw new InvalidDataException("Native334/335/336/337 controller requires its own original effect binding.");
        }
        if (counter <= 16)
        {
            Execute(new NativeBattleRenzokuken160Stage(counter << 7));
        }
        else if (counter >= state.Duration - 8 && !state.InitialControl)
        {
            Execute(new NativeBattleRenzokuken160Stage((state.Duration - counter) << 8));
        }
        if (counter <= 8 || counter >= state.Duration - 8)
        {
            int phase = counter <= 8 ? counter : state.Duration - counter;
            uint color = NativeBattleGteColorInterpolation.CodeConstants45F270.Interpolate(resources.DefaultModelColor, phase << 9, 0x4060F0);
            Execute(new NativeBattleRenzokuken160ActorColor(color));
        }
        if (counter == 1)
        {
            Execute(new NativeBattleRenzokuken160Sound(resources.AppearanceSound, 0x8000, 128));
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        int result = unchecked((short)task.ReadWord(12)) <= ReadState(frame).Core.Duration ? 0 : 2;
        if (result == 2)
        {
            if (!state.InitialControl)
            {
                Execute(new NativeBattleRenzokuken160Stage(0));
            }
            Execute(new NativeBattleRenzokuken160ActorColor(resources.DefaultModelColor));
            ushort flags = inputs.ReadModel(state.Actor).Flags0;
            Execute(new NativeBattleRenzokuken160ActorFlags((ushort)(task.ReadWord(14) | (flags & 0xF7FF))));
        }
        return new(frame, pool.WriteSlot(slot, task), result);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
        int New(uint callback)
        {
            (pool, int child) = pool.Append(callback);
            Set(child, 12, 0);
            return child;
        }
        int Stored(uint callback)
        {
            (frame, pool, int child) = AllocateStoredTask(frame, pool, callback);
            return child;
        }
        void Set(int child, int offset, int value)
        {
            pool = pool.WriteSlot(child, pool.Slots[child].WriteWord(offset, unchecked((ushort)value)));
        }
        void Scale(int child, int scale)
        {
            Set(child, 28, scale);
            Set(child, 30, scale);
        }
        void Position(int child, NativeBattleVector point, ushort? padding)
        {
            Set(child, 16, point.X);
            Set(child, 18, point.Y);
            Set(child, 20, point.Z);
            if (padding is ushort known)
            {
                Set(child, 22, known);
            }
        }
        NativeBattleVector TargetBone() => (inputs.ReadBone ?? throw new NotSupportedException("Native special Renzokuken requires its target bone241."))((byte)state.FirstTarget, 241, 0);
        void Capture(int child)
        {
            byte[] bank = ReadState(frame).ExtraStorage.ToArray();
            int offset = pool.Slots[child].ReadWord(14) * 32;
            GroupPut(bank, offset + 8, state.Origin.X);
            GroupPut(bank, offset + 10, state.Origin.Y);
            GroupPut(bank, offset + 12, state.Origin.Z);
            GroupPut(bank, offset + 14, state.OriginPadding);
            GroupPut(bank, offset + 24, state.Direction.X);
            GroupPut(bank, offset + 26, state.Direction.Y);
            GroupPut(bank, offset + 28, state.Direction.Z);
            frame = SetExtraPaddingUnknown(frame, offset / 32, false);
            frame = Store(frame, ReadState(frame) with { ExtraStorage = bank.ToImmutableArray() });
        }
        void Execute(NativeBattleRenzokuken160Operation operation)
        {
            frame = inputs.Execute(frame, operation);
        }
        void Append(uint callback, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> initialize)
        {
            (pool, int child) = pool.Append(callback);
            pool = pool.WriteSlot(child, initialize(pool.Slots[child]));
        }
    }
    private static short GroupWord(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
    private static void GroupPut(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), unchecked((short)value));
}
