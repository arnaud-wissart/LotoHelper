using Microsoft.Extensions.Options;

namespace Loto.Ingestion.Worker;

public class FileNewLotoDataSource : IFdjDataSource
{
    private readonly IOptions<FdjOptions> _options;
    private readonly ILogger<FileNewLotoDataSource> _logger;

    public FileNewLotoDataSource(
        IOptions<FdjOptions> options,
        ILogger<FileNewLotoDataSource> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<Stream> GetNewLotoArchiveAsync(CancellationToken cancellationToken)
    {
        var path = _options.Value.LocalArchivePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Fdj:LocalArchivePath is not configured while UseLocalFile=true.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("FDJ local archive not found.", path);
        }

        _logger.LogInformation("Reading FDJ new-loto archive from local file {File}", path);
        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }
}
