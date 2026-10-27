using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public abstract record NativeBattleDuel215SceneOperation;
public sealed record NativeBattleDuel215Stage(int Intensity) : NativeBattleDuel215SceneOperation;
public sealed record NativeBattleDuel215StageFlags(bool Enabled) : NativeBattleDuel215SceneOperation;
public sealed record NativeBattleDuel215ProgramDraw(int Slot, int Offset, int Size, NativeBattleDuel215Program Context, bool Anchored) : NativeBattleDuel215SceneOperation;
public sealed record NativeBattleDuel215BodyDraw(int Actor) : NativeBattleDuel215SceneOperation;
public sealed record NativeBattleDuel215TargetsDraw(int Slot, short Tick) : NativeBattleDuel215SceneOperation;
public sealed record NativeBattleDuel215DebrisDraw(int Slot) : NativeBattleDuel215SceneOperation;
public sealed record NativeBattleDuel215Sound(uint Identifier) : NativeBattleDuel215SceneOperation;
public sealed record NativeBattleDuel215Contact : NativeBattleDuel215SceneOperation;
public sealed record NativeBattleDuel215Camera(short Tick, short OriginX, short OriginZ, short CenterX, short FarZ) : NativeBattleDuel215SceneOperation;
public sealed record NativeBattleDuel215SceneServices(Func<int, NativeBattleGraphicsModel> ReadModel, Action<int, NativeBattleGraphicsModel> WriteModel, Func<int, NativeBattleModuleAnchor> ReadAnchor571400, Func<int, int, int, NativeBattleVector> ReadBone502170, ImmutableArray<byte> Targets, NativeBattleActionMatrix View, Func<NativeBattleDuel215Frame, NativeBattleDuel215SceneOperation, NativeBattleDuel215Frame> Execute);

