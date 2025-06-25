namespace DatabaseManager.Models
{
    public class Document
    {
        public int Id { get; set; }
        public string? Type { get; set; }  // Может быть null
        public DateTime Date { get; set; }
        public int? CounterpartyId { get; set; }
        public decimal TotalAmount { get; set; }
        public string DisplayName => $"{Type ?? "N/A"} - {Date}";
    }
}