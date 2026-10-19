namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLionHeart165Silence : NativeBattleLionHeart165Operation;
public sealed record NativeBattleLionHeart165ForceCamera : NativeBattleLionHeart165Operation;
public sealed record NativeBattleLionHeart165BindActor : NativeBattleLionHeart165Operation;
public sealed record NativeBattleLionHeart165Sequence(byte Actor, int Sequence) : NativeBattleLionHeart165Operation;
public sealed record NativeBattleLionHeart165Sound(uint Value) : NativeBattleLionHeart165Operation;
public sealed record NativeBattleLionHeart165ReadTexture(short FileId) : NativeBattleLionHeart165Operation;
public sealed record NativeBattleLionHeart165EnqueueTexture : NativeBattleLionHeart165Operation;
public sealed record NativeBattleLionHeart165ModelFlags(byte Actor, ushort Set, ushort Clear) : NativeBattleLionHeart165Operation;
public sealed record NativeBattleLionHeart165Position(byte Actor, NativeBattleVector Position, short Vertical34) : NativeBattleLionHeart165Operation;
public sealed record NativeBattleLionHeart165Stage(int Intensity) : NativeBattleLionHeart165Operation;
public sealed record NativeBattleLionHeart165StageFlags(bool Set = true) : NativeBattleLionHeart165Operation;
public sealed record NativeBattleLionHeart165Flash(short Rise, short Hold, short Fall, short Intensity) : NativeBattleLionHeart165Operation;

