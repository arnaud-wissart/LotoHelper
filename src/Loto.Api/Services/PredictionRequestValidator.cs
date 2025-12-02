using Loto.Api.Contracts;

namespace Loto.Api.Services;

public static class PredictionRequestValidator
{
    public static PredictionValidationResult Validate(PredictionRequest request, PredictionOptions options)
    {
        var normalizedCount = request.Count <= 0
            ? options.DefaultCount
            : Math.Min(request.Count, options.MaxCount);

        var include = new HashSet<int>();
        var exclude = new HashSet<int>();

        if (request.IncludeNumbers is { Length: > 0 })
        {
            if (request.IncludeNumbers.Length > options.MaxIncludeNumbers)
            {
                return PredictionValidationResult.Invalid(
                    $"includeNumbers cannot contain more than {options.MaxIncludeNumbers} values.");
            }

            foreach (var n in request.IncludeNumbers)
            {
                if (!IsValidMainNumber(n))
                {
                    return PredictionValidationResult.Invalid("includeNumbers must be between 1 and 49.");
                }
                include.Add(n);
            }
        }

        if (request.ExcludeNumbers is { Length: > 0 })
        {
            foreach (var n in request.ExcludeNumbers)
            {
                if (!IsValidMainNumber(n))
                {
                    return PredictionValidationResult.Invalid("excludeNumbers must be between 1 and 49.");
                }
                exclude.Add(n);
            }
        }

        var hasConstraints =
            request.MinSum.HasValue || request.MaxSum.HasValue ||
            request.MinEven.HasValue || request.MaxEven.HasValue ||
            request.MinLow.HasValue || request.MaxLow.HasValue ||
            include.Count > 0 || exclude.Count > 0;

        PredictionConstraints? constraints = null;

        if (hasConstraints)
        {
            constraints = new PredictionConstraints
            {
                MinSum = request.MinSum,
                MaxSum = request.MaxSum,
                MinEven = request.MinEven,
                MaxEven = request.MaxEven,
                MinLow = request.MinLow,
                MaxLow = request.MaxLow,
                IncludeNumbers = include,
                ExcludeNumbers = exclude
            };
        }

        return PredictionValidationResult.Valid(normalizedCount, constraints);
    }

    private static bool IsValidMainNumber(int value) => value is >= 1 and <= 49;
}

public sealed record PredictionValidationResult(bool IsValid, string? ErrorMessage, int Count, PredictionConstraints? Constraints)
{
    public static PredictionValidationResult Invalid(string message) => new(false, message, 0, null);

    public static PredictionValidationResult Valid(int count, PredictionConstraints? constraints) =>
        new(true, null, count, constraints);
}
