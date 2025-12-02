using Loto.Api.Contracts;
using Loto.Domain;

namespace Loto.Api.Services;

public interface ILotoPredictionService
{
    Task<PredictionsResponse> GeneratePredictionsAsync(
        int count,
        PredictionStrategy strategy,
        PredictionConstraints? constraints,
        CancellationToken cancellationToken,
        Random? random = null,
        IReadOnlyList<Draw>? preloadedDraws = null);
}
