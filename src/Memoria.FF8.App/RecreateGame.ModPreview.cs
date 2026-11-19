using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private static int? CatalogFieldActorModel(FieldAssets field, int objectId)
    {
        JsmProgram program = JsmProgram.Read(field.Jsm);
        int[] models = program.Objects.Where(obj => obj.Index == objectId)
            .SelectMany(obj => obj.Scripts)
            .SelectMany(script => script.ReachableOperations)
            .Select(index => program.Operations[index])
            .Where(operation => operation.Opcode == Jsm.Opcode.SETMODEL && operation.Parameter >= 0 && operation.Parameter < field.Models.Count)
            .Select(operation => operation.Parameter)
            .Distinct()
            .Take(2)
            .ToArray();
        return models.Length == 1 ? models[0] : null;
    }
}
