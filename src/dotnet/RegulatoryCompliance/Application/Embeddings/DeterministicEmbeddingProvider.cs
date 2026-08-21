using System.Security.Cryptography;
using System.Text;

namespace RegulatoryCompliance.Application.Embeddings;

public sealed class DeterministicEmbeddingProvider : IEmbeddingProvider
{
    public EmbeddingModelDescriptor Model { get; } = new("deterministic-local", "1", 64);

    public Task<IReadOnlyList<float[]>> GenerateAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (texts.Count is < 1 or > EmbeddingBatchProcessor.MaximumBatchSize)
            throw new ArgumentOutOfRangeException(nameof(texts));

        var vectors = new List<float[]>(texts.Count);
        foreach (var text in texts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Embedding text is required.", nameof(texts));
            var vector = new float[Model.Dimension];
            foreach (var token in text.ToLowerInvariant().Split(
                         [' ', '\r', '\n', '\t', '.', ',', ';', ':'],
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
                vector[BitConverter.ToUInt32(hash, 0) % vector.Length] += 1f;
            }
            Normalize(vector);
            vectors.Add(vector);
        }
        return Task.FromResult<IReadOnlyList<float[]>>(vectors);
    }

    private static void Normalize(float[] vector)
    {
        var magnitude = Math.Sqrt(vector.Sum(value => value * value));
        if (magnitude == 0)
            throw new ArgumentException("Embedding text produced an empty vector.");
        for (var index = 0; index < vector.Length; index++)
            vector[index] = (float)(vector[index] / magnitude);
    }
}
