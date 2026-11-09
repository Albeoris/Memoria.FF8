namespace Memoria.FF7.Events;

public sealed partial class FieldMachine
{
    private bool ExecuteHostOpcode(int id, byte opcode)
    {
        switch (opcode)
        {
            case 0x08:
                int[] joiningParty = ReadParty();
                return Host(id, new FieldPartyJoin(B(id, 1), joiningParty[0], joiningParty[1], joiningParty[2]), 2);
            case 0x09:
                int[] splittingParty = ReadParty();
                return Host(id, new FieldPartySplit(B(id, 14), splittingParty[0], splittingParty[1], splittingParty[2], R16(id, 1, 4), R16(id, 2, 6), R8(id, 3, 8), R16(id, 4, 9), R16(id, 5, 11), R8(id, 6, 13), new FieldByteOperand(Bank(id, 3), B(id, 8)), new FieldByteOperand(Bank(id, 6), B(id, 13))), 15);
            case 0x0D:
                return Host(id, new FieldLineCommand(FieldLineOperation.Slip, Value: B(id, 1)), 2);
            case 0x22:
                return Host(id, new FieldControlCommand(FieldControlKind.BattleMode2, W(id, 1), W(id, 3)), 5);
            case 0x23:
                return Host(id, new FieldControlCommand(FieldControlKind.BattleResult, 0), 3, result: value => Variables.WriteWord(Bank(id, 2), B(id, 2), unchecked((short)value)));
            case 0x28:
                int effectLength = B(id, 1);
                if (effectLength < 4)
                {
                    throw new InvalidDataException("FFVII KAWAI requires its native length, selector and payload.");
                }
                _ = B(id, effectLength - 1);
                return Actor(id, new FieldModelEffectCommand(B(id, 2), B(id, 3)), effectLength);
            case 0x29:
                return Actor(id, new FieldModelEffectCommand(13, Wait: true), 1);
            case 0x27:
                return Host(id, new FieldMovieCommand(FieldMovieOperation.SetBackground, B(id, 1)), 2);
            case 0x25:
                return Host(id, new FieldFadeCommand(B(id, 3), R8(id, 1, 4), R8(id, 2, 5), R8(id, 3, 6), R16(id, 4, 7), NewFade: true), 9);
            case 0x33:
                return Host(id, new FieldControlCommand(FieldControlKind.PlayerLocked, B(id, 1)), 2);
            case 0x40:
                return Host(id, new FieldDialogueCommand(B(id, 1), B(id, 2)), 3);
            case 0x43:
                return Host(id, new FieldControlCommand(FieldControlKind.CurrentMapName, B(id, 1)), 2);
            case 0x48:
                return Host(id, new FieldDialogueCommand(B(id, 2), B(id, 3), B(id, 4), B(id, 5), R8(id, 2, 6)), 7, result: value =>
                {
                    if (value < 0 || value > 255)
                    {
                        throw new InvalidDataException("Dialogue host returned an invalid native choice value.");
                    }
                    Variables.WriteByte(Bank(id, 2), B(id, 6), (byte)value);
                }, applyPendingResult: true);
            case 0x49:
                return Host(id, new FieldMenuCommand(B(id, 2), R8(id, 2, 3)), 4, firstCallYields: true);
            case 0x4A:
                return Host(id, new FieldControlCommand(FieldControlKind.MenuDisabled, B(id, 1)), 2);
            case 0x4B:
                return Host(id, new FieldControlCommand(FieldControlKind.BattleTable, B(id, 1)), 2);
            case 0x50:
                return Host(id, new FieldWindowCommand(FieldWindowOperation.Size, B(id, 1), S(id, 2), S(id, 4), S(id, 6), S(id, 8)), 10);
            case 0x51:
                return Host(id, new FieldWindowCommand(FieldWindowOperation.Move, B(id, 1), S(id, 2), S(id, 4)), 6);
            case 0x52:
                return Host(id, new FieldWindowCommand(FieldWindowOperation.Style, B(id, 1), Style: B(id, 2), Flags: B(id, 3)), 4);
            case 0x53:
                return Host(id, new FieldWindowCommand(FieldWindowOperation.Reset, B(id, 1)), 2);
            case 0x54:
                return Host(id, new FieldWindowCommand(FieldWindowOperation.Close, B(id, 1)), 2);
            case 0x2E:
                return Host(id, new FieldWindowCommand(FieldWindowOperation.CloseAndWait, B(id, 1)), 2);
            case 0x55:
                return Host(id, new FieldWindowCommand(FieldWindowOperation.Rows, B(id, 1), Height: (short)((B(id, 2) << 4) | 9)), 3);
            case 0x60:
                return Host(id, new FieldTransitionCommand(W(id, 1), S(id, 3), S(id, 5), W(id, 7), B(id, 9)), 10, firstCallYields: true);
            case 0x61:
                return Host(id, new FieldCameraCommand(FieldCameraOperation.Lock, B(id, 1)), 2);
            case 0x62:
                return Host(id, new FieldCameraCommand(FieldCameraOperation.Player, B(id, 4), Steps: R16(id, 2, 2)), 5);
            case 0x63:
                if (B(id, 4) >= entities.Length)
                {
                    throw new InvalidDataException("Camera targets absent entity.");
                }
                if (!entities[B(id, 4)].ModelBound)
                {
                    Advance(id, 6);
                    return false;
                }
                return Host(id, new FieldCameraCommand(FieldCameraOperation.Entity, B(id, 5), Steps: R16(id, 2, 2), Entity: B(id, 4)), 6);
            case 0x64:
                return Host(id, new FieldCameraCommand(FieldCameraOperation.Coordinates, 4, R16(id, 1, 2), R16(id, 2, 4)), 6);
            case 0x65:
                return Host(id, new FieldCameraCommand(FieldCameraOperation.ResetToPlayer), 1);
            case 0x66:
            case 0x68:
                return Host(id, new FieldCameraCommand(FieldCameraOperation.Coordinates, opcode == 0x66 ? 6 : 5, R16(id, 1, 3), R16(id, 2, 5), R16(id, 4, 7)), 9);
            case 0x67:
                return Host(id, new FieldCameraCommand(FieldCameraOperation.Wait), 1);
            case 0x69:
                return Host(id, new FieldControlCommand(FieldControlKind.MapDisplay, B(id, 1)), 2);
            case 0x6B:
                return Host(id, new FieldFadeCommand(B(id, 7), R8(id, 1, 3), R8(id, 2, 4), R8(id, 4, 5), B(id, 6), B(id, 8)), 9);
            case 0x6C:
                return Host(id, new FieldFadeCommand(0, Wait: true), 1);
            case 0x6D:
                return Host(id, new FieldTriangleCommand(W(id, 1), B(id, 3) != 0), 4);
            case 0x70:
                return Host(id, new FieldBattleCommand(unchecked((ushort)R16(id, 2, 2))), 4, firstCallYields: true);
            case 0x71:
                return Host(id, new FieldControlCommand(FieldControlKind.BattlesDisabled, B(id, 1)), 2);
            case 0x72:
                return Host(id, new FieldControlCommand(FieldControlKind.BattleMode, B(id, 1), B(id, 2)), 3);
            case 0x7E:
                return ActorProperty(id, FieldActorPropertyKind.TalkEnabled, B(id, 1) == 0 ? 1 : 0, 2);
            case 0x99:
                return Host(id, new FieldRandomCommand(), 3, result: value => Variables.WriteByte(Bank(id, 2), B(id, 2), checked((byte)value)));
            case 0xA0:
                if (!entities[id].ModelBound)
                {
                    throw new InvalidDataException("PC requires an allocated CHAR model.");
                }
                return Host(id, new FieldPartyCharacter(B(id, 1), Array.IndexOf(ReadParty(), (int)B(id, 1))), 2);
            case 0xA1:
                return Host(id, new FieldActorBind(B(id, 1)), 2, result: _ => entities[id].ModelBound = true);
            case 0xA2:
                if (B(id, 2) == 0)
                {
                    throw new InvalidDataException("Animation speed divisor is zero.");
                }
                return Actor(id, new FieldAnimation(B(id, 1), B(id, 2), false, false, true), 3, true);
            case 0xA3:
            case 0xAE:
            case 0xAF:
            case 0xBA:
                if (B(id, 2) == 0)
                {
                    throw new InvalidDataException("Animation speed divisor is zero.");
                }
                bool blocking = opcode is 0xA3 or 0xBA;
                return Actor(id, new FieldAnimation(B(id, 1), B(id, 2), blocking, opcode is 0xAF or 0xBA), 3, firstCallYields: blocking);
            case 0xA4:
                return ActorProperty(id, FieldActorPropertyKind.Visible, B(id, 1), 2);
            case 0xB0:
            case 0xB1:
            case 0xBB:
            case 0xBC:
                if (B(id, 4) == 0)
                {
                    throw new InvalidDataException("Partial animation speed divisor is zero.");
                }
                bool partialBlocking = opcode is 0xBB or 0xBC;
                return Actor(id, new FieldAnimation(B(id, 1), B(id, 4), partialBlocking, opcode is 0xB1 or 0xBC, FirstFrame: B(id, 2), LastFrame: B(id, 3), DivideFirstFrameBySpeed: opcode is 0xB0 or 0xBB), 5, firstCallYields: partialBlocking);
            case 0xA5:
                return Actor(id, new FieldActorPlacement(R16(id, 1, 3), R16(id, 2, 5), R16(id, 3, 7), R16(id, 4, 9)), 11, true, yieldWithoutModel: true);
            case 0xA6:
                return Actor(id, new FieldActorPlacement(R16(id, 1, 3), R16(id, 2, 5), null, R16(id, 3, 7)), 9, true, yieldWithoutModel: true);
            case 0xA7:
                return Actor(id, new FieldActorPlacement(R16(id, 1, 3), R16(id, 2, 5), R16(id, 3, 7), null), 9, true, yieldWithoutModel: true);
            case 0xA8:
                return Actor(id, new FieldActorMove(R16(id, 1, 2), R16(id, 2, 4)), 6, firstCallYields: true);
            case 0xAB:
                int faceEntity = B(id, 1);
                if (faceEntity >= entities.Length)
                {
                    throw new InvalidDataException("FFVII TURA references an absent target entity.");
                }
                if (!entities[faceEntity].ModelBound)
                {
                    Advance(id, 4);
                    return false;
                }
                return Actor(id, new FieldActorFace(faceEntity, B(id, 2), B(id, 3)), 4);
            case 0xAC:
                return ActorProperty(id, FieldActorPropertyKind.AnimationWait, 0, 1);
            case 0xB2:
                return ActorProperty(id, FieldActorPropertyKind.MoveSpeed, unchecked((ushort)(R16(id, 2, 2) * program.FieldScale / 512)), 4);
            case 0xB3:
                return ActorProperty(id, FieldActorPropertyKind.Direction, R8(id, 2, 2), 3, true);
            case 0xB4:
                return Actor(id, new FieldActorTurn(R8(id, 2, 2), B(id, 3), B(id, 4), B(id, 5)), 6, firstCallYields: true);
            case 0xB5:
                return Actor(id, new FieldActorTurn(R16(id, 2, 2), 2, B(id, 4), B(id, 5), true), 6, firstCallYields: true);
            case 0xB8:
                int positionEntity = B(id, 2);
                if (positionEntity >= entities.Length)
                {
                    throw new InvalidDataException("Actor coordinate query targets an absent entity.");
                }
                if (!entities[positionEntity].ModelBound)
                {
                    Advance(id, 5);
                    return false;
                }
                return Host(id, new FieldActorQuery(FieldActorQueryKind.Coordinates, positionEntity), 5, result: value =>
                {
                    Variables.WriteWord(Bank(id, 1), B(id, 3), unchecked((short)value));
                    Variables.WriteWord(Bank(id, 2), B(id, 4), unchecked((short)(value >> 16)));
                });
            case 0xB7:
            case 0xB9:
                int queryEntity = B(id, 2);
                if (queryEntity >= entities.Length)
                {
                    throw new InvalidDataException("Actor query targets absent entity.");
                }
                if (!entities[queryEntity].ModelBound)
                {
                    Advance(id, 4);
                    return false;
                }
                return Host(id, new FieldActorQuery(opcode == 0xB9 ? FieldActorQueryKind.Triangle : FieldActorQueryKind.Direction, queryEntity), 4, result: value =>
                {
                    if (opcode == 0xB9)
                    {
                        Variables.WriteWord(Bank(id, 2), B(id, 3), unchecked((short)value));
                    }
                    else
                    {
                        Variables.WriteByte(Bank(id, 2), B(id, 3), unchecked((byte)value));
                    }
                });
            case 0xBD:
                return ActorProperty(id, FieldActorPropertyKind.AnimationSpeed, R16(id, 2, 2), 4);
            case 0xBF:
                int playerEntity = B(id, 1);
                if (playerEntity >= entities.Length)
                {
                    throw new InvalidDataException("Player control targets absent entity.");
                }
                if (!entities[playerEntity].ModelBound)
                {
                    Advance(id, 2);
                    return false;
                }
                return Host(id, new FieldActorProperty(FieldActorPropertyKind.PlayerCharacter, B(id, 1)), 2);
            case 0xC0:
                return Actor(id, new FieldActorJump(R16(id, 1, 3), R16(id, 2, 5), R16(id, 3, 7), R16(id, 4, 9)), 11);
            case 0xC1:
                int positionTarget = B(id, 3);
                if (positionTarget >= entities.Length)
                {
                    throw new InvalidDataException("FFVII AXYZI references an absent target entity.");
                }
                if (!entities[positionTarget].ModelBound)
                {
                    Advance(id, 8);
                    return false;
                }
                return Host(id, new FieldActorQuery(FieldActorQueryKind.Position, positionTarget), 8, replyResult: reply =>
                {
                    FieldActorPosition position = reply.Position ?? throw new InvalidDataException("FFVII AXYZI host omitted its native coordinate result.");
                    Variables.WriteWord(Bank(id, 1), B(id, 4), position.X);
                    Variables.WriteWord(Bank(id, 2), B(id, 5), position.Y);
                    Variables.WriteWord(Bank(id, 3), B(id, 6), position.Z);
                    Variables.WriteWord(Bank(id, 4), B(id, 7), position.Triangle);
                });
            case 0xC2:
                return Actor(id, new FieldActorLadder(R16(id, 1, 3), R16(id, 2, 5), R16(id, 3, 7), R16(id, 4, 9), B(id, 11), B(id, 12), B(id, 13), B(id, 14)), 15);
            case 0xC3:
                return Actor(id, new FieldActorOffset(R16(id, 1, 4), R16(id, 2, 6), R16(id, 3, 8), B(id, 3), R16(id, 4, 10)), 12);
            case 0xC4:
                return ActorProperty(id, FieldActorPropertyKind.OffsetWait, 0, 1);
            case 0xC5:
                return ActorProperty(id, FieldActorPropertyKind.TalkRadius, unchecked((ushort)(R8(id, 2, 2) * program.FieldScale / 512)), 3);
            case 0xC6:
                return ActorProperty(id, FieldActorPropertyKind.CollisionRadius, unchecked((ushort)(R8(id, 2, 2) * program.FieldScale / 512)), 3);
            case 0xC7:
                return ActorProperty(id, FieldActorPropertyKind.CollisionEnabled, B(id, 1) == 0 ? 1 : 0, 2);
            case 0xC8:
            case 0xC9:
            case 0xCA:
            case 0x0A:
                return ChangeParty(id, opcode);
            case 0x0B:
                int[] party = ReadParty();
                for (int slot = 0; slot < 3; slot++)
                {
                    Variables.WriteByte(Bank(id, slot + 1), B(id, slot + 3), (byte)party[slot]);
                }
                Advance(id, 6);
                return false;
            case 0xCB:
                Advance(id, ReadParty().Contains((int)B(id, 1)) ? 3 : B(id, 2) + 2);
                return false;
            case 0xCC:
                return Host(id, new FieldPartyCommand(FieldPartyOperation.Available, B(id, 1)), 0, result: value => Advance(id, value != 0 ? 3 : B(id, 2) + 2));
            case 0xCD:
                return Host(id, new FieldPartyCommand(FieldPartyOperation.SetAvailable, B(id, 2), B(id, 1)), 3);
            case 0xCE:
            case 0xCF:
                return Host(id, new FieldPartyCommand(FieldPartyOperation.SetLocked, B(id, 1), opcode == 0xCE ? 1 : 0), 2);
            case 0xD0:
                return Host(id, new FieldLineCommand(FieldLineOperation.Define, S(id, 1), S(id, 3), S(id, 5), S(id, 7), S(id, 9), S(id, 11)), 13);
            case 0xD1:
                return Host(id, new FieldLineCommand(FieldLineOperation.Enable, Value: B(id, 1)), 2);
            case 0xD3:
                return Host(id, new FieldLineCommand(FieldLineOperation.Replace, R16(id, 1, 4), R16(id, 2, 6), R16(id, 3, 8), R16(id, 4, 10), R16(id, 5, 12), R16(id, 6, 14)), 16);
            case 0xD2:
                return Host(id, new FieldControlCommand(FieldControlKind.MapJumpDisabled, B(id, 1)), 2);
            case 0xD8:
                return Host(id, new FieldPreloadCommand(W(id, 1)), 3);
            case 0xD9:
                return Host(id, new FieldPreloadCommand(Wait: true), 1);
            case >= 0xE0 and <= 0xE4:
                FieldBackgroundOperation operation = opcode switch
                {
                    0xE0 => FieldBackgroundOperation.On,
                    0xE1 => FieldBackgroundOperation.Off,
                    0xE2 => FieldBackgroundOperation.Roll,
                    0xE3 => FieldBackgroundOperation.RollBack,
                    _ => FieldBackgroundOperation.Clear
                };
                return Host(id, new FieldBackgroundCommand(operation, R8(id, opcode < 0xE2 ? 1 : 2, 2), opcode < 0xE2 ? R8(id, 2, 3) : 0), opcode < 0xE2 ? 4 : 3);
            case 0xE5:
            case 0xE6:
            case 0xE7:
                return Host(id, new FieldPaletteCommand(opcode == 0xE5 ? FieldPaletteOperation.Store : opcode == 0xE6 ? FieldPaletteOperation.Load : FieldPaletteOperation.Copy, R8(id, 1, 2), R8(id, 2, 3), B(id, 4) + 1), 5);
            case 0xEB:
            case 0xEC:
                return Host(id, new FieldPaletteCommand(opcode == 0xEB ? FieldPaletteOperation.Store : FieldPaletteOperation.Load, B(id, 1), B(id, 2), B(id, 4) + 1, First: B(id, 3)), 5);
            case 0xE9:
            case 0xEA:
                return Host(id, new FieldPaletteCommand(opcode == 0xE9 ? FieldPaletteOperation.Add : FieldPaletteOperation.Multiply, R8(id, 1, 4), R8(id, 2, 5), B(id, 9) + 1, R8(id, 5, 8), R8(id, 4, 7), R8(id, 3, 6)), 10);
            case 0xDD:
                return ActorProperty(id, FieldActorPropertyKind.StopAnimation, 0, 1);
            case 0xDA:
                return Host(id, new FieldAudioCommand(B(id, 4), R16(id, 1, 5), R16(id, 2, 7), R16(id, 3, 9), R16(id, 4, 11), R16(id, 6, 13)), 15);
            case 0xF0:
                return Host(id, new FieldMusicCommand(FieldMusicOperation.Play, B(id, 1)), 2);
            case 0xF3:
                return Host(id, new FieldMusicCommand(FieldMusicOperation.SaveCurrentAndPlay, B(id, 1)), 2);
            case 0xF4:
                return Host(id, new FieldMusicCommand(FieldMusicOperation.SwapSaved, B(id, 1)), 2);
            case 0xF5:
                return Host(id, new FieldMusicCommand(FieldMusicOperation.Lock, B(id, 1)), 2);
            case 0xF6:
                return Host(id, new FieldMusicCommand(FieldMusicOperation.NextBattle, B(id, 1)), 2);
            case 0xFC:
                return Host(id, new FieldMusicCommand(FieldMusicOperation.NextField, B(id, 1)), 2);
            case 0xF1:
                return Host(id, new FieldAudioCommand(0x20, R8(id, 2, 4), R16(id, 1, 2)), 5);
            case 0xF2:
                return Host(id, new FieldAudioCommand(B(id, 4), R8(id, 1, 5), R16(id, 2, 6), R16(id, 3, 8), R16(id, 4, 10), R16(id, 6, 12)), 14);
            case 0xF8:
                return Host(id, new FieldMovieCommand(FieldMovieOperation.Prepare, B(id, 1)), 2, firstCallYields: true);
            case 0xF9:
                return Host(id, new FieldMovieCommand(FieldMovieOperation.Play), 1, firstCallYields: true);
            case 0xFA:
                return Host(id, new FieldMovieCommand(FieldMovieOperation.ReadFrame), 3, result: value => Variables.WriteWord(Bank(id, 2), B(id, 2), unchecked((short)value)));
            default:
                throw new NotSupportedException("Opcode is outside the proved command subset.");
        }
    }

    private bool ActorProperty(int id, FieldActorPropertyKind property, int value, int length, bool yieldAfter = false)
    {
        return Actor(id, new FieldActorProperty(property, value), length, yieldAfter);
    }

    private bool Actor(int id, FieldCommand command, int length, bool yieldAfter = false, bool firstCallYields = false, bool yieldWithoutModel = false)
    {
        if (!entities[id].ModelBound)
        {
            Advance(id, length);
            return yieldWithoutModel;
        }
        return Host(id, command, length, yieldAfter, firstCallYields);
    }
}
