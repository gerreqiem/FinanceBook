namespace DatabaseManager.Models
{
    public class Role
    {
        public int Id { get; set; }
        public string? Name { get; set; }  // Может быть null
        public string DisplayName => Name ?? "No Name";
    }
}