using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterInitializationResource(int ArenaOffset, int Length, uint Address);
public sealed record NativeBattleWaterTemporaryFrame(uint Address, ImmutableArray<byte> Bytes, ImmutableArray<bool> Known);
public sealed record NativeBattleWaterInitializationInputs(NativeBattleWaterInitializationResource Resource0, NativeBattleWaterInitializationResource Resource1, NativeBattleWaterTemporaryFrame TemporaryFrame, uint ActionParametersAddress, uint HeaderBaseAddress, uint InitializationDirectoryAddress, byte Caster, int CurrentHeaderIndex, byte FirstHeaderFirstActor, ImmutableArray<byte> Contacts, byte ContactCount, byte FirstHeaderCount, ImmutableArray<NativeBattleGraphicsModel> Graphics);
public sealed record NativeBattleWaterResetCaptureInputs(byte Caster, int CurrentHeaderIndex, byte FirstHeaderFirstActor, ImmutableArray<byte> Contacts, byte ContactCount, ImmutableArray<NativeBattleGraphicsModel> Graphics);
public sealed record NativeBattleWaterResetResult(NativeBattleScriptedEffectMemory Core, NativeBattleEffectArena Arena, NativeBattleWaterTemporaryFrame TemporaryFrame, NativeBattleWaterSlotBank SlotBank, int CurrentHeaderIndex);
public sealed record NativeBattleWaterInitialPoolRequest(uint DescriptorAddress, uint BankAddress, int Stride, int Capacity, uint Callback);
public sealed record NativeBattleWaterInitializationResult(NativeBattleScriptedEffectMemory Core, NativeBattleEffectArena Arena, NativeBattleWaterSlotBank SlotBank, NativeBattleWaterTemporaryFrame TemporaryFrame, int CurrentHeaderIndex, NativeBattleWaterInitialPoolRequest PoolRequest);

