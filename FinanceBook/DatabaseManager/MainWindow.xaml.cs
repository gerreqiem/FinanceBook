using DatabaseManager.Services;
using DatabaseManager.ViewModel;
using System.Windows;
namespace DatabaseManager
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel(new DatabaseService(), new JsonService());
            DataContext = _viewModel;
            Loaded += async (s, e) => await _viewModel.LoadDataAsync();
        }
    }
}