using Microsoft.JSInterop;

namespace DropShot.Services;

public record FuzzyPlayerItem(int PlayerId, string DisplayName, string? FirstName, string? LastName, bool IsLight);
public record FuzzySearchResult(int PlayerId, string DisplayName, string? FirstName, string? LastName, bool IsLight, double Score);

public class FuzzySearchService(IJSRuntime js)
{
    public async Task InitializeAsync(IEnumerable<FuzzyPlayerItem> players)
    {
        await js.InvokeVoidAsync("FuzzySearch.initialize", players);
    }

    public async Task<List<FuzzySearchResult>> SearchAsync(string query, int limit = 5)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        return await js.InvokeAsync<List<FuzzySearchResult>>("FuzzySearch.search", query, limit);
    }

    public async Task UpdateCollectionAsync(IEnumerable<FuzzyPlayerItem> players)
    {
        await js.InvokeVoidAsync("FuzzySearch.updateCollection", players);
    }
}