/// <summary>The exact initializer over explicit current resources and a caller-bound, transient temporary frame.</summary>
public static class NativeBattleWaterInitialization
{
    public static NativeBattleWaterInitializationResult InitializeA9B200(NativeBattleScriptedEffectMemory core, NativeBattleEffectArena arena, NativeBattleWater222Profile profile, NativeBattleWaterInitializationInputs inputs)
    {
        core.Validate();
        arena.Validate();
        profile.Validate();
        Resource(inputs.Resource0, 0x1C);
        Resource(inputs.Resource1, 0x20);
        NativeBattleWaterTemporaryFrame frame = inputs.TemporaryFrame;
        if (frame.Bytes.IsDefault || frame.Known.IsDefault || frame.Bytes.Length != 0x180 || frame.Known.Length != 0x180 || frame.Address == 0 || frame.Address > uint.MaxValue - 0x180 || inputs.ActionParametersAddress == 0 || inputs.HeaderBaseAddress == 0 || profile.OwnerAddress(2) != NativeBattleScriptedEffectMemory.Address)
        {
            throw new InvalidDataException("Native initialization requires its actual bounded temporary frame and parameter/header bindings.");
        }
        byte[] temporary = frame.Bytes.ToArray();
        bool[] known = frame.Known.ToArray();
        uint manager = profile.OwnerAddress(0);
        uint command = profile.OwnerAddress(4);
        Dword(0x2798A68, inputs.Resource0.Address);
        Dword(0x2798A6C, inputs.Resource1.Address);
        NativeBattleWaterFrameBindingResult binding = NativeBattleWaterFrameBinding.Bind(core, frame, profile);
        core = binding.Core;
        temporary = binding.TemporaryFrame.Bytes.ToArray();
        known = binding.TemporaryFrame.Known.ToArray();
        Dword(manager + 0xC0, inputs.ActionParametersAddress);
        Dword(manager + 0xBC, 0);
        Word(manager, 0);
        Zero(0x2798BF8, 32);
        Zero(0x27971E4, 32);
        Dword(manager + 0x30, 0);
        Word(manager + 0x34, 0);
        Word(manager + 2, 0x0200);
        Dword(manager + 0x84, 0x803F4000);
        Dword(manager + 0x8C, 0x4000);
        TempWord(0x140, 0);
        Word(0x2797084, 0x3814);
        uint mainPc = ResourcePointer(inputs.Resource0, 4);
        Dword(manager + 0x94, mainPc);
        Dword(manager + 0xC4, ResourcePointer(inputs.Resource0, 0x10));
        Dword(manager + 0xC8, ResourcePointer(inputs.Resource0, 0x18));
        Dword(manager + 0x98, Directory(0));
        Dword(manager + 0x9C, Directory(4));
        Dword(manager + 0xA4, Directory(8));
        Dword(manager + 0xA8, Directory(0xC));
        Dword(manager + 0xAC, Directory(0x10));
        Dword(manager + 0xB0, Directory(0x18));
        Dword(0x2797774, 0x0200);
        Dword(0x2797778, 0x4000);
        Word(0x2797780, 0x0100);
        Word(0x2797782, 0x0300);
        Word(0x2797784, 0);
        TempDword(0xF0, 0);
        TempDword(0xF4, 0);
        ImmutableArray<short> rotation = NativeBattlePoseMatrices.RotationWords(new(0, 0, 0));
        for (int index = 0; index < rotation.Length; index++)
        {
            Word(0x2797968 + (uint)(2 * index), unchecked((ushort)rotation[index]));
        }
        Dword(0x279797C, 0);
        Dword(0x2797980, 0);
        Dword(0x2797984, 0);
        core = core.Copy(0x2797968, 0x2797988, 32);
        core = core.Copy(0x2797968, 0x27979A8, 32);
        core = core.Copy(0x2797968, 0x27979C8, 32);
        Word(0x2797988, 0xF000);
        Word(0x27979B0, 0xF000);
        Word(0x27979C8, 0xF000);
        Word(0x27979D0, 0xF000);
        Dword(manager + 0x70, 0x80400000);
        Dword(manager + 0x74, 0x80400000);
        Word(command, 0);
        Byte(command + 6, 0);
        Word(command + 8, 0);
        Dword(0x2797554, uint.MaxValue);
        Dword(0x2797454, uint.MaxValue);
        Byte(manager + 0xD0, 0);
        Byte(manager + 0xD1, inputs.FirstHeaderCount);
        Byte(0x2798219, 0);
        Byte(0x2798218, 0xFF);
        Word(manager + 0xA0, 0x0233);
        Word(command, 1);
        NativeBattleWaterResetResult reset = ResetAA4B60(core, arena, new(frame.Address, temporary.ToImmutableArray(), known.ToImmutableArray()), inputs.Resource1, (_, _, _) => inputs.HeaderBaseAddress, (_, _, _, _) => new(inputs.Caster, inputs.CurrentHeaderIndex, inputs.FirstHeaderFirstActor, inputs.Contacts, inputs.ContactCount, inputs.Graphics));
        return new(reset.Core, reset.Arena, reset.SlotBank, reset.TemporaryFrame, reset.CurrentHeaderIndex, new(0x2796BA8, 0x2796BB8, 0x10, 1, 0xA9B870));

        void Resource(NativeBattleWaterInitializationResource resource, int minimum)
        {
            if (resource.ArenaOffset < 0 || resource.Length < minimum || resource.ArenaOffset > NativeBattleEffectArena.Length - resource.Length || resource.Address != NativeBattleEffectNodeMemory.ArenaAddress + (uint)resource.ArenaOffset)
            {
                throw new InvalidDataException("Native initializer resources require their actual current arena pointer and extent.");
            }
        }

        uint ResourcePointer(NativeBattleWaterInitializationResource resource, int offset) => unchecked(resource.Address + BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(resource.ArenaOffset + offset, 4)));
        uint Directory(int offset) => unchecked(inputs.InitializationDirectoryAddress + profile.InitializationOffset(offset));
        void Zero(uint address, int length) => core = core.Write(address, new byte[length]);
        void Byte(uint address, byte value) => core = core.Write(address, new byte[] { value });

        void Word(uint address, ushort value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            core = core.Write(address, bytes);
        }

        void Dword(uint address, uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            core = core.Write(address, bytes);
        }

        void TempWord(int offset, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(temporary.AsSpan(offset), value);
            Array.Fill(known, true, offset, 2);
        }

