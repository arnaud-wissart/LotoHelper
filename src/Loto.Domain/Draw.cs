namespace Loto.Domain;

public class Draw
{
    public int Id { get; set; }
    public DateTime DrawDate { get; set; }
    public string? OfficialDrawId { get; set; }
    public string? DrawDayName { get; set; }
    public string? Source { get; set; }
    public int Number1 { get; set; }
    public int Number2 { get; set; }
    public int Number3 { get; set; }
    public int Number4 { get; set; }
    public int Number5 { get; set; }
    public int LuckyNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}
