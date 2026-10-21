using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public abstract record NativeBattleRaldo19BallOperation;
public sealed record NativeBattleRaldo19BallActorWrite(int Actor, int Offset, NativeBattleEffectArena Source, int SourceOffset, int Length) : NativeBattleRaldo19BallOperation;
public sealed record NativeBattleRaldo19BallPivot(int Actor) : NativeBattleRaldo19BallOperation;
public sealed record NativeBattleRaldo19BallTargetAnchor(int Actor) : NativeBattleRaldo19BallOperation;
public sealed record NativeBattleRaldo19BallRender(int Actor) : NativeBattleRaldo19BallOperation;
public sealed record NativeBattleRaldo19BallSound(NativeBattleModuleSound Sound) : NativeBattleRaldo19BallOperation;
public sealed record NativeBattleRaldo19BallInputs(Func<int, NativeBattleEffectArena> ReadActor, Func<NativeBattleRaldo19Frame, NativeBattleRaldo19BallOperation, NativeBattleRaldo19Frame> Execute, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> SelectActors);

public static class NativeBattleRaldo19BallController
{
    public static NativeBattleRaldo19LeafStep Advance(NativeBattleRaldo19Frame frame, uint address, NativeBattleRaldo19Resources resources, NativeBattleRaldo19BallInputs inputs)
    {
        NativeBattleRaldo19Frame current = frame;
        NativeBattleModulePoolTask task = Task();
        if (task.Callback != NativeBattleRaldo19Profile.BallCallback)
        {
            throw new InvalidDataException("Raldo ball requires its original callback.");
        }
        int actor = task.ReadByte(0x2C);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                InitializeBall();
                Next();
                break;
            case 1:
                if (unchecked((short)task.ReadWord(0x24)) >= 28)
                {
                    Execute(new NativeBattleRaldo19BallSound(new(resources.Sound, 0, null) { Pan = 128, RawPan = 128 }));
                    Next();
                }
                break;
            case 2:
                Put(Task().WriteByte(0x26, (byte)(Task().ReadByte(0x26) | 8)));
                WriteActor(0, Bytes((byte)(inputs.ReadActor(actor).Read(0, 1)[0] | 4)), 0, 1);
                Execute(new NativeBattleRaldo19BallPivot(actor));
                SetWord(0xA6, 448);
                SetWord(0xA8, 448);
                SetWord(0xAA, 448);
                for (int index = 0; index < 9; index++)
                {
                    SetWord(index * 2, index % 4 == 0 ? 4096 : 0);
                }
                for (int offset = 0x14; offset <= 0x1C; offset += 4)
                {
                    SetDword(offset, 0);
                }
                CopyActor(0x1C, 0x7C, 4);
                SetWord(0x7E, Word(0x7E) + Word(0x76));
                CopyActor(0x20, 0x80, 4);
                CopyBall(0x7C, 0x8C, 4);
                CopyActor(0x20, 0x90, 4);
                Execute(new NativeBattleRaldo19BallTargetAnchor(Task().ReadByte(0x2D)));
                SetWord(0x96, -384);
                SetWord(0xC2, 0);
                SpawnDebris(2);
                NativeBattleEffectHierarchyAppend overlay = Hierarchy().Append8DC540(5, 0x8DDC30, 64, address, inputs.SelectActors);
                SetHierarchy(overlay.Hierarchy.Write(overlay.Address, overlay.Hierarchy.Read(overlay.Address).WriteDword(0x34, 0x162A470).WriteWord(0x38, 9)));
                Next();
                break;
            case 3:
                Execute(new NativeBattleRaldo19BallPivot(actor));
                SetWord(0xA4, Math.Min((int)unchecked((short)(Word(0xA4) + 512)), 4096));
                Rotate(512);
                short forward = unchecked((short)(Word(0xC0) + 512));
                SetWord(0xC0, forward);
                short velocity;
                if (forward >= 4096)
                {
                    SetWord(0xC0, 4096);
                    current = current with { State = current.State.WithWord(0x48, 1) };
                    velocity = -608;
                    Next();
                }
                else
                {
                    velocity = Word(0x96);
                }
                SetWord(0x96, velocity + 128);
                Interpolate();
                Fall();
                if (Word(0x7E) >= Word(0x76))
                {
                    SetWord(0x7E, Word(0x76));
                    if (Word(0xC2) == 0)
                    {
                        SetWord(0xC2, 1);
                    }
                    SpawnDebris(1);
                }
                break;
            case 4:
                Execute(new NativeBattleRaldo19BallPivot(actor));
                Rotate(-512);
                short backward = unchecked((short)(Word(0xC0) - 256));
                SetWord(0xC0, backward);
                if (backward <= 0)
                {
                    SetWord(0xC0, 0);
                    Next();
                }
                Interpolate();
                SetWord(0x96, Word(0x96) + 128);
                Fall();
                if (Word(0x7E) >= Word(0x76))
                {
                    SetWord(0x7E, Word(0x76));
                    if (Word(0xC2) == 1)
                    {
                        SetWord(0xC2, 2);
                        SetWord(0x96, -384);
                        SpawnDebris(4);
                    }
                    else
                    {
                        if (Word(0xC2) == 2)
                        {
                            SetWord(0xC2, 3);
                        }
                        SpawnDebris(1);
                    }
                }
                break;
            case 5:
                Execute(new NativeBattleRaldo19BallPivot(actor));
                short morph = unchecked((short)(Word(0xA4) - 768));
                SetWord(0xA4, morph);
                if (morph <= 0)
                {
                    SetWord(0xA4, 0);
                    Next();
                }
                break;
            case 6:
                Put(Task().WriteByte(0x26, (byte)(Task().ReadByte(0x26) & 0xF7)).WriteWord(0xB4, 8));
                WriteActor(0, WordBytes(unchecked((ushort)(ReadActorWord(0) & 0xFFFB))), 0, 2);
                WriteActor(0x1E, inputs.ReadActor(actor), 0x24, 2);
                WriteActor(0x1C, current.State.BallC4, 0xB0, 4);
                WriteActor(0x20, current.State.BallC4, 0xB4, 4);
                Next();
                break;
            case 7:
                Put(Task().WriteWord(0xB4, unchecked((ushort)(Task().ReadWord(0xB4) - 1))));
                if (unchecked((short)Task().ReadWord(0xB4)) <= 0)
                {
                    WriteActor(0x0C, current.State.BallC4, 0xB8, 4);
                    WriteActor(0x10, current.State.BallC4, 0xBC, 4);
                    Next();
                }
                break;
            case 8:
                Put(Task().WriteByte(0x26, (byte)(Task().ReadByte(0x26) | 1)));
                Next();
                break;
            case 9:
                break;
            default:
                throw new InvalidDataException("Raldo signed ball phase leaves its ten original handlers.");
        }
        if ((Task().ReadByte(0x26) & 8) != 0)
        {
            short x = unchecked((short)(Dword(0x7C) - Dword(0x74)));
            short y = unchecked((short)(Word(0x7E) - Word(0x76)));
            short z = unchecked((short)(Dword(0x80) - Dword(0x78)));
            WriteActor(0x1C, WordBytes(unchecked((ushort)x)), 0, 2);
            WriteActor(0x54, DwordBytes(unchecked((uint)(int)x)), 0, 4);
            WriteActor(0x1E, WordBytes(unchecked((ushort)y)), 0, 2);
            WriteActor(0x58, DwordBytes(unchecked((uint)(int)y)), 0, 4);
            WriteActor(0x20, WordBytes(unchecked((ushort)z)), 0, 2);
            WriteActor(0x5C, DwordBytes(unchecked((uint)(int)z)), 0, 4);
            Execute(new NativeBattleRaldo19BallRender(actor));
        }
        Put(Task().WriteWord(0x24, unchecked((ushort)(Task().ReadWord(0x24) + 1))));
        bool complete = (Task().ReadByte(0x26) & 1) != 0 && Task().ReadByte(0x28) == 0;
        if (complete)
        {
            SetHierarchy(Hierarchy().Complete8DC530(address));
        }
        return new(current, complete ? 2 : 0);

