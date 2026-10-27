using System.Reflection;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePrivateMemoryPublicationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Register_publication_defers_private_component_until_explicit_prefix_freeze(bool common, bool publishPrefix)
    {
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400().ApplyCommonWrite(0, [1, 2, 3, 4], [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known]);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = process };
        NativeBattleEffectArena storage = common ? process.CommonPacketMemory!.Bank070Buffer0! : memory.ScriptedEffects!.Core;
        int offset = common ? 0 : 0x100;
        storage.Write(offset, [1]);
        NativeBattleEffectArena original = NativeBattleStorageSnapshots.Capture(storage);
        BattleSession session = (BattleSession)RuntimeHelpers.GetUninitializedObject(typeof(BattleSession));
        typeof(BattleSession).GetField("nativeEffectMemory", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(session, memory);
        PropertyInfo memoryProperty = typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!;
        NativeBattleOriginalEffectState effect = new(202, 201, new(202, 1, 0), null, null);
        using (NativeBattleTaskOperation operation = new(memory, effect))
        {
            NativeBattleTaskOperation.AdmitCurrentRetainedArena(storage);
            using NativeBattleEffectArenaTransaction transaction = new(storage);
            transaction.BeginCheckpoint();
            transaction.View.Write(offset, [7]);
            memory.Arena!.Write(0, [3]);
            NativeBattleEffectMemory registers = memory with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() };
            memoryProperty.SetValue(session, registers);
            Assert.Same(registers.GeometryRegisters, session.NativeEffectMemory!.GeometryRegisters);
            Assert.Equal((byte)7, transaction.View.ReadByte(offset));
            Assert.Equal((byte)1, storage.ReadByte(offset));
            Assert.Throws<InvalidOperationException>(() => NativeBattleEffectArenaTransaction.RequireCommittedForCapture(storage.Working));
            Assert.Throws<InvalidOperationException>(() => NativeBattleStorageSnapshots.Capture(storage));
            if (publishPrefix)
            {
                transaction.CommitCheckpoint();
                transaction.Freeze();
                memoryProperty.SetValue(session, registers);
                Assert.Equal((byte)7, storage.ReadByte(offset));
                transaction.BeginCheckpoint();
                transaction.View.Write(offset, [8]);
                memoryProperty.SetValue(session, registers);
                Assert.Equal((byte)8, transaction.View.ReadByte(offset));
                Assert.Equal((byte)7, storage.ReadByte(offset));
            }
            memory.Arena.Write(0, [4]);
        }
        Assert.Equal(publishPrefix ? (byte)7 : (byte)1, storage.ReadByte(offset));
        Assert.Equal((byte)3, memory.Arena!.ReadByte(0));
        Assert.Equal((byte)1, original.ReadByte(offset));
    }
}
