using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenSpecialController
{
    private static NativeBattleRenzokuken160ChildStep AdvanceLateController(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        ushort effect = frame.Effect.Binding.Effect;
        NativeBattleRenzokukenSpecialState state = ReadState(frame);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = unchecked((short)task.ReadWord(12));
        bool delayedBinding = effect is 339 or 340;
        if (counter == 0)
        {
            ushort flags = inputs.ReadModel(state.Core.Actor).Flags0;
            task = task.WriteWord(14, (ushort)(flags & 0x800));
            Execute(new NativeBattleRenzokuken160ActorFlags((ushort)(flags | 0x800)));
        }
        if (counter == 1 && delayedBinding)
        {
            Execute(new NativeBattleRenzokukenSpecialTargetSequence(checked((byte)state.Core.FirstTarget), 1));
        }
        if (counter == (delayedBinding ? 2 : 1))
        {
            Execute(new NativeBattleRenzokuken160ActorResource(resources.ActorResource, state.Core.InitialControl ? (byte)2 : (byte)1));
            for (int bank = 0; bank < resources.ActorResource.TargetResources.Length; bank++)
            {
                Execute(new NativeBattleRenzokukenSpecialTargetResource(resources.ActorResource, checked((byte)(state.Core.FirstTarget + bank)), checked((byte)bank), 1) { StartSequence = !delayedBinding });
            }
        }
        if (counter == (delayedBinding ? 3 : 2))
        {
            if (inputs.AbSegments.IsDefaultOrEmpty)
            {
                throw new NotSupportedException("Native special Renzokuken requires its reached AB byte bank.");
            }
            frame = WriteState(frame, ReadState(frame).Core with { Duration = inputs.AbSegments.Sum(value => (int)value) + (effect == 342 ? 35 : 0), AbSegments = inputs.AbSegments });
        }
        if (counter == 2)
        {
            (uint trail, uint spiral, uint contact) = LateCallbacks(effect);
            Append(trail);
            byte[] history = ReadState(frame).Core.TrailHistory.ToArray();
            for (int offset = 0; offset < history.Length; offset += 20)
            {
                BinaryPrimitives.WriteInt32LittleEndian(history.AsSpan(offset), 0);
            }
            frame = WriteState(frame, ReadState(frame).Core with { TrailHistory = history.ToImmutableArray() });
            int spiralSlot = Append(spiral);
            Set(spiralSlot, 14, Rand(24));
            Append(contact);
        }
        state = ReadState(frame);
        if (state.Core.InitialControl && counter == state.Core.Duration)
        {
            uint emitter = effect switch
            {
                339 => 0x57EC70,
                340 => 0x57C390,
                341 => 0x57A010,
                342 => 0x577D90,
                343 => 0x575BA0,
                _ => throw new InvalidDataException()
            };
            Execute(new NativeBattleRenzokuken160SharedTask(emitter, 0, state.Core.Actor));
        }
        switch (effect)
        {
            case 339:
                if (counter is 22 or 29 or 72)
                {
                    int stored = Allocate(0x57FF90);
                    Set(stored, 26, counter == 22 ? 2 : counter == 29 ? 4 : 8);
                    SaveRow(stored, state.Core.Origin, LateVector(0, counter == 22 ? -2560 : counter == 29 ? -2048 : -2816, counter == 22 ? 1280 : counter == 29 ? 1536 : 1024));
                }
                if (counter == 72)
                {
                    int first = Allocate(0x580310);
                    Scale(first, 28, (Rand(1024) + 2560) / 3);
                    SaveRow(first, state.Core.Origin, state.Core.Direction);
                    int second = Allocate(0x5804A0);
                    Set(second, 28, 5632);
                    Scale(second, 32, (Rand(1024) + 2560) / 3);
                    SaveRow(second, state.Core.Origin, state.Core.Direction);
                }
                break;
            case 340:
                if (counter is 28 or 35 or 67)
                {
                    int stored = Allocate(counter == 67 ? 0x57D980u : 0x57D6B0u);
                    NativeBattleVector origin = state.Core.Origin;
                    if (counter == 35)
                    {
                        NativeBattleActionMatrix body = inputs.ReadModel(state.Core.Actor).GeometryWorldMatrix ?? throw new NotSupportedException("Native special Renzokuken requires its current actor matrix64.");
                        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(body, new(600, 100, -400));
                        Set(stored, 16, offset.X);
                        Set(stored, 18, offset.Y);
                        Set(stored, 20, offset.Z);
                        origin = AddLate(origin, offset);
                    }
                    Scale(stored, 28, counter == 67 ? (Rand(1280) + 3328) / 3 : (Rand(2304) + 5120) / 3);
                    SaveRow(stored, origin, state.Core.Direction);
                }
                if (counter == 59)
                {
                    int mesh = Append(0x57D840);
                    NativeBattleVector bone = (inputs.ReadBone ?? throw new NotSupportedException("Native special Renzokuken requires its current target bone29."))(checked((byte)state.Core.FirstTarget), 29, 0);
                    Position(mesh, bone);
                    Scale(mesh, 28, (Rand(2048) + 8192) / 3);
                }
                if (counter == 68)
                {
                    int mesh = Append(0x57DB10);
                    Position(mesh, state.Core.Origin with { Y = 550 });
                    Set(mesh, 24, Rand(4096));
                    Set(mesh, 28, 12800);
                    Scale(mesh, 32, (Rand(3072) + 5120) / 3);
                }
                if (counter == 59)
                {
                    Append(0x57DC60);
                }
                if (counter == 67)
                {
                    int sprite = Append(0x57DF60);
                    Position(sprite, state.Core.Origin with { Y = unchecked((short)(state.Core.Origin.Y - 300)) }, true);
                }
                break;
            case 341:
                if (counter == 80)
                {
                    int mesh = Allocate(0x57B330);
                    Scale(mesh, 28, (Rand(1536) + 3584) / 3);
                    SaveRow(mesh, state.Core.Origin, state.Core.Direction);
                }
                if (counter == 87)
                {
                    Set(Append(0x57B520), 14, 15);
                    Set(Append(0x57B880), 14, 15);
                }
                if (state.Core.InitialControl && counter is 90 or 125)
                {
                    ushort flags = inputs.ReadModel(state.Core.Actor).Flags0;
                    Execute(new NativeBattleRenzokuken160ActorFlags(counter == 90 ? (ushort)(flags | 8) : (ushort)(flags & ~8)));
                }
                break;
            case 342:
                if (counter == 22)
                {
                    int mesh = Allocate(0x579440);
                    Scale(mesh, 28, (Rand(1024) + 2304) / 3);
                    SaveRow(mesh, state.Core.Origin, state.Core.Direction);
                }
                if (counter is 105 or 106)
                {
                    int mesh = Allocate(0x5795C0);
                    Set(mesh, 26, counter == 105 ? 20 : 22);
                    Scale(mesh, 28, counter == 105 ? (Rand(768) + 2560) / 3 : (Rand(1408) + 3328) / 3);
                    NativeBattleActionMatrix body = inputs.ReadModel(state.Core.Actor).GeometryWorldMatrix ?? throw new NotSupportedException("Native special Renzokuken requires its current actor matrix64.");
                    var rotated = NativeBattleGfMatrices.RotateLong56C600(body, resources.ImpactAxis[0], resources.ImpactAxis[1], resources.ImpactAxis[2]);
                    int distance = counter == 105 ? -100 : -800;
                    NativeBattleVector origin = AddLate(state.Core.Origin, LateVector(unchecked(distance * rotated.X) >> 12, unchecked(distance * rotated.Y) >> 12, unchecked(distance * rotated.Z) >> 12));
                    SaveRow(mesh, origin, state.Core.Direction);
                }
                break;
            case 343:
                if (counter is 36 or 44)
                {
                    int sprite = Allocate(0x576EC0);
                    Set(sprite, 26, counter == 36 ? 4 : 8);
                    SaveRow(sprite, state.Core.Origin, counter == 36 ? new(1536, -2816, 1280) : new(-512, -2048, 1792));
                }
                if (counter == 55)
                {
                    int mesh = Append(0x577240);
                    Position(mesh, state.Core.Origin, true);
                    Scale(mesh, 28, (Rand(3072) + 7168) / 3);
                    frame = Store(frame, ReadState(frame) with { CapturedMatrix = inputs.ReadModel(state.Core.Actor).GeometryWorldMatrix ?? throw new NotSupportedException("Native special Renzokuken requires its captured actor matrix64.") });
                }
                break;
        }
        state = ReadState(frame);
        if (counter <= 16)
        {
            Execute(new NativeBattleRenzokuken160Stage(counter << 7));
        }
        else if (counter >= state.Core.Duration - 8 && !state.Core.InitialControl)
        {
            Execute(new NativeBattleRenzokuken160Stage((state.Core.Duration - counter) << 8));
        }
        if (counter <= 8 || counter >= state.Core.Duration - 8)
        {
            int phase = counter <= 8 ? counter : state.Core.Duration - counter;
            Execute(new NativeBattleRenzokuken160ActorColor(NativeBattleGteColorInterpolation.CodeConstants45F270.Interpolate(resources.DefaultModelColor, phase << 9, 0x4060F0)));
        }
        if (counter == 1)
        {
            Execute(new NativeBattleRenzokuken160Sound(resources.AppearanceSound, 0x8000, 128));
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        int result = unchecked((short)task.ReadWord(12)) <= state.Core.Duration ? 0 : 2;
        if (result == 2)
        {
            if (!state.Core.InitialControl)
            {
                Execute(new NativeBattleRenzokuken160Stage(0));
            }
            Execute(new NativeBattleRenzokuken160ActorColor(resources.DefaultModelColor));
            ushort flags = inputs.ReadModel(state.Core.Actor).Flags0;
            Execute(new NativeBattleRenzokuken160ActorFlags((ushort)((flags & 0xF7FF) | task.ReadWord(14))));
        }
        return new(frame, pool.WriteSlot(slot, task), result);

        void Execute(NativeBattleRenzokuken160Operation operation) => frame = inputs.Execute(frame, operation);
        int Rand(int maximum)
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value % maximum;
        }
        int Append(uint callback)
        {
            (pool, int child) = pool.Append(callback);
            Set(child, 12, 0);
            return child;
        }
        int Allocate(uint callback)
        {
            (frame, pool, int child) = AllocateStoredTask(frame, pool, callback);
            return child;
        }
        void Set(int child, int offset, int value) => pool = pool.WriteSlot(child, pool.Slots[child].WriteWord(offset, unchecked((ushort)value)));
        void Scale(int child, int offset, int value)
        {
            Set(child, offset, value);
            Set(child, offset + 2, value);
        }
        void Position(int child, NativeBattleVector position, bool copyPadding = false)
        {
            Set(child, 16, position.X);
            Set(child, 18, position.Y);
            Set(child, 20, position.Z);
            if (copyPadding)
            {
                Set(child, 22, state.Core.OriginPadding);
            }
        }
        void SaveRow(int child, NativeBattleVector position, NativeBattleVector direction)
        {
            byte[] bytes = ReadState(frame).ExtraStorage.ToArray();
            int offset = StoredOffset(pool.Slots[child]);
            StoredWrite(bytes, offset + 8, position.X);
            StoredWrite(bytes, offset + 10, position.Y);
            StoredWrite(bytes, offset + 12, position.Z);
            StoredWrite(bytes, offset + 14, state.Core.OriginPadding);
            StoredWrite(bytes, offset + 24, direction.X);
            StoredWrite(bytes, offset + 26, direction.Y);
            StoredWrite(bytes, offset + 28, direction.Z);
            frame = Store(frame, ReadState(frame) with { ExtraStorage = bytes.ToImmutableArray() });
            frame = SetExtraPaddingUnknown(frame, offset / 32, false);
        }
    }

    private static (uint Trail, uint Spiral, uint Contact) LateCallbacks(ushort effect) => effect switch
    {
        339 => (0x57F230, 0x57F7B0, 0x57F8E0),
        340 => (0x57C950, 0x57CED0, 0x57D000),
        341 => (0x57A5D0, 0x57AB50, 0x57AC80),
        342 => (0x578350, 0x5788D0, 0x578A00),
        343 => (0x576160, 0x5766E0, 0x576810),
        _ => throw new NotSupportedException("Native special Renzokuken has no original callback family.")
    };

    private static NativeBattleVector LateVector(int x, int y, int z) => new(unchecked((short)x), unchecked((short)y), unchecked((short)z));

    private static NativeBattleVector AddLate(NativeBattleVector left, NativeBattleVector right) => new(unchecked((short)(left.X + right.X)), unchecked((short)(left.Y + right.Y)), unchecked((short)(left.Z + right.Z)));
}
