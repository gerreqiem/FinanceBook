namespace DatabaseManager.Models
{
    public class FixedAsset
    {
        public int Id { get; set; }
        public string? Name { get; set; }  // Может быть null
        public string? InventoryNumber { get; set; }  // Может быть null
        public DateTime AcquisitionDate { get; set; }
        public decimal InitialCost { get; set; }
        public int UsefulLife { get; set; }
        public string DisplayName => Name ?? "No Name";
    }
}