namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSiren95ContactServices(NativeBattleActionPacketsState Packets, Func<byte, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleSiren95Frame, byte, int, NativeBattleSiren95Frame> SelectSequence);

public static class NativeBattleSiren95Contacts
{
    public static NativeBattleSiren95TaskStep Advance7447C0(NativeBattleSiren95Frame frame, uint address, NativeBattleSiren95ContactServices services)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x7447C0)
        {
            throw new InvalidDataException("Native Siren contact emitter requires its selected callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                NativeBattleGraphicsModel target = services.ReadModel(task.ReadByte(0x2D));
                NativeBattleEffectLocalBounds bounds = target.GeometryBounds52 ?? throw new NotSupportedException("Native Siren contact emitter requires its current target bounds.");
                if (bounds.Maximum.Y <= -512)
                {
                    frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    break;
                }
                NativeBattleModelTransform transform = target.Transform ?? throw new NotSupportedException("Native Siren contact emitter requires its current target position.");
                if (transform.Position28.Z <= unchecked((short)State(frame).ReadWord(0x258FB64)))
                {
                    if (((target.Flags8 ?? throw new NotSupportedException("Native Siren contact emitter requires its current target status flags.")) & 2) == 0)
                    {
                        frame = frame with { Random = frame.Random.Draw(out ushort random) };
                        frame = services.SelectSequence(frame, task.ReadByte(0x2D), 4 + random % 3);
                    }
                    frame = Next(Write(frame, address, Read(frame, address).WriteWord(0xDC, 8)), address);
                }
                break;
            case 1:
                if ((task.ReadByte(0x24) & 1) != 0)
                {
                    Spawn(0x744AA0, false);
                }
                Spawn(0x744C30, true);
                task = Read(frame, address);
                short remaining = unchecked((short)(task.ReadWord(0xDC) - 1));
                task = task.WriteWord(0xDC, unchecked((ushort)remaining));
                if (Spawned(frame) >= 3 || remaining <= 0)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                    frame = Next(Write(frame, address, task), address);
                }
                else
                {
                    frame = Write(frame, address, task);
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException($"Native Siren contact emitter signed phase {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
        }
        return Finish(frame, address);

        void Spawn(uint callback, bool gravity)
        {
            byte actor = Read(frame, address).ReadByte(0x2D);
            var appended = NativeBattleSiren95Actors.AppendTask(frame, 0x257FA90, callback, 0xE0, address, services.Packets);
            frame = appended.Frame;
            NativeBattleModulePoolTask child = Read(frame, appended.Address);
            for (int corner = 0; corner < 4; corner++)
            {
                NativeBattleGraphicsModel model = services.ReadModel(actor);
                NativeBattleEffectLocalBounds bounds = model.GeometryBounds52 ?? throw new NotSupportedException("Native Siren contact particle requires its current target bounds.");
                NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Siren contact particle requires its current target position.");
                frame = frame with { Random = frame.Random.Draw(out ushort x) };
                short px = unchecked((short)(bounds.Minimum.X + unchecked((bounds.Maximum.X - bounds.Minimum.X) * (x & 4095)) / 4096 + transform.Position28.X));
                frame = frame with { Random = frame.Random.Draw(out ushort y) };
                short py = unchecked((short)-(y & 511));
                frame = frame with { Random = frame.Random.Draw(out ushort z) };
                short pz = unchecked((short)(bounds.Minimum.Z + unchecked((bounds.Maximum.Z - bounds.Minimum.Z) * (z & 4095)) / 4096 + transform.Position28.Z));
                child = child.WriteWord(0x6C + 8 * corner, unchecked((ushort)px)).WriteWord(0x6E + 8 * corner, unchecked((ushort)py)).WriteWord(0x70 + 8 * corner, unchecked((ushort)pz));
                if (gravity)
                {
                    frame = frame with { Random = frame.Random.Draw(out ushort vx) };
                    frame = frame with { Random = frame.Random.Draw(out ushort vz) };
                    frame = frame with { Random = frame.Random.Draw(out ushort vy) };
                    child = child.WriteWord(0x8C + 8 * corner, unchecked((ushort)((vx & 2047) - 1024))).WriteWord(0x90 + 8 * corner, unchecked((ushort)((vz & 2047) - 1024))).WriteWord(0x8E + 8 * corner, unchecked((ushort)(-1536 - (vy & 2047))));
                }
                else
                {
                    frame = frame with { Random = frame.Random.Draw(out ushort vy) };
                    child = child.WriteWord(0x8E + 8 * corner, unchecked((ushort)-(vy & 1023)));
                }
            }
            frame = Write(frame, appended.Address, child);
        }
    }

    public static NativeBattleSiren95TaskStep AdvanceParticle(NativeBattleSiren95Frame frame, uint address, Func<NativeBattleSiren95Frame, uint, short, NativeBattleSiren95Frame> draw)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool gravity = task.Callback == 0x744C30;
        bool modelParticle = task.Callback is 0x746FD0 or 0x747220;
        bool screenParticle = task.Callback == 0x7466A0;
        bool targetParticle = task.Callback == 0x7433C0;
        if (!gravity && !modelParticle && !screenParticle && !targetParticle && task.Callback != 0x744AA0)
        {
            throw new InvalidDataException("Native Siren contact particle requires its selected callback.");
        }
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        switch (phase)
        {
            case 0:
                task = task.WriteWord(0x56, unchecked((ushort)(modelParticle || screenParticle || targetParticle ? -16 : -32))).WriteWord(0x52, modelParticle || screenParticle || targetParticle ? (ushort)10 : gravity ? (ushort)8 : (ushort)15);
                if (task.Callback is not (0x746FD0 or 0x7466A0 or 0x7433C0))
                {
                    task = task.WriteDword(0x4C, modelParticle ? 0x15334A0u : gravity ? 0x15335CCu : 0x1533024u);
                }
                if (!modelParticle && !screenParticle && !targetParticle && Spawned(frame) >= 3)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4));
                }
                if (targetParticle)
                {
                    frame = frame with { Random = frame.Random.Draw(out ushort sprite) };
                    uint source = sprite % 3 == 0 ? 0x15331D0u : sprite % 3 == 1 ? 0x1533328u : 0x15334A0u;
                    task = task.WriteDword(0x4C, source);
                }
                if (gravity)
                {
                    frame = frame with { Random = frame.Random.Draw(out ushort random) };
                    task = task.WriteWord(0xDC, (ushort)(random & 7));
                }
                frame = Next(Write(frame, address, task), address);
                break;
            case 1:
            case 2 when gravity:
                if (!modelParticle && !screenParticle && !targetParticle && Spawned(frame) >= 3)
                {
                    frame = Next(Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5))), address);
                    break;
                }
                short image = unchecked((short)(task.ReadWord(0x50) + 1));
                bool ended = image > unchecked((short)task.ReadWord(0x52));
                task = task.WriteWord(0x50, unchecked((ushort)image));
                if (ended)
                {
                    task = task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4));
                }
                if (gravity && phase == 1)
                {
                    if (unchecked((short)task.ReadWord(0x50)) > 1)
                    {
                        short repeat = unchecked((short)(task.ReadWord(0xDC) - 1));
                        task = task.WriteWord(0xDC, unchecked((ushort)repeat)).WriteWord(0x50, 0);
                        frame = Write(frame, address, task);
                        if (repeat <= 0)
                        {
                            frame = Next(frame, address);
                        }
                    }
                    else
                    {
                        frame = Write(frame, address, task);
                    }
                }
                else if (ended)
                {
                    frame = Next(Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5))), address);
                }
                else
                {
                    frame = Write(frame, address, task);
                }
                break;
            case 2:
            case 3 when gravity:
                break;
            default:
                throw new NotSupportedException($"Native Siren contact particle signed phase {phase} is unsupported.");
        }
        for (int corner = 0; corner < 4; corner++)
        {
            task = Read(frame, address);
            if (gravity)
            {
                task = task.WriteWord(0x8E + 8 * corner, unchecked((ushort)(task.ReadWord(0x8E + 8 * corner) + 192)));
            }
            if (!screenParticle)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    int velocityOffset = 0x8C + 8 * corner + axis * 2;
                    short velocity = unchecked((short)task.ReadWord(velocityOffset));
                    if (!gravity || axis != 1)
                    {
                        velocity = unchecked((short)(velocity - velocity / (gravity ? 16 : 4)));
                        task = task.WriteWord(velocityOffset, unchecked((ushort)velocity));
                    }
                    int positionOffset = 0x6C + 8 * corner + axis * 2;
                    task = task.WriteWord(positionOffset, unchecked((ushort)(task.ReadWord(positionOffset) + velocity / 16)));
                }
            }
            task = task.WriteDword(0x1C, task.ReadDword(0x6C + 8 * corner)).WriteDword(0x20, task.ReadDword(0x70 + 8 * corner));
            if (task.Callback == 0x746FD0)
            {
                task = task.WriteDword(0x4C, corner == 0 ? 0x15336C4u : 0x15334A0u);
            }
            if (screenParticle)
            {
                task = task.WriteDword(0x4C, corner == 0 ? 0x15334A0u : corner == 1 ? 0x1533328u : 0x15331D0u);
            }
            frame = Write(frame, address, task);
            if (!gravity || corner == 0)
            {
                frame = draw(frame, address, screenParticle ? Spawned(frame) >= 5 ? (short)4096 : (short)1024 : modelParticle ? task.Callback == 0x746FD0 ? (short)0x400 : (short)0x800 : gravity ? (short)0x3000 : (short)4096);
            }
        }
        return Finish(frame, address);
    }

    private static NativeBattleSiren95TaskStep Finish(NativeBattleSiren95Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(Write(frame, address, task), (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);
    }

    private static short Spawned(NativeBattleSiren95Frame frame) => unchecked((short)State(frame).ReadWord((State(frame).DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren contact particle requires its director pointer.")) + 0x40));
    private static NativeBattleModulePoolTask Read(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Write(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
    private static NativeBattleSiren95Frame Next(NativeBattleSiren95Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 1))));
    private static NativeBattleSiren95State State(NativeBattleSiren95Frame frame) => frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren contact particle requires its actual module owner.");
}
