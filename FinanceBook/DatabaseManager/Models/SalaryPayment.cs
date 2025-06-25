namespace DatabaseManager.Models
{
    public class SalaryPayment
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public DateTime Month { get; set; }
        public decimal BaseSalary { get; set; }
        public decimal Bonus { get; set; }
        public decimal TaxDeduction { get; set; }
        public string DisplayName => $"Payment {EmployeeId} - {Month}";
    }
}