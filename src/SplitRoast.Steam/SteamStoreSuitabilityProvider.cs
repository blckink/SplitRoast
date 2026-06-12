using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SplitRoast.Core.Abstractions;
using SplitRoast.Core.Models;
using SplitRoast.Core.Services;

namespace SplitRoast.Steam;

/// <summary>
/// Looks up a game's co-op suitability from the public Steam store API
/// (<c>appdetails?filters=categories</c>) and caches the category ids under
/// <c>%AppData%/SplitRoast/meta/{appid}.json</c> so we hit the network at most once
/// per game per month. Every failure path returns <see cref="GameCoopInfo.Unknown"/>;
/// this is a best-effort hint and must never break scanning or the UI.
/// </summary>
public sealed class SteamStoreSuitabilityProvider : IGameSuitabilityProvider
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(30);

    private readonly HttpClient _http;
    private readonly string _cacheDir;
    // Serialise network lookups so a freshly scanned library doesn't fire dozens of
    // store requests at once (the API rate-limits); cached reads stay parallel-safe.
    private readonly SemaphoreSlim _fetchGate = new(1, 1);

    public SteamStoreSuitabilityProvider()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _http.DefaultRequestHeaders.Add("User-Agent", "SplitRoast");
        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SplitRoast", "meta");
    }

    public async Task<GameCoopInfo> GetAsync(SteamGame game, CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<int>? ids = ReadCache(game.AppId)
                ?? await FetchAsync(game.AppId, cancellationToken).ConfigureAwait(false);
            return CoopSuitabilityClassifier.Classify(ids);
        }
        catch
        {
            // Best-effort only: a lookup failure must never bubble into the UI.
            return GameCoopInfo.Unknown;
        }
    }

    private IReadOnlyList<int>? ReadCache(uint appId)
    {
        string path = CachePath(appId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > CacheLifetime)
            {
                return null;
            }

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("categoryIds", out JsonElement arr)
                || arr.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return arr.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.Number)
                .Select(e => e.GetInt32())
                .ToList();
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<int>?> FetchAsync(uint appId, CancellationToken ct)
    {
        await _fetchGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Another caller may have populated the cache while we waited.
            IReadOnlyList<int>? cached = ReadCache(appId);
            if (cached is not null)
            {
                return cached;
            }

            string url = $"https://store.steampowered.com/api/appdetails?appids={appId}&filters=categories";
            string json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
            List<int> ids = ParseCategoryIds(json, appId);

            WriteCache(appId, ids);

            // Be polite to the store API between uncached lookups.
            await Task.Delay(250, ct).ConfigureAwait(false);
            return ids;
        }
        finally
        {
            _fetchGate.Release();
        }
    }

    private static List<int> ParseCategoryIds(string json, uint appId)
    {
        var result = new List<int>();
        using JsonDocument doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty(appId.ToString(), out JsonElement entry)
            || !entry.TryGetProperty("success", out JsonElement ok)
            || ok.ValueKind != JsonValueKind.True
            || !entry.TryGetProperty("data", out JsonElement data)
            || !data.TryGetProperty("categories", out JsonElement cats)
            || cats.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (JsonElement c in cats.EnumerateArray())
        {
            if (c.TryGetProperty("id", out JsonElement id) && id.TryGetInt32(out int value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    private void WriteCache(uint appId, IReadOnlyList<int> ids)
    {
        try
        {
            Directory.CreateDirectory(_cacheDir);
            var payload = new { fetchedUtc = DateTime.UtcNow, categoryIds = ids };
            File.WriteAllText(CachePath(appId), JsonSerializer.Serialize(payload));
        }
        catch
        {
            // Caching is an optimisation; ignore disk failures.
        }
    }

    private string CachePath(uint appId) => Path.Combine(_cacheDir, $"{appId}.json");
}
