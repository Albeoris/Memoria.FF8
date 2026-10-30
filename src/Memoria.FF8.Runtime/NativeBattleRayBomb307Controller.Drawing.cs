namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRayBomb307Controller
{
    private static NativeBattlePackedMatrix32 ReadView(NativeBattleRayBomb307Frame frame, NativeBattleRayBomb307Inputs inputs) => inputs.ReadView?.Invoke(frame) ?? throw new NotSupportedException("Ray-Bomb requires its current packed camera.");

    private static NativeBattleRayBomb307Frame DrawStatic(NativeBattleRayBomb307Frame frame, NativeBattleActionMatrix matrix, uint source, uint flags, int? cue, bool zeroRgb, NativeBattleRayBomb307Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        frame = LoadMatrix(frame, matrix, true);
        return EmitStatic(frame, source, flags, cue, zeroRgb, inputs, pilotIdentity);
    }

    private static NativeBattleRayBomb307Frame EmitStatic(NativeBattleRayBomb307Frame frame, uint source, uint flags, int? cue, bool zeroRgb, NativeBattleRayBomb307Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 88);
        NativeBattleEffectArena temporary = Dword(Dword(Bounded(allocation.Staging, allocation.Address, 88), 0, source), 0x1C, flags);
        if (zeroRgb)
        {
            temporary = temporary.Write(8, new byte[3]);
        }
        if (cue is int value)
        {
            temporary = Dword(temporary, 12, unchecked((uint)value));
        }
        frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 88) };
        frame = inputs.Execute(frame, new NativeBattleRayBomb307DrawMesh88(allocation.Address, source) { PilotIdentity = pilotIdentity });
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 88) };
    }

    public static NativeBattleRayBomb307TaskStep AdvanceMesh(NativeBattleRayBomb307Frame frame, uint address, NativeBattleRayBomb307Resources resources, NativeBattleRayBomb307Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        uint callback = task.Callback;
        if (callback == 0x5A23E0)
        {
            frame = SetupSprite(frame, frame.State.ReadView(0x2242580, 8), Signed(task, 0x1C), unchecked((short)-Signed(task, 0x16)), inputs);
            int? cue = age < 16 ? (16 - age) << 8 : age >= 22 ? (age - 22) * 682 : null;
            frame = EmitStatic(frame, 0xCE350C, cue.HasValue ? 0xC0u : 0u, cue, true, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(307, task) with { ResourceGeneration = 0xCE350C } : null);
        }
        else
        {
            bool burst = callback == 0x5A1AE0;
            NativeBattleActionMatrix local = burst ? NativeBattleGfMatrices.Euler56CD50(new(Signed(task, 0x1C), 0, Signed(task, 0x20))) : NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0));
            local = local with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
            local = NativeBattlePoseMatrices.Scale56BEF0(local, burst ? Signed(task, 0x1A) : Signed(task, 0x1C), burst ? Signed(task, 0x1A) : Signed(task, 0x20), burst ? Signed(task, 0x1A) : Signed(task, 0x1C));
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(ReadView(frame, inputs).ReadMatrix(), local);
            int shape = Signed(task, 0x18);
            if (burst && (uint)shape >= resources.MeshSequence.Length)
            {
                throw new InvalidDataException("Ray-Bomb selected a shape outside its actual mesh sequence.");
            }
            int start = burst ? Signed(task, 14) - 8 : 10;
            int? cue = age >= start ? burst ? (age - start) << 9 : (age - start) * 292 : null;
            frame = DrawStatic(frame, matrix, burst ? resources.MeshSequence[shape] : 0xCE190C, cue.HasValue ? burst ? 0xC3u : 0xF0u : burst ? 0u : 0x30u, cue, cue.HasValue, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(307, task) with { ResourceGeneration = burst ? resources.MeshSequence[shape] : 0xCE190C } : null);
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        if (callback == 0x5A1AE0)
        {
            short velocity = Signed(task, 0x16);
            task = task.WriteWord(0x12, unchecked((ushort)(Signed(task, 0x12) + velocity))).WriteWord(0x16, unchecked((ushort)(velocity - velocity / 9))).WriteWord(0x20, unchecked((ushort)(Signed(task, 0x20) + Signed(task, 0x22)))).WriteWord(0x1C, unchecked((ushort)(Signed(task, 0x1C) + Signed(task, 0x1E))));
        }
        else if (callback == 0x5A1C50)
        {
            short velocity = Signed(task, 0x22);
            task = task.WriteWord(0x20, unchecked((ushort)(Signed(task, 0x20) + velocity))).WriteWord(0x22, unchecked((ushort)(velocity - (velocity >> 1))));
        }
        else if (age < 10)
        {
            short velocity = Signed(task, 0x1E);
            task = task.WriteWord(0x1C, unchecked((ushort)(Signed(task, 0x1C) - velocity))).WriteWord(0x1E, unchecked((ushort)(velocity + (velocity >> 5))));
        }
        ushort next = unchecked((ushort)(task.ReadWord(12) + 1));
        int terminal = callback == 0x5A1AE0 ? Signed(task, 14) : callback == 0x5A1C50 ? 24 : 28;
        return new(Write(frame, address, task.WriteWord(12, next)), unchecked((short)next) >= terminal ? 2 : 0);
    }

    public static NativeBattleRayBomb307TaskStep AdvanceSprite(NativeBattleRayBomb307Frame frame, uint address, NativeBattleRayBomb307Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint callback = task.Callback;
        bool beamParticle = callback == 0x5A2340;
        if (!beamParticle && Signed(task, 14) > 0)
        {
            if ((inputs.GraphicsFlags & 0x201) == 0)
            {
                frame = Write(frame, address, task.WriteWord(14, unchecked((ushort)(task.ReadWord(14) - 1))));
            }
            return new(frame, 0);
        }
        int age = Signed(task, 12);
        uint source;
        ushort phase;
        ushort flags;
        if (callback == 0x5A1D90)
        {
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(1024, Signed(task, 0x18), 0)) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
            short scale = Signed(task, 0x1C);
            local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
            frame = LoadMatrix(frame, NativeBattleGfMatrices.Compose56C2F0(ReadView(frame, inputs).ReadMatrix(), local), true);
            source = 0xCE158C;
            phase = 0;
            flags = 12;
        }
        else
        {
            short scale = Signed(task, 0x1C);
            short depth = beamParticle ? unchecked((short)-(scale >> 3)) : unchecked((short)-(Signed(task, 0x16) >> 1));
            frame = SetupSprite(frame, beamParticle ? TaskView(task, 0x10, 8) : frame.State.ReadView(0x2242580, 8), scale, depth, inputs);
            source = beamParticle ? 0xCE15B4u : 0xCE13E0u;
            phase = beamParticle ? task.ReadWord(12) : task.ReadWord(0x20);
            flags = beamParticle ? (ushort)0 : age < 8 || age >= 14 ? (ushort)5 : (ushort)1;
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 180);
        NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(allocation.Staging, allocation.Address, source, flags).Write(allocation.Address + 4, BitConverter.GetBytes(phase));
        if (callback == 0x5A1D90)
        {
            sbyte lowAge = unchecked((sbyte)task.ReadByte(12));
            byte color = unchecked((byte)(128 - (128 / Signed(task, 0x20)) * lowAge));
            staging = staging.Write(allocation.Address + 0x1C, new byte[] { color, color, color });
        }
        else if (!beamParticle)
        {
            staging = staging.Write(allocation.Address + 8, BitConverter.GetBytes((int)Signed(task, 0x18)));
            if (flags == 5)
            {
                byte color = unchecked((byte)(age < 8 ? age << 4 : 0x60 - (age << 4)));
                staging = staging.Write(allocation.Address + 0x1C, new byte[] { color, color, color });
            }
        }
        frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleRayBomb307DrawSprite180(allocation.Address, source, phase, flags) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(307, task) with { ResourceGeneration = source } : null });
        short feedback = callback == 0x5A24D0 && (inputs.GraphicsFlags & 0x201) == 0 ? BitConverter.ToInt16(RequireStaging(frame).Read(allocation.Address + 0x28, 2)) : (short)0;
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 180) };
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        if (beamParticle)
        {
            task = task.WriteWord(0x12, unchecked((ushort)(Signed(task, 0x12) + Signed(task, 0x16))));
        }
        else if (callback == 0x5A24D0)
        {
            task = task.WriteWord(0x20, feedback < 0 ? (ushort)0 : unchecked((ushort)(task.ReadWord(0x20) + 1))).WriteWord(0x1C, unchecked((ushort)(Signed(task, 0x1C) + Signed(task, 0x1E))));
        }
        ushort next = unchecked((ushort)(task.ReadWord(12) + 1));
        int terminal = beamParticle ? Signed(task, 14) : callback == 0x5A1D90 ? Signed(task, 0x20) : 22;
        return new(Write(frame, address, task.WriteWord(12, next)), unchecked((short)next) >= terminal ? 2 : 0);
    }

    private static NativeBattleRayBomb307Frame SetupSprite(NativeBattleRayBomb307Frame frame, NativeBattleEffectArena packed, short scale, short depth, NativeBattleRayBomb307Inputs inputs)
    {
        (byte[] bytes, bool[] known) = packed.ReadSnapshot(6, 2);
        short? w = known.All(value => value) ? BitConverter.ToInt16(bytes) : null;
        NativeBattleVector position = new(BitConverter.ToInt16(packed.Read(0, 2)), BitConverter.ToInt16(packed.Read(2, 2)), BitConverter.ToInt16(packed.Read(4, 2)));
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, ReadView(frame, inputs), position, w, scale, depth) };
    }

    private static NativeBattleModulePoolTask CopyTask(NativeBattleModulePoolTask task, int offset, NativeBattleEffectArena source, int length)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, length);
        task = task.WriteBytes(offset, bytes);
        for (int index = 0; index < length; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(offset + index, 1);
            }
        }
        return task;
    }
    private static NativeBattleRayBomb307Frame LoadMatrix(NativeBattleRayBomb307Frame frame, NativeBattleActionMatrix matrix, bool translation) => frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), matrix, true, translation, null) } };
    private static NativeBattleFileStaging RequireStaging(NativeBattleRayBomb307Frame frame) => frame.Staging ?? throw new NotSupportedException("Ray-Bomb requires its actual physical temporary memory.");
    private static NativeBattleEffectArena Bounded(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena TaskView(NativeBattleModulePoolTask task, int offset, int length)
    {
        return task.BorrowPayloadArena(offset, length);
    }
}
