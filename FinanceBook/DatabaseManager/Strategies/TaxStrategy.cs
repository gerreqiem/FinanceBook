using DatabaseManager.Models;
using System;
namespace DatabaseManager.Strategies
{
    public interface ITaxStrategy
    {
        decimal CalculateTax(SalaryPayment payment);
        string GetTaxType();
    }
    public class IncomeTaxStrategy : ITaxStrategy
    {
        public decimal CalculateTax(SalaryPayment payment)
        {
            return (payment.BaseSalary + payment.Bonus) * 0.13m;
        }
        public string GetTaxType() => "НДФЛ";
    }
    public class SocialTaxStrategy : ITaxStrategy
    {
        public decimal CalculateTax(SalaryPayment payment)
        {
            return (payment.BaseSalary + payment.Bonus) * 0.30m;
        }
        public string GetTaxType() => "Социальный налог";
    }
}