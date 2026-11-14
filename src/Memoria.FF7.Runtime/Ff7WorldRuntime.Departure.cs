using System.Buffers.Binary;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7WorldRuntime
{
    private Ff7WorldFieldDeparture? fieldDeparture;
    private PcWorldFieldTable? fieldTable;

    internal Ff7WorldFieldDeparture? ReadyFieldDeparture => mainState is 8 or 9 && Machine.IsUnloading ? fieldDeparture : null;

    private WorldHostReply RequestFieldDeparture(WorldHostRequest request)
    {
        bool fade = request.Command.Operation == WorldOperation.FieldTransition;
        if (fade && mainState > 1)
        {
            return new(true);
        }
        int[] values = request.Command.Arguments;
        if (values.Length != (fade ? 2 : 1))
        {
            throw new InvalidDataException("FFVII world field transition has an invalid selector operand count.");
        }
        int selector = fade ? values[0] << 8 | values[1] : values[0];
        if (selector < (fade ? short.MinValue : 0) || selector > ushort.MaxValue)
        {
            throw new InvalidDataException("FFVII world transition selector is outside its supported range.");
        }
        fieldDeparture = new(request.Id, request.ExecutingModel, request.ProgramCounter, unchecked((ushort)selector), request.Command.Operation);
        mainState = fade ? 2 : 8;
        if (fade)
        {
            movementEnabled = false;
            controlLock = 0;
            fadeRate = fadeAlpha < 255 ? 16 : 0;
        }
        return new(true);
    }

    private void AdvanceFieldDeparture()
    {
        if (fieldDeparture is null)
        {
            return;
        }
        if (mainState == 3)
        {
            mainState = 9;
        }
        else if (mainState == 2 && fadeRate == 0 && fadeDisplayedAlpha == 255)
        {
            mainState = 3;
        }
        if (mainState is 8 or 9)
        {
            Machine.CompleteUnload();
        }
    }

    internal (FieldTransitionCommand Entry, byte[] Persistent) PrepareFieldDeparture(Ff7WorldFieldDeparture request)
    {
        if (ReadyFieldDeparture != request || request.Operation != WorldOperation.FieldTransition)
        {
            throw new InvalidOperationException("FFVII world departure must still own the field handoff.");
        }
        return (ResolveFieldEntry(request.Selector), CaptureNativeWorldExit());
    }

    internal FieldTransitionCommand ResolveFieldEntry(ushort selector)
    {
        PcWorldFieldEntry entry = (fieldTable ??= Resources.ReadFieldTable()).Select(selector);
        return new(entry.Field, entry.X, entry.Y, entry.Triangle, entry.Direction);
    }

    internal byte[] CaptureNativeWorldExit()
    {
        byte[] saved = Machine.CopyPersistent();
        saved[31] |= 3;
        int heading = viewMode == 0 && Resources.World != 3 ? 0 : camera.Heading & 4095;
        BinaryPrimitives.WriteUInt16LittleEndian(saved.AsSpan(0x3F8), (ushort)(heading | viewMode << 12 | radarMode << 14));
        saved[0x3FD] = unchecked((byte)controlledModel);
        saved[0x3FE] = (byte)Resources.World;
        Ff7WorldBridgeState bridge = input.Bridge ?? initialBridge;
        BinaryPrimitives.WriteUInt16LittleEndian(saved.AsSpan(0x3B6), (ushort)input.BridgeCooldown);
        saved[0x3F4] = (byte)bridge.Counter0;
        saved[0x3F5] = (byte)bridge.Counter1;
        saved[0x3F6] = (byte)bridge.Counter2;
        saved[0x3F7] = unchecked((byte)bridge.Phase);
        foreach (WorldEntitySnapshot model in Machine.Capture().Models.Where(model => !model.Detached))
        {
            Ff7WorldEntity entity = RequireEntity(model.Model);
            int slot = entity.Model switch { 0 or 1 or 2 => 0, 4 => 1, 5 or 19 => 2, 3 or 6 => 3, 13 => 4, 10 or 11 or 29 => 5, _ => -1 };
            if (slot >= 0 && (entity.Flags & 8) == 0)
            {
                uint xAndModel = (uint)(entity.PreviousX & 0x7FFFF) | (uint)entity.Model << 19 | (uint)(entity.TargetDirection & 0xFF0) << 20;
                uint yz = (uint)(entity.PreviousZ & 0x3FFFF) | (uint)((entity.PreviousY ?? entity.Y) & 0x3FFF) << 18;
                BinaryPrimitives.WriteUInt32LittleEndian(saved.AsSpan(0x3B8 + slot * 8), xAndModel);
                BinaryPrimitives.WriteUInt32LittleEndian(saved.AsSpan(0x3BC + slot * 8), yz);
            }
        }
        int sum = 0;
        for (int address = 0x380; address < 0x3FF; address++)
        {
            sum += saved[address];
        }
        saved[0x3FF] = unchecked((byte)-sum);
        return saved;
    }

    private static bool IsValidFieldDeparture(Ff7WorldProcess state)
    {
        if (state.FieldDeparture is not { } departure)
        {
            if (state.MapTransfer is not null)
            {
                return state.MainState is 4 or 5 or 6 or 7;
            }
            bool leavingForSpecialBattle = state.BattleState?.Pending is { } pending && (pending.ReturnsToField || pending.ReloadsWorld);
            return state.MainState is >= -15 and <= 1 && (!state.Vm.Unloading || leavingForSpecialBattle);
        }
        return departure.RequestId >= 1 && departure.RequestId < state.Vm.NextRequestId && departure.ExecutingModel is >= -1 and <= 255 && departure.ProgramCounter > 0 &&
            (departure.Operation == WorldOperation.FieldTransition && state.MainState is 2 or 3 or 9 || departure.Operation == WorldOperation.FieldTransitionSelector && state.MainState == 8) &&
            state.Vm.Unloading == (state.MainState is 8 or 9) && (!state.Vm.Unloading || state.Vm.System is { ProgramCounter: 0 } && state.Vm.Models.All(model => model.Detached || model.ProgramCounter == 0));
    }
}
