using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedJunction(Fiber fiber)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || services.CharacterVitals is not NativeCharacterVitals state || services.WorldControls is not NativeWorldControlState controls || !services.CanStoreNativeEventOwners)
        {
            return "Native JUNCTION requires current saved identities/stat inputs, event flags and atomic owner publication";
        }
        if (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact)
        {
            return "Native JUNCTION needs its exact original DWORD operand";
        }
        string? aliases = UnsupportedJunctionBackupAliases();
        if (aliases is not null)
        {
            return aliases;
        }
        try
        {
            NativeJunctionResult prepared = state.PrepareJunction(fiber.Stack.Peek(), Scene.Party, controls.EventFlags104 & 7);
            if (prepared.FieldParty.Where(identity => identity != 255).Distinct().Count() != prepared.FieldParty.Count(identity => identity != 255))
            {
                return "Native JUNCTION restore requires unsupported duplicate field body-slot ownership";
            }
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            return exception.Message;
        }
    }

    private int[] ExecuteJunction(Fiber fiber)
    {
        NativeWorldControlState controls = services.WorldControls!;
        NativeJunctionResult prepared = services.CharacterVitals!.PrepareJunction(fiber.Stack.Peek(), Scene.Party, controls.EventFlags104 & 7);
        try
        {
            services.SetNativeEventOwners(controls, characterVitals: prepared.Vitals);
        }
        catch (NotSupportedException exception)
        {
            fiber.Position--;
            string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native JUNCTION owner publication rejected; PC and operands preserved.";
            LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, nameof(Jsm.Opcode.JUNCTION), message);
            throw new EventScriptException(message, exception);
        }
        Scene.Party.Clear();
        Scene.Party.AddRange(prepared.FieldParty.Select(identity => (int)identity));
        int[] operands = Pop(fiber, 1);
        // Native51EFB0 returns3 on enable and disable; do not refresh retained actor bindings here.
        WaitFrames(fiber, 1);
        return operands;
    }

    private string? UnsupportedJunctionBackupAliases()
    {
        return variables.Keys.Any(address => address < 194 && (long)address + 4 > 188) || services.NativeVariableValues?.Any(pair => pair.Key < 194 && (long)pair.Key + pair.Value.Width > 188) == true ? "Native JUNCTION backup bytes overlap a separate typed/untyped VM alias; recreate authentic inputs" : null;
    }

    private string? UnsupportedJunctionBackupMemory(Fiber fiber, JsmOperation operation)
    {
        int width = NativeMemoryWidth(operation.Opcode);
        if (width == 0 || operation.Parameter >= 194 || (long)operation.Parameter + width <= 188)
        {
            return null;
        }
        if (NativeRuntimeProfile is not { IsVerified: true } || services.CharacterVitals is null)
        {
            return "Native JUNCTION backup projection requires its actual NewGame saved owner";
        }
        if (operation.Parameter < 188 || (long)operation.Parameter + width > 194)
        {
            return "Native JUNCTION backup projection cannot consume separately owned surrounding bytes";
        }
        if (UnsupportedJunctionBackupAliases() is string alias)
        {
            return alias;
        }
        bool write = operation.Opcode is Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L;
        return write && (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact) ? "Native JUNCTION backup write requires its exact consumed integer" : null;
    }

    private bool TryReadJunctionBackupMemory(JsmOperation operation, out int value)
    {
        int width = NativeMemoryWidth(operation.Opcode);
        if (width != 0 && operation.Parameter >= 188 && (long)operation.Parameter + width <= 194 && services.CharacterVitals is NativeCharacterVitals state)
        {
            value = state.ReadJunctionPartyBackup(operation.Parameter, width);
            return true;
        }
        value = 0;
        return false;
    }

    private bool TryWriteJunctionBackupMemory(Fiber fiber, JsmOperation operation, out int[] operands)
    {
        int width = NativeMemoryWidth(operation.Opcode);
        if (operation.Opcode is Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L && operation.Parameter >= 188 && (long)operation.Parameter + width <= 194 && services.CharacterVitals is NativeCharacterVitals state)
        {
            NativeCharacterVitals next = state.WriteJunctionPartyBackup(operation.Parameter, width, fiber.Stack.Peek());
            CommitSavedCharacters(fiber, next, operation.Opcode);
            operands = Pop(fiber, 1);
            return true;
        }
        operands = [];
        return false;
    }
}
