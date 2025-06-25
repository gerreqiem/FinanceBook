using System;

namespace DatabaseManager.Models
{
    public class Depreciation
    {
        public int Id { get; set; }
        public int AssetId { get; set; }
        public DateTime Month { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; }
    }
}