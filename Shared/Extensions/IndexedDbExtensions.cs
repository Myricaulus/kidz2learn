using Tavenem.Blazor.IndexedDB;
using Tavenem.DataStorage;

namespace Kidz2Learn.Shared.Extensions;
public static class IndexedDbExtensions
{
    public static async Task<Dictionary<string, T>> ToDictionaryAsync<T>(this IndexedDbStore store) where T : IIdItem
    {
        Dictionary<string, T> statesById = [];
        await foreach(var item in store.GetAllAsync<T>())
        {
            statesById[item.Id] = item;
        }
        return statesById;
    }
}