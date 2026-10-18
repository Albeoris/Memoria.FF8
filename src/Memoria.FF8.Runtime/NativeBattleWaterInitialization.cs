using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterInitializationResource(int ArenaOffset, int Length, uint Address);
public sealed record NativeBattleWaterTemporaryFrame(uint Address, ImmutableArray<byte> Bytes, ImmutableArray<bool> Known);
public sealed record NativeBattleWaterInitializationInputs(NativeBattleWaterInitializationResource Resource0, NativeBattleWaterInitializationResource Resource1, NativeBattleWaterTemporaryFrame TemporaryFrame, uint ActionParametersAddress, uint HeaderBaseAddress, uint InitializationDirectoryAddress, byte Caster, int CurrentHeaderIndex, byte FirstHeaderFirstActor, ImmutableArray<byte> Contacts, sbyte ContactCount, byte FirstHeaderCount, ImmutableArray<NativeBattleGraphicsModel> Graphics);
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
        TempDword(0x100, frame.Address);
        TempDword(0x104, frame.Address + 0x100);
        for (int index = 0; index < 5; index++)
        {
            TempDword(0x108 + 4 * index, profile.OwnerAddress(index));
        }
        Dword(0x2797624, frame.Address);
        Dword(0x27973B8, frame.Address + 0x100);
        Dword(0x27973EC, profile.OwnerAddress(0));
        Dword(0x27973C4, profile.OwnerAddress(1));
        Dword(0x279744C, profile.OwnerAddress(2));
        Dword(0x27973BC, profile.OwnerAddress(3));
        Dword(0x27973C0, profile.OwnerAddress(4));
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
        Dword(command + 8, 0x80000000);
        Dword(manager + 0x74, 0x80400000);
        uint slotPointer = ResourcePointer(inputs.Resource1, 0x1C);
        Dword(manager + 0x90, slotPointer);
        int slotOffset = checked(inputs.Resource1.ArenaOffset + (int)unchecked(slotPointer - inputs.Resource1.Address));
        NativeBattleWaterSlotBank bank = new(inputs.Resource1.ArenaOffset, inputs.Resource1.Length, 8, slotOffset);
        _ = bank.SlotOffset(arena, 7);
        core = core.Write(0x2797454, Enumerable.Repeat((byte)0xFF, 0x80).ToArray());
        core = core.Write(0x2797554, Enumerable.Repeat((byte)0xFF, 0x80).ToArray());
        Byte(0x2797454, 0);
        Word(0x279706A, 0);
        Word(0x2797080, 1);
        Word(0x2797082, 0);
        Word(0x2797068, 0x8000);
        Dword(0x27970A8, 0);
        Dword(0x279707C, 0);
        Word(0x27970A0, 8);
        Byte(0x27970A2, 0);
        Dword(command + 0xC, 0x04000080);
        Byte(0x2798219, 0);
        Byte(0x2798218, 0xFF);
        TempByte(0x145, 0);
        Byte(manager + 0xA2, 0);
        Dword(0x27971D0, 0);
        Dword(0x27971D4, 0);
        Dword(0x27971D8, 0);
        Zero(0x2797204, 0x80);
        for (int index = 0; index < 8; index++)
        {
            arena = arena.Write(bank.SlotOffset(arena, index), new byte[4]);
        }
        arena = NativeBattleWaterSlots.InitializeB65160(arena, bank, 0, 0x80, 0x400, 0x3814);
        arena = arena.Write(bank.SlotOffset(arena, 0) + 0x12, new byte[2]);
        Dword(manager + 0xCC, inputs.HeaderBaseAddress);
        NativeBattleWaterHeaderCaptureResult captured = NativeBattleWaterHeaderCapture.CaptureAA4E90(core.ReadHeader(), arena, bank, 0, inputs.Caster, inputs.CurrentHeaderIndex, inputs.FirstHeaderFirstActor, inputs.Contacts, inputs.ContactCount, inputs.Graphics);
        arena = captured.Arena;
        core = core.WithHeader(NativeBattleWaterHeaderCapture.CacheRowsAA4B60(captured.Header, inputs.Graphics));
        byte[] head = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(head, mainPc);
        arena = arena.Write(bank.SlotOffset(arena, 0), head);
        return new(core, arena, bank, new(frame.Address, temporary.ToImmutableArray(), known.ToImmutableArray()), captured.CurrentHeaderIndex, new(0x2796BA8, 0x2796BB8, 0x10, 1, 0xA9B870));

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

        void TempByte(int offset, byte value)
        {
            temporary[offset] = value;
            known[offset] = true;
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
}
