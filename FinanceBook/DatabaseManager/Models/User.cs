namespace DatabaseManager.Models
{
    public class User
    {
        public int Id { get; set; }
        public string? Username { get; set; }  // Может быть null
        public string? PasswordHash { get; set; }  // Может быть null
        public string? FullName { get; set; }  // Может быть null
        public string? Email { get; set; }  // Может быть null
        public bool IsActive { get; set; }
        public string DisplayName => FullName ?? "No Name";
    }
}