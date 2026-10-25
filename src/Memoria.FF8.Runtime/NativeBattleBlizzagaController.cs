using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBlizzagaFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleBlizzagaOperation;
public sealed record NativeBattleBlizzagaMainDraw(NativeBattleModulePoolTask Task, int Sequence, int Phase) : NativeBattleBlizzagaOperation;
public sealed record NativeBattleBlizzagaAuxiliaryDraw(NativeBattleModulePoolTask Task, int Phase, bool First) : NativeBattleBlizzagaOperation;
public sealed record NativeBattleBlizzagaSpark(NativeBattleModulePoolTask Task, NativeBattleActionMatrix Matrix) : NativeBattleBlizzagaOperation;
public sealed record NativeBattleBlizzagaSprite(NativeBattleModulePoolTask Task) : NativeBattleBlizzagaOperation;
public sealed record NativeBattleBlizzagaContact(int Contact) : NativeBattleBlizzagaOperation;
public sealed record NativeBattleBlizzagaSound(NativeBattleModuleAnchor Position) : NativeBattleBlizzagaOperation;
public sealed record NativeBattleBlizzagaStage(short Value) : NativeBattleBlizzagaOperation;
public sealed record NativeBattleBlizzagaModelBandRequest(int Actor, int Phase, short LowY, short HighY, bool Enabled, bool BandEnabled);
public sealed record NativeBattleBlizzagaModelBandStep(NativeBattleBlizzagaFrame Frame, short Minimum, short Maximum);
public sealed record NativeBattleBlizzagaRibbonStep(NativeBattleBlizzagaFrame Frame, NativeBattleModulePoolTask Task);
public sealed record NativeBattleBlizzagaInputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, int SourceActor, NativeBattleActionMatrix View, Func<int, NativeBattleVector> ReadBone241, Func<int, NativeBattleModelTransform> ReadTransform, Func<int, NativeBattleModuleAnchor> ReadSoundPosition, Func<NativeBattleBlizzagaFrame, NativeBattleBlizzagaModelBandRequest, NativeBattleBlizzagaModelBandStep> ModelBand, Func<NativeBattleBlizzagaFrame, NativeBattleModulePoolTask, bool, sbyte, NativeBattleActionMatrix, NativeBattleBlizzagaRibbonStep> ProcessRibbon, Func<NativeBattleBlizzagaFrame, NativeBattleBlizzagaOperation, NativeBattleBlizzagaFrame> Execute);

/// <summary>Blizzaga's six ordered task banks, capacity-sensitive particle births and retained ribbons.</summary>
public static class NativeBattleBlizzagaController
{
    public const uint MainDescriptor = 0x2543E20;
    public const uint MainBank = 0x2543E08;
    public const uint PrimaryDescriptor = 0x2543E30;
    public const uint SecondDescriptor = 0x2543E40;
    public const uint ThirdDescriptor = 0x2543E50;
    public const uint FourthDescriptor = 0x2543E60;
    public const uint FifthDescriptor = 0x2543E70;
    public const uint SixthDescriptor = 0x2543E80;
    public const uint Arena = NativeBattleEffectNodeMemory.ArenaAddress;
    public const uint SecondBank = Arena + 0xC850;
    private const uint ActorBank = 0x1D972C0;

