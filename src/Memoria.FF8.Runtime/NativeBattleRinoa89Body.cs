using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa89WorldPoint(int X, int Y, int Z)
{
    public static implicit operator NativeBattleRinoa89WorldPoint(NativeBattleVector vector) => new(vector.X, vector.Y, vector.Z);
}

public abstract record NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89Sequence(byte Sequence) : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89Visibility(uint Callback) : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89BoneEmission(uint Model, uint Reference, int Extras) : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89Program(uint Source, int WorkOffset, int ClearSize, bool Initialize, bool Common, NativeBattleRinoa89WorldPoint Anchor, short Depth, bool Advance = true) : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89ApproachMesh(int Delta, NativeBattleRinoa89WorldPoint Anchor) : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89ReturnMeshes(int Age, int Ratio, NativeBattleVector Anchor) : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89SpawnBeam(short X, short Y, bool Wide) : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89Stars(NativeBattleVector Anchor) : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89CaptureScene : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89Fade(int Age, int Red, int Green, int Blue, int In, int Hold, int Out) : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89DrawShards : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89PublishView : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89DrawBody : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89RestoreCamera : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89DrawParticles(uint Callback) : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89PollIo : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89AdvanceIo : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89Sound(uint Source) : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89CameraUpdate(short Age, NativeBattleVector Center, NativeBattleVector Caster) : NativeBattleRinoa89Operation;
public sealed record NativeBattleRinoa89BodyInputs(NativeBattleRinoa89Resources Resources, Func<int, NativeBattleGraphicsModel> ReadModel, Action<int, NativeBattleGraphicsModel> WriteModel, Func<NativeBattleRinoa89Frame, NativeBattleRinoa89Operation, NativeBattleRinoa89Frame> Execute, Func<NativeBattleRinoa89Frame, NativeBattleModulePoolTask, NativeBattleRinoa89BoneStep> ReadBone19, Func<NativeBattleRinoa89Frame, NativeBattleRinoa89Frame> ApplyContacts6FC410, Func<NativeBattleRinoa89Frame, (NativeBattleRinoa89Frame Frame, bool Busy)> Poll534270, Func<NativeBattleRinoa89Frame, (NativeBattleRinoa89Frame Frame, bool Complete)> Reload534300);

