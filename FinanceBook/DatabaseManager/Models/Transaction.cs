using System;

namespace DatabaseManager.Models
{
    public class Transaction
    {
        public int TransactionId { get; set; }
        public DateTime Date { get; set; }
        public int? DebitAccountId { get; set; } 
        public int? CreditAccountId { get; set; } 
        public decimal Amount { get; set; }
        public string Description { get; set; }
    }
}