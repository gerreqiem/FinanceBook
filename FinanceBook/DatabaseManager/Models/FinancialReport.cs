namespace DatabaseManager.Models
{
    public class FinancialReport
    {
        public int Id { get; set; }
        public string? Type { get; set; }  // Может быть null
        public string? Period { get; set; }  // Может быть null
        public int GeneratedBy { get; set; }
        public DateTime CreationDate { get; set; }
        public string DisplayName => $"{Type ?? "N/A"} - {Period ?? "N/A"}";
    }
}