    public static NativeBattleOriginalEffectState Initialize6D6320(NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets)
    {
        if (effect.Binding.Entry != 0x6D6310 || effect.Parameters is null || effect.Binding.BlizzagaResources is null)
        {
            throw new InvalidDataException("Native Blizzaga requires its original6D6310 entry, action parameters and resources.");
        }
        int first = effect.Parameters.HeaderIndex7C;
        int last = packets.Headers[first].ReadByte(17);
        if (last >= 3 || first + last >= NativeBattleActionPacketsState.HeaderCapacity)
        {
            throw new NotSupportedException("Native Blizzaga owns three header-indexed geometry clones.");
        }
        _ = NativeBattleBlizzagaFaces.Extents(effect.Binding.BlizzagaResources);
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 20, 1);
        (main, int root) = main.Append(0x6D6400);
        main = Put(main, root, Clear(main.Slots[root], 8));
        return effect with { Pools = [main], ReturnedDescriptor = MainDescriptor };
    }

    public static NativeBattleBlizzagaFrame Advance6D6400(NativeBattleBlizzagaFrame frame, NativeBattleBlizzagaInputs inputs)
    {
        frame.Effect.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Blizzaga lost its action parameters.");
        NativeBattleBlizzagaResources resources = frame.Effect.Binding.BlizzagaResources ?? throw new NotSupportedException("Native Blizzaga requires its original resources.");
        (short minimum, short span) = NativeBattleBlizzagaFaces.Extents(resources);
        NativeBattleEffectTaskPool main = Pool(MainDescriptor)!;
        NativeBattleEffectTaskPool? primary = Pool(PrimaryDescriptor);
        NativeBattleEffectTaskPool? second = Pool(SecondDescriptor);
        NativeBattleEffectTaskPool? third = Pool(ThirdDescriptor);
        NativeBattleEffectTaskPool? fourth = Pool(FourthDescriptor);
        NativeBattleEffectTaskPool? fifth = Pool(FifthDescriptor);
        NativeBattleEffectTaskPool? sixth = Pool(SixthDescriptor);
        short stage = 0;
        NativeBattleEffectTaskStep rootStep = main.Advance((pool, slot) =>
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            Require(task, 0x6D6400);
            bool alternate = Dword(task, 16) == 0;
            frame = frame with { Effect = frame.Effect with { DrawArenaOffset = alternate ? 0x28928 : 0x18928 } };
            task = WriteDword(task, 16, alternate ? 1u : 0u);
            if (Word(task, 12) == 1 && task.ReadByte(15) == 0)
            {
                primary = NativeBattleEffectTaskPool.Initialize(PrimaryDescriptor, Arena, 0x6428, 2);
                second = NativeBattleEffectTaskPool.Initialize(SecondDescriptor, SecondBank, 0x478, 6);
                third = NativeBattleEffectTaskPool.Initialize(ThirdDescriptor, Arena + 0xE320, 0x464, 4);
                fourth = NativeBattleEffectTaskPool.Initialize(FourthDescriptor, Arena + 0xF4B0, 0x2C, 96);
                fifth = NativeBattleEffectTaskPool.Initialize(FifthDescriptor, Arena + 0x10530, 0x28, 64);
                sixth = NativeBattleEffectTaskPool.Initialize(SixthDescriptor, Arena + 0x10F30, 0x2C, 96);
                task = WriteByte(task, 15, 1);
            }
            bool completionAllowed = true;
            int sequence = task.ReadByte(14);
            if (Word(task, 12) == 1 && sequence <= Header(0).ReadByte(17))
            {
                int contact = Header(sequence).FirstContactIndex ?? throw new NotSupportedException("Native Blizzaga requires its header contact owner.");
                int actor = inputs.Packets.ReadContact(contact).Primary.ActorIndex;
                uint row = ActorAddress(actor);
                if (primary!.Slots.Any(current => (current.Flags & 1) != 0 && Dword(current, 24) == row))
                {
                    task = task.WriteWord(12, 0);
                    completionAllowed = false;
                }
                else
                {
                    (primary, int child) = primary.Append(0x6D67E0);
                    NativeBattleVector target = inputs.ReadBone241(actor);
                    NativeBattleVector source = inputs.ReadBone241(inputs.SourceActor);
                    NativeBattleModulePoolTask next = Clear(primary.Slots[child], 0x641C).WriteWord(14, (ushort)sequence).WriteWord(16, unchecked((ushort)NativeWorldScriptedPathMath.NativeAngle(target.X - source.X, target.Z - source.Z))).WriteWord(18, unchecked((ushort)-32767)).WriteWord(20, unchecked((ushort)-32767));
                    next = WriteDword(next, 24, row);
                    next = WriteDword(next, 28, Arena + (uint)NativeBattleBlizzagaFaces.MeshOffset(sequence));
                    next = Position(next, 32, target).WriteWord(34, unchecked((ushort)inputs.ReadTransform(actor).Angle36));
                    NativeBattleBlizzagaFaceInitialization initialized = NativeBattleBlizzagaFaces.Initialize(frame.Memory, next, sequence, resources);
                    frame = frame with { Memory = initialized.Memory };
                    primary = Put(primary, child, initialized.Task);
                    task = WriteByte(task, 14, (byte)(sequence + 1));
                }
            }
            int survivors = 1;
            if (task.ReadByte(15) != 0)
            {
                NativeBattleEffectTaskStep a = primary!.Advance(Primary);
                primary = a.Pool;
                NativeBattleEffectTaskStep b = second!.Advance(Second);
                second = b.Pool;
                NativeBattleEffectTaskStep c = third!.Advance(Third);
                third = c.Pool;
                NativeBattleEffectTaskStep d = fourth!.Advance(Fourth);
                fourth = d.Pool;
                NativeBattleEffectTaskStep e = fifth!.Advance(Fifth);
                fifth = e.Pool;
                NativeBattleEffectTaskStep f = sixth!.Advance(Sixth);
                sixth = f.Pool;
                survivors = a.Survivors + b.Survivors + c.Survivors + d.Survivors + e.Survivors + f.Survivors;
            }
            Emit(new NativeBattleBlizzagaStage(stage));
            if (completionAllowed && task.ReadByte(15) != 0 && survivors == 0)
            {
                return new(Put(pool, slot, task), 2);
            }
            task = Add(task, 12, 1);
            if (Word(task, 12) >= 50)
            {
                task = task.WriteWord(12, 0);
            }
            return new(Put(pool, slot, task), 0);
        });
        ImmutableArray<NativeBattleEffectTaskPool> pools = primary is null ? [rootStep.Pool] : [rootStep.Pool, primary, second!, third!, fourth!, fifth!, sixth!];
        return frame with { Effect = frame.Effect with { Pools = pools, ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : MainDescriptor } };

        NativeBattleEffectTaskCallback Primary(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            Require(task, 0x6D67E0);
            int phase = Word(task, 12);
            int sequence = task.ReadWord(14);
            int actor = ActorIndex(Dword(task, 24));
            stage = (short)Math.Max((int)stage, phase < 4 ? phase * 256 : phase <= 94 ? 1024 : (98 - phase) * 256);
            int t = phase - 8;
            bool enabled = (uint)t < 61;
            short low = Word(task, 18);
            short high = Word(task, 20);
            if (enabled && t < 18)
            {
                low = unchecked((short)(high + (low - high) * t / 18));
            }
            else if (enabled && t >= 52)
            {
                low = unchecked((short)(high + (low - high) * (61 - t) / 9));
            }
            NativeBattleBlizzagaModelBandStep band = inputs.ModelBand(frame, new(actor, phase, low, high, enabled, enabled && (t is >= 18 and <= 51 || (t & 1) == 0)));
            frame = band.Frame;
            if (enabled)
            {
                task = task.WriteWord(18, unchecked((ushort)band.Minimum)).WriteWord(20, unchecked((ushort)band.Maximum));
            }
            if (phase is >= 0 and <= 2)
            {
                (second, int child) = second!.Append(0x6D97A0);
                NativeBattleModulePoolTask next = Clear(second.Slots[child], 0x46C).WriteWord(16, (ushort)((phase << 12) / 3)).WriteWord(18, 256).WriteWord(20, unchecked((ushort)-10)).WriteWord(22, 2048);
                second = Put(second, child, WriteDword(next, 28, Dword(task, 24)));
            }
            if (phase is 0 or 1)
            {
                (third, int child) = third!.Append(0x6DA0D0);
                NativeBattleModulePoolTask next = Clear(third.Slots[child], 0x458).WriteWord(16, (ushort)(phase << 11)).WriteWord(20, (ushort)(phase << 11)).WriteWord(24, 4096).WriteWord(18, unchecked((ushort)(phase != 0 ? -192 : 192))).WriteWord(22, unchecked((ushort)(phase != 0 ? -20 : 20)));
                next = WriteDword(WriteDword(next, 36, Arena + (uint)slot * 0x6428), 40, Dword(task, 24));
                third = Put(third, child, next);
            }
            if (phase is >= 28 and <= 97)
            {
                NativeBattleBlizzagaFaceStep step = NativeBattleBlizzagaFaces.Step(frame.Memory, task, sequence, phase, minimum, span, frame.Random, resources);
                frame = frame with { Memory = step.Memory, Random = step.Random };
                task = step.Task;
                Emit(new NativeBattleBlizzagaMainDraw(task, sequence, phase));
            }
            if (phase is >= 46 and <= 70)
            {
                Emit(new NativeBattleBlizzagaAuxiliaryDraw(task, phase, true));
            }
            if (phase is >= 31 and <= 40)
            {
                Emit(new NativeBattleBlizzagaAuxiliaryDraw(task, phase, false));
            }
            if (phase is >= 28 and <= 43)
            {
                SpawnSixth(task, slot);
            }
            if (phase is >= 38 and <= 68)
            {
                SpawnFifth(task);
            }
            if (phase == 0)
            {
                Emit(new NativeBattleBlizzagaSound(inputs.ReadSoundPosition(actor)));
            }
            if (phase == 68)
            {
                Emit(new NativeBattleBlizzagaContact(Header(sequence).FirstContactIndex ?? throw new NotSupportedException("Native Blizzaga lost its contact owner.")));
            }
            return new(Put(pool, slot, phase >= 98 ? task : Add(task, 12, 1)), phase >= 98 ? 2 : 0);
        }

        void SpawnSixth(NativeBattleModulePoolTask parent, int parentSlot)
        {
            int seed = Rand();
            for (int attempt = 0; attempt < 8; attempt++)
            {
                if (Full(sixth!))
                {
                    continue;
                }
                (sixth, int child) = sixth!.Append(0x6D96A0);
                int a = Rand();
                int b = Rand();
                int angle = seed + ((a + b - 32768) >> 8);
                seed += 512;
                NativeBattleModulePoolTask next = Clear(sixth.Slots[child], 32);
                next = WriteDword(next, 16, Arena + (uint)parentSlot * 0x6428);
                next = next.WriteWord(20, unchecked((ushort)(NativeBattleCameraMath.Cosine(angle) / 3))).WriteWord(24, unchecked((ushort)(NativeBattleCameraMath.Sine(angle) / 3))).WriteWord(26, (ushort)(6000 - (Rand() >> 4)));
                var velocity = NativeBattleActionGeometryRules.NormalizeVector56BC50(Word(next, 20), 0, Word(next, 24));
                int vx = unchecked((short)velocity.X) / 48;
                int vz = unchecked((short)velocity.Z) / 48;
                next = next.WriteWord(28, unchecked((ushort)vx)).WriteWord(32, unchecked((ushort)vz)).WriteWord(36, unchecked((ushort)(-vx / 14))).WriteWord(40, unchecked((ushort)(-vz / 14)));
                next = Add(Add(next, 20, Word(parent, 32)), 24, Word(parent, 36)).WriteWord(22, parent.ReadWord(34));
                sixth = Put(sixth, child, next);
            }
        }

        void SpawnFifth(NativeBattleModulePoolTask parent)
        {
            int seed = Rand();
            for (int attempt = 0; attempt < 8; attempt++)
            {
                if (Full(fifth!))
                {
                    continue;
                }
                (fifth, int child) = fifth!.Append(0x6D95E0);
                int a = Rand();
                int b = Rand();
                int angle = seed + ((a + b - 32768) >> 8);
                seed += 512;
                NativeBattleModulePoolTask next = Clear(fifth.Slots[child], 28).WriteWord(16, unchecked((ushort)(NativeBattleCameraMath.Cosine(angle) >> 1))).WriteWord(20, unchecked((ushort)(NativeBattleCameraMath.Sine(angle) >> 1))).WriteWord(22, (ushort)(4096 + (Rand() >> 4)));
                var velocity = NativeBattleActionGeometryRules.NormalizeVector56BC50(Word(next, 16), 0, Word(next, 20));
                next = next.WriteWord(24, unchecked((ushort)((short)velocity.X >> 5))).WriteWord(26, unchecked((ushort)-100)).WriteWord(28, unchecked((ushort)((short)velocity.Z >> 5)));
                next = Add(Add(next, 16, Word(parent, 32)), 20, Word(parent, 36)).WriteWord(18, unchecked((ushort)(Word(parent, 34) - (Rand() & 511) - 512)));
                next = next.WriteWord(32, unchecked((ushort)(-NativeBattleCameraMath.Sine(Word(parent, 16)) >> 8))).WriteWord(34, 10).WriteWord(36, unchecked((ushort)(NativeBattleCameraMath.Cosine(Word(parent, 16)) >> 8)));
                fifth = Put(fifth, child, next);
            }
        }

        NativeBattleEffectTaskCallback Second(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            Require(task, 0x6D97A0);
            int phase = Word(task, 12);
            int actor = ActorIndex(Dword(task, 28));
            NativeBattleVector target = inputs.ReadBone241(actor) with { Y = inputs.ReadTransform(actor).Angle36 };
            var camera = Mac(inputs.View, target);
            task = WriteDword(WriteDword(WriteDword(task, 52, unchecked((uint)camera.X)), 56, unchecked((uint)camera.Y)), 60, unchecked((uint)camera.Z));
            if (phase <= 20)
            {
                int remaining = 4096 - (phase << 12) / 20;
                int height = unchecked(camera.Z * remaining) >> 12;
                int radius = remaining + (unchecked(NativeBattleCameraMath.Sine(remaining >> 1) * Word(task, 22)) >> 12);
                // Generated scratch points canonicalize the unconsumed fourth WORD; no reached arithmetic reads it.
                ImmutableArray<byte> first = NativeBattleBlizzagaRibbon.Point6D9CF0(ImmutableArray.CreateRange(new byte[8]), Word(task, 16), height, radius);
                ImmutableArray<byte> last = NativeBattleBlizzagaRibbon.Point6D9CF0(ImmutableArray.CreateRange(new byte[8]), Word(task, 16), height, unchecked(radius * 4));
                task = Add(task, 16, Word(task, 18));
                task = Add(task, 18, Word(task, 20));
                task = Insert(task, 24, 64, first, last, 96, (byte)((3 - (phase & 3)) * 32));
                if (Dword(task, 24) > 4)
                {
                    for (int point = 0; point < 2; point++)
                    {
                        byte[] local = NativeBattleTelemetry.Materialize(1166, Slice(task, 0xAC + point * 8, 8));
                        short x = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(local) + ((Rand() - 16384) >> 6)));
                        short y = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(local.AsSpan(2)) + ((Rand() - 16384) >> 6)));
                        BinaryPrimitives.WriteInt16LittleEndian(local, x);
                        BinaryPrimitives.WriteInt16LittleEndian(local.AsSpan(2), y);
                        SpawnSpark(local, SecondBank + (uint)slot * 0x478 + 32);
                    }
                }
            }
            NativeBattleActionMatrix matrix = Basis(camera.X, camera.Y, camera.Z);
            task = WriteMatrix(task, 32, matrix);
            NativeBattleBlizzagaRibbonStep step = inputs.ProcessRibbon(frame, task, true, -8, matrix);
            frame = step.Frame;
            task = Add(step.Task, 12, 1);
            return new(Put(pool, slot, task), Word(task, 12) > 8 && Dword(task, 24) < 2 ? 2 : 0);
        }

        NativeBattleEffectTaskCallback Third(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            Require(task, 0x6DA0D0);
            int phase = Word(task, 12);
            if (phase <= 20)
            {
                int t = (phase << 12) / 20;
                int x = NativeBattleCameraMath.Sine(Word(task, 16)) >> 1;
                int y = NativeBattleCameraMath.Cosine(Word(task, 16)) >> 1;
                int z = -t >> 2;
                int edge = phase < 8 ? phase : 20 - phase;
                int secondZ = phase is < 8 or > 12 ? z - (300 * edge >> 3) - 18 : z - 300;
                ImmutableArray<byte> first = Point(ImmutableArray.CreateRange(new byte[8]), x, y, z);
                ImmutableArray<byte> last = Point(ImmutableArray.CreateRange(new byte[8]), x, y, secondZ);
                task = Add(task, 16, Word(task, 18));
                task = Insert(task, 32, 44, first, last, 88, (byte)((3 - (phase & 3)) * 32));
            }
            int actor = ActorIndex(Dword(task, 40));
            NativeBattleVector position = inputs.ReadBone241(actor) with { Y = inputs.ReadTransform(actor).Angle36 };
            NativeBattleActionMatrix local = ScaledYaw(Word(task, 20), Word(task, 24), unchecked((short)(3072 + (Word(task, 24) >> 2)))) with { X = position.X, Y = position.Y, Z = position.Z };
            NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.ComposeStageCamera([local], inputs.View, 0)[0];
            NativeBattleBlizzagaRibbonStep step = inputs.ProcessRibbon(frame, task, false, -4, matrix);
            frame = step.Frame;
            task = Add(step.Task, 20, Word(step.Task, 22));
            NativeBattleModulePoolTask parent = primary!.Slots[PrimarySlot(Dword(task, 36))];
            if (Word(parent, 12) >= 28)
            {
                if (Word(parent, 12) == 28)
                {
                    task = task.WriteWord(24, 4096).WriteWord(26, 500).WriteWord(28, unchecked((ushort)-32));
                }
                task = Add(task, 24, Word(task, 26));
                task = Add(task, 26, Word(task, 28));
                task = task.WriteWord(26, unchecked((ushort)Math.Max(64, (int)Word(task, 26))));
            }
            task = Add(task, 12, 1);
            return new(Put(pool, slot, task), Word(task, 12) > 8 && Dword(task, 32) < 2 ? 2 : 0);
        }

        void SpawnSpark(byte[] point, uint matrixPointer)
        {
            if (Full(fourth!))
            {
                return;
            }
            (fourth, int child) = fourth!.Append(0x6D9DD0);
            NativeBattleModulePoolTask task = Clear(fourth.Slots[child], 32);
            for (int axis = 0; axis < 3; axis++)
            {
                short value = BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(axis * 2));
                task = task.WriteWord(16 + axis * 2, unchecked((ushort)value)).WriteWord(24 + axis * 2, unchecked((ushort)(value >> 4))).WriteWord(32 + axis * 2, unchecked((ushort)(value >> 8)));
            }
            fourth = Put(fourth, child, WriteDword(task, 40, matrixPointer));
        }

        NativeBattleEffectTaskCallback Fourth(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            Require(task, 0x6D9DD0);
            NativeBattleActionMatrix parent = ReadSecondMatrix(second!, Dword(task, 40));
            var position = Mac(parent, Vector(task, 16));
            Emit(new NativeBattleBlizzagaSpark(task, new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], position.X, position.Y, position.Z, 4096)));
            for (int axis = 0; axis < 3; axis++)
            {
                task = Add(task, 24 + axis * 2, Word(task, 32 + axis * 2));
                task = Add(task, 16 + axis * 2, Word(task, 24 + axis * 2) >> 4);
            }
            task = Add(task, 12, 1);
            return new(Put(pool, slot, task), Word(task, 12) >= 10 ? 2 : 0);
        }

        NativeBattleEffectTaskCallback Fifth(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            Require(task, 0x6D95E0);
            Emit(new NativeBattleBlizzagaSprite(task));
            task = Add(task, 12, 1);
            if (Word(task, 12) >= 10)
            {
                return new(Put(pool, slot, task), 2);
            }
            for (int axis = 0; axis < 3; axis++)
            {
                task = Add(task, 24 + axis * 2, Word(task, 32 + axis * 2));
                task = Add(task, 16 + axis * 2, Word(task, 24 + axis * 2));
            }
            return new(Put(pool, slot, task), 0);
        }

        NativeBattleEffectTaskCallback Sixth(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            Require(task, 0x6D96A0);
            Emit(new NativeBattleBlizzagaSprite(task));
            task = Add(task, 12, 1);
            if (Word(task, 12) >= 14)
            {
                return new(Put(pool, slot, task), 2);
            }
            NativeBattleModulePoolTask parent = primary!.Slots[PrimarySlot(Dword(task, 16))];
            if (task.ReadWord(42) == 0 && Word(parent, 12) >= 38)
            {
                task = task.WriteWord(42, 1).WriteWord(36, unchecked((ushort)(-NativeBattleCameraMath.Sine(Word(parent, 16)) >> 9))).WriteWord(38, unchecked((ushort)-10)).WriteWord(40, unchecked((ushort)(NativeBattleCameraMath.Cosine(Word(parent, 16)) >> 9)));
            }
            for (int axis = 0; axis < 3; axis++)
            {
                task = Add(task, 28 + axis * 2, Word(task, 36 + axis * 2));
                task = Add(task, 20 + axis * 2, Word(task, 28 + axis * 2));
            }
            return new(Put(pool, slot, task), 0);
        }

        NativeBattleEffectTaskPool? Pool(uint descriptor) => frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == descriptor);
        NativeBattleActionHeaderSlot Header(int sequence) => inputs.Packets.Headers[parameters.HeaderIndex7C + sequence];
        int Rand()
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            return value;
        }
        void Emit(NativeBattleBlizzagaOperation operation) => frame = inputs.Execute(frame, operation);
    }

    public static int PrimarySlot(uint pointer) => Slot(pointer, Arena, 0x6428, 2);
    public static int ActorIndex(uint pointer) => Slot(pointer, ActorBank, 156, 7);
    public static NativeBattleActionMatrix ReadSecondMatrix(NativeBattleEffectTaskPool pool, uint pointer)
    {
        int slot = Slot(pointer, SecondBank + 32, 0x478, 6);
        return ReadMatrix(pool.Slots[slot], 32);
    }
    private static int Slot(uint pointer, uint bank, uint stride, uint capacity)
    {
        uint delta = unchecked(pointer - bank);
        if (delta % stride != 0 || delta / stride >= capacity)
        {
            throw new NotSupportedException("Native Blizzaga pointer has no exact physical task or actor owner.");
        }
        return (int)(delta / stride);
    }
    private static uint ActorAddress(int actor) => actor is >= 0 and < 7 ? ActorBank + (uint)actor * 156 : throw new NotSupportedException("Native Blizzaga requires an actual actor graphics row.");
    private static bool Full(NativeBattleEffectTaskPool pool) => pool.Slots.All(task => (task.Flags & 1) != 0);
    private static (int X, int Y, int Z) Mac(NativeBattleActionMatrix matrix, NativeBattleVector vector)
    {
        var rotated = NativeBattleGfMatrices.Rotate56C3D0(matrix, vector);
        return (unchecked(rotated.X + matrix.X), unchecked(rotated.Y + matrix.Y), unchecked(rotated.Z + matrix.Z));
    }
    public static NativeBattleActionMatrix ScaledYaw(short angle, short horizontal, short vertical)
    {
        short cosine = unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * horizontal) >> 12));
        short sine = unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * horizontal) >> 12));
        return new([cosine, 0, sine, 0, vertical, 0, unchecked((short)-sine), 0, cosine], 0, 0, 0, 4096);
    }
    private static NativeBattleActionMatrix Basis(int cameraX, int cameraY, int cameraZ)
    {
        int x = unchecked(-cameraX);
        int y = unchecked(-cameraY);
        int z = cameraZ;
        while (Math.Abs((long)x) > 10000 || Math.Abs((long)y) > 10000 || Math.Abs((long)z) > 10000)
        {
            x = x / 2 + (x & 1);
            y = y / 2 + (y & 1);
            z = z / 2 + (z & 1);
        }
        var forward = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        var right = NativeBattleActionGeometryRules.NormalizeVector56BC50(forward.Z, 0, -forward.X);
        var up = NativeBattleActionGeometryRules.NormalizeVector56BC50(unchecked(forward.Y * right.Z - forward.Z * right.Y) >> 12, unchecked(forward.Z * right.X - forward.X * right.Z) >> 12, unchecked(forward.X * right.Y - forward.Y * right.X) >> 12);
        return new([unchecked((short)right.X), unchecked((short)right.Y), unchecked((short)right.Z), unchecked((short)up.X), unchecked((short)up.Y), unchecked((short)up.Z), unchecked((short)forward.X), unchecked((short)forward.Y), unchecked((short)forward.Z)], cameraX, cameraY, cameraZ, 4096);
    }
    private static NativeBattleModulePoolTask Insert(NativeBattleModulePoolTask task, int countOffset, int nodeOffset, ImmutableArray<byte> first, ImmutableArray<byte> last, sbyte velocity, byte u)
    {
        NativeBattleBlizzagaRibbonNodes inserted = NativeBattleBlizzagaRibbon.Insert6D99D0(checked((int)Dword(task, countOffset)), Slice(task, nodeOffset, 30 * 36), first, last, 16, velocity, u);
        return WriteDword(WriteBytes(task, nodeOffset, inserted.Nodes), countOffset, (uint)inserted.Count);
    }
    private static ImmutableArray<byte> Point(ImmutableArray<byte> source, int x, int y, int z)
    {
        byte[] bytes = NativeBattleTelemetry.Materialize(1167, source);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)x));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), unchecked((ushort)y));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), unchecked((ushort)z));
        return NativeBattleTelemetry.Immutable(1172, bytes);
    }
    private static NativeBattleActionMatrix ReadMatrix(NativeBattleModulePoolTask task, int offset) => new(Enumerable.Range(0, 9).Select(index => Word(task, offset + index * 2)).ToImmutableArray(), unchecked((int)Dword(task, offset + 20)), unchecked((int)Dword(task, offset + 24)), unchecked((int)Dword(task, offset + 28)), 4096);
    private static NativeBattleModulePoolTask WriteMatrix(NativeBattleModulePoolTask task, int offset, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(offset + index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        return WriteDword(WriteDword(WriteDword(task, offset + 20, unchecked((uint)matrix.X)), offset + 24, unchecked((uint)matrix.Y)), offset + 28, unchecked((uint)matrix.Z));
    }
    private static void Require(NativeBattleModulePoolTask task, uint callback)
    {
        if (task.Callback != callback)
        {
            throw new NotSupportedException($"Native Blizzaga callback{task.Callback:X8} is unsupported in this task bank.");
        }
    }
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static uint Dword(NativeBattleModulePoolTask task, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(offset - 12, 4));
    private static NativeBattleModulePoolTask WriteDword(NativeBattleModulePoolTask task, int offset, uint value)
    {
        byte[] bytes = NativeBattleTelemetry.Materialize(1168, task.Payload);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset - 12, 4), value);
        return task with { Payload = NativeBattleTelemetry.Immutable(1173, bytes) };
    }
    private static ImmutableArray<byte> Slice(NativeBattleModulePoolTask task, int offset, int length) => NativeBattleTelemetry.Immutable(1174, NativeBattleTelemetry.Materialize(1169, task.Payload.AsSpan().Slice(offset - 12, length)));
    private static NativeBattleModulePoolTask WriteBytes(NativeBattleModulePoolTask task, int offset, ImmutableArray<byte> bytes)
    {
        byte[] payload = NativeBattleTelemetry.Materialize(1170, task.Payload);
        NativeBattleTelemetry.Copy(1177, bytes.AsSpan(), payload.AsSpan(offset - 12));
        return task with { Payload = NativeBattleTelemetry.Immutable(1175, payload) };
    }
    private static NativeBattleModulePoolTask WriteByte(NativeBattleModulePoolTask task, int offset, byte value) => task with { Payload = task.Payload.SetItem(offset - 12, value) };
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(Word(task, offset) + value)));
    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, int offset, NativeBattleVector position) => task.WriteWord(offset, unchecked((ushort)position.X)).WriteWord(offset + 2, unchecked((ushort)position.Y)).WriteWord(offset + 4, unchecked((ushort)position.Z));
    private static NativeBattleVector Vector(NativeBattleModulePoolTask task, int offset) => new(Word(task, offset), Word(task, offset + 2), Word(task, offset + 4));
    private static NativeBattleModulePoolTask Clear(NativeBattleModulePoolTask task, int length)
    {
        byte[] bytes = NativeBattleTelemetry.Materialize(1171, task.Payload);
        bytes.AsSpan(0, length).Clear();
        return task with { Payload = NativeBattleTelemetry.Immutable(1176, bytes) };
    }
    private static NativeBattleEffectTaskPool Put(NativeBattleEffectTaskPool pool, int slot, NativeBattleModulePoolTask task) => pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot] with { Payload = task.Payload }) };
}
