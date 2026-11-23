using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    public static byte[] CaptureNativeSaveBank(FieldVariables variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        return variables.CopyPersistent();
    }

    private void ApplyNativeSaveWrites(Ff7BattleAiResult result)
    {
        if (result.SaveBank1Writes.Count == 0)
        {
            return;
        }
        byte[] bank = state.AiSaveBank1!.ToArray();
        HashSet<byte> dirty = new(state.AiSaveBankDirty ?? []);
        HashSet<ushort> extendedDirty = new(state.AiSaveBankExtendedDirty ?? []);
        foreach ((ushort address, byte value) in result.SaveBank1Writes)
        {
            bank[address] = value;
            if (address <= byte.MaxValue)
            {
                dirty.Add((byte)address);
            }
            else
            {
                extendedDirty.Add(address);
            }
        }
        state = state with { AiSaveBank1 = bank, AiSaveBankDirty = dirty.Order().ToArray(), AiSaveBankExtendedDirty = extendedDirty.Count == 0 ? null : extendedDirty.Order().ToArray() };
    }

    public int ResolveNativeFieldResult(int defaultResult)
    {
        if (state.AiSaveBankDirty?.Contains((byte)2) != true)
        {
            return defaultResult;
        }
        if (state.AiSaveBank1 is not { Length: Ff7BattleAi.NativeSaveBankSize } bank)
        {
            throw new InvalidDataException("FFVII battle result has no valid save-bank owner.");
        }
        return bank[2];
    }

    public void PublishNativeSaveWrites(FieldVariables variables)
    {
        ApplyNativeSaveWrites(variables);
        state = state with { AiSaveBankDirty = [], AiSaveBankExtendedDirty = null };
    }

    internal void ApplyNativeSaveWrites(FieldVariables variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        byte[] bank = state.AiSaveBank1!;
        foreach (byte address in state.AiSaveBankDirty ?? [])
        {
            variables.WriteByte(FieldBank.Persistent0Byte, address, bank[address]);
        }
        ReadOnlySpan<FieldBank> owners = [FieldBank.Persistent0Byte, FieldBank.Persistent1Byte, FieldBank.Persistent2Byte, FieldBank.Persistent3Byte, FieldBank.Persistent4Byte];
        foreach (ushort address in state.AiSaveBankExtendedDirty ?? [])
        {
            variables.WriteByte(owners[address >> 8], (byte)(address & byte.MaxValue), bank[address]);
        }
    }
}
