using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect31Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect31Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect31Nodes
{
    private const uint Callback = 0x86D1D0;

    public static NativeBattleEffect31Step Advance(NativeBattleEffect31Frame frame, uint address, NativeBattleEffect31Resources resources, NativeBattleEffect31Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != Callback)
        {
            throw new InvalidDataException("Effect31 custom nodes require their selected callback.");
        }
        switch (NativePhase(task, 2))
        {
            case 0:
                if (unchecked((short)task.ReadWord(0x24)) >= unchecked((short)task.ReadWord(0x298)))
                {
                    frame = Initialize(frame, address, resources, inputs);
                    frame = AdvanceContext(frame, address, resources, inputs).Frame;
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 1:
                (frame, bool complete) = AdvanceContext(frame, address, resources, inputs);
                if (complete)
                {
                    frame = IncrementPhase(MarkComplete(frame, address), address);
                }
                break;
        }
        return Finish(frame, address);
    }

    private static NativeBattleEffect31Frame Initialize(NativeBattleEffect31Frame frame, uint address, NativeBattleEffect31Resources resources, NativeBattleEffect31Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint resource = task.ReadDword(0x30);
        _ = Directory(frame, resource, resources);
        NativeBattleModulePoolTask root = Read(frame, task.ReadDword(0x10));
        task = NativeBattleEffectNodeContext.Initialize(task, root, Parameters(frame), inputs.ReadPackets(frame), actor => inputs.ReadModel(frame, actor), actor => inputs.ReadPoseSource(frame, actor), resource, observeBone: (actor, selector) =>
        {
            NativeBattleEffect31PointStep point = inputs.Point502170(frame, checked(0x1D972C0u + (uint)actor * 0x9C), selector, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
            frame = point.Frame;
        });
        frame = Write(frame, address, task);
        return frame with { State = frame.State.WriteDword(ContextGlobal, checked(address + 0x34)) };
    }

    private static (NativeBattleEffect31Frame Frame, bool Complete) AdvanceContext(NativeBattleEffect31Frame frame, uint address, NativeBattleEffect31Resources resources, NativeBattleEffect31Inputs inputs)
    {
        frame = frame with { State = frame.State.WriteDword(ContextGlobal, checked(address + 0x34)) };
        NativeBattleModulePoolTask task = Read(frame, address);
        int phase = unchecked((short)task.ReadWord(0x54));
        if (phase is < 0 or > 2)
        {
            throw new InvalidDataException("Effect31 custom context state leaves its native dispatch table.");
        }
        var result = NativeBattleEffectNodeLifecycle.AdvanceContext(() => Read(frame, address), updated => frame = Write(frame, address, updated), () => frame = AdvanceNodes(frame, address, resources, inputs));
        return (frame with { State = frame.State.WriteWord(0x26D33C8, unchecked((ushort)(frame.State.ReadWord(0x26D33C8) + result.EmitterCount))).WriteWord(0x26D2DAA, unchecked((ushort)(frame.State.ReadWord(0x26D2DAA) + result.GeometryCount))) }, result.Complete);
    }

    private static NativeBattleEffect31Frame AdvanceNodes(NativeBattleEffect31Frame frame, uint address, NativeBattleEffect31Resources resources, NativeBattleEffect31Inputs inputs)
    {
        uint resource = Read(frame, address).ReadDword(0x30);
        ImmutableArray<byte> directory = Directory(frame, resource, resources);
        if (frame.State.ReadDword(ContextGlobal) != checked(address + 0x34) || frame.State.ReadDword(ScratchCursorGlobal) != 0x2793E58)
        {
            throw new NotSupportedException("Effect31 custom nodes require their current context and shared descending temporary cursor.");
        }
        int first = checked((int)(frame.State.ReadDword(EmitterBankGlobal) - CommonAddress));
        int second = checked((int)(frame.State.ReadDword(ParticleBankGlobal) - CommonAddress));
        using NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, address, first, EmitterStride * EmitterCapacity, second, ParticleStride * ParticleCapacity, useSharedScratch: true)
        {
            SpawnScratchObserver = new(NativeBattleEffectNodeContextMemory.Capture(frame.Memory))
        };
        NativeBattleEffectNodeBank bank = new(resource, directory, ReadCurrent, pointer => resources.ReadSprite(pointer).Bytes);
        ushort emitterCursor = frame.State.ReadWord(EmitterIndexGlobal);
        ushort particleCursor = frame.State.ReadWord(ParticleIndexGlobal);
        NativeBattlePackedMatrix32 camera = Packed(ReadBytes(frame, frame.State.ReadDword(ViewGlobal), 32));
        int walkerEbp = unchecked((int)Pools[3].Descriptor);
        NativeBattleEffectNodeInputs nodeInputs = new(camera.ReadMatrix(), actor => inputs.ReadModel(frame, actor), (parent, source, target) => NativeBattleEffectNodeAllocation.AllocateEmitter(memory, first, EmitterCapacity, ref emitterCursor, parent, source, target), (emitter, source) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, second, ParticleCapacity, ref particleCursor, emitter, source))
        {
            SpriteNodeSelected = (_, count) =>
            {
                if (count != 1)
                {
                    walkerEbp = 0;
                }
            },
            SpriteCall = (node, count, instance) =>
            {
                int savedEbp = count == 1 ? walkerEbp : instance;
                frame = frame with { State = frame.State with { CallbackStack = NativeBattleEffect31Stack.ObserveDraw(frame.State.ReadCallbackStack(), NativeBattleEffectNodeContextMemory.Address, savedEbp, memory.Pointer(node + 0xAC)) } };
                if (count != 1)
                {
                    walkerEbp = instance + 1;
                }
            }
        };
        NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, nodeInputs);
        NativeCrtRandomState random = frame.Random;
        lifecycle.Birth();
        NativeBattleEffectNodeAllocation.WalkList(() => memory, (node, kind) =>
        {
            if (kind == 0)
            {
                frame = frame with { State = frame.State with { CallbackStack = NativeBattleEffect31Stack.InvalidateCustom(frame.State.ReadCallbackStack(), 0x86F522) } };
                byte[] source = lifecycle.ReadSource(unchecked((sbyte)memory.Byte(node + 0x6A)));
                bool transforms = memory.Word(node + 0x62) == 0 || source[0x11] == 4;
                short emitterFrame = unchecked((short)memory.Word(node + 0x60));
                lifecycle.AdvanceEmitter(node, source, ref random);
                if (transforms)
                {
                    PublishEmitterRegisters(memory, node, source, emitterFrame, bank);
                }
            }
            else if (kind == 1)
            {
                frame = frame with { State = frame.State with { CallbackStack = NativeBattleEffect31Stack.InvalidateCustom(frame.State.ReadCallbackStack(), 0x86F51A) } };
                byte[] source = lifecycle.ReadSource(unchecked((sbyte)memory.Byte(node + 0x1D6)));
                ushort priorState = memory.Word(node + 0x1CC);
                lifecycle.AdvanceGeometry(node, source);
                if (priorState == 0 && memory.Word(node + 0x1CC) == 0)
                {
                    PublishDrawableRegisters(memory, node, camera);
                }
            }
            else
            {
                throw new NotSupportedException("Effect31 custom list contains an unsupported node kind.");
            }
        });
        memory.PublishProcessMemory(NativeBattleEffectNodeContextMemory.Publish(memory.CaptureProcessMemory(), memory.SpawnScratchObserver!.View));
        _ = lifecycle.DrawNodes(inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), (_, _) => throw new NotSupportedException("Effect31 selected custom directory has no mesh material path."), (_, batch) =>
        {
            Publish();
            frame = inputs.Execute(frame, new NativeBattleEffect31DrawBatch(batch, CursorGlobal));
            memory.PublishProcessMemory(frame.Memory);
            memory.SynchronizePublishedStorage(frame.Memory);
        });
        Publish();
        return frame;

        byte[] ReadCurrent(uint pointer, int length)
        {
            if (MutableStreamRanges.Any(range => pointer >= range.Address && (ulong)pointer + (uint)length <= (ulong)range.Address + (uint)range.Length))
            {
                return ReadBytes(frame, pointer, length).Read(0, length);
            }
            return resources.ReadStatic(pointer, length);
        }

        void Publish()
        {
            NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
            frame = frame with { Memory = written.Memory, Effect = written.Effect, Random = random, State = frame.State.WriteWord(EmitterIndexGlobal, emitterCursor).WriteWord(ParticleIndexGlobal, particleCursor) };
        }
    }

    private static ImmutableArray<byte> Directory(NativeBattleEffect31Frame frame, uint address, NativeBattleEffect31Resources resources)
    {
        if (address != CustomDirectoryAddress)
        {
            throw new NotSupportedException("Effect31 custom nodes require their selected source directory.");
        }
        byte[] bytes = ReadBytes(frame, address, 0x104).Read(0, 0x104);
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 1)
        {
            throw new NotSupportedException("Effect31 custom resource relocation requires its current writable source owner.");
        }
        if (!bytes.AsSpan().SequenceEqual(resources.ReadStatic(address, 0x104)))
        {
            throw new NotSupportedException("Effect31 custom directory differs from its selected sprite resource contract.");
        }
        return bytes.ToImmutableArray();
    }

    private static void PublishDrawableRegisters(NativeBattleEffectNodeMemory memory, int node, NativeBattlePackedMatrix32 camera)
    {
        NativeBattleEffectMemory current = memory.CaptureProcessMemory();
        NativeBattleGeometryRegisterStep step = new(current.GeometryRegisters ?? throw new NotSupportedException("Effect31 drawable requires its current geometry registers."), current.ScriptedColorRegisters ?? throw new NotSupportedException("Effect31 drawable requires its current data registers."));
        NativeBattleActionMatrix matrix = camera.ReadMatrix();
        short? padding = camera.Known[18] && camera.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(camera.Bytes.AsSpan().Slice(18)) : null;
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, matrix, true, false, padding) };
        for (int column = 0; column < 3; column++)
        {
            step = NativeBattleGeometry.LoadIrColumn(step, memory.Word(node + 0x8C + column * 2), memory.Word(node + 0x92 + column * 2), memory.Word(node + 0x98 + column * 2));
            step = NativeBattleGeometry.Transform(step, false, true);
        }
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, matrix, false, true, null) };
        NativeBattleVector point = new(unchecked((short)memory.Dword(node + 0xA0)), unchecked((short)memory.Dword(node + 0xA4)), unchecked((short)memory.Dword(node + 0xA8)));
        step = NativeBattleGeometry.LoadVector(step, 0, point, point.Z < 0 ? (short)-1 : (short)0);
        step = NativeBattleGeometry.Transform(step);
        memory.PublishProcessMemory(current with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors });
    }

    private static void PublishEmitterRegisters(NativeBattleEffectNodeMemory memory, int node, byte[] source, short frame, NativeBattleEffectNodeBank bank)
    {
        NativeBattleEffectMemory current = memory.CaptureProcessMemory();
        NativeBattleGeometryRegisterStep step = new(current.GeometryRegisters ?? throw new NotSupportedException("Effect31 emitter requires its current geometry registers."), current.ScriptedColorRegisters ?? throw new NotSupportedException("Effect31 emitter requires its current data registers."));
        ImmutableArray<short>.Builder rotation = ImmutableArray.CreateBuilder<short>(9);
        for (int index = 0; index < 9; index++)
        {
            rotation.Add(unchecked((short)memory.Word(node + 0x2C + index * 2)));
        }
        NativeBattleActionMatrix matrix = new(rotation.ToImmutable(), 0, 0, 0, 4096);
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, matrix, true, false, unchecked((short)memory.Word(node + 0x3E))) };
        NativeBattleVector point;
        if (source[0x11] == 4)
        {
            point = new(Channel(0x11C), Channel(0x120), Channel(0x124));
        }
        else if (source[0x11] == 0)
        {
            point = new(Component(0), Component(4), Component(8));
        }
        else
        {
            throw new NotSupportedException("Effect31 emitter register feedback requires its selected anchor kind.");
        }
        step = NativeBattleGeometry.LoadVector(step, 0, point, null);
        step = NativeBattleGeometry.Transform(step, false);
        memory.PublishProcessMemory(current with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors });

        short Component(int offset) => unchecked((short)(BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(offset)) / 65536));
        short Channel(int offset)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(offset));
            return BinaryPrimitives.ReadInt16LittleEndian(bank.Read(unchecked(pointer + (uint)(frame * 2)), 2));
        }
    }
}
