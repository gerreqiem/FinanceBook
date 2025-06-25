namespace DatabaseManager.Models
{
    public class Counterparty
    {
        public int Id { get; set; }
        public string? Name { get; set; }  // Может быть null
        public string? Type { get; set; }  // Может быть null
        public string? TaxNumber { get; set; }  // Может быть null
        public string? BankDetails { get; set; }  // Может быть null
        public string DisplayName => Name ?? "No Name";
    }
}