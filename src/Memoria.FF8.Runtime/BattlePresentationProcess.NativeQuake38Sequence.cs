using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private sealed partial class NativeQuake38Execution
    {
        private bool TargetsParty => state.Word(0x278C9B0) != 0;
        private BattleSide FadedSide => TargetsParty ? BattleSide.Enemies : BattleSide.Allies;
        private BattleSide TargetSide => TargetsParty ? BattleSide.Allies : BattleSide.Enemies;
        private bool UsesOriginalModelCells => graphics.Effects.StorageContract == BattleContactTaskStorageContract.OriginalArena;
        private NativeBattleStageColors Colors => session.NativeStage?.ModelColors ?? throw new NotSupportedException("Native Quake requires its current original stage model colors.");

        private void Sequence(uint address, ref NativeBattleModulePoolTask task)
        {
            switch (unchecked((sbyte)task.ReadByte(41)))
            {
                case 0:
                    InitializeGround(address, task);
                    task = Next(task);
                    break;
                case 1:
                    DrawGround();
                    task = Next(task.WriteWord(48, 16));
                    break;
                case 2:
                    DrawGround();
                    task = SetWord(task, 48, Word(task, 48) - 1);
                    if (Word(task, 48) < 0)
                    {
                        task = Next(task);
                    }
                    break;
                case 3:
                    CaptureFlags();
                    task = task.WriteWord(28, 0);
                    UpdateModels(FadedSide, true, (model, _, _, _) => model with { Flags0 = (ushort)(model.Flags0 | 0x800), Color40 = (Colors.Color40 & 0xFFFFFFu) | 0x02000000u });
                    DrawGround();
                    task = Next(task);
                    break;
                case 4:
                    task = SetWord(task, 28, Word(task, 28) + 512);
                    if (Word(task, 28) >= 4096)
                    {
                        task = task.WriteWord(28, 4096);
                        state = state.SetWord(0x2792E20, 1);
                        Sound(resources.RumbleSound, 0);
                        task = Next(task);
                    }
                    Fade(Word(task, 28));
                    DrawGround();
                    break;
                case 5:
                    UpdateModels(FadedSide, true, (model, _, _, _) => model with { Flags0 = (ushort)(model.Flags0 | 4) });
                    UpdateModels(TargetSide, true, (model, _, _, _) => model with { Flags0 = (ushort)(model.Flags0 | 0x1020) });
                    task = Next(task);
                    DrawGround();
                    break;
                case 6:
                    if (state.Word(0x279299E) == 5)
                    {
                        CaptureTransforms();
                        Sound(resources.QuakeSound, 1);
                        task = Next(task);
                    }
                    DrawGround();
                    break;
                case 7:
                    BounceTargets(task);
                    DrawGround();
                    if (state.Word(0x279299E) == 7)
                    {
                        RestoreTransforms();
                        task = Next(task);
                    }
                    break;
                case 8:
                    task = task.WriteWord(28, 4096);
                    UpdateModels(null, false, (model, side, gate, restore) => model with { Flags0 = NativeBattleQuake38ActorFlags.Restore8DBE80(side == BattleSide.Allies, model.Flags0, gate, restore, TargetsParty) });
                    task = Next(task);
                    break;
                case 9:
                    task = SetWord(task, 28, Word(task, 28) - 256);
                    if (Word(task, 28) <= 0)
                    {
                        task = Next(task.WriteWord(28, 0));
                    }
                    Fade(Word(task, 28));
                    break;
                case 10:
                    UpdateModels(FadedSide, true, (model, side, gate, restore) => model with { Flags0 = NativeBattleQuake38ActorFlags.Restore8DC180(side == BattleSide.Allies, model.Flags0, gate, restore, TargetsParty), Color40 = Colors.Color40, Color44 = Colors.Color44 });
                    task = Next(task.WriteByte(38, (byte)(task.ReadByte(38) | 1)));
                    break;
                case 11:
                    break;
                default:
                    throw new NotSupportedException("Native Quake sequence phase leaves its original table.");
            }
        }

        private void VisitModels(BattleSide? side, Action<BattleModelOwner, NativeBattleModelSlot?> bound, Action<NativeBattleModelSlot> unbound)
        {
            if (UsesOriginalModelCells)
            {
                for (byte operand = 0; operand < NativeBattleModelBoundary.CellCount; operand++)
                {
                    NativeBattleModelSlot slot = NativeBattleModelSlot.Decode(operand);
                    if (side is not null && side != slot.Side)
                    {
                        continue;
                    }
                    if (graphics.NativeBindings.FindOriginalOwner(slot, graphics.ActorModels) is { } owner)
                    {
                        bound(owner, slot);
                    }
                    else
                    {
                        unbound(slot);
                    }
                }
                return;
            }
            foreach (BattleModelOwner owner in graphics.ActorModels.Owners)
            {
                if (side is null || side == owner.Side)
                {
                    bound(owner, null);
                }
            }
        }

        private void UpdateModels(BattleSide? side, bool excludeFlagged, Func<NativeBattleGraphicsModel, BattleSide, uint, uint, NativeBattleGraphicsModel> update)
        {
            VisitModels(side, (owner, slot) =>
            {
                uint gate = GateFlags(owner, slot);
                if (!excludeFlagged || (gate & 4) == 0)
                {
                    WriteModel(owner, update(graphics.ActorModels.Get(owner), owner.Side, gate, RestoreFlags(gate, slot)));
                }
            }, slot =>
            {
                uint gate = ReadOriginalFlags(slot);
                if (!excludeFlagged || (gate & 4) == 0)
                {
                    WriteCell(slot, update(ReadCell(slot), slot.Side, gate, RestoreFlags(gate, slot)));
                }
            });
        }

        private void CaptureFlags()
        {
            var flags = ImmutableDictionary.CreateBuilder<BattleModelOwner, ushort>();
            VisitModels(null, (owner, slot) =>
            {
                ushort current = graphics.ActorModels.Get(owner).Flags0;
                if (slot is { } original)
                {
                    state.SetDword(FlagAddress(original), current);
                }
                else
                {
                    flags.Add(owner, current);
                }
            }, slot => state.SetDword(FlagAddress(slot), ReadCell(slot).Flags0));
            state.SetSavedActorFlags(flags.ToImmutable());
        }

        private void CaptureTransforms()
        {
            var models = ImmutableDictionary.CreateBuilder<BattleModelOwner, NativeBattleQuake38SavedActor>();
            var cells = ImmutableDictionary.CreateBuilder<NativeBattleModelSlot, NativeBattleQuake38SavedActor>();
            VisitModels(null, (owner, _) => models.Add(owner, NativeBattleQuake38SavedActor.Capture8DBBA0(graphics.ActorModels.Get(owner))),
                slot => cells.Add(slot, NativeBattleQuake38SavedActor.Capture8DBBA0(ReadCell(slot))));
            state.SetSavedActors(models.ToImmutable());
            state.SetSavedUnboundCells(cells.ToImmutable());
        }

        private void RestoreTransforms()
        {
            VisitModels(null, (owner, _) => WriteModel(owner, state.ReadSavedActor(owner).Restore8DBE40(graphics.ActorModels.Get(owner))),
                slot => WriteCell(slot, state.ReadSavedUnboundCell(slot).Restore8DBE40(ReadCell(slot))));
        }

        private void BounceTargets(NativeBattleModulePoolTask task)
        {
            NativeBattleActionHeaderSlot header = Header(task);
            BattleContactCursor first = header.FirstContact;
            VisitModels(TargetSide, (owner, slot) =>
            {
                if ((GateFlags(owner, slot) & 4) != 0)
                {
                    return;
                }
                bool hit = Enumerable.Range(0, header.ReadContactCount()).Any(index => session.nativeActionPackets.ReadContact(first.Advance(index)).Primary.Recipient == owner);
                NativeBattleQuake38ActorBounceStep bounce = NativeBattleQuake38ActorBounce.Advance8DBCA0(graphics.ActorModels.Get(owner), state.ReadSavedActor(owner), state, resources, RandomState, hit);
                RandomState = bounce.Random;
                WriteModel(owner, bounce.Model);
                if (bounce.ModelSequence is { } sequence && NativeBattleSequenceSelections.CanSelectReady505CE0(bounce.Model))
                {
                    graphics = session.SelectNativeHeaderSequence(graphics, owner, sequence);
                }
                state.SetSavedActor(owner, bounce.Saved);
            }, slot =>
            {
                if ((ReadOriginalFlags(slot) & 4) != 0)
                {
                    return;
                }
                NativeBattleQuake38ActorBounceStep bounce = NativeBattleQuake38ActorBounce.Advance8DBCA0(ReadCell(slot), state.ReadSavedUnboundCell(slot), state, resources, RandomState, false);
                RandomState = bounce.Random;
                WriteCell(slot, bounce.Model);
                state.UpdateSavedUnboundCell(slot, bounce.Saved);
            });
        }

        private void WriteModel(BattleModelOwner owner, NativeBattleGraphicsModel value)
        {
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(owner, value) };
        }

        private NativeBattleGraphicsModel ReadCell(NativeBattleModelSlot slot) => graphics.NativeBindings.ReadOriginalCell(slot, graphics.ActorModels);

        private void WriteCell(NativeBattleModelSlot slot, NativeBattleGraphicsModel value)
        {
            graphics = graphics with { NativeBindings = graphics.NativeBindings.WriteUnboundCell(slot, value) };
        }

        private static uint FlagAddress(NativeBattleModelSlot slot) => 0x278C8F0u + 4u * slot.Offset;

        private uint ReadOriginalFlags(NativeBattleModelSlot slot) => state.Dword(FlagAddress(slot));

        private uint GateFlags(BattleModelOwner owner, NativeBattleModelSlot? slot) => slot is { } original ? ReadOriginalFlags(original) : state.ReadSavedActorFlags(owner);

        private uint RestoreFlags(uint gateFlags, NativeBattleModelSlot? slot)
        {
            return slot is { Side: BattleSide.Enemies } enemy ? ReadOriginalFlags(NativeBattleModelSlot.Decode((byte)(enemy.Offset - 3))) : gateFlags;
        }

        private void InitializeGround(uint address, NativeBattleModulePoolTask task)
        {
            state = state.SetDword(0x27929C8, address).SetWord(0x278C9B0, (short)(task.ReadExecutionReference<BattleExecutionReference.Model>(45, 1).Owner.Side == BattleSide.Allies ? 1 : 0));
            foreach (uint field in new uint[] { 0x2792E06, 0x2792E2E })
            {
                state = state.SetWord(field, -8192);
            }
            foreach (uint field in new uint[] { 0x278C8E8, 0x278C8E6, 0x2792E04, 0x2792E2C })
            {
                state = state.SetWord(field, 8192);
            }
            foreach (uint field in new uint[] { 0x2792D6E, 0x279299E, 0x2792E20, 0x278C8EA, 0x2792D70, 0x2792B40, 0x279299C, 0x2792D6C, 0x2792E18, 0x2792D6A })
            {
                state = state.SetWord(field, 0);
            }
            Memory.Arena!.Write(0x28000, new byte[0xB600]).Write(0x33600, new byte[0xCC0]);
            state = state.Write(0x27929A0, NativeBattleQuake38Templates.Crack8D88C0(state.Read(0x27929A0, 40)).AsSpan());
            state = state.Write(0x2792E38, NativeBattleQuake38Templates.Wall8D88C0(state.Read(0x2792E38, 40)).AsSpan());
        }

        private void Fade(int fade)
        {
            UpdateModels(FadedSide, true, (model, _, _, _) => model with { Color40 = FadeRgb(model.Color40 ?? throw new NotSupportedException("Native Quake fade requires its current actor transition color."), Colors.Color40, fade), Color44 = FadeRgb(model.Color44 ?? throw new NotSupportedException("Native Quake fade requires its current actor normal color."), Colors.Color44, fade) });
        }

        private static uint FadeRgb(uint current, uint original, int fade)
        {
            uint result = current & 0xFF000000;
            for (int shift = 0; shift < 24; shift += 8)
            {
                int color = (byte)(original >> shift);
                result |= (uint)(color - fade * color / 4096) << shift;
            }
            return result;
        }
    }
}
