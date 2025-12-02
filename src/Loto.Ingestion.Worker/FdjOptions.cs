namespace Loto.Ingestion.Worker;

public class FdjOptions
{
    /// <summary>
    /// URL officielle FDJ pour l’archive ZIP de la nouvelle formule Loto.
    /// </summary>
    public string? NewLotoArchiveUrl { get; set; }

    /// <summary>
    /// Fréquence (en minutes) pour relancer l’ingestion.
    /// </summary>
    public int IntervalMinutes { get; set; } = 1440;

    /// <summary>
    /// Durée minimale entre deux rafraîchissements forcés (ensure fresh).
    /// </summary>
    public int MinRefreshAgeMinutes { get; set; } = 60;

    /// <summary>
    /// Mode fichier local (dev/test).
    /// </summary>
    public bool UseLocalFile { get; set; }
    public string? LocalArchivePath { get; set; }
}
