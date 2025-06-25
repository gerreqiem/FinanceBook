namespace DatabaseManager.Strategies
{
    public class CrudStrategy : IQueryStrategy
    {
        public string ExecuteQuery(string parameters)
        {
            return $"EXECUTING CRUD with {parameters}";
        }
    }
}