public static partial class NativeBattleDuel215Controller
{
    public static NativeBattleDuel215Step Advance69B070(NativeBattleDuel215Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleDuel215SceneServices services)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (pool.DescriptorAddress != Scene || task.Callback != 0x69B070)
        {
            throw new InvalidDataException("Native Duel215 scene requires its actual registered callback.");
        }
        int actor = frame.Memory.Duel215?.Parameters?.Actor78 ?? throw new NotSupportedException("Native Duel215 scene requires its entered actor.");
        NativeBattleDuel215Resources resources = frame.Memory.Duel215.Resources ?? throw new NotSupportedException("Native Duel215 scene requires its entered resources.");
        short tick = Signed(12);
        int intensity = tick < 12 ? 2300 * tick / 12 : tick < 270 ? 2300 : 2300 * (282 - tick) / 12;
        Execute(new NativeBattleDuel215Stage(intensity));
        int phase = tick - 1;
        if ((uint)phase < 61)
        {
            Initialize(phase == 0, 1420, 0x11550A8, 880);
            NativeBattleModuleAnchor anchor = services.ReadAnchor571400(actor);
            Program(1420, 880, new(services.View, new(anchor.X, 0, anchor.Z), "0100", -128, unchecked((short)(phase * 32)), 0, Arena + 63768), true);
        }
        phase = tick - 62;
        if ((uint)phase < 14)
        {
            Initialize(phase == 0, 392, 0x1168A5C, 176);
            MoveZ(-256);
            Program(392, 176, MatrixProgram("1", -128, phase * 32, 0));
            if (phase % 6 is 2 or 5)
            {
                NativeBattleVector point = services.ReadBone502170(actor, phase % 6 == 2 ? 20 : 19, 4096);
                Spawn(point with { Y = 0 }, 4096, 0, 0x115C4C8, 64);
            }
        }
        phase = tick - 76;
        if ((uint)phase < 40)
        {
            if (phase == 0)
            {
                NativeBattleEffectTaskPool particles = ParticlesPool();
                ReplaceParticles(particles.UpdateLinks(null, null));
                Initialize(true, 392, 0x1160BB0, 392);
                NativeBattleGraphicsModel model = services.ReadModel(actor);
                NativeBattleModelTransform transform = Transform(model);
                short z = unchecked((short)(Signed(388) + 6144));
                NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native Duel215 scene requires its actual actor world matrix.");
                services.WriteModel(actor, model with { Transform = transform with { Position28 = transform.Position28 with { Z = z } }, GeometryWorldMatrix = world with { Z = z } });
                MoveZ(-1228);
            }
            else if (phase >= 10)
            {
                Flags(actor, 8, 0);
            }
            else
            {
                MoveZ(-1228);
            }
            if (phase >= 20)
            {
                foreach (int target in services.Targets)
                {
                    NativeBattleGraphicsModel model = services.ReadModel(target);
                    int difference = (Transform(model).Position28.X - Transform(services.ReadModel(actor)).Position28.X) >> 2;
                    if (difference > -1024 && difference < 1024)
                    {
                        short yaw = model.Rotation14 ?? throw new NotSupportedException("Native Duel215 scene requires the actual target yaw.");
                        services.WriteModel(target, model with { Rotation14 = unchecked((short)(yaw + difference + (difference <= 0 ? 1024 : -1024))) });
                    }
                }
            }
            Program(392, 392, MatrixProgram("1", -128, phase * 64, 0));
        }
        phase = tick - 116;
        if ((uint)phase < 48)
        {
            if (phase == 0)
            {
                Execute(new NativeBattleDuel215StageFlags(false));
                for (int index = 0; index < services.Targets.Length; index++)
                {
                    int target = Target(index);
                    int offset = 64 + index * 32;
                    NativeBattleGraphicsModel model = services.ReadModel(target);
                    bool paddingUnknown = task.ReadUnknownPayloadRanges()?.Any(range => range.Offset < offset + 20 && range.Offset + range.Length > offset + 18) == true;
                    services.WriteModel(target, model with { Flags0 = (ushort)(model.Flags0 | 4), Rotation12 = Signed(offset + 12), Rotation14 = Signed(offset + 14), Rotation16 = Signed(offset + 16), RotationPadding18 = paddingUnknown ? null : task.ReadWord(offset + 18) });
                }
                Initialize(true, 392, 0x11625E0, 148);
            }
            NativeBattleActionMatrix basis = Identity() with { Z = 2800 };
            Program(392, 148, new(basis, new(0, 0, 0), "0900", 0, unchecked((short)(((phase << 12) / 48) + 77)), phase <= 44 ? (short)0 : unchecked((short)((phase << 10) + 20992)), Arena + 63768));
        }
        phase = tick - 164;
        if ((uint)phase < 18)
        {
            Initialize(phase == 0, 392, 0x1169844, 340);
            NativeBattleActionMatrix local = Identity() with { X = Transform(services.ReadModel(actor)).Position28.X, Z = Signed(388) + 24000 };
            Program(392, 340, new(NativeBattleGfMatrices.Compose56C2F0(services.View, local), new(0, 0, 0), "000", 0, 0, 0, Arena + 63768));
        }
        phase = tick - 182;
        if ((uint)phase < 40)
        {
            short fade = phase < 8 ? unchecked((short)((8 - phase) << 9)) : phase <= 22 ? (short)0 : unchecked((short)((phase - 22) << 9));
            short depth = 0;
            if (phase == 0)
            {
                NativeBattleGraphicsModel model = services.ReadModel(actor);
                NativeBattleModelTransform transform = Transform(model);
                services.WriteModel(actor, model with { Transform = transform with { Position28 = transform.Position28 with { Z = unchecked((short)(Signed(388) + 23200)) } } });
                Initialize(true, 392, 0x116703C, 188);
                Initialize(true, 784, 0x115DCCC, 636);
                ReplaceParticles(NativeBattleEffectTaskPool.Initialize(Particles, Arena + 5120, 660, 36));
            }
            if (phase < 30)
            {
                Execute(new NativeBattleDuel215BodyDraw(actor));
                Flags(actor, 4, 0);
                depth = -2048;
            }
            if (phase == 30)
            {
                Flags(actor, 0, 4);
            }
            else
            {
                if (phase < 20)
                {
                    MoveZ(-800);
                }
                else if (phase < 25)
                {
                    MoveZ(-1600);
                }
                else if (phase < 29)
                {
                    if (phase == 25)
                    {
                        MoveZ(-1024);
                    }
                    MoveZ(-(unchecked(600 * (3584 - ((phase - 25) << 12) / 4)) >> 12));
                }
                if (phase == 26)
                {
                    foreach (int target in services.Targets)
                    {
                        NativeBattleModuleAnchor anchor = services.ReadAnchor571400(target);
                        int scale = Transform(services.ReadModel(target)).Scale38;
                        if (!Spawn(new(anchor.X, anchor.Y, anchor.Z), unchecked((short)(2 * (scale / 2) + 2048)), 2048, 0x115CA08, 352))
                        {
                            break;
                        }
                    }
                }
                else if (phase < 25)
                {
                    for (int remaining = 4; remaining > 0; remaining--)
                    {
                        NativeBattleDuel215ParticleBirth birth = AllocateParticle69CC10(frame, task, checked(pool.BankAddress + (uint)(slot * pool.Stride)));
                        frame = birth.Frame;
                        task = birth.Task;
                        if (birth.Address is not uint address)
                        {
                            break;
                        }
                        int offset = checked((int)(address - pool.BankAddress));
                        NativeBattleVector position = Transform(services.ReadModel(actor)).Position28;
                        short vx = unchecked((short)((Rand() & 63) + 63));
                        if ((remaining & 1) != 0)
                        {
                            vx = unchecked((short)-vx);
                        }
                        task = task.WriteWord(offset, unchecked((ushort)position.X)).WriteWord(offset + 2, 0).WriteWord(offset + 4, unchecked((ushort)(position.Z + 1024))).WriteWord(offset + 6, 15).WriteWord(offset + 8, unchecked((ushort)vx)).WriteWord(offset + 10, unchecked((ushort)(-80 - (Rand() & 63)))).WriteWord(offset + 12, unchecked((ushort)((Rand() & 63) + 63))).WriteWord(offset + 14, unchecked((ushort)((Rand() & 4095) + 1024)));
                    }
                }
                if (phase == 10)
                {
                    Flags(actor, 0, 8);
                }
                if (phase < 29)
                {
                    NativeBattleVector position = Transform(services.ReadModel(actor)).Position28;
                    Spawn(new(unchecked((short)(position.X + ((phase & 1) != 0 ? 200 : -200))), 0, unchecked((short)(position.Z + 1024))), 8192, 0, 0x115C4C8, 64);
                }
                if (phase == 10)
                {
                    Execute(new NativeBattleDuel215StageFlags(true));
                }
                if (phase is >= 10 and <= 26)
                {
                    Execute(new NativeBattleDuel215Stage(4096 - (unchecked(459776 * (phase - 10)) >> 12)));
                }
            }
            if (fade < 4096)
            {
                Program(392, 188, MatrixProgram("12", depth, phase * 32, fade));
                if (phase >= 10)
                {
                    NativeBattleVector position = services.ReadBone502170(actor, 22, 0);
                    NativeBattleActionMatrix local = Identity() with { X = position.X, Y = position.Y, Z = position.Z };
                    Program(784, 636, new(NativeBattleGfMatrices.Compose56C2F0(services.View, local), new(0, 0, 0), "000", depth, unchecked((short)(phase * 32)), fade, Arena + 63768));
                }
            }
        }
        phase = tick - 222;
        if ((uint)phase < 32)
        {
            Initialize(phase == 0, 1420, 0x116AD0C, 340);
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler701310(new(0, 2048, 0)) with { X = Signed(384), Z = Signed(388) + 1024 };
            Program(1420, 340, new(NativeBattleGfMatrices.Compose56C2F0(services.View, local), new(0, 0, 0), "100", -128, unchecked((short)(phase * 32)), 0, Arena + 63768));
        }
        else if (tick == 254)
        {
            Flags(actor, 0, 4);
        }
        Execute(new NativeBattleDuel215TargetsDraw(slot, tick));
        NativeBattleGraphicsModel caster = services.ReadModel(actor);
        NativeBattleVector raw = Transform(caster).Position28;
        NativeBattleActionMatrix actorWorld = caster.GeometryWorldMatrix ?? throw new NotSupportedException("Native Duel215 scene requires its actual actor world matrix.");
        services.WriteModel(actor, caster with { GeometryWorldMatrix = actorWorld with { X = raw.X, Y = raw.Y, Z = raw.Z } });
        phase = tick - 24;
        if ((uint)phase < 92)
        {
            Initialize(phase == 0, 784, 0x115A910, 436);
            NativeBattleVector anchor = services.ReadBone502170(actor, 22, 0);
            Program(784, 436, new(services.View, anchor, "000", -128, 0, 0, Arena + 63768), true);
        }
        Execute(new NativeBattleDuel215DebrisDraw(slot));
        task = AdvanceParticles69D080(task, checked(pool.BankAddress + (uint)(slot * pool.Stride)));
        if (tick is 5 or 162)
        {
            Execute(new NativeBattleDuel215Sound(resources.Dword(tick == 5 ? 0x1154F48u : 0x1154F4Cu)));
        }
        else if (tick == 264)
        {
            Execute(new NativeBattleDuel215Contact());
        }
        if (tick == 163)
        {
            short far = Signed(388);
            task = task.WriteWord(384, 0);
            foreach (int target in services.Targets)
            {
                NativeBattleGraphicsModel model = services.ReadModel(target);
                NativeBattleModelTransform transform = Transform(model);
                services.WriteModel(target, model with { Flags0 = (ushort)(model.Flags0 & ~4), Transform = transform with { Position28 = transform.Position28 with { Z = unchecked((short)(transform.Position28.Z - far)) } } });
            }
        }
        Execute(new NativeBattleDuel215Camera(tick, GlobalWord(0x2508998), GlobalWord(0x250899C), GlobalWord(0x25089A0), GlobalWord(0x25089A4)));
        task = task.WriteWord(12, unchecked((ushort)(tick + 1)));
        int result = 0;
        if (unchecked((short)(tick + 1)) >= 282)
        {
            foreach (int target in services.Targets)
            {
                Flags(target, 0, 12);
            }
            for (int index = 0; index < 3; index++)
            {
                Flags(index, 0, 4);
            }
            Execute(new NativeBattleDuel215Stage(0));
            result = 2;
        }
        Store();
        return new(frame, pool, result);

