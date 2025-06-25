namespace DatabaseManager.Models
{
    public class InventoryMovement
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int? FromWarehouseId { get; set; }
        public int? ToWarehouseId { get; set; }
        public decimal Quantity { get; set; } 
        public DateTime Date { get; set; }
        public string DisplayName => $"Movement {Id} - {ProductId}";
    }
}