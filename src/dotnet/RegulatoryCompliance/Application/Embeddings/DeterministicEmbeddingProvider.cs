using System.Security.Cryptography;
using System.Text;

namespace RegulatoryCompliance.Application.Embeddings;

public sealed class DeterministicEmbeddingProvider : IEmbeddingProvider
{
    public EmbeddingModelDescriptor Model { get; } = new("deterministic-local", "1", 64);

    public Task<IReadOnlyList<float[]>> GenerateAsync(
        IReadOnlyList<EmbeddingInput> inputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Count is < 1 or > EmbeddingBatchProcessor.MaximumBatchSize)
            throw new ArgumentOutOfRangeException(nameof(inputs));

        var vectors = new List<float[]>(inputs.Count);
        foreach (var input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(input);
            var text = input.Text;
            var vector = new float[Model.Dimension];
            var tokens = text.ToLowerInvariant().Split(
                         [' ', '\r', '\n', '\t', '.', ',', ';', ':', '-', '_', '/', '\\', '(', ')', '[', ']', '{', '}', '|', '*', '=', '#', '`', '~', '!', '?', '@', '$', '%', '^', '&', '+', '<', '>', '"', '\''],
                         StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length > 0)
            {
                foreach (var token in tokens)
                {
                    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
                    vector[BitConverter.ToUInt32(hash, 0) % vector.Length] += 1f;
                }
            }
            else
            {
                // Fallback for texts consisting of symbols or characters not in word list
                var rawHash = SHA256.HashData(Encoding.UTF8.GetBytes(text.Trim()));
                for (var i = 0; i < rawHash.Length && i < vector.Length; i++)
                {
                    vector[i] = rawHash[i] + 1f;
                }
            }

            Normalize(vector);
            vectors.Add(vector);
        }
        return Task.FromResult<IReadOnlyList<float[]>>(vectors);
    }

    private static void Normalize(float[] vector)
    {
        var magnitude = Math.Sqrt(vector.Sum(value => value * value));
        if (magnitude == 0 || double.IsNaN(magnitude) || double.IsInfinity(magnitude))
        {
            var uniform = (float)(1.0 / Math.Sqrt(vector.Length));
            for (var index = 0; index < vector.Length; index++)
                vector[index] = uniform;
            return;
        }

        for (var index = 0; index < vector.Length; index++)
            vector[index] = (float)(vector[index] / magnitude);
    }
}