public static partial class NativeBattleLionHeart165Controller
{
    public static NativeBattleLionHeart165ChildStep Advance5E9650(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleLionHeart165Inputs inputs, NativeBattleActionMatrix view)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        frame = frame with { Staging = StoreMatrix(frame.Staging, 0x23C6878, NativeBattlePoseMatrices.Compose56C2F0(view, ReadMatrix(frame.Staging, 0x23C58E0))) };
        if ((inputs.GraphicsFlags & 0x201) != 0 && ((inputs.GraphicsFlags & 1) != 0 || inputs.Pending508500() < 0))
        {
            return new(frame, pool, 0);
        }
        byte actor = frame.Parameters.Actor78;
        if (counter == 0)
        {
            Execute(new NativeBattleLionHeart165Silence());
            NativeBattleLionHeart165ChildStep camera = BeginCamera63E960(frame, pool);
            frame = camera.Frame;
            pool = camera.Pool;
            Execute(new NativeBattleLionHeart165ForceCamera());
            Execute(new NativeBattleLionHeart165BindActor());
            SavePosition(0x23C6938, frame.FirstRecipient);
            SavePosition(0x23C6868, actor);
            for (byte index = 0; index < 7; index++)
            {
                SavePosition(0x23C6744u + (uint)index * 40, index);
            }
            Anchor(0x23C32F0, inputs.ReadPosition(frame.FirstRecipient));
        }
        switch (counter)
        {
            case 0:
                Sequence(1);
                break;
            case 1:
                Spawn(0x5E9DD0);
                break;
            case 2:
                Execute(new NativeBattleLionHeart165ReadTexture(540));
                break;
            case 8:
                Spawn(0x5EA850);
                break;
            case 10:
                Sound(0xD886D4);
                Spawn(0x5EAA50);
                break;
            case 14:
            case 162:
            case 211:
                if (inputs.Pending508500() < 0)
                {
                    return new(frame, pool, 0);
                }
                Execute(new NativeBattleLionHeart165EnqueueTexture());
                break;
            case 15:
                Execute(new NativeBattleLionHeart165ReadTexture(541));
                break;
            case 20:
                Spawn(0x5EB6D0);
                break;
            case 24:
                Spawn(0x5EB890);
                break;
            case 26:
                Spawn(0x5EBB90);
                break;
            case 67:
                Execute(new NativeBattleLionHeart165Flash(0, 1, 8, 255));
                break;
            case 68:
                Spawn(0x5EBEF0);
                break;
            case 78:
                Sequence(2);
                Spawn(0x5EB3E0);
                break;
            case 90:
                Anchor(0x23C58F4, inputs.ReadPosition(actor));
                break;
            case 91:
                AppendSimple(0x5EC140);
                break;
            case 100:
                Spawn(0x5ED270);
                break;
            case 103:
                Execute(new NativeBattleLionHeart165ModelFlags(actor, 0, 8));
                break;
            case 111:
                Spawn(0x5ED440);
                break;
            case >= 112 and <= 116:
                NativeBattleModelTransform lowered = Transform(actor);
                Execute(new NativeBattleLionHeart165Position(actor, lowered.Position28 with { Y = unchecked((short)(lowered.Position28.Y - 800)) }, lowered.Vertical34));
                break;
            case 119:
                Sound(0xD886D8);
                break;
            case 141:
                Spawn(0x5E9D20);
                break;
            case 161:
                AppendSimple(0x5ED4E0);
                break;
            case 201:
                Execute(new NativeBattleLionHeart165ReadTexture(542));
                Spawn(0x5ED700);
                break;
            case 202:
                Sound(0xD886DC);
                break;
            case 207:
                Spawn(0x5ED8A0);
                break;
            case 241:
                Execute(new NativeBattleLionHeart165Flash(0, 4, 8, 255));
                break;
            case 242:
                frame = frame with { Staging = WriteDword(WriteDword(frame.Staging, 0x23C58F4, unchecked((uint)ReadWord(frame.Staging, 0x23C6910))), 0x23C58FC, unchecked((uint)ReadWord(frame.Staging, 0x23C6914))) };
                break;
            case 243:
                Sequence(3);
                NativeBattleModuleAnchor ownPosition = inputs.ReadPosition(actor);
                NativeBattleModuleAnchor targetPosition = inputs.ReadPosition(frame.FirstRecipient);
                Anchor(0x23C58C0, ownPosition);
                Anchor(0x23C57A8, targetPosition);
                int distance = targetPosition.Z - ownPosition.Z;
                task = task.WriteWord(34, unchecked((ushort)(distance >= -2200 ? 0 : (2 * distance + 4400) / 16)));
                break;
            case 245:
                Spawn(0x5EDBC0);
                break;
            case 288:
                Spawn(0x5EDE90);
                break;
            case 332:
                frame = frame with { Staging = WriteDword(WriteDword(WriteDword(frame.Staging, 0x23C58F4, unchecked((uint)ReadWord(frame.Staging, 0x23C6868))), 0x23C58F8, unchecked((uint)ReadWord(frame.Staging, 0x23C686A))), 0x23C58FC, unchecked((uint)ReadWord(frame.Staging, 0x23C686C))) };
                break;
            case 333:
                Sequence(4);
                Execute(new NativeBattleLionHeart165Position(actor, new(ReadWord(frame.Staging, 0x23C6868), ReadWord(frame.Staging, 0x23C686A), ReadWord(frame.Staging, 0x23C686C)), ReadWord(frame.Staging, 0x23C686E)));
                Execute(new NativeBattleLionHeart165Stage(0));
                Execute(new NativeBattleLionHeart165StageFlags());
                break;
            case 335:
                Spawn(0x5EEC00);
                AppendSimple(0x5F01B0);
                break;
            case 343:
                Sound(0xD886E0);
                break;
        }
        if (counter is >= 243 and < 259)
        {
            NativeBattleModelTransform moved = Transform(actor);
            Execute(new NativeBattleLionHeart165Position(actor, moved.Position28 with { Z = unchecked((short)(moved.Position28.Z + Word(task, 34))) }, moved.Vertical34));
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1))) with { Next = pool.Slots[slot].Next };
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        if (Word(task, 12) <= 370)
        {
            return new(frame, pool, 0);
        }
        Execute(new NativeBattleLionHeart165StageFlags());
        Execute(new NativeBattleLionHeart165Stage(0));
        return new(frame, pool, 2);

        NativeBattleModelTransform Transform(byte index) => inputs.ReadModel(index).Transform ?? throw new NotSupportedException("Native Lion Heart requires its actual raw model position WORDs.");
        void SavePosition(uint destination, byte index)
        {
            NativeBattleModelTransform transform = Transform(index);
            frame = frame with { Staging = WriteWord(Point(frame.Staging, destination, transform.Position28), destination + 6, unchecked((ushort)transform.Vertical34)) };
        }
        void Anchor(uint destination, NativeBattleModuleAnchor anchor)
        {
            frame = frame with { Staging = Point(frame.Staging, destination, new(unchecked((short)anchor.X), unchecked((short)anchor.Y), unchecked((short)anchor.Z))) };
        }
        void Execute(NativeBattleLionHeart165Operation operation)
        {
            frame = inputs.Execute(frame, operation);
        }
        void Sequence(int sequence)
        {
            Execute(new NativeBattleLionHeart165Sequence(actor, sequence));
        }
        void Sound(uint address)
        {
            Execute(new NativeBattleLionHeart165Sound(unchecked((uint)ReadDword(frame.Staging, address))));
        }
        void Spawn(uint initializer)
        {
            NativeBattleLionHeart165ChildStep spawned = RunInitializer(frame, pool, initializer, inputs);
            frame = spawned.Frame;
            pool = spawned.Pool;
        }
        void AppendSimple(uint callback)
        {
            (pool, int created) = pool.Append(callback);
            pool = pool with { Slots = pool.Slots.SetItem(created, pool.Slots[created].WriteWord(12, 0).WriteWord(14, 0)) };
        }
    }
}
