namespace DatabaseManager.Models
{
    public class AssetMovement
    {
        public int Id { get; set; }
        public int AssetId { get; set; }
        public string? FromDepartment { get; set; }  // Может быть null
        public string? ToDepartment { get; set; }  // Может быть null
        public DateTime Date { get; set; }
        public string DisplayName => $"Movement {AssetId} - {Date}";    
    }
}