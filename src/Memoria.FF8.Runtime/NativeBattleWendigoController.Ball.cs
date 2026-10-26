using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleWendigoController
{
    private sealed partial class Execution
    {
        private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        private NativeBattleGraphicsModel Model(int actor) => inputs.ReadModel(actor);
        private NativeBattleModelTransform Transform(int actor) => Model(actor).Transform ?? throw new NotSupportedException("Native Wendigo requires its selected actor transform.");
        private void Model(NativeBattleGraphicsModel value) => Emit(new NativeBattleWendigoModel(value));
        private void Sequence(int actor, byte value) => Emit(new NativeBattleWendigoSequence((byte)actor, value));
        private short Facing(int actor) => Model(actor).Rotation14 ?? throw new NotSupportedException("Native Wendigo requires its actor facing.");
        private void InitializeBall(uint address, int actor)
        {
            SetState(State with { Module = State.Module.Write(checked((int)(Ball - Base)), new byte[shoot ? 0xE0 : 0xB8]) });
            uint descriptor = shoot ? 0x267AC68u : 0x2698A60u;
            Dword(Ball + 0x60, shoot ? 0x26591F8u : 0x267B920u);
            Dword(Ball + 0x64, descriptor);
            Dword(Ball + 0x68, 0x1D972C0 + (uint)actor * 0x9C);
            Dword(Ball + 0x6C, 0x1D8E054);
            Dword(Ball + 0x70, 0x1D8E054);
            Word(Ball + (shoot ? 0xD0u : 0xB0u), actor);
            Dword(descriptor + 4, shoot ? 0x2672DF0u : 0x2690BE8u);
            Word(descriptor + 0x14, 0);
            Word(descriptor + 0x16, 0);
            Word(descriptor + 0x18, 0x140);
            Word(descriptor + 0x1A, 0);
            SetState(State with { Module = State.Module.Write(checked((int)(descriptor + 0x1C - Base)), new byte[] { 128, 128, 128 }) });
            Dword(descriptor + 0x20, uint.MaxValue);
            Word(descriptor + 0x24, 0);
            NativeBattleModelTransform transform = Transform(actor);
            Position(Ball + 0x8C, transform.Position28);
            Word(Ball + 0x92, transform.Vertical34);
            Model(Model(actor) with { Flags0 = (ushort)(Model(actor).Flags0 | 4) });
            Store(address, Read(address).WriteByte(0x26, (byte)(Read(address).ReadByte(0x26) | 8)));
            if ((byte)Roll() == 0)
            {
                Word(Ball + (shoot ? 0xD4u : 0xB4u), 1);
                Word(Ball + 0x9E, 0x800);
            }
        }
        private void Pivot(int actor)
        {
            NativeBattleGraphicsModel local = Model(actor) with { Transform = Transform(actor) with { Position28 = new(0, 0, 0) }, GeometryWorldMatrix = Identity() };
            Position(Ball + 0x74, NativeBattlePoseMatrices.ReadBoneLazy(local, () => inputs.ReadPoseSource(actor), Word(Ball + (shoot ? 0xD4u : 0xB4u)) == 1 ? (byte)0xF0 : (byte)0xF1, 0));
        }
        private void Radii(uint address)
        {
            short age = Signed(Read(address), 0x24);
            if (age > (shoot ? 76 : 56))
            {
                return;
            }
            ImmutableArray<byte> table = shoot ? resources.KickScript : resources.Radii;
            int offset = age * (shoot ? 14 : 6);
            short radius = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan(offset, 2));
            uint first = Ball + (shoot ? 0xCAu : 0xAAu);
            Word(first, 512 - radius);
            Word(first + 2, radius);
            Word(first + 4, 512 - radius);
            if (shoot)
            {
                Word(Ball + 0xA0, System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan(offset + 2, 2)));
            }
        }
        private void Placement(uint address)
        {
            short age = Signed(Read(address), 0x24);
            NativeBattleActionMatrix basis;
            if (age <= 30)
            {
                NativeBattleVector start = Bone(Caster, 0x18);
                NativeBattleVector end = Bone(Caster, 0x15);
                NativeBattleVector direction = NativeBattlePoseMatrices.NormalizeLong56BD20(unchecked((short)(end.X - start.X)), unchecked((short)(end.Y - start.Y)), unchecked((short)(end.Z - start.Z)));
                basis = new([direction.Y, direction.X, 0, unchecked((short)-direction.X), direction.Y, 0, 0, direction.Z, 4096], 0, 0, 0, 4096);
            }
            else if (shoot && age < 39)
            {
                NativeBattleVector point = Bone(Caster, 0x18);
                basis = Matrix(Ball) with { X = point.X, Y = point.Y, Z = point.Z };
                basis = NativeBattleCuragaMatrices.RotateX8DD7E0(basis, 0x800);
            }
            else
            {
                basis = Identity();
            }
            Matrix(Ball, basis);
            short bottom = unchecked((short)(Word(Ball + 0x76) + 1024));
            int y = unchecked((Word(Ball + (shoot ? 0xCCu : 0xACu)) - bottom) * Word(Ball + (shoot ? 0xC8u : 0xA8u))) / 4096 + unchecked((ushort)bottom);
            basis = NativeBattleCuragaMatrices.RotateZ8DD960(basis, Word(Ball + 0xA0));
            basis = NativeBattleCuragaMatrices.RotateX8DD7E0(basis, Word(Ball + 0x9C));
            basis = NativeBattleCuragaMatrices.RotateY8DD8A0(basis, Word(Ball + 0x9E));
            NativeBattleVector delta = NativeBattleGfMatrices.RotateShort56C4F0(basis, new(0, unchecked((short)y), 0));
            NativeBattleVector origin = Bone(Caster, 0x18);
            Position(Ball + 0x7C, new(unchecked((short)(origin.X + delta.X)), unchecked((short)(origin.Y + delta.Y)), unchecked((short)(origin.Z + delta.Z))));
        }
        private void DrawBall(uint address, int actor)
        {
            if ((Read(address).ReadByte(0x26) & 8) == 0)
            {
                return;
            }
            NativeBattleVector point = Position(Ball + 0x7C);
            NativeBattleVector pivot = Position(Ball + 0x74);
            NativeBattleVector position = new(unchecked((short)(point.X - pivot.X)), unchecked((short)(point.Y - pivot.Y)), unchecked((short)(point.Z - pivot.Z)));
            NativeBattleActionMatrix world = Model(actor).GeometryWorldMatrix ?? throw new NotSupportedException("Native Wendigo requires the ball actor world matrix.");
            Model(Model(actor) with { Transform = Transform(actor) with { Position28 = position }, GeometryWorldMatrix = world with { X = position.X, Y = position.Y, Z = position.Z } });
            Emit(new NativeBattleWendigoDrawBall(actor));
        }
        private void DribbleBall(uint address)
        {
            int actor = Read(address).ReadByte(0x2D);
            switch (Stage(Read(address)))
            {
                case 0:
                    InitializeBall(address, actor);
                    Pivot(actor);
                    Radii(address);
                    Placement(address);
                    Store(address, Next(Read(address)));
                    break;
                case 1:
                case 2:
                case 3:
                case 4:
                    int stage = Stage(Read(address));
                    Pivot(actor);
                    if (stage == 1 && Signed(Read(address), 0x24) >= 19 || stage == 3)
                    {
                        Store(address, Next(Read(address)));
                    }
                    if (stage == 2)
                    {
                        Word(Ball + 0xA8, Math.Min(4096, Word(Ball + 0xA8) + 512));
                        if (Word(Ball + 0xA8) == 4096)
                        {
                            Store(address, Next(Read(address)));
                        }
                    }
                    if (stage == 4 && Signed(Read(address), 0x24) >= 51)
                    {
                        Sequence(actor, 6);
                        Word(Ball + 0x94, NativeBattleCameraMath.Sine(Facing(actor)) * 128 / 4096);
                        Word(Ball + 0x96, -512);
                        Word(Ball + 0x98, NativeBattleCameraMath.Cosine(Facing(actor)) * 128 / 4096);
                        Word(Ball + 0xB2, 0);
                        Store(address, Next(Read(address)));
                    }
                    Radii(address);
                    Placement(address);
                    break;
                case 5:
                    Pivot(actor);
                    int flight = Word(Ball + 0xB2);
                    if (flight <= 16)
                    {
                        Word(Ball + 0x9C, flight * -256 & 4095);
                        if (Word(Ball + 0xB4) == 1)
                        {
                            Word(Ball + 0x9E, (16 - flight) << 7);
                        }
                    }
                    Word(Ball + 0xA8, Math.Max(0, Word(Ball + 0xA8) - 256));
                    Word(Ball + 0x7C, Word(Ball + 0x7C) + Word(Ball + 0x94));
                    Word(Ball + 0x80, Word(Ball + 0x80) + Word(Ball + 0x98));
                    Word(Ball + 0x96, Word(Ball + 0x96) + 48);
                    Word(Ball + 0x7E, Word(Ball + 0x7E) + Word(Ball + 0x96));
                    if (Word(Ball + 0x96) > 0 && Word(Ball + 0x7E) >= Word(Ball + 0x76))
                    {
                        Sequence(actor, 6);
                        Word(Ball + 0x7E, Word(Ball + 0x76));
                        uint dust = Append(3, 0x84C8E0, TaskSize, address);
                        NativeBattleVector p = Transform(actor).Position28;
                        Store(dust, Read(dust).WriteWord(0x1C, unchecked((ushort)p.X)).WriteWord(0x1E, 0).WriteWord(0x20, unchecked((ushort)p.Z)).WriteWord(0x22, unchecked((ushort)Transform(actor).Vertical34)));
                        Emit(new NativeBattleWendigoSound(resources.LandSound, 1));
                        Store(address, Next(Read(address)));
                    }
                    Word(Ball + 0xB2, Word(Ball + 0xB2) + 1);
                    Radii(address);
                    break;
                case 6:
                    Store(address, Next(Read(address).WriteByte(0x26, (byte)(Read(address).ReadByte(0x26) & ~8))));
                    Model(Model(actor) with { Flags0 = (ushort)(Model(actor).Flags0 & ~4), Transform = Transform(actor) with { Position28 = Transform(actor).Position28 with { Y = Transform(actor).Angle36 } } });
                    break;
                case 7:
                    Dword(Ball + 0xA4, Model(actor).Flags0);
                    Model(Model(actor) with { Flags0 = (ushort)(Model(actor).Flags0 | 0x1000) });
                    if (Model(actor).Sequence != 6)
                    {
                        Sequence(actor, 0x1A);
                        Model(Model(actor) with { Rotation14 = (short)((Facing(actor) - 0x800) & 4095) });
                        Word(Ball + 0xB2, 10);
                        Word(Ball + 0x94, (Word(Ball + 0x8C) - Transform(actor).Position28.X) / 10);
                        Word(Ball + 0x98, (Word(Ball + 0x90) - Transform(actor).Position28.Z) / 10);
                        Store(address, Next(Read(address)));
                    }
                    break;
                case 8:
                    NativeBattleVector old = Transform(actor).Position28;
                    Model(Model(actor) with { Transform = Transform(actor) with { Position28 = old with { X = unchecked((short)(old.X + Word(Ball + 0x94))), Z = unchecked((short)(old.Z + Word(Ball + 0x98))) } } });
                    Word(Ball + 0xB2, Word(Ball + 0xB2) - 1);
                    if (Word(Ball + 0xB2) <= 0)
                    {
                        Sequence(actor, Model(actor).SequenceState?.Default2 ?? throw new NotSupportedException("Native Dribble requires its return animation."));
                        Model(Model(actor) with { Transform = Transform(actor) with { Position28 = Position(Ball + 0x8C), Vertical34 = Word(Ball + 0x92) }, Rotation14 = (short)((Facing(actor) - 0x800) & 4095) });
                        Store(address, Next(Read(address)));
                    }
                    break;
                case 9:
                    Model(Model(actor) with { Flags0 = (ushort)(Model(actor).Flags0 ^ ((Model(actor).Flags0 ^ Word(Ball + 0xA4)) & 0x1000)) });
                    Store(address, Next(Read(address).WriteByte(0x26, (byte)(Read(address).ReadByte(0x26) | 1))));
                    break;
                case 10:
                    break;
                default:
                    throw new NotSupportedException("Native Dribble reached an unproved ball state.");
            }
            DrawBall(address, actor);
        }
    }
}
