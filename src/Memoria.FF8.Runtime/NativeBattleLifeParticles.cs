namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLifeParticleStep(NativeBattleLifeFrame Frame, NativeCrtRandomState Random, int Result);

public static partial class NativeBattleLifeController
{
    /// <summary>864DD0/C80/F00/F90/865050, in the exact target-callback producer order.</summary>
    public static NativeBattleEffectHierarchy Produce(NativeBattleEffectHierarchy hierarchy, uint address, NativeBattleEffectParameters parameters, NativeBattleActionPacketsState packets, ref NativeCrtRandomState random)
    {
        NativeBattleModulePoolTask parent = hierarchy.Read(address);
        int counter = Word(parent, 0x24);
        NativeCrtRandomState currentRandom = random;
        NativeBattleLifeProfile profile = NativeBattleLifeProfile.ForDescriptor(hierarchy.Pools[0].DescriptorAddress);
        if (counter <= 30)
        {
            int height = Word(parent, 0x5A);
            if (height == 0)
            {
                height = 1;
            }
            for (int index = 0; index < Word(parent, 0x5C); index++)
            {
                short radius = unchecked((short)((Rand() & 1) != 0 ? Word(parent, 0x58) / 2 : Word(parent, 0x58)));
                if (radius == 0)
                {
                    radius = 1;
                }
                uint sprite = Sprite((Rand() & 1) != 0 ? 0x15E638Cu : 0x15E6538u);
                uint child = AppendSprite(sprite, 15, -512);
                int angle = Rand() & 4095;
                short distance = unchecked((short)(Rand() % radius));
                NativeBattleModulePoolTask task = hierarchy.Read(child).WriteWord(0x68, parent.ReadWord(0x5E)).WriteWord(0x6A, unchecked((ushort)((Word(parent, 0x60) - Word(parent, 0x5E)) / 16)));
                task = Add(task, 0x1C, NativeBattleCameraMath.Sine(angle) * distance / 4096);
                task = Add(task, 0x20, NativeBattleCameraMath.Cosine(angle) * distance / 4096);
                task = Add(task, 0x1E, -256 - Rand() % height);
                hierarchy = hierarchy.Write(child, task);
            }
        }
        if (counter == 0)
        {
            _ = AppendSprite(Sprite(0x15E6890), 2, -1024);
        }
        if (counter > 10 && counter < 22 && (counter & 1) == 0)
        {
            uint child = AppendSprite(Sprite(0x15E6890), 2, -1024);
            short x = unchecked((short)(Rand() % (Word(parent, 0x58) / 2 + 1)));
            short y = unchecked((short)(Rand() % (Word(parent, 0x5A) + 1)));
            short z = unchecked((short)(Rand() % (Word(parent, 0x58) / 2 + 1)));
            if ((Rand() & 1) != 0)
            {
                x = unchecked((short)-x);
            }
            if ((Rand() & 1) != 0)
            {
                z = unchecked((short)-z);
            }
            hierarchy = hierarchy.Write(child, Add(Add(Add(hierarchy.Read(child), 0x1C, x), 0x1E, -y), 0x20, z));
        }
        if (counter > 10 && counter < 30)
        {
            uint child = Append(3, 0x865270, 112);
            NativeBattleModulePoolTask task = CopyAnchor(hierarchy.Read(child));
            task = Add(task, 0x1E, Word(parent, 0x62));
            task = task.WriteDword(0x60, task.ReadDword(0x1C)).WriteDword(0x64, task.ReadDword(0x20)).WriteDword(0x4C, Sprite(0x15E66E4)).WriteWord(0x52, 15).WriteWord(0x68, 4096).WriteWord(0x42, (ushort)(Rand() & 4095)).WriteWord(0x6C, (ushort)((counter - 10) / 3 + 8));
            hierarchy = hierarchy.Write(child, task);
        }
        if (counter > 20 && counter < 37)
        {
            uint child = Append(3, 0x865120, 112);
            NativeBattleModulePoolTask task = hierarchy.Read(child).WriteDword(0x4C, Sprite(0x15E68EC)).WriteWord(0x52, 9).WriteWord(0x68, 4096).WriteWord(0x42, (ushort)(Rand() & 4095));
            task = CopyAnchor(task);
            int angle = Word(task, 0x42);
            task = Add(task, 0x1C, NativeBattleCameraMath.Sine(angle) / 8);
            task = Add(task, 0x20, NativeBattleCameraMath.Cosine(angle) / 8);
            task = Add(task, 0x1E, -128 - (Rand() & 511)).WriteWord(0x5A, (ushort)((Rand() & 15) + 16));
            hierarchy = hierarchy.Write(child, task);
        }
        if (counter > 3 && counter < 16)
        {
            int count = counter > 14 ? 1 : counter > 10 ? 3 : counter > 5 ? 2 : 1;
            for (int index = 0; index < count; index++)
            {
                uint child = Append(4, 0x8DCC20, 92, shared: true);
                NativeBattleModulePoolTask task = hierarchy.Read(child).WriteWord(0x56, 8).WriteByte(0x4C, 16).WriteByte(0x4D, 16).WriteByte(0x4E, 16).WriteByte(0x50, 16).WriteByte(0x51, 5).WriteByte(0x52, 8).WriteWord(0x3C, 256).WriteWord(0x3E, 16).WriteWord(0x40, 64).WriteWord(0x42, 16).WriteWord(0x58, unchecked((ushort)((Rand() & 1) != 0 ? 64 : -64)));
                hierarchy = hierarchy.Write(child, task);
            }
        }
        random = currentRandom;
        return hierarchy;

        int Rand()
        {
            currentRandom = currentRandom.Draw(out ushort value);
            return value;
        }

        uint Sprite(uint lifeAddress) => checked(lifeAddress + profile.StaticDelta);

        uint Append(int pool, uint callback, int size, bool shared = false)
        {
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, shared ? callback : profile.Code(callback), size, address, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, packets, pointer, group, target));
            hierarchy = appended.Hierarchy;
            if (appended.Address == 0)
            {
                throw new NotSupportedException("Native Life producer dereferenced an exhausted8DC540 allocation.");
            }
            return appended.Address;
        }

        NativeBattleModulePoolTask CopyAnchor(NativeBattleModulePoolTask task)
        {
            NativeBattleModulePoolTask first = hierarchy.Read(task.ReadDword(0x14));
            return task.WriteDword(0x1C, first.ReadDword(0x30)).WriteDword(0x20, first.ReadDword(0x34));
        }

        uint AppendSprite(uint sprite, ushort duration, short depth)
        {
            uint child = Append(3, 0x8653E0, 112);
            hierarchy = hierarchy.Write(child, CopyAnchor(hierarchy.Read(child)).WriteWord(0x54, unchecked((ushort)depth)).WriteDword(0x4C, sprite).WriteWord(0x52, duration).WriteWord(0x68, 4096));
            return child;
        }
    }

    public static NativeBattleLifeParticleStep AdvanceParticle(NativeBattleLifeFrame frame, uint address, NativeBattleLifeResources resources, NativeBattleLifeInputs inputs, NativeCrtRandomState random)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback == 0x8DCC20)
        {
            NativeBattleEffectWedgeStep wedge = NativeBattleEffectWedge.Advance8DCC20(new(frame.Memory, frame.Effect.Pools), address, random, index => inputs.ReadModel(index).Transform ?? throw new NotSupportedException("Native8DCC20 requires the actual target transform."), inputs.Projection, (current, batch) =>
            {
                NativeBattleLifeFrame written = inputs.Execute(frame with { Memory = current.Memory, Effect = frame.Effect with { Pools = current.Pools } }, batch);
                frame = written;
                return new(written.Memory, written.Effect.Pools);
            });
            return new(frame with { Memory = wedge.Frame.Memory, Effect = frame.Effect with { Pools = wedge.Frame.Pools } }, wedge.CrtRandomAfter, wedge.Result);
        }
        NativeBattleLifeProfile profile = NativeBattleLifeProfile.ForEntry(frame.Effect.Binding.Entry);
        uint callback = task.Callback - profile.CodeDelta;
        if (callback is not (0x865120 or 0x865270 or 0x8653E0))
        {
            throw new NotSupportedException($"Native Life particle callback{task.Callback:X} has no original contract.");
        }
        int phase = Phase(task);
        if (phase == 0)
        {
            task = Increment(task);
        }
        else if (phase == 1)
        {
            if (callback == 0x865120)
            {
                task = Add(task, 0x1E, Word(task, 0x5A));
            }
            else if (callback == 0x865270)
            {
                short angle = unchecked((short)((task.ReadWord(0x42) + 128) & 4095));
                task = task.WriteWord(0x42, unchecked((ushort)angle)).WriteWord(0x1C, task.ReadWord(0x60)).WriteWord(0x20, task.ReadWord(0x64));
                int divisor = Word(task, 0x6C);
                task = Add(task, 0x1C, NativeBattleCameraMath.Sine(angle) / divisor);
                task = Add(task, 0x20, NativeBattleCameraMath.Cosine(angle) / divisor);
                task = Add(task, 0x1E, -64);
            }
            task = AdvanceSpriteFrame865230(task, out bool ended);
            if (ended)
            {
                task = Increment(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
            }
        }
        else if (phase != 2)
        {
            throw new InvalidDataException("Native Life sprite signed phase exceeds its three original callbacks.");
        }
        if (callback != 0x865120)
        {
            task = Add(task, 0x68, Word(task, 0x6A));
        }
        hierarchy = hierarchy.Write(address, task);
        if ((task.ReadByte(0x26) & 4) == 0)
        {
            int spriteFrame = Word(task, 0x50);
            NativeBattleModuleResource resource = resources.Sprites.Single(resource => resource.Address == task.ReadDword(0x4C));
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
            {
                Resource = resource,
                SpriteFrame = resource.SpriteFrame(spriteFrame),
                Anchor = new(Word(task, 0x1C), Word(task, 0x1E), Word(task, 0x20), 0),
                Phase = unchecked((ushort)spriteFrame),
                Scale = callback == 0x865120 ? 4096 : Word(task, 0x68),
                DepthOffset = Word(task, 0x54),
                Direction = 4096,
                SpriteFlags = 0,
                SpriteColor = 0x808080
            };
            frame = inputs.Execute(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, NativeBattleModulePackets.EmitSprite571DC0(draw, inputs.Projection, inputs.CameraRoll, 17, 2));
            hierarchy = new(frame.Effect.Pools);
            task = hierarchy.Read(address);
        }
        task = Add(task, 0x24, 1);
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, random, complete ? 2 : 0);
    }

    public static NativeBattleModulePoolTask AdvanceSpriteFrame865230(NativeBattleModulePoolTask task, out bool ended)
    {
        task = Add(task, 0x50, 1);
        ended = Word(task, 0x50) > Word(task, 0x52);
        return ended ? task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4)) : task;
    }
}
