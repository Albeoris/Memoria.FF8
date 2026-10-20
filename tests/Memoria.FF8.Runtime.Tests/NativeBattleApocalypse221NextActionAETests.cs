using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleApocalypse221NextActionAETests
{
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 1, true)]
    [InlineData(200, 201, true)]
    [InlineData(200, 127, false)]
    [InlineData(254, 255, true)]
    [InlineData(255, 255, false)]
    public void Unsigned_index_either_jumps_from_opcode_or_increments_before_capture_and_advances_current_PC(byte index, byte limit, bool capture)
    {
        const uint basis = NativeBattleEffectNodeMemory.ArenaAddress;
        const uint manager = 0x2797690;
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0xAE);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, basis + 0x1000);
        frame = frame with { Core = frame.Core.Write(manager + 0xD0, new[] { index, limit }), Arena = frame.Arena.Write(0x1002, BitConverter.GetBytes((short)-6)) };
        if (capture)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        }
        int calls = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicNextActionAE.Execute(frame, new(0x1000, 4, basis + 0x1000), owner =>
        {
            calls++;
            Assert.Equal(unchecked((byte)(index + 1)), owner.Core.Read(manager + 0xD0, 1)[0]);
            Assert.Equal(basis + 0x1000, WaterSchedulerFixture.ReadCoreDword(owner, 0x2797450));
            return WaterSchedulerFixture.CoreDword(owner, 0x2797450, basis + 0x1008);
        });
        Assert.Equal(capture ? 1 : 0, calls);
        Assert.Equal(capture ? basis + 0x100C : basis + 0xFFA, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(capture ? unchecked((byte)(index + 1)) : index, result.Core.Read(manager + 0xD0, 1)[0]);
        Assert.Equal(frame.Random, result.Random);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Capture_uses_selected_action_contacts_and_actual_seven_flags_without_recaching_positions(bool contactsPresent)
    {
        const uint basis = NativeBattleEffectNodeMemory.ArenaAddress;
        const uint header = NativeBattleScriptedEffectMemory.Address;
        const uint manager = 0x2797690;
        const uint parameters = 0x1D99A78;
        const uint first = 0x1D280C4 + 7 * 20;
        const uint selected = first + 2 * 20;
        const uint firstContacts = 0x1D29000;
        const uint contacts = 0x1D29100;
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A6C, basis + 0x2000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973E8, basis + 0x2100);
        frame = WaterSchedulerFixture.CoreDword(frame, manager + 0xC0, parameters);
        frame = WaterSchedulerFixture.CoreWord(frame, header + 0x38, 4);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x1B, 5);
        frame = frame with { Core = frame.Core.Write(manager + 0xD0, new byte[] { 2 }).Write(header + 0xCC, Enumerable.Repeat((byte)0xA5, 7).ToArray()).Write(header + 0x84, Enumerable.Repeat((byte)0xA5, 56).ToArray()) };
        ImmutableArray<NativeBattleGraphicsModel> models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, (ushort)(0x8100 + index))).ToImmutableArray();
        Dictionary<uint, byte[]> owners = new()
        {
            [parameters] = new byte[12],
            [first] = new byte[20],
            [selected] = new byte[20],
            [firstContacts] = new byte[24],
            [contacts] = new byte[96]
        };
        owners[parameters][0] = 5;
        BinaryPrimitives.WriteUInt32LittleEndian(owners[parameters].AsSpan(4), first);
        BinaryPrimitives.WriteUInt32LittleEndian(owners[first].AsSpan(8), firstContacts);
        owners[firstContacts][0] = 2;
        BinaryPrimitives.WriteUInt32LittleEndian(owners[selected].AsSpan(8), contacts);
        owners[selected][0x10] = contactsPresent ? (byte)4 : (byte)0;
        byte[] actors = [3, 1, 3, 6];
        for (int index = 0; index < actors.Length; index++)
        {
            owners[contacts][24 * index] = actors[index];
        }
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicNextActionAE.CaptureCurrent(frame, 0x400, (_, address, length) =>
        {
            KeyValuePair<uint, byte[]> owner = owners.Single(row => address >= row.Key && address - row.Key <= row.Value.Length - length);
            return owner.Value.AsSpan(checked((int)(address - owner.Key)), length).ToArray();
        }, models);
        Assert.Equal(selected, WaterSchedulerFixture.ReadCoreDword(result, manager + 0xCC));
        Assert.Equal((byte)5, result.Core.Read(header + 0x44, 1)[0]);
        Assert.Equal(contactsPresent ? (byte)3 : (byte)0, result.Core.Read(header + 0x41, 1)[0]);
        Assert.Equal(contactsPresent ? (byte)3 : (byte)2, result.Arena.Read(0x211B, 1)[0]);
        Assert.Equal((byte)5, result.Arena.Read(0x201B, 1)[0]);
        if (contactsPresent)
        {
            Assert.Equal(new byte[] { 3, 1, 6 }, result.Core.Read(header + 0x48, 3));
            Assert.Equal((byte)0, result.Core.Read(header + 0xCF, 1)[0]);
            Assert.Equal((byte)1, result.Core.Read(header + 0xCD, 1)[0]);
            Assert.Equal((byte)2, result.Core.Read(header + 0xD2, 1)[0]);
        }
        Assert.Equal((byte)0xA5, result.Core.Read(header + 0xCE, 1)[0]);
        for (int index = 0; index < models.Length; index++)
        {
            Assert.Equal(models[index].Flags0, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(header + 0x4Eu + (uint)(index * 2), 2)));
        }
        Assert.Equal(frame.Core.Read(header + 0x84, 56), result.Core.Read(header + 0x84, 56));
        Assert.Equal(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(frame.Random, result.Random);
    }
}
