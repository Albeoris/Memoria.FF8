using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The original Cactuar module's physical scheduler and arena-owned particle/cache lifecycle.</summary>
public static class NativeBattleCactuar199Controller
{
    public const uint MainDescriptor = 0x22599F0;
    public const uint MainBank = 0x22599D0;
    public const uint ChildDescriptor = 0x2259A08;
    public const uint ChildBank = 0x2259A18;
    public const int TargetCacheOffset = 0x20000;
    public const int ParticleOffset = 0x2A000;
    public const int NeedleOffset = 0x2B000;

    public static NativeBattleCactuar199Frame Initialize5A8750(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleCactuar199Inputs inputs, NativeBattleCactuar199Resources resources, NativeCrtRandomState random)
    {
        if (effect.Binding is not { Effect: 199, Entry: 0x5A8750, Initializer: 0x5A8730 } || effect.Parameters is null)
        {
            throw new InvalidDataException("Native Cactuar requires its actual module binding and action parameters.");
        }
        NativeBattleCactuar199State retained = memory.Cactuar199 ?? throw new NotSupportedException("Native Cactuar requires its retained raw model/control owner.");
        retained.Validate();
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[effect.Parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Cactuar requires its first header's physical contacts.");
        int actor = inputs.Packets.ReadContact(first).Primary.ActorIndex;
        NativeBattleGraphicsModel target = inputs.ReadModel(actor);
        NativeBattleActionMatrix targetMatrix = target.GeometryWorldMatrix ?? throw new NotSupportedException("Native Cactuar placement requires its first target's current raw matrix.");
        short yaw = target.Rotation14 ?? throw new NotSupportedException("Native Cactuar orientation requires its first target's current yaw.");
        NativeBattleVector center = Vector(inputs.ReadCentroid(effect.Parameters.TargetMask7A));
        NativeBattleVector displacement = NativeBattleGfMatrices.RotateShort56C4F0(targetMatrix, new(0, 0, -4500));
        NativeBattleActionMatrix basis = NativeBattleGfMatrices.Euler56CD50(new(0, unchecked((short)(yaw + 2048)), 0)) with { X = displacement.X, Y = displacement.Y, Z = unchecked(center.Z + displacement.Z) };
        if (resources.EntryModelCopySeed.Data.IsDefault || resources.EntryModelCopySeed.Data.Length != 0x21EC)
        {
            throw new InvalidDataException("Native Cactuar requires its exact backwards model-copy seed including the boundary DWORD.");
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Cactuar requires its571870-established arena.");
        arena = arena.Write(4, resources.EntryModelCopySeed.Data.AsSpan().Slice(4));
        for (int index = 0; index < 100; index++)
        {
            arena = arena.Write(ParticleOffset + index * 0x1C, new byte[4]);
        }
        for (int index = 0; index < 800; index++)
        {
            arena = arena.Write(NeedleOffset + index * 0x3C, new byte[4]);
        }
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Cactuar requires its retained texture queue.");
        (textures, _) = textures.EnqueueTim505E30(effect.ReadHeapResource(memory, 0x225A82C));
        (textures, _) = textures.EnqueueTim505E30(resources.ModelTim);
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 0x10, 2);
        (main, _) = main.Append(0x5AA3A0);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 0x24, 100);
        (children, _) = children.Append(0x5A8940);
        memory = memory with { Arena = arena, Textures = textures, Cactuar199 = retained with { BaseMatrix = basis, BaseView = null, TerminalFlags = 0, FirstTargetActor = actor } };
        return new(memory, effect with { Pools = [main, children], ReturnedDescriptor = MainDescriptor }, random);
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        NativeBattleEffectTaskPool pool = effect.Pools.Single(pool => pool.DescriptorAddress == ChildDescriptor);
        long delta = (long)address - ChildBank - 0xE;
        if (delta < 0 || delta % 0x24 != 0 || delta / 0x24 >= pool.Slots.Length)
        {
            throw new InvalidDataException("Native Cactuar completion requires an actual child BYTE E owner.");
        }
        int slot = (int)(delta / 0x24);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if ((task.Flags & 1) == 0 || task.Callback != 0x5AA360)
        {
            throw new InvalidDataException("Native Cactuar completion requires its live final-load task.");
        }
        task = task.WriteWord(0xE, (ushort)((task.ReadWord(0xE) & 0xFF00) | value));
        return effect with { Pools = effect.Pools.SetItem(effect.Pools.IndexOf(pool), Put(pool, slot, task)) };
    }

