namespace Memoria.FF8.Runtime;

public abstract record NativeBattleBlastingZone164Operation;
public sealed record NativeBattleBlastingZone164BindActor : NativeBattleBlastingZone164Operation;
public sealed record NativeBattleBlastingZone164ForceCamera : NativeBattleBlastingZone164Operation;
public sealed record NativeBattleBlastingZone164Sequence(byte Actor, int Sequence) : NativeBattleBlastingZone164Operation;
public sealed record NativeBattleBlastingZone164ModelFlags(byte Actor, ushort Set, ushort Clear) : NativeBattleBlastingZone164Operation;
public sealed record NativeBattleBlastingZone164StageFlags(bool Set) : NativeBattleBlastingZone164Operation;
public sealed record NativeBattleBlastingZone164Sound(uint Value) : NativeBattleBlastingZone164Operation;
public sealed record NativeBattleBlastingZone164ReadTexture(short FileId) : NativeBattleBlastingZone164Operation;
public sealed record NativeBattleBlastingZone164EnqueueTexture : NativeBattleBlastingZone164Operation;
public sealed record NativeBattleBlastingZone164Flash(int Mode, int First, int Last, int Intensity) : NativeBattleBlastingZone164Operation;
public sealed record NativeBattleBlastingZone164Contacts : NativeBattleBlastingZone164Operation;
public sealed record NativeBattleBlastingZone164Stage(int Intensity) : NativeBattleBlastingZone164Operation;
public sealed record NativeBattleBlastingZone164Inputs(uint GraphicsFlags, Func<int> Pending508500, Func<byte, short> ReadScale38, Func<byte, NativeBattleRenzokuken160WeaponTip> ReadWeaponTip, Func<NativeBattleBlastingZone164Frame, NativeBattleBlastingZone164Operation, NativeBattleBlastingZone164Frame> Execute);

