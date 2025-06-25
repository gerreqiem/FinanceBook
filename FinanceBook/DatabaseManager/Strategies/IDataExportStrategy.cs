namespace DatabaseManager.Services
{
    public interface IDataExportStrategy
    {
        void Export(string filePath, object data);
        object Import(string filePath);
    }
    public class CsvExportStrategy : IDataExportStrategy
    {
        public void Export(string filePath, object data)
        {
            throw new NotImplementedException();
        }
        public object Import(string filePath)
        {
            throw new NotImplementedException();
        }
    }
}