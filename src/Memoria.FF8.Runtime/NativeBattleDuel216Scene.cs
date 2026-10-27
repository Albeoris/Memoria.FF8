using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public abstract record NativeBattleDuel216SceneOperation;
public sealed record NativeBattleDuel216ProgramDraw(int Slot, int Offset, int Size, NativeBattleDuel216Program Context) : NativeBattleDuel216SceneOperation;
public sealed record NativeBattleDuel216MorphDraw(int Phase, int Divisor, bool Fade) : NativeBattleDuel216SceneOperation;
public sealed record NativeBattleDuel216Spawn69A550(short X, short Y, short Z, short Scale) : NativeBattleDuel216SceneOperation;
public sealed record NativeBattleDuel216Spawn69A110(int Count, short X, short Y, short Z) : NativeBattleDuel216SceneOperation;
public sealed record NativeBattleDuel216Spawn69A2D0(int Count, short X, short Y, short Z, int MinimumDuration, int MaximumDuration, int MinimumX, int MaximumX, int MinimumZ, int MaximumZ) : NativeBattleDuel216SceneOperation;
public sealed record NativeBattleDuel216Spawn699F80(int Count, short X, short Y, short Z, int MinimumX, int MaximumX, int MinimumZ, int MaximumZ) : NativeBattleDuel216SceneOperation;
public sealed record NativeBattleDuel216BodyDraw(int Model, NativeBattleVector Rotation, NativeBattleVector Pivot, NativeBattleModelScaleVector Scale) : NativeBattleDuel216SceneOperation;
public sealed record NativeBattleDuel216Sequence(int Model, byte Sequence) : NativeBattleDuel216SceneOperation;
public sealed record NativeBattleDuel216BoundsUpdate(int Slot, int Model) : NativeBattleDuel216SceneOperation;
public sealed record NativeBattleDuel216EchoDraw(int Slot) : NativeBattleDuel216SceneOperation;
public sealed record NativeBattleDuel216EchoCapture(int Slot) : NativeBattleDuel216SceneOperation;
public sealed record NativeBattleDuel216Flash699EE0(int Phase) : NativeBattleDuel216SceneOperation;
public sealed record NativeBattleDuel216Sound501330 : NativeBattleDuel216SceneOperation;
public sealed record NativeBattleDuel216Contact506690 : NativeBattleDuel216SceneOperation;
public sealed record NativeBattleDuel216MainCamera(short Tick) : NativeBattleDuel216SceneOperation;
public sealed record NativeBattleDuel216MainFile(short File, uint Destination, short Type) : NativeBattleDuel216SceneOperation;
public sealed record NativeBattleDuel216MainStageReset : NativeBattleDuel216SceneOperation;
public sealed record NativeBattleDuel216SceneServices(Func<int, NativeBattleGraphicsModel> ReadModel, Action<int, NativeBattleGraphicsModel> WriteModel, Func<int, NativeBattleModuleAnchor> ReadAnchor571400, Func<int, int, NativeBattleVector> ReadBone502170, ImmutableArray<byte> TargetRing, NativeBattleActionMatrix View, Func<NativeBattleDuel216Frame, NativeBattleDuel216SceneOperation, NativeBattleDuel216Frame> Execute);

