using System.Collections.ObjectModel;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private NativeEventMemory componentMemory = NativeEventMemory.Empty;
    private IReadOnlyDictionary<int, int> variables => EventMemory.KnownValues;

    public NativeEventMemory EventMemory => services.CanStoreEventMemory ? services.EventMemory : componentMemory;
    public IReadOnlyDictionary<int, int> Variables => CreateVariableView();

    private static NativeEventSceneMemoryOwners SceneMemoryOwners(SceneEventState scene)
    {
        NativeEventSceneMemoryOwners owners = NativeEventSceneMemoryOwners.None;
        if (scene.PreviousFieldWord84 is not null)
        {
            owners |= NativeEventSceneMemoryOwners.PreviousField;
        }
        if (scene.PreviousWorldEntityByte86 is not null)
        {
            owners |= NativeEventSceneMemoryOwners.PreviousWorldEntity;
        }
        if (scene.FieldLoadReasonByte87 is not null)
        {
            owners |= NativeEventSceneMemoryOwners.FieldLoadReason;
        }
        if (scene.MusicChannel108 is not null)
        {
            owners |= NativeEventSceneMemoryOwners.PrimaryChannel;
        }
        if (scene.MusicChannel112 is not null)
        {
            owners |= NativeEventSceneMemoryOwners.SecondaryChannel;
        }
        if (scene.PartyBindingMemory is not null)
        {
            owners |= NativeEventSceneMemoryOwners.PartyBindings;
        }
        if (scene.SoundMusicVolume197 is not null)
        {
            owners |= NativeEventSceneMemoryOwners.MusicVolume;
        }
        if (scene.SoundMusicVolume198 is not null)
        {
            owners |= NativeEventSceneMemoryOwners.SoundVolume;
        }
        if (scene.MusicProcess is not null)
        {
            owners |= NativeEventSceneMemoryOwners.MusicProcess;
        }
        if (scene.WorldMusicSelection is not null)
        {
            owners |= NativeEventSceneMemoryOwners.WorldMusicSelection;
        }
        if (scene.DrawPointFieldStateVersion == 1)
        {
            owners |= NativeEventSceneMemoryOwners.DrawPointDisplay;
        }
        if (scene.DrawPointIndex is not null)
        {
            owners |= NativeEventSceneMemoryOwners.DrawPointIndex;
        }
        return owners;
    }

    private static List<NativeEventMemoryRegion> SceneMemoryRegions(SceneEventState scene)
    {
        return [.. NativeEventMemoryOwnership.SceneRegions(SceneMemoryOwners(scene))];
    }

    private List<NativeEventMemoryRegion> MemoryRegions()
    {
        List<NativeEventMemoryRegion> regions = SceneMemoryRegions(Scene);
        regions.AddRange(NativeEventMemoryOwnership.RootRegions(services));
        return regions;
    }

    private static void ValidateSceneMemoryOwners(NativeEventMemory memory, SceneEventState scene)
    {
        NativeEventMemoryOwnership.Validate(memory, SceneMemoryRegions(scene));
    }

    private NativeEventMemoryOwner ResolveMemoryOwner(int address, int width)
    {
        return NativeEventMemoryOwnership.Resolve(address, width, MemoryRegions());
    }

    private void SetEventMemory(NativeEventMemory memory)
    {
        NativeEventMemoryOwnership.Validate(memory, MemoryRegions());
        PublishEventMemory(memory);
    }

    private void PublishEventMemory(NativeEventMemory memory)
    {
        if (services.CanStoreEventMemory)
        {
            services.SetEventMemory(memory);
        }
        else
        {
            componentMemory = memory;
        }
    }

    private void WriteGenericEventMemory(int address, int width, NativeScalarValue value)
    {
        if (ResolveMemoryOwner(address, width) != NativeEventMemoryOwner.Generic)
        {
            throw new NotSupportedException("Generic event stores cannot replace a canonical memory owner.");
        }
        SetEventMemory(EventMemory.Write(address, width, value));
    }

    private NativeScalarValue ReadEventMemory(JsmOperation operation)
    {
        int address = operation.Parameter;
        int width = NativeMemoryWidth(operation.Opcode);
        bool signed = operation.Opcode is Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHSM_W;
        if (NativeRuntimeProfile is { IsVerified: true } && address <= 87 && (long)address + width > 87 && Scene.FieldLoadReasonByte87 is null)
        {
            throw new NotSupportedException("Native field-load BYTE87 requires an explicit field-allocation reason.");
        }
        NativeEventMemoryOwner owner = ResolveMemoryOwner(address, width);
        if (owner == NativeEventMemoryOwner.Generic)
        {
            NativeScalarValue read = EventMemory.Read(address, width, signed);
            return operation.Opcode == Jsm.Opcode.PSHSM_L ? read.SaturateSignedLong() : read;
        }
        int value;
        if (TryReadWorldEntryMemory(operation, out value) || TryReadNativeFadeMemory(operation, out value) || TryReadNativeDrawPointMemory(operation, out value) || TryReadJunctionBackupMemory(operation, out value) || TryReadNativeEconomyMemory(operation, out value) || TryReadNativeOwnedEventMemory(operation, out value) || TryReadMusicStopMemory(operation, out value) || TryReadPreviousFieldWord(operation, out value))
        {
            NativeScalarValue read = NativeScalarValue.Exact(value).ToWidth(width, signed);
            return operation.Opcode == Jsm.Opcode.PSHSM_L ? read.SaturateSignedLong() : read;
        }
        if (width == 1)
        {
            if (owner == NativeEventMemoryOwner.PreviousWorldEntity && Scene.PreviousWorldEntityByte86 is byte entity)
            {
                value = entity;
            }
            else if (owner == NativeEventMemoryOwner.FieldLoadReason && Scene.FieldLoadReasonByte87 is byte reason)
            {
                value = reason;
            }
            else if (owner == NativeEventMemoryOwner.PartyBindings && Scene.PartyBindingMemory is { } bindings)
            {
                value = bindings.At(address - 194);
            }
            else if (owner == NativeEventMemoryOwner.SoundVolume && TryReadSoundVolumeByte(address, out value))
            {
            }
            else if (owner == NativeEventMemoryOwner.DrawPoints && TryReadDrawPointByte(address, out value))
            {
            }
            else if (owner == NativeEventMemoryOwner.MusicProcess && TryReadNativeMusicByte(address, out value))
            {
            }
            else if (owner == NativeEventMemoryOwner.Disc && services.DiscProcess is { } disc)
            {
                value = disc.SavedDisc204;
            }
            else if (owner == NativeEventMemoryOwner.WorldControls && address == 209 && services.WorldControls is { } controls)
            {
                value = controls.Menu209;
            }
            else if (owner == NativeEventMemoryOwner.WorldMusicSelection && Scene.WorldMusicSelection is { } selection)
            {
                value = selection.Music;
            }
            else
            {
                throw new NotSupportedException($"Native owner {owner} has no executed value for BYTE{address}.");
            }
            return NativeScalarValue.Exact(value).ToWidth(1, signed);
        }
        throw new NotSupportedException($"Native owner {owner} does not support access {address}/{width}.");
    }

    private IReadOnlyDictionary<int, int> CreateVariableView()
    {
        Dictionary<int, int> view = new(variables);
        if (services.WorldEntryMemory is { } worldEntry)
        {
            view = new Dictionary<int, int>(worldEntry.ProjectVariables(view));
        }
        void Add(int address, int? value)
        {
            if (value is int known)
            {
                view[address] = known;
            }
        }
        Add(84, Scene.PreviousFieldWord84);
        Add(86, Scene.PreviousWorldEntityByte86);
        Add(87, Scene.FieldLoadReasonByte87);
        Add(108, Scene.MusicChannel108?.ChannelId);
        Add(112, Scene.MusicChannel112?.ChannelId);
        for (int slot = 0; slot < 3; slot++)
        {
            if (Scene.PartyBindingMemory is { } bindings)
            {
                view[194 + slot] = bindings.At(slot);
            }
        }
        Add(197, Scene.SoundMusicVolume197);
        Add(198, Scene.SoundMusicVolume198);
        Add(199, Scene.MusicChannel108?.MusicId);
        Add(200, Scene.MusicChannel112?.MusicId);
        Add(201, Scene.MusicProcess?.Bank201);
        Add(202, Scene.MusicProcess?.Bank202);
        Add(204, services.DiscProcess?.SavedDisc204);
        Add(207, services.WorldControls?.MusicKeep207);
        Add(209, services.WorldControls?.Menu209);
        Add(213, Scene.MusicProcess?.Requested213);
        Add(214, Scene.MusicProcess?.Complete214);
        Add(268, Scene.WorldMusicSelection?.Music);
        return new ReadOnlyDictionary<int, int>(view);
    }
}
