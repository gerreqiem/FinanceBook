namespace DatabaseManager.Models
{
    public class RolePermission
    {
        public int RoleId { get; set; }
        public int PermissionId { get; set; }
        public string DisplayName => $"Role {RoleId} - Permission {PermissionId}";
    }
}