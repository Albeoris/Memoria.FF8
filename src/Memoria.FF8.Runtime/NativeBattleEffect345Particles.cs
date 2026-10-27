using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect345Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect345Particles
{
    public static NativeBattleEffect345Frame Initialize706B10(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        operation?.AdmitStaging(frame.Staging);
        return NativeBattleTaskOperation.CompleteStandalone(operation, Initialize706B10Core(frame, address, resources, inputs));
    }

    private static NativeBattleEffect345Frame Initialize706B10Core(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        frame = frame with { State = frame.State.BindNodeSources(resources) };
        NativeBattleModulePoolTask task = Read(frame, address);
        uint source = task.ReadDword(0x30);
        if (frame.State.NodeData is null || !frame.State.NodeData.TryGetValue(source, out NativeBattleNodeSource? data))
        {
            throw new NotSupportedException("Effect345 particle initialization requires its selected current node source.");
        }
        using NativeBattleNodeInitialization<NativeBattleNodeSource> initialization = PrepareRelocate(frame, source);
        frame = frame with { State = frame.State.WriteDword(0x255EA94, checked(address + 0x34)) };
        NativeBattleEffectNodeDogOrigins? origins = null;
        if (task.ReadWord(0x29C) == 2)
        {
            uint workspace = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, 0x1524658, 4).Read(0, 4));
            NativeBattleEffectArena view = ReadBytes(frame, workspace, 16);
            origins = new(Vector(view, 0), Vector(view, 8));
        }
        task = NativeBattleEffectNodeContext.Initialize(task, Read(frame, task.ReadDword(0x10)), Parameters(frame), inputs.ReadPackets(frame), actor => inputs.ReadModel(frame, actor), actor => inputs.ReadPoseSource(frame, actor), source, singleSelectedTarget: true, dogOrigins: origins, observeBone: (actor, selector) => frame = inputs.Point502170(frame, checked(0x1D972C0u + (uint)(actor * 0x9C)), selector, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 8)).Frame);
        frame = Write(frame, address, task);
        initialization.Publish();
        return frame;
    }

    public static NativeBattleEffect345Frame Relocate707370(NativeBattleEffect345Frame frame, uint address)
    {
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        operation?.AdmitStaging(frame.Staging);
        return NativeBattleTaskOperation.CompleteStandalone(operation, Relocate707370Core(frame, address));
    }

    private static NativeBattleEffect345Frame Relocate707370Core(NativeBattleEffect345Frame frame, uint address)
    {
        using NativeBattleNodeInitialization<NativeBattleNodeSource> initialization = PrepareRelocate(frame, address);
        initialization.Publish();
        return frame;
    }

    private static NativeBattleNodeInitialization<NativeBattleNodeSource> PrepareRelocate(NativeBattleEffect345Frame frame, uint address)
    {
        NativeBattleNodeSource data = frame.State.NodeData is not null && frame.State.NodeData.TryGetValue(address, out NativeBattleNodeSource? current) ? current : throw new NotSupportedException("Effect345 relocation requires its bound current node source.");
        NativeBattleTaskOperation.AdmitCurrentRetainedMemoryStorage(data);
        if (data.ReadUInt32(0) != 0)
        {
            return new(data, null);
        }
        NativeBattleNodeBankOperation operation = new(data);
        try
        {
            NativeBattleEffectNodeBankFixups.Relocate(operation, address);
            operation.WriteUInt32(0, 1);
            return new(data, operation);
        }
        catch
        {
            operation.Dispose();
            throw;
        }
    }

    public static NativeBattleEffect345Step Advance7075D0(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        operation?.AdmitStaging(frame.Staging);
        return NativeBattleTaskOperation.CompleteStandalone(operation, Advance7075D0Core(frame, address, resources, inputs));
    }

    private static NativeBattleEffect345Step Advance7075D0Core(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        frame = frame with { State = frame.State.WriteDword(0x255EA94, checked(address + 0x34)) };
        var context = NativeBattleEffectNodeLifecycle.AdvanceContext(() => Read(frame, address), task => frame = Write(frame, address, task), () =>
        {
            uint source = Read(frame, address).ReadDword(0x30);
            NativeBattleNodeSource current = frame.State.NodeData is not null && frame.State.NodeData.TryGetValue(source, out NativeBattleNodeSource? data) ? data : throw new NotSupportedException("Effect345 particles require their current relocated source.");
            int first = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(0x255EA44), 0x870);
            int second = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(0x255EA88), 0x9D80);
            if (frame.State.ReadDword(0x255EA84) - 0x50 != NativeBattleEffectNodeContextMemory.Address + 100)
            {
                throw new NotSupportedException("Effect345 spawn scratch requires its actual current physical suffix.");
            }
            NativeBattleTheEnd85SpawnScratchObserver spawnScratch = new(NativeBattleEffectNodeContextMemory.Capture(frame.Memory));
            using NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, address, first, 0x870, second, 0x9D80, useSharedScratch: true) { SpawnScratchObserver = spawnScratch };
            NativeBattleTaskOperation.AdmitCurrentRetainedMemoryStorage(current);
            using NativeBattleEffectNodeBank bank = new(source, current);
            ushort emitterCursor = frame.State.ReadWord(0x2557758);
            ushort particleCursor = frame.State.ReadWord(0x255EA50);
            NativeBattleActionMatrix camera = Packed(ReadBytes(frame, frame.State.ReadDword(0x2557208), 32)).ReadMatrix();
            NativeBattleEffectNodeInputs nodeInputs = new(camera, actor => inputs.ReadModel(frame, actor), (parent, descriptor, target) => NativeBattleEffectNodeAllocation.AllocateEmitter(memory, first, 20, ref emitterCursor, parent, descriptor, target), (emitter, descriptor) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, second, 60, ref particleCursor, emitter, descriptor))
            {
                SelectedMorph = (mesh, firstShape, secondShape, channel) =>
                {
                    Flush();
                    frame = Morph70ABF0(frame, bank, mesh, firstShape, secondShape, channel);
                    memory.PublishProcessMemory(frame.Memory);
                },
                GeometryAdvanced = (particle, complete) =>
                {
                    if (!complete)
                    {
                        frame = PublishAppearance70A460(frame, memory, bank, particle);
                    }
                },
                EmitterAdvanced = () =>
                {
                    frame = frame with { Memory = NativeBattleEffectNodeContextMemory.Publish(memory.CaptureProcessMemory(), spawnScratch.View) };
                    memory.PublishProcessMemory(frame.Memory);
                }
            };
            NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, nodeInputs);
            lifecycle.Birth();
            NativeCrtRandomState random = frame.Random;
            lifecycle.AdvanceList(ref random);
            frame = frame with { Random = random };
            Flush();
            DrawList(false);
            DrawList(true);
            Flush();
            frame = frame with { State = frame.State.WriteWord(0x2557758, emitterCursor).WriteWord(0x255EA50, particleCursor) };

            void Flush()
            {
                NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
                bank.Publish();
                frame = frame with { Memory = written.Memory, Effect = written.Effect, State = frame.State.ReplaceNodeBank(bank.Address, bank.Source) };
                memory.PublishProcessMemory(frame.Memory);
            }



            void DrawList(bool meshes)
            {
                NativeBattleEffectNodeAllocation.WalkList(() => memory, (particle, kind) =>
                {
                    if (kind != 1)
                    {
                        return;
                    }
                    int selector = unchecked((sbyte)memory.Byte(particle + 0x1D6));
                    uint descriptorAddress = BinaryPrimitives.ReadUInt32LittleEndian(bank.Read(unchecked(memory.ContextDword(0x224) + (uint)(selector * 4)), 4));
                    byte[] descriptor = bank.Read(descriptorAddress, 344);
                    if (!(meshes ? descriptor[0x16] is 4 or 5 : descriptor[0x16] is 0 or 1 or 2))
                    {
                        return;
                    }
                    uint resourceAddress = memory.Dword(particle + 0x170);
                    if (resourceAddress == 0)
                    {
                        return;
                    }
                    int count = unchecked((sbyte)memory.Byte(particle + 0x1D8));
                    for (int instance = 0; instance < count; instance++)
                    {
                        if (count != 1)
                        {
                            for (int axis = 0; axis < 3; axis++)
                            {
                                memory.Dword(particle + 0xC0 + axis * 4, unchecked((uint)(int)(short)memory.Word(particle + 0x19C + instance * 8 + axis * 2)));
                            }
                        }
                        Flush();
                        uint matrixAddress = memory.Pointer(particle + 0xAC);
                        NativeBattlePackedMatrix32 packed = Packed(ReadBytes(frame, matrixAddress, 32));
                        uint scratch = frame.State.ReadDword(0x255EA84);
                        if (meshes)
                        {
                            uint temporary = checked(scratch - 104);
                            frame = frame with { State = frame.State.WriteDword(0x255EA84, temporary) };
                            NativeBattleEffectArena view = NativeBattleActorWaveMesh.PrepareContext(ReadBytes(frame, temporary, 104), resourceAddress, BinaryPrimitives.ReadInt16LittleEndian(descriptor.AsSpan(0x3A)), unchecked((short)memory.Word(particle + 0x1CE)), memory.Word(particle + 0x1CE) != 0 ? memory.Dword(particle + 0x16C) : null);
                            frame = WriteBytes(frame, temporary, view, 104);
                            frame = LoadMatrix(frame, packed);
                            frame = NativeBattleEffect345Body.Draw707820(frame, temporary, ReadMesh(frame, resources, resourceAddress), resourceAddress, resources, inputs);
                        }
                        else
                        {
                            uint temporary = checked(scratch - 180);
                            if (temporary != NativeBattleEffectNodeContextMemory.Address)
                            {
                                throw new NotSupportedException("Effect345 projected sprites require their actual canonical physical context.");
                            }
                            frame = frame with { State = frame.State.WriteDword(0x255EA84, temporary) };
                            ImmutableArray<byte> resource = bank.ReadResource(resourceAddress);
                            ushort phase = unchecked((ushort)(short)(sbyte)memory.Byte(particle + 0x1D0));
                            NativeBattleGfGeometryBatch batch = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(resource, phase, packed.ReadMatrix(), inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2, observeRolledMatrix: memory.WriteSpriteMatrix, observeQuadMatrix: memory.WriteQuadMatrix, geometry: memory.ProjectedSpriteGeometry(particle, resourceAddress, resource, phase, inputs.ReadProjection(frame)));
                            frame = frame with { Memory = memory.CaptureProcessMemory() };
                            frame = inputs.Execute(frame, new NativeBattleEffect345DrawBatch(batch, NativeBattleEffect345Profile.PrimaryCursorGlobal));
                        }
                        frame = frame with { State = frame.State.WriteDword(0x255EA84, scratch) };
                        memory.SynchronizePublishedStorage(frame.Memory);
                    }
                });
            }
        });
        frame = frame with { State = frame.State.WriteWord(0x255EA7C, unchecked((ushort)(frame.State.ReadWord(0x255EA7C) + context.EmitterCount))).WriteWord(0x255EA40, unchecked((ushort)(frame.State.ReadWord(0x255EA40) + context.GeometryCount))) };
        return new(frame, context.Complete ? 1 : 0);
    }

    public static ImmutableArray<byte> ReadMesh(NativeBattleEffect345Frame frame, NativeBattleEffect345Resources resources, uint address)
    {
        uint polygon = BinaryPrimitives.ReadUInt32LittleEndian(Source(frame, resources, address, 4).Read(0, 4));
        int cursor = checked((int)polygon);
        int extent = 8;
        for (int group = 0; group < 8; group++)
        {
            int count = Math.Max(BinaryPrimitives.ReadInt32LittleEndian(Source(frame, resources, checked(address + (uint)cursor), 4).Read(0, 4)), 0);
            cursor = checked(cursor + 4);
            int stride = new[] { 12, 12, 20, 24, 20, 24, 28, 36 }[group];
            ImmutableArray<byte> faces = Source(frame, resources, checked(address + (uint)cursor), checked(count * stride)).Read(0, checked(count * stride)).ToImmutableArray();
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < (group % 2 == 0 ? 3 : 4); corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(faces.AsSpan().Slice(face * stride + 4 + corner * 2, 2));
                    extent = Math.Max(extent, checked(8 + index * 4 + 8));
                }
            }
            cursor = checked(cursor + count * stride);
        }
        return Source(frame, resources, address, Math.Max(cursor, extent)).Read(0, Math.Max(cursor, extent)).ToImmutableArray();
    }

    private static NativeBattleVector Vector(NativeBattleEffectArena view, int offset) => new(BinaryPrimitives.ReadInt16LittleEndian(view.Read(offset, 2)), BinaryPrimitives.ReadInt16LittleEndian(view.Read(offset + 2, 2)), BinaryPrimitives.ReadInt16LittleEndian(view.Read(offset + 4, 2)));
}
