namespace Loto.Ingestion.Worker;

public record FdjIngestionResult(int TotalRead, int Inserted, int Skipped, bool UsedFullHistory, bool UsedIncremental);
