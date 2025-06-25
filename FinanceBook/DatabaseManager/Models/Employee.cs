namespace DatabaseManager.Models
{
    public class Employee
    {
        public int Id { get; set; }
        public string? FullName { get; set; }  // Может быть null
        public string? Position { get; set; }  // Может быть null
        public int DepartmentId { get; set; }
        public DateTime HireDate { get; set; }
        public string DisplayName => FullName ?? "No Name";
    }
}