public static class NativeBattleDuel216Scene
{
    public static NativeBattleDuel216Step Advance6960E0(NativeBattleDuel216Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleDuel216SceneServices services)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (pool.DescriptorAddress != NativeBattleDuel216Controller.Main || task.Callback != 0x6960E0)
        {
            throw new InvalidDataException("Native Different Beat scene requires its registered main task.");
        }
        NativeBattleDuel216Resources resources = frame.Memory.Duel216?.Resources ?? throw new NotSupportedException("Native Different Beat scene requires its entered resources.");
        int actor = ModelIndex(0x2508880);
        int target = ModelIndex(0x2508884);
        if (services.TargetRing.IsDefaultOrEmpty || services.TargetRing[0] != target)
        {
            throw new InvalidDataException("Native Different Beat requires its actual target-ring traversal.");
        }
        short tick = Signed(12);
        Stage(tick < 12 ? 2700 * tick / 12 : tick < 127 ? 2700 : 2700 * (139 - tick) / 12);
        int phase = tick - 9;
        if ((uint)phase < 14)
        {
            if (phase == 0)
            {
                NativeBattleGraphicsModel model = services.ReadModel(actor);
                NativeBattleModelTransform transform = Transform(model);
                services.WriteModel(actor, model with { Rotation14 = 0, Transform = transform with { Position28 = new(Signed(48), Signed(50), Signed(52)), Vertical34 = Signed(54) } });
                Initialize(88, 0x1140C5C, 64);
                Word(82, 3136);
                task = task.WriteDword(3668, 0);
            }
            NativeBattleModuleAnchor anchor = services.ReadAnchor571400(actor);
            Program(88, 64, Identity() with { X = anchor.X, Z = anchor.Z }, 0xFE0FC8, 0, phase * 32, 0);
            if (phase >= 8 && phase < 13)
            {
                Execute(new NativeBattleDuel216MorphDraw(phase - 8, 5, phase == 12));
            }
            if (phase == 9)
            {
                Impact();
            }
            if (phase >= 9)
            {
                Move(target, 0, -640, 0);
            }
        }
        phase = tick - 23;
        if ((uint)phase < 48)
        {
            int cycle = phase % 6;
            if (phase == 0)
            {
                Move(actor, 0, -2560, 0);
                RingFlags(8, 0);
            }
            Move(actor, 0, -208, 0);
            if (cycle >= 2 && cycle <= 4)
            {
                Move(target, 0, -416, 0);
            }
            if (cycle == 0)
            {
                Impact();
                RingFlags(0, 8);
                Execute(new NativeBattleDuel216Sequence(target, 4));
            }
            if (cycle <= 3)
            {
                Execute(new NativeBattleDuel216MorphDraw(cycle, 3, cycle == 3));
            }
            if (((phase / 6) & 1) != 0)
            {
                Word(6112, -Signed(6112));
            }
            DrawTargets(GlobalVector(0x25088F0), GlobalVector(0x25088F0), new(4096, 4096, 4096));
        }
        phase = tick - 93;
        if ((uint)phase < 10)
        {
            if (phase == 0)
            {
                Initialize(88, 0x11431D4, 1156);
                RingFlags(8, 12);
                Execute(new NativeBattleDuel216BoundsUpdate(slot, target));
                Word(6108, Signed(6108) - Position(target).Y);
                Word(6110, Signed(6110) - Position(target).Y);
            }
            NativeBattleVector bone = services.ReadBone502170(actor, 20);
            NativeBattleActionMatrix matrix = NativeBattlePandemona291Restoration.Rotation701220(-512) with { X = bone.X, Y = bone.Y, Z = bone.Z };
            Program(88, 1156, matrix, 0x1154F40, -128, phase * 32, 0);
            if (phase >= 8)
            {
                Move(actor, 0, 512, -512);
            }
        }
        phase = tick - 71;
        if ((uint)phase < 22 && tick == 71)
        {
            Move(actor, 0, Signed(6110) - Signed(6108) - 1536, 6024 - (Transform(services.ReadModel(actor)).Scale38 >> 1));
            Move(target, 0, 0, 5000);
            Word(6112, 0);
            Word(82, GlobalWord(0x2508908) - 4181);
            Word(84, Position(target).Y + 1536);
            NativeBattleEffectTaskPool secondary = frame.Effect.Pools.Single(bank => bank.DescriptorAddress == NativeBattleDuel216Controller.Secondary);
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(bank => bank.DescriptorAddress == secondary.DescriptorAddress ? secondary.UpdateLinks(null, null) : bank).ToImmutableArray() } };
        }
        if ((uint)phase < 32)
        {
            int angle = (phase << 7) & 0x7FFFFFF;
            Word(82, Signed(82) + NativeBattleCameraMath.Cosine(angle >> 2) / 12);
            SetPosition(target, Position(target) with { Y = unchecked((short)(Signed(84) - (NativeBattleCameraMath.Sine(angle >> 2) >> 1))) });
            Word(6112, NativeBattleCameraMath.Sine(angle * 2) >> 4);
            DrawTargets(new(Signed(6112), Signed(6114), Signed(6116)), new(0, unchecked((short)((Signed(6108) + Signed(6110)) >> 1)), 0), new(4096, 4096, 4096));
        }
        else if (tick == 103)
        {
            RingFlags(0, 4);
        }
        phase = tick - 103;
        if ((uint)phase < 18)
        {
            if (phase == 0)
            {
                Initialize(1244, 0x11484D0, 352);
            }
            NativeBattleVector bone = services.ReadBone502170(actor, 20);
            NativeBattleActionMatrix matrix = NativeBattlePandemona291Restoration.Rotation701220(phase < 10 ? -512 : -1024) with { X = bone.X, Y = bone.Y, Z = bone.Z };
            int fade = phase < 10 ? 0 : (phase - 10) << 9;
            Program(88, 1156, matrix, 0x1154F40, -128, phase * 32, fade);
            Program(1244, 352, matrix, 0x1154F40, -128, phase * 32, fade);
            if (phase < 10)
            {
                Move(actor, 0, 1254, -500);
                Move(target, 0, 1254, -500);
            }
            if (phase == 9)
            {
                frame = frame with { Staging = frame.Staging.Write(0x2508904, BitConverter.GetBytes(task.ReadWord(60))) };
            }
        }
        phase = tick - 113;
        if ((uint)phase < 25)
        {
            if (phase == 0)
            {
                Initialize(1596, 0x1141444, 168);
                Initialize(1764, 0x114A6DC, 1668);
                SetPosition(target, Position(target) with { Y = unchecked((short)(Signed(6122) - Signed(6108))) });
                RestoreRotation(actor, 72);
                SetPosition(actor, new(Signed(56), unchecked((short)(Signed(6110) - Signed(6108))), Signed(60)));
            }
            else if (phase <= 2)
            {
                Execute(new NativeBattleDuel216Flash699EE0(tick - 114));
            }
            if (phase < 4)
            {
                int half = Signed(80) >> 1;
                Execute(new NativeBattleDuel216Spawn69A2D0(16, Signed(56), 0, Signed(60), 5, 15, half, half, 1000, 1000));
                frame = frame with { Random = frame.Random.Draw(out ushort random) };
                Execute(new NativeBattleDuel216Spawn699F80(8, Signed(56), unchecked((short)-(random & 1023)), Signed(60), half, half, 2000, 2000));
            }
            int curve = 4096 - NativeBattleCameraMath.Sine((phase << 12) / 48);
            int square = unchecked(curve * curve) >> 12;
            int fourth = unchecked(square * square) >> 12;
            int eighth = unchecked(fourth * fourth);
            int scaleY = (eighth >> 14) + (eighth >> 13) + 1024;
            SetPosition(target, Position(target) with { Y = unchecked((short)(Signed(6122) - unchecked(scaleY * Signed(6108)) / 4096)) });
            DrawTargets(GlobalVector(0x25088F0), GlobalVector(0x25088F0), new(4096, scaleY, 4096));
            if (phase < 12)
            {
                SetPosition(actor, Position(actor) with { Y = unchecked((short)(unchecked(scaleY * (Signed(6110) - Signed(6108))) / 4096)) });
                NativeBattleVector position = Position(actor);
                Program(1596, 168, Identity() with { X = position.X, Y = position.Y, Z = position.Z + 500 }, 0x1154F3C, 0, 0, 0);
            }
            short scale = Signed(80);
            NativeBattleActionMatrix ring = new([scale, 0, 0, 0, scale, 0, 0, 0, scale], Signed(56), 0, Signed(60), 4096);
            Program(1764, 1668, ring, 0x1154F34, 0, 0, 0);
            if (phase >= 13 && phase <= 17)
            {
                if (phase == 14)
                {
                    services.WriteModel(actor, services.ReadModel(actor) with { Rotation14 = Signed(74) });
                }
                int retreat = (17 - phase) << 8;
                int distance = retreat + 3000;
                SetPosition(actor, new(unchecked((short)(Signed(64) - unchecked(distance * NativeBattleCameraMath.Sine(Signed(74))) / 4096)), unchecked((short)(Signed(66) - retreat)), unchecked((short)(Signed(68) - unchecked(distance * NativeBattleCameraMath.Cosine(Signed(74))) / 4096))));
            }
            else if (phase == 18)
            {
                NativeBattleGraphicsModel model = services.ReadModel(actor);
                services.WriteModel(actor, model with { Transform = Transform(model) with { Position28 = new(Signed(64), Signed(66), Signed(68)), Vertical34 = Signed(70) } });
            }
        }
        else if (tick == 138)
        {
            RingFlags(0, 12);
            Execute(new NativeBattleDuel216Contact506690());
        }
        phase = tick - 29;
        if ((uint)phase < 96)
        {
            int strength = phase < 12 ? ((phase << 12) / 12) + 170 : phase < 84 ? 4096 : (389120 - (phase << 12)) / 12 + 170;
            Stage((unchecked(1396 * strength) >> 12) + 2700);
        }
        phase = tick - 41;
        if ((uint)phase < 72)
        {
            if (phase == 0)
            {
                Initialize(3432, 0x11512C8, 236);
            }
            NativeBattleActionMatrix local = Identity() with { X = Signed(48), Y = Signed(82) - 14976, Z = Signed(52) };
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(services.View, local);
            NativeBattleActionMatrix basis = NativeBattlePoseMatrices.Scale56BEF0(Identity(), 4096, 4608, 4096) with { X = composed.X, Y = composed.Y, Z = composed.Z };
            int fade = phase < 12 ? (49152 - (phase << 12)) / 12 + 170 : phase < 60 ? 0 : unchecked((phase + 1048516) << 12) / 12 + 170;
            Program(3432, 236, basis, 0x1154F3C, -512, 0, fade, false);
        }
        if (tick > 0 && tick <= 12 || tick > 123 && tick <= 130 || tick > 23 && tick < 71)
        {
            Execute(new NativeBattleDuel216EchoDraw(slot));
        }
        if ((ushort)tick <= 10 || tick >= 123 && tick <= 128 || tick >= 23 && tick <= 35)
        {
            Execute(new NativeBattleDuel216EchoCapture(slot));
        }
        if (tick == 0)
        {
            Execute(new NativeBattleDuel216Sound501330());
        }
        Execute(new NativeBattleDuel216MainCamera(tick));
        Word(12, tick + 1);
        int result = 0;
        if (unchecked((short)(tick + 1)) >= 139)
        {
            for (int index = 0; index < 3; index++)
            {
                Execute(new NativeBattleDuel216MainFile((short)(360 + index), NativeBattleDuel216Controller.Arena + 12696, index == 2 ? (short)0 : (short)1));
            }
            Execute(new NativeBattleDuel216MainStageReset());
            result = 2;
        }
        Store();
        return new(frame, pool, result);

        void Store()
        {
            pool = pool.WriteSlot(slot, task);
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(bank => bank.DescriptorAddress == pool.DescriptorAddress ? pool : bank).ToImmutableArray() } };
        }
        void Execute(NativeBattleDuel216SceneOperation operation)
        {
            Store();
            frame = services.Execute(frame, operation);
            pool = frame.Effect.Pools.Single(bank => bank.DescriptorAddress == NativeBattleDuel216Controller.Main);
            task = pool.Slots[slot];
        }
        void Initialize(int offset, uint address, int size)
        {
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, offset, size);
            task = NativeBattleEffectTaskMemory.PublishAnimationWorkspace(task, offset, workspace.Initialize7016B0(resources.Hierarchy(address), size));
        }
        void Program(int offset, int size, NativeBattleActionMatrix local, uint windows, int depth, int vertical, int fade, bool compose = true)
        {
            NativeBattleActionMatrix basis = compose ? NativeBattleGfMatrices.Compose56C2F0(services.View, local) : local;
            Execute(new NativeBattleDuel216ProgramDraw(slot, offset, size, new(basis, null, windows, unchecked((short)depth), unchecked((short)vertical), unchecked((short)fade), NativeBattleDuel216Controller.Arena + 11480)));
        }
        void Impact()
        {
            Execute(new NativeBattleDuel216Spawn69A550(0, -500, -1000, 4096));
            Execute(new NativeBattleDuel216Spawn69A110(16, 0, -500, -1000));
        }
        void Stage(int value) => frame = frame with { Staging = frame.Staging.Write(0x25088E8, BitConverter.GetBytes(value)) };
        void Word(int offset, int value) => task = task.WriteWord(offset, unchecked((ushort)value));
        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
        short GlobalWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Staging.Read(address, 2));
        NativeBattleVector GlobalVector(uint address) => new(GlobalWord(address), GlobalWord(address + 2), GlobalWord(address + 4));
        NativeBattleModelTransform Transform(NativeBattleGraphicsModel model) => model.Transform ?? throw new NotSupportedException("Native Different Beat requires actual model position WORDs.");
        NativeBattleVector Position(int index) => Transform(services.ReadModel(index)).Position28;
        void SetPosition(int index, NativeBattleVector position)
        {
            NativeBattleGraphicsModel model = services.ReadModel(index);
            services.WriteModel(index, model with { Transform = Transform(model) with { Position28 = new(unchecked((short)position.X), unchecked((short)position.Y), unchecked((short)position.Z)) } });
        }
        void Move(int index, int x, int y, int z)
        {
            NativeBattleVector point = Position(index);
            SetPosition(index, new(unchecked((short)(point.X + x)), unchecked((short)(point.Y + y)), unchecked((short)(point.Z + z))));
        }
        void RestoreRotation(int index, int offset)
        {
            bool unknownPadding = task.ReadUnknownPayloadRanges()?.Any(range => range.Offset < offset + 8 && range.Offset + range.Length > offset + 6) == true;
            services.WriteModel(index, services.ReadModel(index) with { Rotation12 = Signed(offset), Rotation14 = Signed(offset + 2), Rotation16 = Signed(offset + 4), RotationPadding18 = unknownPadding ? null : task.ReadWord(offset + 6) });
        }
        void RingFlags(int set, int clear)
        {
            foreach (byte index in services.TargetRing)
            {
                NativeBattleGraphicsModel model = services.ReadModel(index);
                if ((model.Flags0 & 2) != 0)
                {
                    services.WriteModel(index, model with { Flags0 = unchecked((ushort)((model.Flags0 & ~clear) | set)) });
                }
            }
        }
        void DrawTargets(NativeBattleVector rotation, NativeBattleVector pivot, NativeBattleModelScaleVector scale)
        {
            foreach (byte index in services.TargetRing)
            {
                if ((services.ReadModel(index).Flags0 & 2) != 0)
                {
                    Execute(new NativeBattleDuel216BodyDraw(index, rotation, pivot, scale));
                    NativeBattleGraphicsModel model = services.ReadModel(index);
                    services.WriteModel(index, model with { Flags0 = (ushort)(model.Flags0 | 4) });
                }
            }
        }
        int ModelIndex(uint address)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(address, 4));
            uint displacement = pointer - 0x1D972C0u;
            if (displacement >= 7 * 156 || displacement % 156 != 0)
            {
                throw new InvalidDataException("Native Different Beat model pointer leaves its physical rows.");
            }
            return (int)(displacement / 156);
        }
        NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    }
}
