using System.Buffers.Binary;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    /// <summary>Starts an imported normal save with explicit defaults for transient process state.</summary>
    public void InitializeOriginalSave(NativeFieldRuntimeProfile profile)
    {
        if (program is not null || profile is not { IsVerified: true } || services is not GameSession session || session.State.OriginalSave is not { } original || session.State.EventState is not null)
        {
            throw new InvalidOperationException("Original save initialization requires a fresh engine and its imported native save owner.");
        }
        original.Validate();
        byte[] block = original.Block;
        string sourceHash = Convert.ToHexString(SHA256.HashData(block));
        Scene.Party.Clear();
        Scene.Party.AddRange(original.FieldParty.Select(identity => (int)identity));
        foreach ((int subject, string name) in original.Names)
        {
            Scene.Names[subject] = name;
        }
        Scene.UnlockedTutorials.Clear();
        for (int id = 0; id < 128; id++)
        {
            if ((block[NativePcSaveData.MainOffset + 3283 + id / 8] & (1 << (id & 7))) != 0)
            {
                Scene.UnlockedTutorials.Add(id);
            }
        }
        Scene.PlaceId = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(388));
        Scene.NativeLayerZeroScrollCurrent = new NativeLayerZeroScrollCurrent(1, 0, 0);
        Scene.NativeBackgroundScrollCurrents = Enumerable.Range(1, 7).ToDictionary(layer => layer, _ => new NativeBackgroundScrollCurrent(1, 0, 0));
        Scene.NativeDrawPointDisplayMask = 0;
        Scene.DrawPointIndex = 0;
        if (DrawPointParticleTables is not null)
        {
            Scene.NativeDrawPointParticles = NativeDrawPointParticles.Initialize(null, DrawPointParticlePhase(), DrawPointParticleTables);
        }
        // Save files contain no resident music buffers or pending I/O. The new process starts silent.
        Scene.MusicProcess = new NativeMusicProcessState(1, false, 0, 0, 0, 255, new NativeMusicBootstrap(1, NativeMusicBootstrap.OriginalSaveRule, profile.ExecutableHash));
        Scene.MusicChannel108 = new NativeMusicChannelState(-1, 255);
        Scene.MusicChannel112 = new NativeMusicChannelState(-1, 255);
        Scene.NativeBattleMusic203 = block[NativePcSaveData.EventBankOffset + 203];
        Scene.WorldMusicSelection = NativeWorldMusicSelection.FromRetainedPhysicalByte(block[NativePcSaveData.EventBankOffset + 268], sourceHash);
        SetSoundVolumeByte(197, block[NativePcSaveData.EventBankOffset + 197]);
        SetSoundVolumeByte(198, block[NativePcSaveData.EventBankOffset + 198]);
        ushort mode = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(NativePcSaveData.EventBankOffset + 182));
        Scene.NativeBattleFlagWrites = [new NativeBattleFlagWrite(1, NativeBattleFlagWriter.OriginalSave, session.State.FieldId, sourceHash, 0, -1, -1, -1, null, mode, mode)];
        if (session.State.Scene == SceneKind.WorldMap && session.State.WorldArrival is { } arrival)
        {
            // This conversion default records the silent newly allocated process, with no fabricated field VM.
            Scene.WorldMusicLoad = new NativeWorldMusicLoad(1, session.State.FieldId, 0, arrival, 0, 255, 0, 0, NativeWorldMusicOperation.Stop, null, null, 127, 127, 0);
        }
    }
}
