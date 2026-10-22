using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleBlueCinematicPresentationTests
{
    [Fact]
    public void Original_selected_blue_cinematic_presents_declared_contact_and_cold_boundaries()
    {
        List<CallbackTrace> trace = [];
        NativeBattleRemainingCinematicPresentationTests.RunSelected([229, 230], "MEMORIA_FF8_BLUE_CINEMATIC", SelectOriginalFixture, CrossedContact, ObserveCallback, capturePeak: true);

        void ObserveCallback(BattleSnapshot before, BattleSnapshot after)
        {
            trace.Add(new(after.Frame, after.NativeGraphics!.Callback, ReadScript(before), ReadScript(after)));
            string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_BLUE_CINEMATIC_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the Blue Magic cinematic component.");
            File.WriteAllText(Path.Combine(output, "script-trace.json"), JsonSerializer.Serialize(trace));
        }
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        ushort action = effect switch { 229 => 12, 230 => 9, _ => throw new NotSupportedException("Select a supported Blue Magic cinematic component.") };
        BattleNativeAction[] crisis = assets.NativeActions.Where(row => row.Command == 15 && row.ActionId == action && row.MagicId == effect && row.KernelSection == 19 && row.KernelIndex == action).OrderBy(row => row.CrisisLevel).ToArray();
        Assert.Equal(4, crisis.Length);
        byte[] powers = effect == 229 ? [100, 150, 200, 250] : [70, 90, 100, 120];
        for (int index = 0; index < crisis.Length; index++)
        {
            Assert.Equal((byte?)(index + 1), crisis[index].CrisisLevel);
            Assert.Equal(powers[index], crisis[index].Power);
            Assert.Equal((byte?)255, crisis[index].Hit);
            Assert.Equal((ushort)0, crisis[index].Statuses0);
            Assert.Equal(0u, crisis[index].Statuses1);
            Assert.Equal(3, crisis[index].CharacterId);
        }
        BattleEncounter formation = assets.LoadEncounter(368);
        Assert.Equal(140, formation.StageId);
        BattleEnemyPlacement target = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(3, target.ModelId);
        Assert.Equal(new NativeBattleVector(300, 0, -2100), target.NativePosition);
        Assert.False(target.Hidden);
        Assert.False(target.InitiallySuspended);
        Assert.False(target.Untargetable);
        return new(effect == 229 ? "homing-laser229" : "fire-breath230", effect, 15, action, 3, 800, 240) { EncounterId = formation.Id, PartyModelId = 3, CasterActorIndex = 0, PreserveOriginalFormationEnemies = true, CrisisLevel = 1, ControlledContactValue = 7 };
    }

    private static bool CrossedContact(BattleSnapshot before, BattleSnapshot after)
    {
        ScriptTrace? current = ReadScript(after);
        if (current is null)
        {
            return false;
        }
        ScriptTrace? previous = ReadScript(before);
        SlotTrace? slot;
        if (current.Effect == 229)
        {
            slot = current.Slots.SingleOrDefault(item => item.Index == 0);
            if (slot is null || slot.Pc0 != current.File0Address + 0xCA || previous?.Slots.SingleOrDefault(item => item.Index == 0)?.Pc0 == slot.Pc0)
            {
                return false;
            }
        }
        else
        {
            slot = current.Slots.SingleOrDefault(item => item.Actor == 3 && item.Pc0 == current.File0Address + 0x56A && previous?.Slots.SingleOrDefault(prior => prior.Index == item.Index)?.Pc0 == current.File0Address + 0x204);
            if (slot is null)
            {
                return false;
            }
        }
        Assert.Equal((byte)3, slot.Actor);
        NativeBattleOriginalEffectState effect = after.NativeGraphics!.OriginalEffect!;
        int headerIndex = effect.Parameters?.HeaderIndex7C ?? throw new NotSupportedException("The contact boundary requires its captured physical header.");
        Assert.Equal(checked(0x1D280C4u + (uint)(headerIndex * 20)), current.HeaderAddress);
        NativeBattleActionHeaderSlot header = after.NativeActionPackets!.Headers[headerIndex];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("The captured header requires its physical contact pointer.");
        Assert.Equal(slot.Actor, after.NativeActionPackets.ReadContact(first).Primary.ActorIndex);
        NativeBattleEffectArena arena = after.NativeEffectMemory!.Arena!;
        int source = checked((int)(current.File0Address - 0x20DFAB8));
        if (current.Effect == 229)
        {
            Assert.Equal((ushort)0x64, Word(0xC4));
            Assert.Equal((ushort)0x22, Word(0xC6));
            Assert.Equal(9, Word(0xC8) & 0x1FF);
        }
        else
        {
            Assert.Equal((ushort)0x1009, Word(0x202));
            Assert.Equal((ushort)0x22, Word(0x204));
            Assert.Equal((ushort)2, Word(0x206));
            Assert.Equal((ushort)0x362, Word(0x208));
            Assert.Equal((ushort)0xF009, Word(0x568));
        }
        return true;

        ushort Word(int offset)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(source + offset, 2));
        }
    }

    private static ScriptTrace? ReadScript(BattleSnapshot snapshot)
    {
        if (snapshot.NativeEffectMemory?.Cinematic is not { Effect: 229 or 230 } selected || snapshot.NativeGraphics?.OriginalEffect is not { } effect || effect.Binding.Effect != selected.Effect)
        {
            return null;
        }
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(selected.Effect);
        NativeBattleScriptedEffectMemory core = snapshot.NativeEffectMemory.ScriptedEffects ?? throw new NotSupportedException("The active cinematic requires its current scripted-effect owner.");
        NativeBattleEffectArena arena = snapshot.NativeEffectMemory.Arena ?? throw new NotSupportedException("The active cinematic requires its current arena owner.");
        NativeBattleEffectResourcePointer source = Assert.Single(effect.ResourcePointers, item => item.GlobalAddress == profile.File0Publication);
        uint file0 = checked(0x20DFAB8u + (uint)source.ArenaOffset);
        uint manager = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(0x27973EC, 4));
        uint bank = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(manager + 0x90, 4));
        ImmutableArray<SlotTrace>.Builder slots = ImmutableArray.CreateBuilder<SlotTrace>();
        for (int cursor = 0; cursor < 128; cursor++)
        {
            byte entry = core.Read(0x2797454u + (uint)cursor, 1)[0];
            if (entry == 0xFF)
            {
                return new(selected.Effect, file0, BinaryPrimitives.ReadUInt32LittleEndian(core.Read(manager + 0xCC, 4)), slots.ToImmutable());
            }
            byte index = (byte)(entry & 0x7F);
            int offset = checked((int)(bank - 0x20DFAB8) + index * 0x100);
            uint pc0 = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
            uint pc1 = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset + 4, 4));
            uint pc2 = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset + 8, 4));
            slots.Add(new(index, arena.Read(offset + 0x1B, 1)[0], pc0, pc1, pc2, ReadOpcode(pc0), ReadOpcode(pc1), ReadOpcode(pc2)));
        }
        throw new NotSupportedException("The cinematic active-slot trace reached its bounded order without the native terminator.");

        ushort? ReadOpcode(uint pc)
        {
            long offset = (long)pc - 0x20DFAB8;
            return offset >= 0 && offset <= NativeBattleEffectArena.Length - 2 ? BinaryPrimitives.ReadUInt16LittleEndian(arena.Read((int)offset, 2)) : null;
        }
    }

    private sealed record CallbackTrace(long Frame, long NativeCallback, ScriptTrace? Before, ScriptTrace? After);
    private sealed record ScriptTrace(int Effect, uint File0Address, uint HeaderAddress, ImmutableArray<SlotTrace> Slots);
    private sealed record SlotTrace(byte Index, byte Actor, uint Pc0, uint Pc1, uint Pc2, ushort? Opcode0, ushort? Opcode1, ushort? Opcode2);
}
