using System.Net.Http;
using Microsoft.Extensions.Options;

namespace Loto.Ingestion.Worker;

public class HttpNewLotoDataSource : IFdjDataSource
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<FdjOptions> _options;
    private readonly ILogger<HttpNewLotoDataSource> _logger;

    public HttpNewLotoDataSource(
        IHttpClientFactory httpClientFactory,
        IOptions<FdjOptions> options,
        ILogger<HttpNewLotoDataSource> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<Stream> GetNewLotoArchiveAsync(CancellationToken cancellationToken)
    {
        var url = _options.Value.NewLotoArchiveUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogError("FDJ NewLotoArchiveUrl is not configured.");
            throw new InvalidOperationException("FDJ NewLotoArchiveUrl is not configured.");
        }

        _logger.LogInformation("Downloading FDJ new-loto archive from {Url}", url);

        var client = _httpClientFactory.CreateClient();
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var memory = new MemoryStream();
        await response.Content.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        _logger.LogInformation("FDJ new-loto archive downloaded ({Length} bytes).", memory.Length);
        return memory;
    }
}
