using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Reflect's director,41-tick barriers,17-tick hexagons and21-tick rings.</summary>
public static class NativeBattleReflectController
{
    public const uint RootDescriptor = 0x2543CA0;

    public static NativeBattleReflectFrame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleReflectResources resources, ushort randomSeed)
    {
        if (effect.Binding is not { Effect: 106, Entry: 0x6D2AE0 } || effect.Parameters is null)
        {
            throw new InvalidDataException("Native Reflect requires its original106 entry and parameter block.");
        }
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Reflect requires its texture owner.");
        if ((effect.Parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectInitializerAsset texture = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Reflect requires its original mag105.tim.");
            (textures, _) = textures.EnqueueTim505E30(new(texture.Name, texture.Data));
            textures = textures.Register5099A0(resources.Animation);
        }
        NativeBattleEffectLocalBounds bounds = NativeBattleStaticMeshBounds.Read6D6070(resources.Wall.Bytes.AsSpan());
        short delta = unchecked((short)(bounds.Maximum.Z - bounds.Minimum.Z));
        NativeBattleReflectState previous = memory.Reflect106 ?? throw new NotSupportedException("Native Reflect requires its retained executable packet owner; migrate an explicitly authenticated standalone source.");
        NativeBattleReflectState state = previous with { WallMinimum = bounds.Minimum.Z, WallSpan = unchecked((short)(delta + (delta >> 4))) };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, 0x2523C80, 20, 1);
        (root, _) = root.Append(0x6D2C00);
        return new(memory.SeedSharedEffectRandom7059D0(randomSeed) with { Textures = textures, Reflect106 = state }, effect with { Pools = [root, NativeBattleEffectTaskPool.Initialize(0x2543CB0, 0x2521BB8, 0x5C, 3), NativeBattleEffectTaskPool.Initialize(0x2543CC0, 0x2521F78, 0x1C, 256), NativeBattleEffectTaskPool.Initialize(0x2543CD0, 0x2521CD0, 0x1C, 24), NativeBattleEffectTaskPool.Initialize(0x2543CE0, 0x2523B80, 0x1C, 9)], ReturnedDescriptor = RootDescriptor });
    }

    public static NativeBattleReflectStep Advance(NativeBattleReflectFrame frame, NativeBattleReflectResources resources, NativeBattleReflectInputs inputs)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return new(frame, [], []);
        }
        NativeBattleReflectFrame current = frame;
        NativeBattleEffectTaskPool[] pools = frame.Effect.Pools.ToArray();
        if (pools.Length != 5 || pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Native Reflect requires its five original initialized descriptors.");
        }
        NativeBattleReflectState state = frame.Memory.Reflect106 ?? throw new NotSupportedException("Native Reflect lost its retained executable packet owner.");
        NativeBattleModulePoolTask root = pools[0].Slots[0];
        state = state with { Cursor = root.ReadDword(16) != 0 ? 0 : 0x10000 };
        current = current with { Memory = current.Memory with { Reflect106 = state } };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Reflect lost its original action context.");
        ImmutableArray<int>.Builder contacts = ImmutableArray.CreateBuilder<int>();
        ImmutableArray<NativeBattleModuleSound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleModuleSound>();
        int action = root.ReadByte(14);
        if (root.ReadWord(12) == 0 && action <= inputs.Packets.Headers[parameters.HeaderIndex7C].ReadByte(17))
        {
            int first = inputs.Packets.Headers[parameters.HeaderIndex7C + action].FirstContactIndex ?? throw new NotSupportedException("Native Reflect spawn requires its actual first target.");
            byte actor = inputs.Packets.ReadContact(first).Primary.ActorIndex;
            uint pointer = checked(0x1D972C0u + actor * 156u);
            bool busy = pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.ReadDword(16) == pointer && Word(task, 12) <= 35);
            if (!busy)
            {
                (pools[1], int slot) = pools[1].Append(0x6D3160);
                uint parent = checked(pools[1].BankAddress + (uint)(slot * pools[1].Stride));
                NativeBattleVector anchor0 = Bone(actor, 0xF0);
                NativeBattleVector anchor1 = Bone(actor, 0xF1);
                NativeBattleActionMatrix model = new([0, 0, 0, 0, 0, 0, 0, 0, 0], (anchor0.X + anchor1.X) >> 1, (anchor0.Y + anchor1.Y) >> 1, (anchor0.Z + anchor1.Z) >> 1, 4096);
                NativeBattleModulePoolTask task = pools[1].Slots[slot] with { Payload = new byte[0x50].ToImmutableArray() };
                task = WriteMatrix(task.WriteWord(14, checked((ushort)action)).WriteDword(16, pointer), 20, model);
                Store(1, slot, task);
                root = root.WriteByte(14, unchecked((byte)(action + 1)));
                int angle = Roll();
                for (int index = 0; index < 8; index++)
                {
                    if (Available(3))
                    {
                        (pools[3], int child) = pools[3].Append(0x6D2F30);
                        NativeBattleModulePoolTask hexagon = pools[3].Slots[child] with { Payload = new byte[16].ToImmutableArray() };
                        hexagon = hexagon.WriteDword(24, parent).WriteWord(14, 1024).WriteWord(16, unchecked((ushort)angle));
                        hexagon = hexagon.WriteWord(18, (Roll() & 1) != 0 ? unchecked((ushort)-16) : (ushort)16).WriteWord(20, unchecked((ushort)(-37 - (Roll() & 31))));
                        Store(3, child, hexagon);
                        angle += 512;
                    }
                }
            }
        }
        int survivors = 0;
        foreach (int poolIndex in new[] { 1, 3, 4 })
        {
            NativeBattleEffectTaskStep pass = pools[poolIndex].Advance((pool, slot) =>
            {
                pools[poolIndex] = pool;
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int age = Word(task, 12);
                int result = 0;
                if (poolIndex == 1)
                {
                    int actor = NativeBattleStatusMagicController.ModelIndex(task.ReadDword(16));
                    NativeBattleGraphicsModel actorModel = inputs.ReadModel(actor);
                    int size = Math.Min((actorModel.Transform ?? throw new NotSupportedException("Native Reflect requires the target's sizeWORD26.")).Scale38 << 2, 4096);
                    short yaw = actorModel.Rotation14 ?? throw new NotSupportedException("Native Reflect requires the target's facingWORD0E.");
                    short sine = unchecked((short)(NativeBattleCameraMath.Sine(yaw) * size >> 12));
                    short cosine = unchecked((short)(NativeBattleCameraMath.Cosine(yaw) * size >> 12));
                    NativeBattleActionMatrix model = ReadMatrix(task, 20) with { Rotation = [cosine, 0, sine, 0, unchecked((short)size), 0, unchecked((short)-sine), 0, cosine] };
                    NativeBattleActionMatrix view = NativeBattleGfMatrices.Compose56C2F0(inputs.Projection.View ?? throw new NotSupportedException("Native Reflect requires its current camera matrix."), model);
                    task = WriteMatrix(WriteMatrix(task, 20, model), 52, view);
                    if ((uint)age < 20)
                    {
                        task = WriteCenter(task, resources.Wall, age);
                        int angle = Roll();
                        for (int index = 0; index < 12; index++)
                        {
                            //705A00 appends to a header passed by value. The orphan allocation remains occupied; its task never executes.
                            int orphan = Array.FindIndex(pools[2].Slots.ToArray(), candidate => (candidate.Flags & 1) == 0);
                            if (orphan < 0)
                            {
                                continue;
                            }
                            NativeBattleModulePoolTask beam = pools[2].Slots[orphan] with { Flags = 1, State = 0, Next = null, Callback = 0x6D3C50 };
                            beam = beam.WriteWord(12, 0).WriteWord(14, unchecked((ushort)angle)).WriteWord(16, checked((ushort)((Roll() & 1023) + 512))).WriteWord(18, 0).WriteWord(20, 0).WriteDword(24, checked(pool.BankAddress + (uint)(slot * pool.Stride)));
                            Store(2, orphan, beam);
                            angle += 0x155;
                        }
                    }
                    int phase = age - 5;
                    if ((uint)phase < 35)
                    {
                        short maximum = phase <= 20 ? unchecked((short)(state.WallMinimum + unchecked((uint)(state.WallSpan * (phase + 1))) / 20u)) : unchecked((short)(state.WallMinimum + state.WallSpan));
                        int fade = phase <= 20 ? 0 : (phase - 20) * 4096 / 15;
                        NativeBattleCarbuncle278RangeDraw drawn = NativeBattleCarbuncle278RangeMesh.Emit682760(resources.Wall, view, inputs.Projection, state.WallMinimum, maximum, fade, unchecked((ushort)(-(age << 3) & 127)), inputs.Colors);
                        Emit(drawn.Packets, drawn.FarColorWritten);
                        if (phase < 18 && phase % 6 == 0 && Available(4))
                        {
                            (pools[4], int child) = pools[4].Append(0x6D3AF0);
                            NativeBattleModulePoolTask ring = pools[4].Slots[child] with { Payload = new byte[16].ToImmutableArray() };
                            ring = ring.WriteDword(24, checked(pool.BankAddress + (uint)(slot * pool.Stride))).WriteWord(14, unchecked((ushort)(maximum * size >> 12))).WriteWord(16, 2048).WriteWord(18, 256).WriteWord(20, unchecked((ushort)-8));
                            Store(4, child, ring);
                        }
                    }
                    if (age == 0)
                    {
                        NativeBattleModuleAnchor anchor = NativeBattlePoseMatrices.ReadAnchor(actorModel, inputs.ReadPoseSource(actor));
                        sounds.Add(new(resources.Sound, 0x101, anchor));
                    }
                    if (age == 35)
                    {
                        int header = checked(parameters.HeaderIndex7C + task.ReadWord(14));
                        NativeBattleActionHeaderSlot packet = inputs.Packets.Headers[header];
                        int first = packet.FirstContactIndex ?? throw new NotSupportedException("Native Reflect contact requires its actual action target list.");
                        for (int index = 0; index < packet.ReadByte(16); index++)
                        {
                            contacts.Add(first + index);
                            if (inputs.ApplyContact is { } apply)
                            {
                                current = apply(current, first + index);
                            }
                        }
                    }
                    result = age >= 40 ? 2 : 0;
                }
                else
                {
                    NativeBattleModulePoolTask parent = Parent(task.ReadDword(24));
                    NativeBattleActionMatrix view = ReadMatrix(parent, 52);
                    if (poolIndex == 3)
                    {
                        NativeBattleGfGeometryBatch batch = NativeBattleReflectDrawing.Hexagon(task, parent, resources, view, inputs.Projection, inputs.Colors);
                        Emit(batch, true);
                        if (age >= 16)
                        {
                            result = 2;
                        }
                        else
                        {
                            task = task.WriteWord(16, unchecked((ushort)(task.ReadWord(16) + task.ReadWord(18)))).WriteWord(14, unchecked((ushort)(task.ReadWord(14) + task.ReadWord(20))));
                        }
                    }
                    else
                    {
                        Emit(NativeBattleReflectDrawing.Ring(task, resources, view, inputs.Projection, inputs.Colors), true);
                        if (age >= 20)
                        {
                            result = 2;
                        }
                        else
                        {
                            short speed = unchecked((short)(task.ReadWord(18) + task.ReadWord(20)));
                            speed = Math.Max((short)16, speed);
                            task = task.WriteWord(18, unchecked((ushort)speed)).WriteWord(16, unchecked((ushort)(task.ReadWord(16) + speed)));
                        }
                    }
                }
                if (result == 0)
                {
                    task = task.WriteWord(12, unchecked((ushort)(age + 1)));
                }
                Store(poolIndex, slot, task);
                return new(pools[poolIndex], result);
            });
            pools[poolIndex] = pass.Pool;
            survivors += pass.Survivors;
        }
        if (pools[2].Head is not null || pools[2].Tail is not null)
        {
            throw new InvalidDataException("Native Reflect's by-value beam header must remain empty.");
        }
        bool complete = survivors == 0;
        if (!complete)
        {
            root = root.WriteWord(12, (ushort)((root.ReadWord(12) + 1) % 10));
        }
        Store(0, 0, root);
        if (complete)
        {
            pools[0] = pools[0].Advance((pool, _) => new(pool, 2)).Pool;
        }
        return new(current with { Effect = current.Effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = complete ? 0u : RootDescriptor } }, contacts.ToImmutable(), sounds.ToImmutable());

        bool Available(int index) => pools[index].Slots.Any(task => (task.Flags & 1) == 0);
        void Store(int index, int slot, NativeBattleModulePoolTask task) => pools[index] = pools[index] with { Slots = pools[index].Slots.SetItem(slot, task) };
        ushort Roll()
        {
            current = current with { Memory = current.Memory.DrawSharedEffectRandom7059E0(out ushort value) };
            return value;
        }
        NativeBattleVector Bone(int actor, byte bone) => NativeBattlePoseMatrices.ReadBone(inputs.ReadModel(actor), inputs.ReadPoseSource(actor), bone, 0);
        NativeBattleModulePoolTask Parent(uint pointer)
        {
            if (pointer < pools[1].BankAddress || (pointer - pools[1].BankAddress) % pools[1].Stride != 0 || (pointer - pools[1].BankAddress) / pools[1].Stride >= pools[1].Slots.Length)
            {
                throw new InvalidDataException("Native Reflect child lost its physical barrier pointer.");
            }
            return pools[1].Slots[checked((int)((pointer - pools[1].BankAddress) / pools[1].Stride))];
        }
        void Emit(NativeBattleGfGeometryBatch batch, bool farColor)
        {
            if (farColor)
            {
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Reflect requires its retained far-color registers.");
                NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
                current = current with { Memory = current.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = registers.PublishModelColor(0) } } };
            }
            current = inputs.Emit(current, batch);
        }
    }

    public static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));

    public static NativeBattleActionMatrix ReadMatrix(NativeBattleModulePoolTask task, int offset)
    {
        return new(Enumerable.Range(0, 9).Select(index => Word(task, offset + index * 2)).ToImmutableArray(), unchecked((int)task.ReadDword(offset + 20)), unchecked((int)task.ReadDword(offset + 24)), unchecked((int)task.ReadDword(offset + 28)), 4096);
    }

    public static NativeBattleModulePoolTask WriteMatrix(NativeBattleModulePoolTask task, int offset, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(offset + index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        return task.WriteDword(offset + 20, unchecked((uint)matrix.X)).WriteDword(offset + 24, unchecked((uint)matrix.Y)).WriteDword(offset + 28, unchecked((uint)matrix.Z));
    }

    private static NativeBattleModulePoolTask WriteCenter(NativeBattleModulePoolTask task, NativeBattleStaticMeshResource wall, int age)
    {
        int progress = age * 15 * 4096 / 20;
        int vertex = progress >> 12;
        int fraction = progress & 4095;
        short Component(int lane)
        {
            int offset = checked(8 + vertex * 8 + lane);
            short first = BinaryPrimitives.ReadInt16LittleEndian(wall.Bytes.AsSpan().Slice(offset));
            short second = BinaryPrimitives.ReadInt16LittleEndian(wall.Bytes.AsSpan().Slice(offset + 8));
            return unchecked((short)(first + ((second - first) * fraction >> 12)));
        }
        return task.WriteWord(84, 0).WriteWord(86, unchecked((ushort)Component(2))).WriteWord(88, unchecked((ushort)Component(4)));
    }
}