        void TempDword(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(temporary.AsSpan(offset), value);
            Array.Fill(known, true, offset, 4);
        }
    }

    public static NativeBattleWaterResetResult ResetAA4B60(NativeBattleScriptedEffectMemory core, NativeBattleEffectArena arena, NativeBattleWaterTemporaryFrame temporaryFrame, NativeBattleWaterInitializationResource resource1, Func<NativeBattleScriptedEffectMemory, NativeBattleEffectArena, uint, uint> readHeaderBase, Func<NativeBattleScriptedEffectMemory, NativeBattleEffectArena, uint, byte, NativeBattleWaterResetCaptureInputs> readCaptureInputs)
    {
        uint manager = Dword(0x27973EC);
        uint command = Dword(0x27973C0);
        uint header = Dword(0x279744C);
        if (header != NativeBattleScriptedEffectMemory.Address || resource1.ArenaOffset < 0 || resource1.Length < 0x20 || resource1.ArenaOffset > NativeBattleEffectArena.Length - resource1.Length || resource1.Address != NativeBattleEffectNodeMemory.ArenaAddress + (uint)resource1.ArenaOffset || Dword(0x2798A6C) != resource1.Address)
        {
            throw new InvalidDataException("Native shared reset requires its actual core header and current resource-one owner.");
        }
        StoreDword(command + 8, 0x80000000);
        StoreDword(manager + 0x74, Dword(manager + 0x70));
        uint slotPointer = unchecked(resource1.Address + BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(resource1.ArenaOffset + 0x1C, 4)));
        StoreDword(manager + 0x90, slotPointer);
        StoreDword(0x27973E8, slotPointer);
        int slotOffset = NativeBattleWaterExecutionFields.ArenaOffset(slotPointer, 0x800);
        NativeBattleWaterSlotBank bank = new(resource1.ArenaOffset, resource1.Length, 8, slotOffset);
        _ = bank.SlotOffset(arena, 7);
        core = core.Write(0x2797454, Enumerable.Repeat((byte)0xFF, 0x80).ToArray());
        core = core.Write(0x2797554, Enumerable.Repeat((byte)0xFF, 0x80).ToArray());
        StoreByte(0x2797454, 0);
        StoreWord(header + 2, 0);
        StoreWord(header + 0x18, 1);
        StoreWord(header + 0x1A, 0);
        StoreWord(header, 0x8000);
        StoreDword(header + 0x40, 0);
        StoreDword(command + 0xC, 0x04000080);
        StoreDword(header + 0x14, 0);
        StoreWord(header + 0x38, 8);
        StoreByte(header + 0x3A, 0);
        StoreByte(0x2798219, 0);
        StoreByte(0x2798218, 0xFF);
        if (temporaryFrame.Bytes.IsDefault || temporaryFrame.Known.IsDefault || temporaryFrame.Bytes.Length != 0x180 || temporaryFrame.Known.Length != 0x180)
        {
            throw new InvalidDataException("Native shared reset requires the actual bounded temporary frame.");
        }
        temporaryFrame = temporaryFrame with { Bytes = temporaryFrame.Bytes.SetItem(0x145, 0), Known = temporaryFrame.Known.SetItem(0x145, true) };
        StoreByte(manager + 0xA2, 0);
        StoreDword(header + 0x170, 0);
        StoreDword(header + 0x16C, 0);
        StoreDword(header + 0x168, 0);
        core = core.Write(0x2797204, new byte[0x80]);
        for (int index = 0; index < 8; index++)
        {
            arena = arena.Write(bank.SlotOffset(arena, index), new byte[4]);
        }
        arena = NativeBattleWaterSlots.InitializeB65160(arena, bank, 0, BinaryPrimitives.ReadUInt16LittleEndian(core.Read(command + 0xC, 2)), BinaryPrimitives.ReadUInt16LittleEndian(core.Read(command + 0xE, 2)), BinaryPrimitives.ReadUInt16LittleEndian(core.Read(header + 0x1C, 2)));
        arena = arena.Write(bank.SlotOffset(arena, 0) + 0x12, new byte[2]);
        byte relativeIndex = core.Read(manager + 0xD0, 1)[0];
        uint parameters = Dword(manager + 0xC0);
        uint headerAddress = unchecked(readHeaderBase(core, arena, parameters) + (uint)(0x14 * relativeIndex));
        StoreDword(manager + 0xCC, headerAddress);
        NativeBattleWaterResetCaptureInputs inputs = readCaptureInputs(core, arena, headerAddress, relativeIndex);
        NativeBattleWaterHeaderCaptureResult captured = NativeBattleWaterHeaderCapture.CaptureAA4E90(core.ReadHeader(), arena, bank, 0, inputs.Caster, inputs.CurrentHeaderIndex, inputs.FirstHeaderFirstActor, inputs.Contacts, inputs.ContactCount, inputs.Graphics);
        arena = captured.Arena;
        core = core.WithHeader(NativeBattleWaterHeaderCapture.CacheRowsAA4B60(captured.Header, inputs.Graphics));
        byte[] main = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(main, Dword(manager + 0x94));
        arena = arena.Write(bank.SlotOffset(arena, 0), main);
        return new(core, arena, temporaryFrame, bank, captured.CurrentHeaderIndex);

        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(core.Read(address, 4));
        void StoreByte(uint address, byte value) => core = core.Write(address, new byte[] { value });
        void StoreWord(uint address, ushort value)
        {
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            core = core.Write(address, bytes);
        }
        void StoreDword(uint address, uint value)
        {
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            core = core.Write(address, bytes);
        }
    }

}
