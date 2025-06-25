namespace DatabaseManager.Models
{
    public class Tax
    {
        public int Id { get; set; }
        public int PaymentId { get; set; }
        public string? Type { get; set; }  // Может быть null
        public decimal Amount { get; set; }
        public string DisplayName => $"{Type ?? "N/A"} - {PaymentId}";
    }
}