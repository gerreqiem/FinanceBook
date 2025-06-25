namespace DatabaseManager.Strategies
{
    public class CustomQueryStrategy : IQueryStrategy
    {
        public string ExecuteQuery(string parameters)
        {
            return $"EXECUTING CUSTOM QUERY: {parameters}";
        }
    }
}