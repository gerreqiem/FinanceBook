namespace DatabaseManager.Models
{
    public class Inventory
    {
        public int Id { get; set; }
        public int WarehouseId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; } 
        public string DisplayName => $"Warehouse {WarehouseId} - Product {ProductId}";
    }
}