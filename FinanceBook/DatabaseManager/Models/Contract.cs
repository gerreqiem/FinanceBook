namespace DatabaseManager.Models
{
    public class Contract
    {
        public int Id { get; set; }
        public int CounterpartyId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal Amount { get; set; }
        public string DisplayName => $"Contract {Id} - {CounterpartyId}";
    }
}