public static partial class NativeBattleRinoa89Controller
{
    public static NativeBattleRinoa89TaskStep AdvanceBody6FA780(NativeBattleRinoa89Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRinoa89BodyInputs inputs)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x6FA780)
        {
            throw new InvalidDataException("Native6FA780 requires its actual1052-byte body.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native6FA780 requires its actual selected caster.");
        NativeBattleEffectOwnedModelResource body = inputs.Resources.Body ?? throw new NotSupportedException("Native6FA780 requires its original embedded model.");
        int actor = parameters.Actor78;
        short age = Signed(12);
        NativeBattleVector center = new(Signed(216), Signed(218), Signed(220));
        switch (task.ReadWord(12))
        {
            case 30:
                Execute(new NativeBattleRinoa89Sequence(2));
                break;
            case 32:
                Execute(new NativeBattleRinoa89Sequence(3));
                break;
            case 222:
                Execute(new NativeBattleRinoa89Sequence(4));
                break;
        }
        int selector = task.ReadWord(12) switch { 7 => 1, 30 => 2, 32 => 3, _ => -1 };
        if (selector >= 0)
        {
            NativeBattleRinoa89ModelStep selected = NativeBattleRinoa89Model.Select6574D0(frame, task, body, (short)selector);
            frame = selected.Frame;
            task = selected.Task;
        }
        if ((uint)age < 22)
        {
            if (age == 0)
            {
                Execute(new NativeBattleRinoa89Visibility(0x6FBDE0));
            }
            PositionZ(center.Z - 5000 * ((age << 12) / 22) / 4096 + 6000);
            if (age >= 3)
            {
                uint reference = (age % 6) switch { 0 => 0x14AF460u, 1 => 0x14AF468u, 3 => 0x14AF470u, 4 => 0x14AF478u, _ => 0u };
                if (reference != 0)
                {
                    Execute(new NativeBattleRinoa89BoneEmission(checked(pool.BankAddress + (uint)(slot * pool.Stride) + 52), reference, age % 6 is 3 or 4 ? 4 : 0));
                }
                if (age % 7 == 0)
                {
                    Execute(new NativeBattleRinoa89BoneEmission(ModelAddress(parameters.Actor78), checked(21689472u + 8u * (uint)((age / 7) & 1)), 0));
                }
            }
        }
        if ((uint)(age - 30) <= 3)
        {
            PositionY(unchecked(-1100 * (age - 30 + 1)) >> 2);
        }
        if ((uint)(age - 22) < 28)
        {
            if (age == 22)
            {
                Store();
                NativeBattleEffectTaskTransfer detached = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, Sprites);
                NativeBattleEffectTaskPool sprites = NativeBattleEffectTaskPool.Initialize(Sprites, ArenaAddress + 3136, 48, 32);
                frame = frame with { Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(detached.Memory, [sprites]), Effect = detached.Effect with { Pools = detached.Effect.Pools.Add(sprites) } };
                Refresh();
                Execute(new NativeBattleRinoa89Visibility(0x6FBE00));
            }
            PositionZ(center.Z - 12000 * (((age - 22) << 12) / 28) / 4096 + 6000);
        }
        if ((uint)(age - 75) < 20)
        {
            Execute(new NativeBattleRinoa89Program(0x14A6370, 280, 104, age == 75, false, new NativeBattleRinoa89WorldPoint(center.X, center.Y, center.Z + 15000), 0));
        }
        if ((uint)(age - 87) < 15)
        {
            Execute(new NativeBattleRinoa89ApproachMesh(age - 87, new NativeBattleRinoa89WorldPoint(center.X, center.Y, center.Z)));
        }
        if ((uint)(age - 102) < 60)
        {
            switch (task.ReadWord(12))
            {
                case 102:
                    EnableParticles(true);
                    Execute(new NativeBattleRinoa89SpawnBeam(0, 0, false));
                    break;
                case 112:
                case 142:
                    Execute(new NativeBattleRinoa89SpawnBeam(1536, 1536, false));
                    break;
                case 122:
                    Execute(new NativeBattleRinoa89SpawnBeam(3584, 3584, false));
                    break;
                case 132:
                    Execute(new NativeBattleRinoa89SpawnBeam(3584, 1536, false));
                    break;
                case 137:
                    Execute(new NativeBattleRinoa89SpawnBeam(512, 2560, false));
                    break;
                case 147:
                    Execute(new NativeBattleRinoa89SpawnBeam(2560, 0, false));
                    break;
            }
        }
        if ((uint)(age - 32) < 4)
        {
            Execute(new NativeBattleRinoa89Program(0x14A09A0, 280, 112, age == 32, false, Bone19(), -512));
        }
        if ((uint)(age - 162) < 15)
        {
            int delta = age - 162;
            int ratio = (delta << 12) / 15;
            if (delta == 0)
            {
                PositionY(center.Y);
                Flags(0, 12);
                EnableParticles(false);
            }
            PositionZ(center.Z - 13500 * ratio / 4096 + 15000);
            Execute(new NativeBattleRinoa89ReturnMeshes(delta, ratio, Bone19()));
        }
        if ((uint)(age - 177) < 50)
        {
            int delta = age - 177;
            if (delta == 0)
            {
                Flags(12, 0);
                Execute(new NativeBattleRinoa89Program(0x14A6600, 280, 536, true, false, center, 0, false));
                frame = ClearSparks6FA2F0(frame);
                EnableParticles(true);
                Execute(new NativeBattleRinoa89SpawnBeam(0, 0, true));
            }
            else if (delta >= 3)
            {
                if (age == 180)
                {
                    Store();
                    frame = inputs.ApplyContacts6FC410(frame);
                    Refresh();
                }
                Execute(new NativeBattleRinoa89Program(0x14A6600, 280, 536, false, false, center, 0));
            }
        }
        if ((uint)(age - 37) < 189)
        {
            Execute(new NativeBattleRinoa89Program(0x14A9FF0, 816, 236, age == 37, true, center with { Y = 0 }, 0));
            Execute(new NativeBattleRinoa89Stars(center with { Y = 0 }));
        }
        if ((uint)(age - 36) < 39)
        {
            int delta = age - 36;
            if (delta == 0)
            {
                Execute(new NativeBattleRinoa89CaptureScene());
            }
            if (delta < 8)
            {
                Execute(new NativeBattleRinoa89Fade(delta, 255, 255, 255, 0, 1, 7));
            }
            Execute(new NativeBattleRinoa89DrawShards());
        }
        if ((uint)(age - 36) < 190)
        {
            Execute(new NativeBattleRinoa89PublishView());
        }
        if (task.ReadWord(12) < 271)
        {
            if ((task.ReadByte(52) & 4) == 0)
            {
                Execute(new NativeBattleRinoa89DrawBody());
            }
            if ((task.ReadByte(52) & 8) == 0)
            {
                NativeBattleRinoa89ModelStep advanced = NativeBattleRinoa89Model.Advance6FBDB0(frame, task, body);
                frame = advanced.Frame;
                task = advanced.Task;
            }
        }
        if ((uint)(age - 197) < 62)
        {
            Execute(new NativeBattleRinoa89Fade(age - 197, 255, 255, 255, 30, 1, 31));
            if (age == 227)
            {
                Execute(new NativeBattleRinoa89RestoreCamera());
                NativeBattleGraphicsModel caster = inputs.ReadModel(actor);
                NativeBattleModelTransform transform = caster.Transform ?? throw new NotSupportedException("Native6FA780 release requires its retained caster transform.");
                inputs.WriteModel(actor, caster with { Flags0 = unchecked((ushort)(caster.Flags0 & ~12)), Transform = transform with { Position28 = new(Signed(208), Signed(210), Signed(212)), Vertical34 = Signed(214) } });
                Execute(new NativeBattleRinoa89Visibility(0x6FBE40));
                Execute(new NativeBattleRinoa89Visibility(0x6FBE80));
            }
        }
        frame = SpawnLateSparks6FA780(frame, age);
        if (frame.Staging.Read(0x255658E, 1)[0] != 0)
        {
            Execute(new NativeBattleRinoa89DrawParticles(0x6FC500));
        }
        if (frame.Staging.Read(0x255658D, 1)[0] != 0)
        {
            Execute(new NativeBattleRinoa89DrawParticles(0x6FC8B0));
        }
        frame = AdvanceSparks6FCCF0(frame);
        frame = AdvanceEmitters6FC750(frame);
        Store();
        if (age is 36 or 101 or 173)
        {
            (frame, bool busy) = inputs.Poll534270(frame);
            Refresh();
            if (busy)
            {
                return new(frame, pool, 0);
            }
        }
        Execute(new NativeBattleRinoa89IoEvents(age));
        Execute(new NativeBattleRinoa89AdvanceIo());
        uint sound = age switch { 0 => 0x14913F0u, 50 => 0x14913F4u, 162 => 0x14913F8u, _ => 0u };
        if (sound != 0)
        {
            Execute(new NativeBattleRinoa89Sound(sound));
        }
        if (age == 50)
        {
            Execute(new NativeBattleRinoa89Visibility(0x6FBE00));
            Flags(12, 0);
        }
        if ((uint)(age - 36) < 39)
        {
            int delta = age - 36;
            frame = AdvanceShards6FD100(frame, delta >= 10 ? 2560 : 2560 * delta / 10);
        }
        if ((uint)(age - 37) < 189)
        {
            frame = AdvanceStars6FCF70(frame);
        }
        NativeBattleModelTransform currentTransform = inputs.ReadModel(actor).Transform ?? throw new NotSupportedException("Native6FA780 camera requires its current caster position.");
        Execute(new NativeBattleRinoa89CameraUpdate(age, center, currentTransform.Position28));
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        if (Signed(12) >= 271)
        {
            NativeBattleGraphicsModel caster = inputs.ReadModel(actor);
            bool unknownPadding = task.UnknownPayloadRanges is { } unknown && unknown.Any(range => range.Offset < 232 && range.Offset + range.Length > 230);
            inputs.WriteModel(actor, caster with { Rotation12 = Signed(224), Rotation14 = Signed(226), Rotation16 = Signed(228), RotationPadding18 = unknownPadding ? null : task.ReadWord(230) });
            Store();
            (frame, bool complete) = inputs.Reload534300(frame);
            Refresh();
            if (complete)
            {
                return new(frame, pool, 2);
            }
            task = task.WriteWord(12, 287);
        }
        Store();
        return new(frame, pool, 0);

        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
        void Store()
        {
            pool = pool.WriteSlot(slot, task);
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(bank => bank.DescriptorAddress == Body ? pool : bank).ToImmutableArray() } };
        }
        void Refresh()
        {
            pool = frame.Effect.Pools.Single(bank => bank.DescriptorAddress == Body);
            task = pool.Slots[slot];
        }
        void Execute(NativeBattleRinoa89Operation operation)
        {
            Store();
            frame = inputs.Execute(frame, operation);
            Refresh();
        }
        NativeBattleVector Bone19()
        {
            NativeBattleRinoa89BoneStep bone = inputs.ReadBone19(frame, task);
            frame = bone.Frame;
            return bone.Position;
        }
        void EnableParticles(bool enabled)
        {
            frame = frame with { Staging = frame.Staging.Write(0x255658D, [enabled ? (byte)1 : (byte)0, enabled ? (byte)1 : (byte)0]) };
        }
        void Flags(ushort set, ushort clear)
        {
            NativeBattleGraphicsModel caster = inputs.ReadModel(actor);
            inputs.WriteModel(actor, caster with { Flags0 = unchecked((ushort)((caster.Flags0 & ~clear) | set)) });
            task = task.WriteWord(52, unchecked((ushort)((task.ReadWord(52) & ~clear) | set)));
        }
        void PositionY(int value) => Position(1, value);
        void PositionZ(int value) => Position(2, value);
        void Position(int axis, int value)
        {
            NativeBattleGraphicsModel caster = inputs.ReadModel(actor);
            NativeBattleModelTransform transform = caster.Transform ?? throw new NotSupportedException("Native6FA780 requires its current caster transform.");
            NativeBattleActionMatrix world = caster.GeometryWorldMatrix ?? throw new NotSupportedException("Native6FA780 requires its current caster world matrix.");
            short word = unchecked((short)value);
            NativeBattleVector position = axis == 1 ? transform.Position28 with { Y = word } : transform.Position28 with { Z = word };
            world = axis == 1 ? world with { Y = word } : world with { Z = word };
            inputs.WriteModel(actor, caster with { Transform = transform with { Position28 = position }, GeometryWorldMatrix = world });
            task = task.WriteDword(axis == 1 ? 140 : 144, unchecked((uint)(int)word));
        }
    }
}