        void Refresh()
        {
            pool = frame.Effect.Pools.Single(bank => bank.DescriptorAddress == Scene);
            task = pool.Slots[slot];
        }
        void Store()
        {
            pool = pool.WriteSlot(slot, task);
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(bank => bank.DescriptorAddress == Scene ? pool : bank).ToImmutableArray() } };
        }
        void Execute(NativeBattleDuel215SceneOperation operation)
        {
            Store();
            frame = services.Execute(frame, operation);
            Refresh();
        }
        void Initialize(bool initialize, int offset, uint address, int size)
        {
            if (initialize)
            {
                Store();
                frame = NativeBattleDuel215Workspaces.Initialize69B070(frame, slot, offset, address, size);
                Refresh();
            }
        }
        void Program(int offset, int size, NativeBattleDuel215Program context, bool anchored = false) => Execute(new NativeBattleDuel215ProgramDraw(slot, offset, size, context, anchored));
        NativeBattleDuel215Program MatrixProgram(string windows, short depth, int vertical, short fade)
        {
            NativeBattleVector position = Transform(services.ReadModel(actor)).Position28;
            NativeBattleActionMatrix local = Identity() with { X = position.X, Z = position.Z };
            return new(NativeBattleGfMatrices.Compose56C2F0(services.View, local), new(0, 0, 0), windows, depth, unchecked((short)vertical), fade, Arena + 63768);
        }
        bool Spawn(NativeBattleVector position, short scale, int angle, uint address, int size)
        {
            NativeBattleEffectTaskPool particles = ParticlesPool();
            if (particles.Slots.All(record => (record.Flags & 1) != 0))
            {
                return false;
            }
            (particles, int child) = particles.Append(0x69CB10);
            NativeBattleGfAnimationSource source = new(address, resources.Read(address, checked((int)(NativeBattleDuel215Resources.EndAddress - address))));
            NativeBattleModulePoolTask record = particles.Slots[child].WriteWord(12, unchecked((ushort)position.X)).WriteWord(14, unchecked((ushort)position.Y)).WriteWord(16, unchecked((ushort)position.Z)).WriteWord(18, unchecked((ushort)scale)).WriteDword(20, unchecked((uint)angle));
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(record, 24, size);
            record = NativeBattleEffectTaskMemory.PublishAnimationWorkspace(record, 24, workspace.Initialize7016B0(source, size));
            ReplaceParticles(particles.WriteSlot(child, record));
            return true;
        }
        NativeBattleEffectTaskPool ParticlesPool() => frame.Effect.Pools.Single(bank => bank.DescriptorAddress == Particles);
        void ReplaceParticles(NativeBattleEffectTaskPool particles) => frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(bank => bank.DescriptorAddress == Particles ? particles : bank).ToImmutableArray() } };
        void MoveZ(int difference)
        {
            NativeBattleGraphicsModel model = services.ReadModel(actor);
            NativeBattleModelTransform transform = Transform(model);
            services.WriteModel(actor, model with { Transform = transform with { Position28 = transform.Position28 with { Z = unchecked((short)(transform.Position28.Z + difference)) } } });
        }
        void Flags(int index, int set, int clear)
        {
            NativeBattleGraphicsModel model = services.ReadModel(index);
            services.WriteModel(index, model with { Flags0 = unchecked((ushort)((model.Flags0 | set) & ~clear)) });
        }
        NativeBattleModelTransform Transform(NativeBattleGraphicsModel model) => model.Transform ?? throw new NotSupportedException("Native Duel215 scene requires actual raw model position WORDs.");
        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
        short GlobalWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Staging.Read(address, 2));
        NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        ushort Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
        int Target(int index)
        {
            int target = services.Targets[index];
            if (task.ReadDword(64 + index * 32) != 0x1D972C0u + (uint)target * 156)
            {
                throw new InvalidDataException("Native Duel215 scene lost its actual target pointer.");
            }
            return target;
        }
    }
}
