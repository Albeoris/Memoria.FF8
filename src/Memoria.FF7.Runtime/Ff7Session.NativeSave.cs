using System.Buffers.Binary;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private byte[]? nativeSaveData;

    internal static Ff7SessionSave ImportNativeField(Ff7NativeSaveSlot slot, Ff7Installation installation)
    {
        using Ff7Session session = new(installation);
        byte[] data = slot.Encode();
        session.nativeSaveData = data.ToArray();
        session.Progress = Ff7NativeSaveCodec.ImportProgress(slot, Ff7InitialGameData.Load(installation).NativeData.ToArray());
        FieldVariables variables = new(data.AsSpan(0xBA4, 1280).ToArray(), new byte[256]);
        session.LoadField(slot.MapId, variables);
        session.Machine.Initialize();
        session.Host.InitializePlayerMovement(session.Field.Scripts.Scale);
        session.Host.InitializeBackgroundTriggers();
        session.Host.PlacePlayerAtEntry(new(slot.MapId, BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(0xB9A)), BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(0xB9C)), BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0xB9E)), data[0xBA0]));
        return session.Capture();
    }
}
