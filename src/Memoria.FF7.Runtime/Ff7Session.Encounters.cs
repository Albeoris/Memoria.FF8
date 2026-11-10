using System.Buffers.Binary;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    public void StartEncounter(FieldHostRequest request, byte initiative)
    {
        if (Process.GlobalCommand != Ff7GlobalCommand.None || Process.EncounterRequest is not null || request.Command is not FieldBattleCommand)
        {
            throw new InvalidOperationException("A FFVII random encounter requires its idle global battle owner.");
        }
        Execute(request);
        Process = Process with { EncounterRequest = request, EncounterInitiative = initiative };
    }

    public void ResumeEncounter()
    {
        FieldHostRequest request = Process.EncounterRequest ?? throw new InvalidOperationException("No FFVII random encounter is pending.");
        if (!Execute(request).Completed)
        {
            throw new InvalidOperationException("The FFVII random encounter result is not complete.");
        }
        Process = Process with { EncounterRequest = null };
    }
}

public sealed partial class Ff7Session
{
    private IReadOnlyList<PcFieldEncounterTable> encounterTables = [];

    public void SetRandomEncountersEnabled(bool enabled)
    {
        Progress = Progress with { RandomEncountersEnabled = enabled };
    }

    private void ResumeEncounter()
    {
        if (Host.Process.EncounterRequest is null)
        {
            return;
        }
        Host.ResumeEncounter();
        Progress = Progress with { Encounters = Progress.Encounters with { MovementCounter = 0, Danger = 0 } };
    }

    private void AdvanceEncounter(Ff7Actor actor)
    {
        if (Host.Process.GlobalCommand != Ff7GlobalCommand.None)
        {
            return;
        }
        int tableIndex = Host.Process.BattleTable != 0 ? 1 : 0;
        PcFieldEncounterTable table = encounterTables[tableIndex];
        bool disabled = !Progress.RandomEncountersEnabled || Host.Process.BattlesDisabled || Host.Process.Movie is { Playing: true };
        if (table.Enabled && !disabled && Progress.Encounters.MovementCounter == 224)
        {
            ValidateEncounterMateria();
        }
        Ff7EncounterResult result = Ff7NativeEncounters.Step(Progress.Encounters, table, executable.NativeFieldRandomTable.ToArray(), actor.MoveSpeed, disabled);
        Progress = Progress with { Encounters = result.State };
        if (result.CountStep)
        {
            byte low = unchecked((byte)(Machine.Variables.ReadByte(FieldBank.Persistent3Byte, 6) + 1));
            Machine.Variables.WriteByte(FieldBank.Persistent3Byte, 6, low);
            byte high = Machine.Variables.ReadByte(FieldBank.Persistent3Byte, 7);
            if (low == 0 && high != 255)
            {
                Machine.Variables.WriteByte(FieldBank.Persistent3Byte, 7, (byte)(high + 1));
            }
        }
        if (result.Formation is ushort formation)
        {
            long requestId = Progress.NextEncounterRequestId;
            Progress = Progress with { NextEncounterRequestId = checked(requestId + 1) };
            Host.StartEncounter(new FieldHostRequest(requestId, actor.Entity, 7, 0, new FieldBattleCommand(formation)), result.Initiative);
        }
    }

    private void ValidateEncounterMateria()
    {
        battleKernel ??= KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        foreach (Ff7CharacterState character in Progress.Characters.Where(character => Progress.Party.Contains(character.Id)))
        {
            for (int slot = 0; slot < 16; slot++)
            {
                int id = (byte)BinaryPrimitives.ReadUInt32LittleEndian(character.NativeRecord.AsSpan(64 + slot * 4));
                if (id == 255)
                {
                    continue;
                }
                ReadOnlySpan<byte> materia = battleKernel[8].Data.Span.Slice(id * 20, 20);
                if ((materia[13] & 15) is 0 or 1)
                {
                    throw new NotSupportedException("FFVII independent materia encounter modifiers require their native equipment calculation.");
                }
            }
        }
    }
}
