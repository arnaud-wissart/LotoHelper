namespace Loto.Ingestion.Worker;

public interface IFdjDataSource
{
    Task<Stream> GetNewLotoArchiveAsync(CancellationToken cancellationToken);
}
