using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleStorageGraphProjection
{
    internal static NativeBattleStorageGraph? Retained(NativeBattleRetainedMemory retained, BattleSnapshot source)
    {
        NativeBattleEffectArena[] views = NativeBattleStorageGraphViews.Enumerate(retained).Where(view => view.StorageReference is not null).ToArray();
        if (views.Length == 0)
        {
            return null;
        }
        NativeBattleStorageGraph graph = source.StorageGraph ?? throw new InvalidDataException("Retained physical references require the source battle ownership metadata.");
        if (graph.Authorities.IsDefault)
        {
            throw new InvalidDataException("Retained physical references require complete authority definitions.");
        }
        Dictionary<int, NativeBattleStorageAuthority> definitions = [];
        foreach (NativeBattleStorageAuthority authority in graph.Authorities)
        {
            if (!definitions.TryAdd(authority.Id, authority))
            {
                throw new InvalidDataException("Source battle ownership metadata repeats a physical authority.");
            }
        }
        NativeBattleEffectArena[] sourceViews = NativeBattleStorageGraphViews.Enumerate(source).ToArray();
        ImmutableArray<NativeBattleStorageAuthority>.Builder selected = ImmutableArray.CreateBuilder<NativeBattleStorageAuthority>();
        foreach (IGrouping<int, NativeBattleEffectArena> group in views.GroupBy(view => view.StorageReference!.AuthorityId))
        {
            if (!definitions.TryGetValue(group.Key, out NativeBattleStorageAuthority? authority))
            {
                throw new InvalidDataException("Retained physical references leave the source battle ownership metadata.");
            }
            foreach (NativeBattleEffectArena view in group)
            {
                NativeBattleStorageReference reference = view.StorageReference!;
                if (reference.Offset < 0 || reference.Length < 0 || reference.Length != view.LogicalLength || reference.Offset > authority.LogicalLength - reference.Length)
                {
                    throw new InvalidDataException("Retained physical references leave their declared authority.");
                }
            }
            if (authority.Content is null && !group.Any(view => view.StorageReference!.DefinesAuthority))
            {
                NativeBattleEffectArena definition = sourceViews.FirstOrDefault(view => view.StorageReference is { DefinesAuthority: true } reference && reference.AuthorityId == authority.Id) ?? throw new InvalidDataException("Retained physical authority is missing its source data definition.");
                NativeBattleStorageRestoreContext.ValidateDefinitionCoverage(definition, authority);
                authority = authority with { Content = NativeBattleStorageRestoreContext.ExtractContent(definition, authority.StoredLength) };
            }
            selected.Add(authority);
        }
        return new(selected.ToImmutable());
    }
}