public static partial class NativeBattleBlastingZone164Controller
{
    public static NativeBattleBlastingZone164ChildStep Advance5F0430(NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleBlastingZone164Inputs inputs, NativeBattleActionMatrix view, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, ReadMatrix(frame.Staging, 0x23C8F20));
        frame = frame with { Staging = StoreMatrix(frame.Staging, 0x23C9D68, matrix) };
        int factor = (NativeBattleCameraMath.Cosine(counter << 9) + 4096) >> 1;
        frame = frame with { Staging = WriteDword(frame.Staging, 0x23CA110, colors.Interpolate(0x30587F, factor, 0x803030)) };
        if ((inputs.GraphicsFlags & 0x201) != 0 && ((inputs.GraphicsFlags & 1) != 0 || inputs.Pending508500() < 0))
        {
            return new(frame, pool, 0);
        }
        byte actor = frame.Parameters.Actor78;
        switch (counter)
        {
            case 0:
                NativeBattleBlastingZone164ChildStep camera = BeginCamera63E960(frame, pool);
                frame = camera.Frame;
                pool = camera.Pool;
                Execute(new NativeBattleBlastingZone164ForceCamera());
                Execute(new NativeBattleBlastingZone164BindActor());
                Sequence(1);
                break;
            case 5:
                Execute(new NativeBattleBlastingZone164ReadTexture(538));
                break;
            case 15:
            case 134:
                if (inputs.Pending508500() < 0)
                {
                    return new(frame, pool, 0);
                }
                Execute(new NativeBattleBlastingZone164EnqueueTexture());
                break;
            case 35:
                Sequence(2);
                NativeBattleRenzokuken160WeaponTip tip = inputs.ReadWeaponTip(actor);
                NativeBattleVector point = new(unchecked((short)(tip.Position.X + ((380 * tip.DirectionX) >> 12))), unchecked((short)(tip.Position.Y + ((380 * tip.DirectionY) >> 12))), unchecked((short)(tip.Position.Z + ((380 * tip.DirectionZ) >> 12))));
                frame = frame with { Staging = Point(frame.Staging, 0x23C9D98, point) };
                Execute(new NativeBattleBlastingZone164Sound(unchecked((uint)ReadDword(frame.Staging, 0xD99C10))));
                break;
            case 36:
                Spawn(0x5F0B10);
                break;
            case 47:
                Spawn(0x5F0CB0);
                break;
            case 51:
                AppendSimple(0x5F0E80);
                AppendSimple(0x5F1190);
                break;
            case 57:
                Spawn(0x5F1440);
                AppendSimple(0x5F16A0);
                break;
            case 67:
                AppendSimple(0x5F1AC0);
                break;
            case 85:
                Spawn(0x5F1E00);
                break;
            case 97:
                Spawn(0x5F1F40);
                break;
            case 104:
                Execute(new NativeBattleBlastingZone164Flash(2, 1, 4, 255));
                break;
            case 107:
                Spawn(0x5F20D0);
                break;
            case 124:
                Execute(new NativeBattleBlastingZone164StageFlags(false));
                Execute(new NativeBattleBlastingZone164ReadTexture(539));
                Spawn(0x5F3AE0);
                Spawn(0x5F3C70);
                Spawn(0x5F4090);
                break;
            case 164:
                Execute(new NativeBattleBlastingZone164Sound(unchecked((uint)ReadDword(frame.Staging, 0xD99C14))));
                AppendSimple(0x5F4330);
                Spawn(0x5F44A0);
                Execute(new NativeBattleBlastingZone164ModelFlags(frame.FirstRecipient, 4, 0));
                break;
            case 185:
                Spawn(0x5F4940);
                break;
            case 188:
                Spawn(0x5F46A0);
                Spawn(0x5F4B00);
                AppendSimple(0x5F4CC0);
                break;
            case 224:
                Execute(new NativeBattleBlastingZone164StageFlags(true));
                Spawn(0x5F4F70);
                AppendSimple(0x5F5120);
                Spawn(0x5F53F0);
                Execute(new NativeBattleBlastingZone164ModelFlags(frame.FirstRecipient, 0, 4));
                break;
            case 244:
                Sequence(3);
                break;
            case 261:
                AppendSimple(0x5F5A50);
                break;
            case 262:
                Execute(new NativeBattleBlastingZone164ModelFlags(actor, 8, 0));
                break;
            case 263:
                Spawn(0x5F5580);
                break;
            case 264:
                AppendSimple(0x5F57C0);
                break;
            case 276:
                Execute(new NativeBattleBlastingZone164Flash(12, 6, 15, 255));
                break;
            case 289:
                Sequence(4);
                Execute(new NativeBattleBlastingZone164ModelFlags(actor, 0, 8));
                break;
            case 290:
                AppendSimple(0x5F5AD0);
                break;
            case 307:
                Execute(new NativeBattleBlastingZone164Contacts());
                break;
        }
        if (counter >= 306)
        {
            Execute(new NativeBattleBlastingZone164Stage((314 - counter) << 8));
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1))).SetNext(pool.Slots[slot].Next);
        pool = pool.WriteSlot(slot, task);
        if (Word(task, 12) <= 314)
        {
            return new(frame, pool, 0);
        }
        Execute(new NativeBattleBlastingZone164Stage(0));
        return new(frame, pool, 2);

        void AppendSimple(uint callback)
        {
            (pool, int created) = pool.Append(callback);
            pool = pool.WriteSlot(created, pool.Slots[created].WriteWord(12, 0).WriteWord(14, 0));
        }
        void Spawn(uint initializer)
        {
            NativeBattleBlastingZone164ChildStep spawned = RunInitializer(frame, pool, initializer, inputs);
            frame = spawned.Frame;
            pool = spawned.Pool;
        }
        void Execute(NativeBattleBlastingZone164Operation operation)
        {
            frame = inputs.Execute(frame, operation);
        }
        void Sequence(int sequence)
        {
            Execute(new NativeBattleBlastingZone164Sequence(actor, sequence));
        }
    }
}
