namespace DatabaseManager.Models
{
    public class UserRole
    {
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public string DisplayName => $"User {UserId} - Role {RoleId}";
    }
}