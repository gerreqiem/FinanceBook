namespace DatabaseManager.Models
{
    public class BankAccount
    {
        public int Id { get; set; }
        public int CounterpartyId { get; set; }
        public string? AccountNumber { get; set; }  // Может быть null
        public string? BankName { get; set; }  // Может быть null
        public string DisplayName => $"{AccountNumber ?? "N/A"} ({BankName ?? "N/A"})";
    }
}