using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    public static byte[] CaptureNativeSaveBank(FieldVariables variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        return variables.CopyPersistent()[..256];
    }

    private void ApplyNativeSaveWrites(Ff7BattleAiResult result)
    {
        if (result.SaveBank1Writes.Count == 0)
        {
            return;
        }
        byte[] bank = state.AiSaveBank1?.ToArray() ?? new byte[256];
        if (bank.Length != 256)
        {
            throw new InvalidDataException("FFVII battle save-bank projection has an invalid size.");
        }
        HashSet<byte> dirty = new(state.AiSaveBankDirty ?? []);
        foreach ((byte address, byte value) in result.SaveBank1Writes)
        {
            bank[address] = value;
            dirty.Add(address);
        }
        state = state with { AiSaveBank1 = bank, AiSaveBankDirty = dirty.Order().ToArray() };
    }

    public int ResolveNativeFieldResult(int defaultResult)
    {
        if (state.AiSaveBankDirty?.Contains((byte)2) != true)
        {
            return defaultResult;
        }
        if (state.AiSaveBank1 is not { Length: 256 } bank)
        {
            throw new InvalidDataException("FFVII battle result has no valid save-bank owner.");
        }
        return bank[2];
    }

    public void PublishNativeSaveWrites(FieldVariables variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        if (state.AiSaveBankDirty is not { Length: > 0 } dirty)
        {
            return;
        }
        if (state.AiSaveBank1 is not { Length: 256 } bank)
        {
            throw new InvalidDataException("FFVII battle persistent writes have no valid save-bank owner.");
        }
        foreach (byte address in dirty)
        {
            variables.WriteByte(FieldBank.Persistent0Byte, address, bank[address]);
        }
        state = state with { AiSaveBankDirty = [] };
    }
}
