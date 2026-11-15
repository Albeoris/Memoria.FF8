namespace Memoria.FF8.Runtime;

public static partial class NativeBattleLionHeart165Controller
{
    private static NativeBattleLionHeart165ChildStep RunCompoundInitializer(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, uint initializer, NativeBattleLionHeart165Inputs inputs)
    {
        BattleModelOwner actor = frame.Parameters.Source;
        if (initializer == 0x5EDE90)
        {
            int flash = Append(0x5EE0F0);
            NativeBattleModuleAnchor target = inputs.ReadPosition(frame.FirstRecipient);
            SetTask(flash, 16, target.X);
            SetTask(flash, 18, target.Y);
            SetTask(flash, 20, target.Z);
            pool = pool.WriteSlot(flash, pool.Slots[flash].MarkPayloadUnknown(22, 2));
            SetTask(flash, 24, Rand() % 1024 - 512);
            int speed = (Rand() % 4608 + 12288) / 3;
            SetTask(flash, 30, speed);
            SetTask(flash, 28, speed);
            int cloud = Copy(0x5EE240, flash);
            Marker(cloud);
            int fragments = Copy(0x5EE4C0, flash);
            Marker(fragments);
            int circle = Copy(0x5EE850, flash);
            int growth = (Rand() % 3072 + 12288) / 4;
            SetTask(circle, 30, growth);
            SetTask(circle, 28, growth);
            int swarm = Copy(0x5EE980, flash);
            Marker(swarm);
            uint bank = unchecked((uint)ReadDword(frame.Staging, 0x23C5680));
            if (bank != 0x20DFAB8)
            {
                throw new NotSupportedException("Native Lion Heart late particle clear requires its actual571B50 bank.");
            }
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Lion Heart late particles require their retained resource arena.");
            for (int index = 0; index < 500; index++)
            {
                arena = arena.Write(index * 24, new byte[4]);
            }
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
            SaveFlag(actor);
            foreach (BattleModelOwner targetActor in TargetRing(frame, inputs))
            {
                SaveFlag(targetActor);
            }
            frame = ApplyNextContact(frame, inputs);
        }
        else if (initializer == 0x5EEC00)
        {
            foreach (BattleModelOwner targetActor in TargetRing(frame, inputs))
            {
                int echo = Append(0x5EECD0);
                pool = pool.WriteSlot(echo, WriteActor(pool.Slots[echo], targetActor, inputs.Aliases));
            }
            RestoreFlag(actor);
        }
        else
        {
            throw new NotSupportedException("Native Lion Heart compound initializer is unsupported.");
        }
        return new(frame, pool, 0);

        int Append(uint callback)
        {
            (pool, int created) = pool.Append(callback);
            SetTask(created, 12, 0);
            return created;
        }
        int Copy(uint callback, int source)
        {
            int created = Append(callback);
            NativeBattleFileStaging staging = CopyTaskToStaging(frame.Staging, 0x23C6930, pool.Slots[source], 16, 8);
            NativeBattleModulePoolTask copied = CopyStagingToTask(staging, 0x23C6930, pool.Slots[created], 16, 8);
            pool = pool.WriteSlot(created, copied);
            return created;
        }
        void SetTask(int created, int offset, int value) => pool = pool.WriteSlot(created, pool.Slots[created].WriteWord(offset, unchecked((ushort)value)));
        void Marker(int created)
        {
            int marker = ReadDword(frame.Staging, 0x23C6940);
            SetTask(created, 14, marker);
            frame = frame with { Staging = WriteDword(frame.Staging, 0x23C6940, unchecked((uint)(marker + 1))) };
        }
        void SaveFlag(BattleModelOwner owner)
        {
            NativeBattleGraphicsModel model = inputs.ReadModel(owner);
            ushort flags = (ushort)(model.Flags0 & 0x20);
            if (inputs.Aliases.TryEncodeExact(owner, out byte cell))
            {
                frame = frame with { Staging = WriteWord(frame.Staging, 0x23C6738u + (uint)cell * 40, flags) };
            }
            frame = SaveModel(frame, owner, ReadSavedModel(frame, owner) with { ShadowFlags = flags });
            frame = inputs.Execute(frame, new NativeBattleLionHeart165ModelFlags(owner, 0x20, 0));
        }
        void RestoreFlag(BattleModelOwner owner)
        {
            ushort old = ReadSavedShadowFlags(frame, owner);
            frame = inputs.Execute(frame, new NativeBattleLionHeart165ModelFlags(owner, old, old == 0 ? (ushort)0x20 : (ushort)0));
        }
        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    public static NativeBattleLionHeart165ChildStep Advance5EECD0(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleLionHeart165Inputs inputs)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        BattleModelOwner actor = ReadActor(task);
        NativeBattleGraphicsModel model = inputs.ReadModel(actor);
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Lion Heart echo requires its actual raw model transform.");
        if (counter == 0)
        {
            NativeBattleModuleAnchor point = inputs.ReadPosition(actor);
            task = task.WriteWord(16, unchecked((ushort)point.X)).WriteWord(18, unchecked((ushort)point.Y)).WriteWord(20, unchecked((ushort)point.Z)).MarkPayloadUnknown(22, 2).WriteWord(32, unchecked((ushort)point.Y)).WriteWord(34, unchecked((ushort)(point.Y / 8)));
            NativeBattleVector rotation = new(model.Rotation12 ?? throw new NotSupportedException("Native Lion Heart echo requires its retained rotationX."), model.Rotation14 ?? throw new NotSupportedException("Native Lion Heart echo requires its retained rotationY."), model.Rotation16 ?? throw new NotSupportedException("Native Lion Heart echo requires its retained rotationZ."));
            if (inputs.Aliases.TryEncodeExact(actor, out byte cell))
            {
                uint destination = 0x23C673Cu + (uint)cell * 40;
                NativeBattleFileStaging staging = Point(frame.Staging, destination, rotation);
                staging = model.RotationPadding18 is ushort padding ? WriteWord(staging, destination + 6, padding) : Forget(staging, destination + 6, 2);
                frame = frame with { Staging = staging };
            }
            frame = SaveModel(frame, actor, ReadSavedModel(frame, actor) with { Rotation = rotation, RotationPadding = model.RotationPadding18 });
        }
        if (counter < 16)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 424);
            NativeBattleFileStaging staging = Point(allocation.Staging, allocation.Address + 88, new(0, 0, 0));
            staging = Point(staging, allocation.Address + 184, new(0, -4096, 0));
            staging = WriteWord(staging, allocation.Address + 190, 0);
            frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleLionHeart165ModelFlags(actor, 4, 0));
            frame = inputs.Execute(frame, new NativeBattleLionHeart165Echo(actor, allocation.Address));
            frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging, 424) };
        }
        if (counter <= 8)
        {
            NativeBattleVector rotation = new(model.Rotation12 ?? throw new NotSupportedException("Native Lion Heart echo requires its rotationX."), model.Rotation14 ?? throw new NotSupportedException("Native Lion Heart echo requires its rotationY."), 2048);
            frame = inputs.Execute(frame, new NativeBattleLionHeart165Rotation(actor, rotation, model.RotationPadding18));
            frame = inputs.Execute(frame, new NativeBattleLionHeart165Position(actor, transform.Position28 with { Y = unchecked((short)(ReadWord(frame.Staging, 0x23C32F2) + Word(task, 32))) }, transform.Vertical34));
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool.WriteSlot(slot, task), 0);
        }
        if (counter <= 8)
        {
            task = task.WriteWord(32, unchecked((ushort)(Word(task, 32) - Word(task, 34))));
        }
        if (counter == 9)
        {
            Append(0x5F0130);
            int spray = Copy(0x5EFCE0);
            int marker = ReadDword(frame.Staging, 0x23C6940);
            pool = pool.WriteSlot(spray, pool.Slots[spray].WriteWord(14, unchecked((ushort)marker)));
            frame = frame with { Staging = WriteDword(frame.Staging, 0x23C6940, unchecked((uint)(marker + 1))) };
            int ring = Copy(0x5EFFF0);
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            int speed = (value % 2048 + 8192) / 4;
            pool = pool.WriteSlot(ring, pool.Slots[ring].WriteWord(30, (ushort)speed).WriteWord(28, (ushort)speed));
            ushort old = ReadSavedShadowFlags(frame, actor);
            frame = inputs.Execute(frame, new NativeBattleLionHeart165ModelFlags(actor, old, old == 0 ? (ushort)0x20 : (ushort)0));
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1))).SetNext(pool.Slots[slot].Next);
        short next = Word(task, 12);
        if (next == 16)
        {
            NativeBattleModelTransform saved = ReadSavedModel(frame, actor).Transform ?? throw new NotSupportedException("Native Lion Heart echo requires its saved model position.");
            frame = inputs.Execute(frame, new NativeBattleLionHeart165Position(actor, saved.Position28, saved.Vertical34));
            NativeBattleLionHeart165SavedModel rotation = ReadSavedModel(frame, actor);
            frame = inputs.Execute(frame, new NativeBattleLionHeart165Rotation(actor, rotation.Rotation ?? throw new NotSupportedException("Native Lion Heart echo requires its saved model rotation."), rotation.RotationPadding));
            frame = inputs.Execute(frame, new NativeBattleLionHeart165ModelFlags(actor, 0, 0x1000));
            if (actor == frame.FirstRecipient)
            {
                frame = ApplyNextContact(frame, inputs);
            }
        }
        else if (next > 16)
        {
            frame = inputs.Execute(frame, new NativeBattleLionHeart165ModelFlags(actor, 0, 4));
        }
        return new(frame, pool.WriteSlot(slot, task), next > 16 ? 2 : 0);

        int Append(uint callback)
        {
            (pool, int created) = pool.Append(callback);
            pool = pool.WriteSlot(created, pool.Slots[created].WriteWord(12, 0));
            return created;
        }
        int Copy(uint callback)
        {
            int created = Append(callback);
            pool = pool.WriteSlot(created, CopyStagingToTask(frame.Staging, 0x23C6938, pool.Slots[created], 16, 8));
            return created;
        }
    }

    private static NativeBattleLionHeart165Frame SaveModel(NativeBattleLionHeart165Frame frame, BattleModelOwner owner, NativeBattleLionHeart165SavedModel saved)
    {
        return frame with { Effect = frame.Effect with { LionHeartSavedModels = frame.Effect.LionHeartSavedModels.SetItem(owner, saved) } };
    }

    private static NativeBattleLionHeart165SavedModel ReadSavedModel(NativeBattleLionHeart165Frame frame, BattleModelOwner owner)
    {
        return frame.Effect.LionHeartSavedModels.TryGetValue(owner, out NativeBattleLionHeart165SavedModel? saved) ? saved : throw new NotSupportedException("Native Lion Heart requires its saved model state.");
    }

    private static ushort ReadSavedShadowFlags(NativeBattleLionHeart165Frame frame, BattleModelOwner owner)
    {
        return ReadSavedModel(frame, owner).ShadowFlags ?? throw new NotSupportedException("Native Lion Heart requires its saved model shadow flags.");
    }
}
