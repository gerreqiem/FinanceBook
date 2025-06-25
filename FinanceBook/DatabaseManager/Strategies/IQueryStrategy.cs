namespace DatabaseManager.Strategies
{
    public interface IQueryStrategy
    {
        string ExecuteQuery(string parameters);
    }
}