namespace DatabaseManager.Models
{
    public class Permission
    {
        public int Id { get; set; }
        public string? Name { get; set; }  // Может быть null
        public string DisplayName => Name ?? "No Name";
    }
}