    public static NativeBattleCactuar199Frame Advance5AA3A0(NativeBattleCactuar199Frame frame, NativeBattleCactuar199Inputs inputs, NativeBattleCactuar199Resources resources)
    {
        frame.Effect.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Cactuar lost its action parameters.");
        NativeBattleEffectTaskPool main = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == MainDescriptor);
        NativeBattleEffectTaskPool children = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ChildDescriptor);
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        Shared().Validate();
        NativeBattleEffectTaskStep root = main.Advance((pool, slot) =>
        {
            main = pool;
            NativeBattleEffectTaskStep advanced = children.Advance(Child);
            children = advanced.Pool;
            return new(Put(main, slot, Add(main.Slots[slot], 0xC, 1)), advanced.Survivors == 0 ? 2 : 0);
        });
        return frame with { Effect = frame.Effect with { Pools = [root.Pool, children], ReturnedDescriptor = root.Survivors == 0 ? 0u : MainDescriptor } };

        NativeBattleEffectTaskCallback Child(NativeBattleEffectTaskPool pool, int slot)
        {
            children = pool;
            NativeBattleModulePoolTask task = pool.Slots[slot];
            int result = task.Callback switch
            {
                0x5A8940 => Timeline(slot, task),
                0x63E9C0 => Camera(slot, task),
                0x5A8C20 => Body(slot, task),
                0x5A8B40 or 0x5A8BC0 => Flash(slot, task),
                0x5A9140 or 0x5A9570 or 0x5A9730 => Mesh(slot, task),
                0x5A9290 => Particles(slot, task),
                0x5A9880 => Setup(slot, task),
                0x5A9C10 => Needles(slot, task),
                0x5AA2A0 => Hit(slot, task),
                0x5AA360 => Final(slot, task),
                _ => throw new NotSupportedException($"Native Cactuar callback{task.Callback:X8} is unsupported.")
            };
            return new(children, result);
        }
        int Timeline(int slot, NativeBattleModulePoolTask task)
        {
            SetShared(Shared() with { BaseView = NativeBattlePoseMatrices.Compose56C2F0(inputs.ReadView(), Basis()) });
            if (paused)
            {
                return 0;
            }
            switch (Word(task, 0xC))
            {
                case 0:
                    NativeBattleGuardianCameraTask camera = NativeBattleGuardianCameraProgram.Begin63E960(inputs.ReadCameraProcess(), resources.CameraProgram.Data, 0);
                    SetShared(Shared() with { CameraTask = camera });
                    int cameraSlot = Append(0x63E9C0, true);
                    Store(cameraSlot, children.Slots[cameraSlot].WriteDword(0x14, 0xCF35BC).WriteDword(0x18, 0x2259950).WriteDword(0x1C, NativeBattleCactuarRawModel.ModelAddress));
                    Emit(new NativeBattleCactuar199StreamStart(0xE94910));
                    SetShared(Shared() with { LookupCounter = (uint)Rand() });
                    Sync();
                    NativeBattleCactuar199VibrationStep vibration = inputs.StartVibration(frame);
                    frame = vibration.Frame;
                    Reload();
                    SetShared(Shared() with { VibrationHandle = vibration.Handle });
                    break;
                case 10:
                    Append(0x5A8C20, true);
                    break;
                case 15:
                    Emit(new NativeBattleCactuar199Sound(resources.Sound1));
                    Emit(new NativeBattleCactuar199StreamPlay(128, 0, 96));
                    break;
                case 53:
                    Emit(new NativeBattleCactuar199Sound(resources.Sound2));
                    Emit(new NativeBattleCactuar199StreamStart(0xE94914));
                    int increasing = Append(0x5A8B40);
                    Store(increasing, children.Slots[increasing].WriteWord(0xE, 12).WriteWord(0x18, 2048));
                    break;
                case 76:
                    Emit(new NativeBattleCactuar199StreamPlay(128, 0, 96));
                    break;
                case 145:
                    int decreasing = Append(0x5A8BC0);
                    Store(decreasing, children.Slots[decreasing].WriteWord(0xE, 8).WriteWord(0x18, 2048));
                    break;
                case 148:
                    Emit(new NativeBattleCactuar199StopVibration(Shared().VibrationHandle ?? throw new NotSupportedException("Native Cactuar lost its actual vibration handle.")));
                    break;
            }
            task = Add(task, 0xC, 1);
            Store(slot, task);
            return Word(task, 0xC) > 151 ? 2 : 0;
        }
        int Camera(int slot, NativeBattleModulePoolTask task)
        {
            NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(Shared().CameraTask ?? throw new NotSupportedException("Native Cactuar camera task is missing."), Basis(), inputs.ReadCameraPose(), (inputs.GraphicsFlags & 1) != 0);
            SetShared(Shared() with { CameraTask = step.Task });
            task = task.WriteWord(0xC, step.Task.Frame).WriteDword(0x14, checked(0xCF35BCu + (uint)step.Task.Offset));
            Store(slot, task);
            Emit(new NativeBattleCactuar199CameraPublish(step));
            return step.Result;
        }
        int Body(int slot, NativeBattleModulePoolTask task)
        {
            int counter = Word(task, 0xC);
            if (counter == 0)
            {
                byte[] model = Shared().RawModel.Model.ToArray();
                Identity(model);
                W(model, 0x26, 6144);
                SetMatrix(model, Diagonal(6144));
                D(model, 0x54, 0);
                D(model, 0x58, 0);
                D(model, 0x5C, 0);
                SetRaw(NativeBattleCactuarRawModel.Initialize5A8F40(Shared().RawModel with { Model = model.ToImmutableArray() }, resources.Model, NativeBattleEffectNodeMemory.ArenaAddress, 1));
            }
            bool advance = true;
            if (!paused)
            {
                if (counter == 0)
                {
                    Select(0);
                }
                else if (counter == 5)
                {
                    Select(1);
                }
                else if (counter is >= 10 and <= 39)
                {
                    advance = false;
                }
                else if (counter == 40)
                {
                    Select(3);
                }
                else if (counter is >= 43 and <= 58)
                {
                    advance = false;
                    if (counter == 43)
                    {
                        task = task.WriteWord(0x22, unchecked((ushort)-600));
                        Select(2);
                        int ground = Append(0x5A9140);
                        Store(ground, Position(children.Slots[ground], 0, 0).WriteWord(0x18, (ushort)(Rand() % 4096)).WriteWord(0x1C, 4096));
                        Append(0x5A9290, true);
                    }
                    MoveY(Word(task, 0x22));
                    task = Add(task, 0x22, -Word(task, 0x22) / 8);
                }
                else if (counter is >= 59 and <= 86)
                {
                    advance = false;
                    if (counter == 59)
                    {
                        task = task.WriteWord(0x22, unchecked((ushort)-60));
                    }
                    if (counter <= 63)
                    {
                        MoveY(Word(task, 0x22));
                        task = Add(task, 0x22, -Word(task, 0x22) / 3);
                    }
                    if (counter == 63)
                    {
                        int mesh = Append(0x5A9570);
                        Store(mesh, Position(children.Slots[mesh], -380, 100).WriteWord(0x1C, 12288).WriteWord(0x1E, 614));
                    }
                    if (counter == 64)
                    {
                        int mesh = Append(0x5A9730);
                        Store(mesh, Position(children.Slots[mesh], -380, 100).WriteWord(0x18, (ushort)(Rand() % 4096)).WriteWord(0x1A, 30).WriteWord(0x1C, 8192));
                    }
                    if (counter == 79)
                    {
                        Emit(new NativeBattleCactuar199FullscreenFlash(2, 1, 8, 255));
                    }
                }
                else if (counter == 87)
                {
                    Select(4);
                    Append(0x5A9880, true);
                }
                else if (counter is >= 116 and <= 119)
                {
                    advance = false;
                }
            }
            byte[] oriented = Shared().RawModel.Model.ToArray();
            Identity(oriented);
            NativeBattleCameraPose previous = Shared().CameraTask?.Process.PreviousPose ?? throw new NotSupportedException("Native Cactuar body requires the camera's actual previous pose.");
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(unchecked((short)previous.Eye.X) - unchecked((short)previous.Target.X), 0, unchecked((short)previous.Eye.Z) - unchecked((short)previous.Target.Z));
            NativeBattleActionMatrix alignment = Align(resources.AlignmentAxis.Data, direction.X, direction.Y, direction.Z);
            SetMatrix(oriented, NativeBattleGfMatrices.Multiply56C090(alignment, Diagonal(S(oriented, 0x26))));
            SetRaw(Shared().RawModel with { Model = oriented.ToImmutableArray() });
            Sync();
            frame = inputs.DrawBody(frame);
            Reload();
            if (!paused)
            {
                if (advance)
                {
                    SetRaw(NativeBattleCactuarRawModel.Advance5A8FD0(Shared().RawModel, resources.Model).State);
                }
                task = Add(task, 0xC, 1);
                Store(slot, task);
                if (Word(task, 0xC) >= 120)
                {
                    SetShared(Shared() with { TerminalFlags = Shared().TerminalFlags | 1 });
                    Append(0x5AA360, true);
                    return 2;
                }
            }
            return 0;
        }
        int Flash(int slot, NativeBattleModulePoolTask task)
        {
            int duration = Word(task, 0xE);
            int value = unchecked((Word(task, 0x18) / duration) * Word(task, 0xC));
            Emit(new NativeBattleCactuar199Flash(task.Callback == 0x5A8B40 ? value : unchecked(Word(task, 0x18) - value)));
            if (paused)
            {
                return 0;
            }
            task = Add(task, 0xC, 1);
            Store(slot, task);
            if (Word(task, 0xC) < duration)
            {
                return 0;
            }
            if (task.Callback == 0x5A8BC0)
            {
                Emit(new NativeBattleCactuar199Flash(0));
            }
            return 2;
        }
        int Mesh(int slot, NativeBattleModulePoolTask task)
        {
            Sync();
            frame = inputs.DrawMesh(frame, task);
            Reload();
            if (paused)
            {
                return 0;
            }
            int limit = 12;
            if (task.Callback == 0x5A9570)
            {
                limit = 60;
                if (Word(task, 0xC) < 16)
                {
                    task = Add(task, 0x1C, -Word(task, 0x1E));
                    task = Add(task, 0x1E, -Word(task, 0x1E) / 20);
                }
            }
            else if (task.Callback == 0x5A9730)
            {
                limit = 24;
                task = Add(task, 0x18, Word(task, 0x1A));
            }
            task = Add(task, 0xC, 1);
            Store(slot, task);
            return Word(task, 0xC) >= limit ? 2 : 0;
        }
        int Particles(int slot, NativeBattleModulePoolTask task)
        {
            byte[] bank = Arena().Read(ParticleOffset, 100 * 0x1C);
            if (!paused && Word(task, 0xC) <= 4)
            {
                for (int born = 0; born < 6; born++)
                {
                    int index = Enumerable.Range(0, 100).FirstOrDefault(index => U(bank, index * 0x1C) == 0, -1);
                    if (index < 0)
                    {
                        break;
                    }
                    int offset = index * 0x1C;
                    D(bank, offset, 1);
                    W(bank, offset + 4, 0);
                    W(bank, offset + 6, Rand() % 1536 + 2048);
                    int angle = Rand() % 4096;
                    int radius = Rand() % 200 + 300;
                    D(bank, offset + 0xC, task.ReadDword(0x10));
                    D(bank, offset + 0x10, task.ReadDword(0x14));
                    W(bank, offset + 0xC, S(bank, offset + 0xC) + (NativeBattleCameraMath.Sine(angle) * radius >> 12));
                    W(bank, offset + 0xE, S(bank, offset + 0xE) - 80 - Rand() % 80);
                    W(bank, offset + 0x10, S(bank, offset + 0x10) + (NativeBattleCameraMath.Cosine(angle) * radius >> 12));
                    int speed = Rand() % 120 + 90;
                    W(bank, offset + 0x14, NativeBattleCameraMath.Sine(angle) * speed >> 12);
                    W(bank, offset + 0x18, NativeBattleCameraMath.Cosine(angle) * speed >> 12);
                }
            }
            WriteArena(ParticleOffset, bank);
            int survivors = 0;
            for (int index = 0; index < 100; index++)
            {
                int offset = index * 0x1C;
                if ((U(bank, offset) & 1) == 0)
                {
                    continue;
                }
                WriteArena(ParticleOffset + offset, bank.AsSpan(offset, 0x1C).ToArray());
                Sync();
                NativeBattleCactuar199ParticleStep draw = inputs.DrawParticle(frame, bank.AsSpan(offset, 0x1C).ToArray().ToImmutableArray());
                frame = draw.Frame;
                Reload();
                if (!paused)
                {
                    W(bank, offset + 4, S(bank, offset + 4) + 1);
                    if (draw.FrameResult < 0)
                    {
                        D(bank, offset, 0);
                        continue;
                    }
                    W(bank, offset + 0xC, S(bank, offset + 0xC) + S(bank, offset + 0x14));
                    W(bank, offset + 0x10, S(bank, offset + 0x10) + S(bank, offset + 0x18));
                    W(bank, offset + 0x14, S(bank, offset + 0x14) - (S(bank, offset + 0x14) >> 3));
                    W(bank, offset + 0x18, S(bank, offset + 0x18) - (S(bank, offset + 0x18) >> 3));
                    survivors++;
                }
            }
            WriteArena(ParticleOffset, bank);
            if (paused)
            {
                return 0;
            }
            Store(slot, Add(task, 0xC, 1));
            return survivors == 0 ? 2 : 0;
        }
        int Setup(int slot, NativeBattleModulePoolTask task)
        {
            if (paused)
            {
                return 0;
            }
            if (Word(task, 0xC) == 0)
            {
                NativeBattleVector bone = NativeBattlePoseMatrices.ReadEffectOwnedBone(resources.Model.PoseSource, Shared().RawModel.DecodedPose ?? throw new NotSupportedException("Native Cactuar setup requires its current decoded pose."), RawMatrix(), 240, 3072);
                task = task.WriteWord(0x10, unchecked((ushort)bone.X)).WriteWord(0x12, unchecked((ushort)(bone.Y - 600))).WriteWord(0x14, unchecked((ushort)(bone.Z + 900)));
                NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(Basis(), new(Word(task, 0x10), Word(task, 0x12), Word(task, 0x14)));
                NativeBattleVector origin = new(unchecked((short)(rotated.X + Basis().X)), unchecked((short)(rotated.Y + Basis().Y)), unchecked((short)(rotated.Z + Basis().Z)));
                Cache(origin, Vector(inputs.ReadCentroid(parameters.TargetMask7A)));
            }
            if (Word(task, 0xC) == 1)
            {
                Append(0x5A9C10);
                NativeBattleActionHeaderSlot header = Header();
                int firstContact = header.FirstContactIndex ?? throw new NotSupportedException("Native Cactuar lost its first header's contact pointer.");
                for (int contact = 0; contact < header.ReadByte(16); contact++)
                {
                    int hit = Append(0x5AA2A0);
                    Store(hit, children.Slots[hit].WriteWord(0xE, (ushort)(Rand() % 8)).WriteWord(0x18, (ushort)contact).WriteWord(0x1A, inputs.Packets.ReadContact(firstContact + contact).Primary.ActorIndex));
                }
            }
            task = Add(task, 0xC, 1);
            Store(slot, task);
            return Word(task, 0xC) >= 4 ? 2 : 0;
        }
        void Cache(NativeBattleVector origin, NativeBattleVector center)
        {
            byte[] points = Arena().Read(0x1C000, 0x4000);
            int total = 0;
            NativeBattleActionHeaderSlot header = Header();
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Cactuar target cache requires first-header contacts.");
            for (int contact = 0; contact < header.ReadByte(16); contact++)
            {
                int actor = inputs.Packets.ReadContact(first + contact).Primary.ActorIndex;
                int bones = inputs.ReadPoseSource(actor).BoneCount;
                for (int bone = 0; bone < bones; bone++)
                {
                    if (total >= points.Length / 8)
                    {
                        throw new InvalidDataException("Native Cactuar target bone cache exceeds its physical arena owner.");
                    }
                    ImmutableArray<byte> value = inputs.ReadBone(actor, (byte)bone, (short)(Tape() % 4096));
                    RequireRaw8(value);
                    value.AsSpan().Slice(0, 6).CopyTo(points.AsSpan(total * 8, 6));
                    total++;
                }
            }
            if (total == 0)
            {
                throw new NotSupportedException("Native Cactuar target cache has no original target bones.");
            }
            WriteArena(0x1C000, points);
            byte[] records = Arena().Read(TargetCacheOffset, 512 * 0x30);
            for (int index = 0; index < 512; index++)
            {
                int offset = index * 0x30;
                int parity = Tape();
                if (parity % 2 != 0)
                {
                    W(records, offset + 8, Tape() % 8000 + center.X - 4000);
                    W(records, offset + 0xA, 0);
                    W(records, offset + 0xC, center.Z - Tape() % 10000 + 4000);
                }
                else
                {
                    int selected = parity % total * 8;
                    W(records, offset + 8, Tape() % 500 + S(points, selected) - 250);
                    W(records, offset + 0xA, Tape() % 500 + S(points, selected + 2) - 250);
                    W(records, offset + 0xC, Tape() % 500 + S(points, selected + 4) - 250);
                }
                int x = S(records, offset + 8) - origin.X;
                int y = S(records, offset + 0xA) - origin.Y;
                int z = S(records, offset + 0xC) - origin.Z;
                var normal = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                D(records, offset + 0x24, unchecked((uint)normal.X));
                D(records, offset + 0x28, unchecked((uint)normal.Y));
                D(records, offset + 0x2C, unchecked((uint)normal.Z));
                W(records, offset + 0xE, Tape() % 800 + 1000);
                W(records, offset, origin.X);
                W(records, offset + 2, origin.Y);
                W(records, offset + 4, origin.Z);
                W(records, offset + 6, Distance(x, y, z));
            }
            WriteArena(TargetCacheOffset, records);
        }
        int Needles(int slot, NativeBattleModulePoolTask task)
        {
            byte[] bank = Arena().Read(NeedleOffset, 800 * 0x3C);
            byte[] cache = Arena().Read(TargetCacheOffset, 512 * 0x30);
            if (!paused && Word(task, 0xC) < 45)
            {
                for (int born = 0; born < 50; born++)
                {
                    int index = Enumerable.Range(0, 800).FirstOrDefault(index => U(bank, index * 0x3C) == 0, -1);
                    if (index < 0)
                    {
                        break;
                    }
                    int offset = index * 0x3C;
                    D(bank, offset, 1);
                    W(bank, offset + 4, 0);
                    int scale = Rand() % 5120 + 2560;
                    W(bank, offset + 6, scale);
                    int selected = Rand() % 512;
                    int record = selected * 0x30;
                    D(bank, offset + 0xC, U(cache, record));
                    D(bank, offset + 0x10, U(cache, record + 4));
                    int duration = Rand() % 6 + 5;
                    int x = Rand() % 1600 + S(cache, record + 8) - S(bank, offset + 0xC) - 800;
                    int y = S(cache, record + 0xA) == 0 ? -S(bank, offset + 0xE) : Rand() % 1600 + S(cache, record + 0xA) - S(bank, offset + 0xE) - 800;
                    int z = Rand() % 1600 + S(cache, record + 0xC) - S(bank, offset + 0x10) - 800;
                    var normal = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                    int distance = Distance(x, y, z);
                    NativeBattleActionMatrix alignment = Align(resources.NeedleAlignmentAxis.Data, normal.X, normal.Y, normal.Z);
                    int speed = distance / duration;
                    W(bank, offset + 0x14, unchecked(normal.X * speed) >> 12);
                    W(bank, offset + 0x16, unchecked(normal.Y * speed) >> 12);
                    W(bank, offset + 0x18, unchecked(normal.Z * speed) >> 12);
                    W(bank, offset + 0xC, S(bank, offset + 0xC) + (unchecked(normal.X * S(cache, record + 0xE)) >> 12));
                    W(bank, offset + 0xE, S(bank, offset + 0xE) + (unchecked(normal.Y * S(cache, record + 0xE)) >> 12));
                    W(bank, offset + 0x10, S(bank, offset + 0x10) + (unchecked(normal.Z * S(cache, record + 0xE)) >> 12));
                    W(bank, offset + 0x1A, duration + 1);
                    W(bank, offset + 8, 0);
                    W(bank, offset + 0xA, selected);
                    NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Multiply56C090(alignment, new([3072, 0, 0, 0, 3072, 0, 0, 0, unchecked((short)scale)], 0, 0, 0, 4096));
                    for (int element = 0; element < 9; element++)
                    {
                        W(bank, offset + 0x1C + element * 2, matrix.Rotation[element]);
                    }
                }
            }
            WriteArena(NeedleOffset, bank);
            int admitted = 0;
            for (int index = 0; index < 800; index++)
            {
                int offset = index * 0x3C;
                if ((U(bank, offset) & 1) == 0)
                {
                    continue;
                }
                NativeBattleVector position = new(S(bank, offset + 0xC), S(bank, offset + 0xE), S(bank, offset + 0x10));
                D(bank, offset + 0x30, unchecked((uint)position.X));
                D(bank, offset + 0x34, unchecked((uint)position.Y));
                D(bank, offset + 0x38, unchecked((uint)position.Z));
                NativeBattleActionMatrix matrix = new(Enumerable.Range(0, 9).Select(element => S(bank, offset + 0x1C + element * 2)).ToImmutableArray(), 0, 0, 0, 4096);
                int age = S(bank, offset + 4);
                uint color = age < 4 ? Color(resources.EarlyColors.Data, age) : 0x2E808080;
                if (!paused)
                {
                    W(bank, offset + 4, age + 1);
                    if (S(bank, offset + 4) >= S(bank, offset + 0x1A))
                    {
                        int fade = S(bank, offset + 8);
                        color = Color(resources.LateColors.Data, fade);
                        W(bank, offset + 8, fade + 1);
                        if (S(bank, offset + 8) >= 6)
                        {
                            D(bank, offset, 0);
                        }
                    }
                    else
                    {
                        W(bank, offset + 0xC, position.X + S(bank, offset + 0x14));
                        W(bank, offset + 0xE, position.Y + S(bank, offset + 0x16));
                        W(bank, offset + 0x10, position.Z + S(bank, offset + 0x18));
                    }
                }
                WriteArena(NeedleOffset + offset, bank.AsSpan(offset, 0x3C).ToArray());
                Sync();
                NativeBattleCactuar199NeedleStep draw = inputs.DrawNeedle(frame, matrix, position, color);
                frame = draw.Frame;
                Reload();
                if (draw.BAccepted)
                {
                    admitted++;
                }
            }
            WriteArena(NeedleOffset, bank);
            if (paused)
            {
                return 0;
            }
            Store(slot, Add(task, 0xC, 1));
            return admitted == 0 ? 2 : 0;
        }
        int Hit(int slot, NativeBattleModulePoolTask task)
        {
            if (Word(task, 0xE) > 0)
            {
                if (!paused)
                {
                    Store(slot, Add(task, 0xE, -1));
                }
                return 0;
            }
            if (paused)
            {
                return 0;
            }
            int counter = Word(task, 0xC);
            if (counter < 50 && counter % 3 == 1)
            {
                if (counter + 3 >= 50)
                {
                    int first = Header().FirstContactIndex ?? throw new NotSupportedException("Native Cactuar hit requires its first header's contact pointer.");
                    Emit(new NativeBattleCactuar199Contact(first + task.ReadWord(0x18)));
                }
                else
                {
                    Emit(new NativeBattleCactuar199HitReaction(task.ReadWord(0x1A), 4));
                }
            }
            task = Add(task, 0xC, 1);
            Store(slot, task);
            return Word(task, 0xC) >= 55 ? 2 : 0;
        }
        int Final(int slot, NativeBattleModulePoolTask task)
        {
            if (Word(task, 0xC) == 1)
            {
                Emit(new NativeBattleCactuar199CommonTextureLoad(0x4000, ChildBank + (uint)slot * 0x24 + 0xE));
                task = children.Slots[slot];
            }
            ushort completion = task.ReadWord(0xE);
            Store(slot, Add(task, 0xC, 1));
            return completion != 0 ? 2 : 0;
        }

        int Append(uint callback, bool clearExtra = false)
        {
            (children, int slot) = children.Append(callback);
            NativeBattleModulePoolTask task = children.Slots[slot].WriteWord(0xC, 0);
            if (clearExtra)
            {
                task = task.WriteWord(0xE, 0);
            }
            Store(slot, task);
            return slot;
        }
        void Store(int slot, NativeBattleModulePoolTask task) => children = Put(children, slot, task);
        void Sync() => frame = frame with { Effect = frame.Effect with { Pools = [main, children] } };
        void Reload()
        {
            main = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == MainDescriptor);
            children = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ChildDescriptor);
        }
        void Emit(NativeBattleCactuar199Operation operation)
        {
            Sync();
            frame = inputs.Execute(frame, operation);
            Reload();
        }
        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
        int Tape()
        {
            uint counter = Shared().LookupCounter ?? throw new NotSupportedException("Native Cactuar lookup tape counter was not seeded.");
            int value = BinaryPrimitives.ReadInt32LittleEndian(resources.RandomTape.Data.AsSpan().Slice((int)(counter & 255) * 4, 4));
            if (value < 0)
            {
                throw new NotSupportedException("Native Cactuar requires its original nonnegative lookup tape words.");
            }
            SetShared(Shared() with { LookupCounter = unchecked(counter + 1) });
            return value;
        }
        NativeBattleActionHeaderSlot Header() => inputs.Packets.Headers[parameters.HeaderIndex7C];
        NativeBattleCactuar199State Shared() => frame.Memory.Cactuar199 ?? throw new NotSupportedException("Native Cactuar retained state is missing.");
        void SetShared(NativeBattleCactuar199State state) => frame = frame with { Memory = frame.Memory with { Cactuar199 = state } };
        void SetRaw(NativeBattleCactuarRawModelState model) => SetShared(Shared() with { RawModel = model });
        void Select(short pose) => SetRaw(NativeBattleCactuarRawModel.Select5A8FB0(Shared().RawModel, resources.Model, pose));
        NativeBattleEffectArena Arena() => frame.Memory.Arena ?? throw new NotSupportedException("Native Cactuar arena is missing.");
        void WriteArena(int offset, byte[] data) => frame = frame with { Memory = frame.Memory with { Arena = Arena().Write(offset, data) } };
        NativeBattleActionMatrix Basis() => Shared().BaseMatrix ?? throw new NotSupportedException("Native Cactuar base matrix is missing.");
        void MoveY(int velocity)
        {
            byte[] model = Shared().RawModel.Model.ToArray();
            D(model, 0x58, unchecked(U(model, 0x58) + (uint)velocity));
            SetRaw(Shared().RawModel with { Model = model.ToImmutableArray() });
        }
        NativeBattleActionMatrix RawMatrix()
        {
            ImmutableArray<byte> model = Shared().RawModel.Model;
            return new(Enumerable.Range(0, 9).Select(index => S(model.AsSpan(), 0x40 + index * 2)).ToImmutableArray(), unchecked((int)U(model.AsSpan(), 0x54)), unchecked((int)U(model.AsSpan(), 0x58)), unchecked((int)U(model.AsSpan(), 0x5C)), 4096);
        }
        NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, int dy, int dz)
        {
            ImmutableArray<byte> model = Shared().RawModel.Model;
            int y = dy == 0 ? 0 : unchecked((int)U(model.AsSpan(), 0x58) + dy);
            return task.WriteWord(0x10, unchecked((ushort)U(model.AsSpan(), 0x54))).WriteWord(0x12, unchecked((ushort)y)).WriteWord(0x14, unchecked((ushort)(U(model.AsSpan(), 0x5C) + dz)));
        }
    }

    private static NativeBattleEffectTaskPool Put(NativeBattleEffectTaskPool pool, int slot, NativeBattleModulePoolTask task) => pool.WriteSlot(slot, task);
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(task.ReadWord(offset) + value)));
    private static short S(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset, 2));
    private static uint U(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
    private static void W(Span<byte> data, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(data.Slice(offset, 2), unchecked((ushort)value));
    private static void D(Span<byte> data, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.Slice(offset, 4), value);
    private static void RequireRaw8(ImmutableArray<byte> value)
    {
        if (value.IsDefault || value.Length != 8)
        {
            throw new InvalidDataException("Native Cactuar requires its explicit eight-byte point owner.");
        }
    }
    private static NativeBattleVector Vector(ImmutableArray<byte> value)
    {
        RequireRaw8(value);
        return new(S(value.AsSpan(), 0), S(value.AsSpan(), 2), S(value.AsSpan(), 4));
    }
    private static NativeBattleActionMatrix Diagonal(short scale) => new([scale, 0, 0, 0, scale, 0, 0, 0, scale], 0, 0, 0, 4096);
    private static void Identity(Span<byte> model)
    {
        for (int offset = 0; offset < 16; offset += 4)
        {
            D(model, 0x40 + offset, 0);
        }
        W(model, 0x40, 4096);
        W(model, 0x48, 4096);
        W(model, 0x50, 4096);
    }
    private static void SetMatrix(Span<byte> model, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            W(model, 0x40 + index * 2, matrix.Rotation[index]);
        }
    }
    private static NativeBattleActionMatrix Align(ImmutableArray<byte> rawAxis, int x, int y, int z)
    {
        if (rawAxis.IsDefault || rawAxis.Length != 16)
        {
            throw new InvalidDataException("Native Cactuar alignment requires its full original VECTOR.");
        }
        NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(unchecked((int)U(rawAxis.AsSpan(), 0)), unchecked((int)U(rawAxis.AsSpan(), 4)), unchecked((int)U(rawAxis.AsSpan(), 8)), x, y, z);
        return new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), 0, 0, 0, 4096);
    }
    private static int Distance(int x, int y, int z)
    {
        int square = NativeBattleActionGeometryRules.SquaredMagnitude56BC50(x, y, z);
        if (square < 0)
        {
            throw new NotSupportedException("Native Cactuar distance reaches an unsupported exceptional negative square-root conversion.");
        }
        return unchecked((int)(long)Math.Sqrt(square));
    }
    private static uint Color(ImmutableArray<byte> table, int index)
    {
        if (table.IsDefault || index < 0 || index >= table.Length / 4)
        {
            throw new NotSupportedException("Native Cactuar color index leaves its supplied original table.");
        }
        return U(table.AsSpan(), index * 4);
    }
}
