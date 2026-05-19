using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.Infrastructure.AI;

internal sealed class CachedEmbeddingService(
    IEmbeddingService inner,
    IMemoryCache cache) : IEmbeddingService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var key = BuildCacheKey(text);

        if (cache.TryGetValue(key, out float[]? cached) && cached is not null)
            return cached;

        var embedding = await inner.GenerateEmbeddingAsync(text, ct);

        cache.Set(key, embedding, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration,
            Size = 1,
        });

        return embedding;
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IEnumerable<string> texts, CancellationToken ct = default)
    {
        var textList = texts.ToList();
        var results = new float[textList.Count][];
        var uncachedIndices = new List<int>();
        var uncachedTexts = new List<string>();

        for (var i = 0; i < textList.Count; i++)
        {
            var key = BuildCacheKey(textList[i]);
            if (cache.TryGetValue(key, out float[]? cached) && cached is not null)
            {
                results[i] = cached;
            }
            else
            {
                uncachedIndices.Add(i);
                uncachedTexts.Add(textList[i]);
            }
        }

        if (uncachedTexts.Count > 0)
        {
            var generated = await inner.GenerateEmbeddingsAsync(uncachedTexts, ct);
            for (var j = 0; j < uncachedIndices.Count; j++)
            {
                results[uncachedIndices[j]] = generated[j];
                cache.Set(BuildCacheKey(uncachedTexts[j]), generated[j], new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheDuration,
                    Size = 1,
                });
            }
        }

        return results;
    }

    private static string BuildCacheKey(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return $"emb:{Convert.ToHexString(hash)}";
    }
}
