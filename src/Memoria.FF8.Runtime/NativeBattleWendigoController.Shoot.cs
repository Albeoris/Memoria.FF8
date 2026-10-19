namespace Memoria.FF8.Runtime;

public static partial class NativeBattleWendigoController
{
    private sealed partial class Execution
    {
        private void ShootBall(uint address)
        {
            int actor = 1;
            switch (Stage(Read(address)))
            {
                case 0:
                    InitializeBall(address, actor);
                    Word(Ball + 0xD8, 1);
                    Word(Ball + 0xDA, 0);
                    Word(Ball + 0xDC, 2);
                    Position(Ball + 0xB4, Bone(0, 0xF1));
                    Position(Ball + 0xBC, Bone(2, 0xF1));
                    Pivot(actor);
                    Radii(address);
                    Placement(address);
                    KickEffects(address);
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
                        Word(Ball + 0xC8, Math.Min(4096, Word(Ball + 0xC8) + 512));
                        if (Word(Ball + 0xC8) == 4096)
                        {
                            Store(address, Next(Read(address)));
                        }
                    }
                    Radii(address);
                    Placement(address);
                    if (stage >= 3)
                    {
                        KickEffects(address);
                    }
                    if (stage == 4 && Signed(Read(address), 0x24) >= 39)
                    {
                        PassStep();
                        Store(address, Next(Read(address)));
                    }
                    break;
                case 5:
                    Pivot(actor);
                    Radii(address);
                    for (uint axis = 0; axis < 6; axis += 2)
                    {
                        Word(Ball + 0x7C + axis, Word(Ball + 0x7C + axis) + Word(Ball + 0x94 + axis));
                    }
                    Word(Ball + 0xD2, Word(Ball + 0xD2) - 1);
                    if (Word(Ball + 0xD2) <= 0 && PassStep())
                    {
                        Sequence(actor, 6);
                        NativeBattleVector shot = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleCuragaMatrices.RotateY8DD8A0(Identity(), Facing(actor)), new(0, 0, 0xC00));
                        Word(Ball + 0x94, (unchecked((short)(shot.X + Word(Ball + 0x8C))) - Word(Ball + 0x7C)) / 20);
                        Word(Ball + 0x98, (unchecked((short)(shot.Z + Word(Ball + 0x90))) - Word(Ball + 0x80)) / 20);
                        Word(Ball + 0x96, -512);
                        Word(Ball + 0xD2, 0);
                        Word(Ball + 0xDE, 3);
                        Store(address, Next(Read(address)));
                    }
                    KickEffects(address);
                    break;
                case 6:
                    Pivot(actor);
                    Radii(address);
                    int flight = Word(Ball + 0xD2);
                    if (flight <= 16)
                    {
                        Word(Ball + 0x9C, flight * -256 & 4095);
                        if (Word(Ball + 0xD4) == 1)
                        {
                            Word(Ball + 0x9E, (16 - flight) << 7);
                        }
                    }
                    Word(Ball + 0xC8, Math.Max(0, Word(Ball + 0xC8) - 256));
                    Word(Ball + 0x7C, Word(Ball + 0x7C) + Word(Ball + 0x94));
                    Word(Ball + 0x80, Word(Ball + 0x80) + Word(Ball + 0x98));
                    Word(Ball + 0x96, Word(Ball + 0x96) + 48);
                    Word(Ball + 0x7E, Word(Ball + 0x7E) + Word(Ball + 0x96));
                    if (Word(Ball + 0x96) > 0 && Word(Ball + 0x7E) >= Word(Ball + 0x76))
                    {
                        Sequence(actor, 6);
                        Word(Ball + 0x7E, Word(Ball + 0x76));
                        uint dust = Append(3, 0x8459A0, TaskSize, address);
                        NativeBattleVector p = Transform(actor).Position28;
                        Store(dust, Read(dust).WriteWord(0x1C, unchecked((ushort)p.X)).WriteWord(0x1E, 0).WriteWord(0x20, unchecked((ushort)p.Z)).WriteWord(0x22, unchecked((ushort)Transform(actor).Vertical34)));
                        Emit(new NativeBattleWendigoSound(resources.LandSound, 1));
                        Store(address, Next(Read(address)));
                    }
                    KickEffects(address);
                    Word(Ball + 0xD2, Word(Ball + 0xD2) + 1);
                    break;
                case 7:
                    Store(address, Next(Read(address).WriteByte(0x26, (byte)(Read(address).ReadByte(0x26) & ~8))));
                    Model(Model(actor) with { Flags0 = (ushort)(Model(actor).Flags0 & ~4), Transform = Transform(actor) with { Position28 = Transform(actor).Position28 with { Y = Transform(actor).Angle36 } } });
                    break;
                case 8:
                    Dword(Ball + 0xC4, Model(actor).Flags0);
                    Model(Model(actor) with { Flags0 = (ushort)(Model(actor).Flags0 | 0x1000) });
                    if (Model(actor).Sequence != 6)
                    {
                        Sequence(actor, 0x1A);
                        Model(Model(actor) with { Rotation14 = (short)((Facing(actor) - 0x800) & 4095) });
                        Word(Ball + 0xD2, 16);
                        Word(Ball + 0x94, (Word(Ball + 0x8C) - Transform(actor).Position28.X) / 16);
                        Word(Ball + 0x98, (Word(Ball + 0x90) - Transform(actor).Position28.Z) / 16);
                        Store(address, Next(Read(address)));
                    }
                    break;
                case 9:
                    NativeBattleVector old = Transform(actor).Position28;
                    Model(Model(actor) with { Transform = Transform(actor) with { Position28 = old with { X = unchecked((short)(old.X + Word(Ball + 0x94))), Z = unchecked((short)(old.Z + Word(Ball + 0x98))) } } });
                    Word(Ball + 0xD2, Word(Ball + 0xD2) - 1);
                    if (Word(Ball + 0xD2) <= 0)
                    {
                        Sequence(actor, Model(actor).SequenceState?.Default2 ?? throw new NotSupportedException("Native Shoot requires its return animation."));
                        Model(Model(actor) with { Transform = Transform(actor) with { Position28 = Position(Ball + 0x8C), Vertical34 = Word(Ball + 0x92) }, Rotation14 = (short)((Facing(actor) - 0x800) & 4095) });
                        Store(address, Next(Read(address)));
                    }
                    break;
                case 10:
                    Model(Model(actor) with { Flags0 = (ushort)(Model(actor).Flags0 ^ ((Model(actor).Flags0 ^ Word(Ball + 0xC4)) & 0x1000)) });
                    Store(address, Next(Read(address).WriteByte(0x26, (byte)(Read(address).ReadByte(0x26) | 1))));
                    break;
                case 11:
                    break;
                default:
                    throw new NotSupportedException("Native Shoot reached an unproved ball state.");
            }
            DrawBall(address, actor);
        }
    }
}
