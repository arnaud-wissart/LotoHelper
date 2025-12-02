using Loto.Domain;

namespace Loto.Ingestion.Worker;

public interface IFdjParser
{
    IAsyncEnumerable<Draw> ParseNewLotoArchiveAsync(Stream zipStream, CancellationToken cancellationToken);
}
