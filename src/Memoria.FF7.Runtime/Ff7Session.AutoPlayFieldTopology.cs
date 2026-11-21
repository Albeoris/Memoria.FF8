using System.Buffers.Binary;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

internal sealed record Ff7AutoPlayFieldTopology(IReadOnlyList<PcWalkmeshTriangle> Walkmesh, IReadOnlyList<PcFieldGateway> Gateways, IReadOnlyList<Ff7AutoPlayLineTransition> Lines);
internal sealed record Ff7AutoPlayLineTransition(Ff7Line Line, int Script, FieldTransitionCommand Transition);

public sealed partial class Ff7Session
{
    private readonly Dictionary<ushort, Ff7AutoPlayFieldTopology> autoPlayFieldTopologies = [];

    internal Ff7AutoPlayFieldTopology ReadAutoPlayFieldTopology(ushort field)
    {
        if (autoPlayFieldTopologies.TryGetValue(field, out Ff7AutoPlayFieldTopology? known))
        {
            return known;
        }
        if (!IsKnownAutoPlayDestination(field))
        {
            throw new ArgumentOutOfRangeException(nameof(field));
        }
        PcFieldFile file = PcFieldFile.ReadCompressed(fields.ReadEntry(fields.Find(mapList[field]).Single()));
        PcFieldScripts scripts = PcFieldScripts.Read(file.GetSection(PcFieldSectionKind.ScriptAndText).Span);
        IReadOnlyList<PcWalkmeshTriangle> walkmesh = PcFieldWalkmesh.Read(file.GetSection(PcFieldSectionKind.Walkmesh).Span);
        PcFieldTriggers triggers = PcFieldTriggers.Read(file.GetSection(PcFieldSectionKind.TriggersAndGateways).Span);
        FieldMachine inspection = new(FieldProgram.FromPcScripts(scripts));
        ReadOnlyMemory<byte> code = scripts.Data;
        List<Ff7AutoPlayLineTransition> lines = [];
        for (int entity = 0; entity < scripts.Entities.Count; entity++)
        {
            FieldScriptAnalysis init = inspection.AnalyzeScript(entity, 0);
            if (!init.Complete)
            {
                continue;
            }
            FieldScriptInstructionSite[] ownInit = init.InstructionSites.Where(site => site.Entity == entity).ToArray();
            FieldScriptInstructionSite[] definitions = ownInit.Where(site => code.Span[site.ProgramCounter] == 0xD0).ToArray();
            if (definitions.Length != 1 || ownInit.Any(site => code.Span[site.ProgramCounter] == 0xD3))
            {
                continue;
            }
            int definition = definitions[0].ProgramCounter;
            ReadOnlySpan<byte> operands = code.Span.Slice(definition, 13);
            // A foreign allocation has no authenticated runtime slot or enabled state.
            // This literal segment supplies potential geometric advice only.
            Ff7Line line = new(-1, entity, BinaryPrimitives.ReadInt16LittleEndian(operands[1..]), BinaryPrimitives.ReadInt16LittleEndian(operands[3..]), BinaryPrimitives.ReadInt16LittleEndian(operands[5..]), BinaryPrimitives.ReadInt16LittleEndian(operands[7..]), BinaryPrimitives.ReadInt16LittleEndian(operands[9..]), BinaryPrimitives.ReadInt16LittleEndian(operands[11..]));
            for (int script = 1; script <= 6; script++)
            {
                FieldScriptAnalysis analysis = inspection.AnalyzeScript(entity, script);
                if (!analysis.Complete || analysis.InstructionSites.Any(site => site.Entity == entity && code.Span[site.ProgramCounter] == 0xD3))
                {
                    continue;
                }
                foreach (FieldScriptInstructionSite site in analysis.InstructionSites)
                {
                    if (code.Span[site.ProgramCounter] != 0x60)
                    {
                        continue;
                    }
                    ReadOnlySpan<byte> transition = code.Span.Slice(site.ProgramCounter, 10);
                    ushort destination = BinaryPrimitives.ReadUInt16LittleEndian(transition[1..]);
                    if (!IsKnownAutoPlayDestination(destination))
                    {
                        continue;
                    }
                    FieldTransitionCommand command = new(destination, BinaryPrimitives.ReadInt16LittleEndian(transition[3..]), BinaryPrimitives.ReadInt16LittleEndian(transition[5..]), BinaryPrimitives.ReadUInt16LittleEndian(transition[7..]), transition[9]);
                    lines.Add(new(line, script, command));
                }
            }
        }
        Ff7AutoPlayFieldTopology result = new(walkmesh, triggers.Gateways, lines.Distinct().ToArray());
        autoPlayFieldTopologies.Add(field, result);
        return result;
    }
}
