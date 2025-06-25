using DatabaseManager.Models;
using System;
namespace DatabaseManager.Strategies
{
    public interface IDepreciationStrategy
    {
        decimal CalculateDepreciation(FixedAsset asset, DateTime month);
        string GetMethodName();
    }
    public class StraightLineDepreciationStrategy : IDepreciationStrategy
    {
        public decimal CalculateDepreciation(FixedAsset asset, DateTime month)
        {
            if (asset.InitialCost <= 0 || asset.UsefulLife <= 0)
                return 0;
            return asset.InitialCost / (asset.UsefulLife * 12);
        }
        public string GetMethodName() => "Линейный";
    }

    public class DecliningBalanceDepreciationStrategy : IDepreciationStrategy
    {
        private readonly double rate; 
        public DecliningBalanceDepreciationStrategy(double rate = 0.2)
        {
            this.rate = rate;
        }
        public decimal CalculateDepreciation(FixedAsset asset, DateTime month)
        {
            if (asset.InitialCost <= 0 || asset.UsefulLife <= 0)
                return 0;
            decimal monthlyRate = (decimal)(rate / 12);
            decimal bookValue = asset.InitialCost;
            return bookValue * monthlyRate;
        }
        public string GetMethodName() => "Уменьшаемый остаток";
    }
}