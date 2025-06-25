namespace DatabaseManager.Models
{
    public class DocumentItem
    {
        public int Id { get; set; } 
        public int DocumentId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
    }
}