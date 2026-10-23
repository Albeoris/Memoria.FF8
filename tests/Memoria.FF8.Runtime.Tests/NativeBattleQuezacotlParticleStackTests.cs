using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlParticleStackTests
{
    [Theory]
    [InlineData(0, null)]
    [InlineData(1, 0u)]
    [InlineData(2, 0x0211BD34u)]
    [InlineData(3, 0x0211BD70u)]
    public void Last_particle_saves_its_scheduler_predecessor_before_the_path_pass(int count, uint? expected)
    {
        NativeBattleEffectTaskPool pool = Pool(count);
        var pass = NativeBattleQuezacotlParticleCallbacks.AdvancePool(pool, (current, _) => new(current, 0));
        Assert.Equal(expected, pass.PathOutlineDirectionDword);
    }

    [Theory]
    [InlineData(0, 0x0211BD34u)]
    [InlineData(1, 0u)]
    [InlineData(2, 0x0211BD34u)]
    [InlineData(3, 0u)]
    public void Removed_tasks_do_not_become_EDI_and_a_removed_final_task_still_saves_its_entry_EDI(int removedMask, uint expected)
    {
        NativeBattleEffectTaskPool pool = Pool(2);
        var pass = NativeBattleQuezacotlParticleCallbacks.AdvancePool(pool, (current, index) => new(current, (removedMask & (1 << index)) != 0 ? 2 : 0));
        Assert.Equal(expected, pass.PathOutlineDirectionDword);
    }

    [Fact]
    public void Same_pass_child_is_visited_after_JSON_restore_and_its_save_replaces_the_removed_parent_save()
    {
        NativeBattleEffectTaskPool pool = JsonSerializer.Deserialize<NativeBattleEffectTaskPool>(JsonSerializer.Serialize(Pool(2)))!;
        List<int> visited = [];
        var pass = NativeBattleQuezacotlParticleCallbacks.AdvancePool(pool, (current, index) =>
        {
            visited.Add(index);
            if (index == 1)
            {
                (current, _) = current.Append(0x6C7EA0);
                return new(current, 2);
            }
            return new(current, 0);
        });
        Assert.Equal(new[] { 0, 1, 2 }, visited);
        Assert.Equal(0x0211BD34u, pass.PathOutlineDirectionDword);
    }

    private static NativeBattleEffectTaskPool Pool(int count)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2521718, 0x0211BD34, 0x3C, 32);
        for (int index = 0; index < count; index++)
        {
            (pool, _) = pool.Append(0x6C7EA0);
        }
        return pool;
    }
}
