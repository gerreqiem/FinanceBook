namespace DatabaseManager.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string? Name { get; set; }  // Может быть null
        public string? Unit { get; set; }  // Может быть null
        public int CategoryId { get; set; }
        public string DisplayName => Name ?? "No Name";
    }
}