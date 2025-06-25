namespace DatabaseManager.Models
{
    public class ChartOfAccount
    {
        public int Id { get; set; } // Добавлено свойство Id
        public string Code { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
    }
}