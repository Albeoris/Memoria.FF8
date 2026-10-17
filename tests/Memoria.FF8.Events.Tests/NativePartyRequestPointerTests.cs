using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyRequestPointerContract()
    {
        byte[] source = SyntheticObjects([1, (2 << 7) | 1],
        [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 3), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]
        ]);
        JsmProgram program = JsmProgram.Read(source);
        foreach (int selector in new[] { 3, 65539, -65533 })
        {
            NativeScriptPointer physical = program.ResolveNativeRequestPointer(0, selector, [0, 1]);
            Require(physical.Index == 3 && physical.ObjectId == 1 && physical.Script.Id == 3 && physical.Script.Start == program.Objects[1].Scripts[1].Start, "PREQ physical lookup used declared group bounds, changed the requested body, or lost DWORD-addition/WORD truncation.");
        }
        NativeScriptPointer wrapped = program.ResolveNativeRequestPointer(1, 65535, [0, 1]);
        Require(wrapped.Index == 1 && wrapped.Script.Id == 1, "PREQ failed the original unsignedWORD pointer wrap.");
        NativeScriptPointer inclusive = program.ResolveNativeRequestPointer(0, 2, [0]);
        Require(inclusive.Index == 2 && inclusive.ObjectId == 1, "Instruction-copy admission omitted the retained actor's inclusive WORD378 boundary.");
        foreach ((int target, int selector, int[] retained) in new[] { (0, 3, new[] { 0 }), (1, 1, new[] { 0 }), (0, 4, new[] { 0, 1 }), (0, 3, new[] { 0, 99 }) })
        {
            try
            {
                program.ResolveNativeRequestPointer(target, selector, retained);
                throw new InvalidOperationException("Unknown/unallocated physical request pointer acquired an invented original code owner.");
            }
            catch (NotSupportedException)
            {
            }
        }
        int table = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(4));
        ushort entry = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(table + 6));
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(table + 6), (ushort)(entry | 0x8000));
        JsmProgram explicitCopy = JsmProgram.Read(source);
        NativeScriptPointer highBit = explicitCopy.ResolveNativeRequestPointer(0, 3, [0]);
        Require(highBit.Index == 3 && highBit.ObjectId == 1 && (highBit.RawPointer & 0x8000) != 0, "Native original high-bit instruction-copy admission was confused with receiver retention.");
        Console.WriteLine("Native party request pointers: original raw table provenance, cross-group code with unchanged body ownership, DWORD selectors/WORD wrap, inclusive actor-copy boundary, high-bit copy and strict omitted/unallocated/out-of-table refusal passed.");
    }
}