        NativeBattleEffectHierarchy Hierarchy() => new(current.Effect.Pools);
        NativeBattleModulePoolTask Task() => Hierarchy().Read(address);
        void SetHierarchy(NativeBattleEffectHierarchy hierarchy) => current = current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
        void Put(NativeBattleModulePoolTask value) => SetHierarchy(Hierarchy().Write(address, value));
        void Next() => Put(Task().WriteByte(0x29, unchecked((byte)(Task().ReadByte(0x29) + 1))));
        void Execute(NativeBattleRaldo19BallOperation operation) => current = inputs.Execute(current, operation);
        void WriteActor(int offset, NativeBattleEffectArena source, int sourceOffset, int count) => Execute(new NativeBattleRaldo19BallActorWrite(actor, offset, source, sourceOffset, count));
        ushort ReadActorWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(inputs.ReadActor(actor).Read(offset, 2));
        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(current.State.BallC4.Read(offset, 2));
        uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(current.State.BallC4.Read(offset, 4));
        void SetWord(int offset, int value) => SetBall(current.State.BallC4.Write(offset, WordBytes(unchecked((ushort)value)).Read(0, 2)));
        void SetDword(int offset, uint value) => SetBall(current.State.BallC4.Write(offset, DwordBytes(value).Read(0, 4)));
        void SetBall(NativeBattleEffectArena ball) => current = current with { State = current.State with { BallC4 = ball } };
        void CopyActor(int source, int destination, int count) => SetBall(Copy(inputs.ReadActor(actor), source, current.State.BallC4, destination, count));
        void CopyBall(int source, int destination, int count) => SetBall(Copy(current.State.BallC4, source, current.State.BallC4, destination, count));
        void Rotate(int delta)
        {
            SetWord(0x9C, Word(0x9C) + delta);
            SetWord(0xA0, Word(0xA0) & 0xFFF);
        }
        void Interpolate()
        {
            int factor = Word(0xC0);
            SetWord(0x7C, Word(0x8C) + unchecked((Word(0x84) - Word(0x8C)) * factor) / 4096);
            SetWord(0x80, Word(0x90) + unchecked((Word(0x88) - Word(0x90)) * factor) / 4096);
        }
        void Fall() => SetWord(0x7E, Word(0x7E) + Word(0x96));
        void SpawnDebris(ushort count)
        {
            NativeBattleEffectHierarchyAppend child = Hierarchy().Append8DC540(4, 0x8A9F10, 184, address, inputs.SelectActors);
            NativeBattleModulePoolTask value = child.Hierarchy.Read(child.Address).WriteWord(0xB6, count).WriteDword(0x1C, Dword(0x7C)).WriteDword(0x20, Dword(0x80)).WriteWord(0x1E, 0);
            SetHierarchy(child.Hierarchy.Write(child.Address, value));
        }
        void InitializeBall()
        {
            SetBall(NativeBattleEffectArena.Clear());
            SetDword(0x68, checked(0x1D972C0u + 156u * (uint)actor));
            SetDword(0x60, 0x2726AC0);
            SetDword(0x6C, 0x1D8E054);
            SetDword(0x70, 0x1D8E054);
            SetDword(0x64, 0x27303E8);
            SetWord(0xAC, actor);
            NativeBattleEffectArena descriptor = current.State.Descriptor48.Write(4, DwordBytes(0x272E0A0).Read(0, 4));
            descriptor = descriptor.Write(0x14, new byte[] { 0, 0, 0, 0, 0x40, 1, 0, 0, 0x80, 0x80, 0x80 });
            descriptor = descriptor.Write(0x20, DwordBytes(uint.MaxValue).Read(0, 4)).Write(0x24, new byte[2]);
            current = current with { State = current.State with { Descriptor48 = descriptor } };
            CopyActor(0x1C, 0xB0, 4);
            CopyActor(0x20, 0xB4, 4);
            CopyActor(0x0C, 0xB8, 4);
            CopyActor(0x10, 0xBC, 4);
            WriteActor(0x0E, WordBytes(unchecked((ushort)current.State.Word(0x4A))), 0, 2);
        }
    }

    public static NativeBattleEffectArena Copy(NativeBattleEffectArena source, int sourceOffset, NativeBattleEffectArena destination, int destinationOffset, int count)
    {
        var snapshot = source.ReadSnapshot(sourceOffset, count);
        destination = destination.Write(destinationOffset, snapshot.Data);
        for (int index = 0; index < count; index++)
        {
            if (!snapshot.Known[index])
            {
                destination = destination.MarkUnknown(destinationOffset + index, 1);
            }
        }
        return destination;
    }

    private static NativeBattleEffectArena Bytes(byte value) => NativeBattleEffectArena.Clear().Write(0, new[] { value });
    private static NativeBattleEffectArena WordBytes(ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return NativeBattleEffectArena.Clear().Write(0, bytes);
    }
    private static NativeBattleEffectArena DwordBytes(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return NativeBattleEffectArena.Clear().Write(0, bytes);
    }
}
