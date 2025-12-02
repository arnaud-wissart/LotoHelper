using Loto.Api.Contracts;

namespace Loto.Api.Services;

public interface ILotoPredictionService
{
    Task<PredictionsResponse> GeneratePredictionsAsync(int count, PredictionStrategy strategy, CancellationToken cancellationToken);
}
