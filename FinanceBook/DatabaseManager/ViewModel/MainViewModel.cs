using DatabaseManager.Models;
using DatabaseManager.Services;
using DatabaseManager.Strategies;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
namespace DatabaseManager.ViewModel
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _dbService;
        private readonly JsonService _jsonService;
        private ObservableCollection<object> _items;
        private string _selectedTableName;
        private bool _isBusy;
        private string _selectedDepreciationMethod;
        private ObservableCollection<string> _depreciationMethods;
        public ObservableCollection<string> TableNames { get; } = new ObservableCollection<string>
        {
            "Users", "Roles", "UserRoles", "Permissions", "RolePermissions", "Counterparties", "BankAccounts",
            "Contracts", "ChartOfAccounts", "Transactions", "Documents", "DocumentItems", "FixedAssets",
            "Depreciation", "AssetMovements", "Warehouses", "ProductCategories", "Products", "Inventory",
            "InventoryMovements", "Departments", "Employees", "SalaryPayments", "Taxes", "FinancialReports"
        };
        public ObservableCollection<string> DepreciationMethods
        {
            get => _depreciationMethods;
            set
            {
                _depreciationMethods = value;
                OnPropertyChanged(nameof(DepreciationMethods));
            }
        }
        public string SelectedDepreciationMethod
        {
            get => _selectedDepreciationMethod;
            set
            {
                _selectedDepreciationMethod = value;
                OnPropertyChanged(nameof(SelectedDepreciationMethod));
            }
        }
        public ObservableCollection<object> Items
        {
            get => _items;
            set
            {
                _items = value;
                OnPropertyChanged(nameof(Items));
            }
        }
        public string SelectedTableName
        {
            get => _selectedTableName;
            set
            {
                _selectedTableName = value;
                OnPropertyChanged(nameof(SelectedTableName));
                _ = LoadDataAsync();
            }
        }
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
            }
        }
        private ICommand _exportCommand;
        private ICommand _importCommand;
        private ICommand _calculateDepreciationCommand;
        private ICommand _registerTransactionCommand;
        private ICommand _generateTrialBalanceCommand;
        private ICommand _calculateReceivablesPayablesCommand;
        private ICommand _performInventoryCheckCommand;
        private ICommand _calculateSalaryCommand;
        private ICommand _generateBalanceSheetCommand;
        private ICommand _calculateVATCommand;
        public ICommand ExportCommand => _exportCommand ??= new RelayCommand(async () =>
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                DefaultExt = "json",
                FileName = "db_export.json"
            };
            if (saveDialog.ShowDialog() == true)
            {
                await ExportToJsonAsync(saveDialog.FileName);
            }
        });
        public ICommand ImportCommand => _importCommand ??= new RelayCommand(async () =>
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                DefaultExt = "json"
            };

            if (openDialog.ShowDialog() == true)
            {
                await ImportFromJsonAsync(openDialog.FileName);
            }
        });
        public ICommand CalculateDepreciationCommand => _calculateDepreciationCommand ??= new RelayCommand(async () =>
        {
            if (SelectedTableName != "FixedAssets" || Items == null || !Items.Any())
            {
                MessageBox.Show("Выберите таблицу 'FixedAssets' и убедитесь, что она содержит данные.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(SelectedDepreciationMethod))
            {
                MessageBox.Show("Выберите метод амортизации.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            IsBusy = true;
            try
            {
                IDepreciationStrategy strategy = SelectedDepreciationMethod switch
                {
                    "Линейный" => new StraightLineDepreciationStrategy(),
                    "Уменьшаемый остаток" => new DecliningBalanceDepreciationStrategy(),
                    _ => throw new ArgumentException("Неизвестный метод амортизации")
                };
                foreach (var item in Items.Cast<FixedAsset>())
                {
                    await _dbService.CalculateAndSaveDepreciationAsync(item, DateTime.Now, strategy);
                }
                SelectedTableName = "Depreciation";
                await LoadDataAsync();
                MessageBox.Show("Амортизация успешно рассчитана и сохранена!", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка расчета амортизации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        });
        public ICommand RegisterTransactionCommand => _registerTransactionCommand ??= new RelayCommand(async () =>
        {
            try
            {
                var transaction = new Transaction
                {
                    TransactionId = (await _dbService.ExecuteQueryAsync("SELECT COALESCE(MAX(transaction_id), 0) + 1 FROM Transactions", r => r.GetInt32(0))).First(),
                    Date = DateTime.Now,
                    DebitAccountId = 1, 
                    CreditAccountId = 2, 
                    Amount = 1000m,
                    Description = "Примерная проводка"
                };
                await _dbService.RegisterTransactionAsync(transaction);
                SelectedTableName = "Transactions";
                await LoadDataAsync();
                MessageBox.Show("Проводка успешно зарегистрирована!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка регистрации проводки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
        public ICommand GenerateTrialBalanceCommand => _generateTrialBalanceCommand ??= new RelayCommand(async () =>
        {
            try
            {
                var trialBalance = await _dbService.GenerateTrialBalanceAsync();
                var result = string.Join("\n", trialBalance.Select(kv => $"Счет {kv.Key}: Дебет = {kv.Value.Debit}, Кредит = {kv.Value.Credit}, Баланс = {kv.Value.Balance}"));
                MessageBox.Show($"Оборотно-сальдовая ведомость:\n{result}", "Результат", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка формирования ведомости: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
        public ICommand CalculateReceivablesPayablesCommand => _calculateReceivablesPayablesCommand ??= new RelayCommand(async () =>
        {
            try
            {
                var payables = await _dbService.CalculateReceivablesPayablesAsync();
                var result = string.Join("\n", payables.Select(kv => $"Контрагент {kv.Key}: Баланс = {kv.Value}"));
                MessageBox.Show($"Дебиторская/кредиторская задолженность: {result}", "Результат", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка расчета задолженности: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
        public ICommand PerformInventoryCheckCommand => _performInventoryCheckCommand ??= new RelayCommand(async () =>
        {
            try
            {
                var warehouseId = 1; 
                var inventory = await _dbService.PerformInventoryCheckAsync(warehouseId);
                var result = string.Join("\n", inventory.Select(kv => $"Продукт {kv.Key}: Количество = {kv.Value}"));
                MessageBox.Show($"Результат инвентаризации для склада {warehouseId}:\n{result}", "Результат", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инвентаризации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
        public ICommand CalculateSalaryCommand => _calculateSalaryCommand ??= new RelayCommand(async () =>
        {
            if (SelectedTableName != "Employees" || Items == null || !Items.Any())
            {
                MessageBox.Show("Выберите таблицу 'Employees' и убедитесь, что она содержит данные.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            IsBusy = true;
            try
            {
                var taxStrategy = new IncomeTaxStrategy(); 
                foreach (var item in Items.Cast<Employee>())
                {
                    await _dbService.CalculateAndSaveSalaryAsync(item, DateTime.Now, 50000m, 5000m, taxStrategy);
                }
                SelectedTableName = "SalaryPayments";
                await LoadDataAsync();
                MessageBox.Show("Зарплата успешно начислена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка начисления зарплаты: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        });
        public ICommand GenerateBalanceSheetCommand => _generateBalanceSheetCommand ??= new RelayCommand(async () =>
        {
            try
            {
                var balanceSheet = await _dbService.GenerateBalanceSheetAsync();
                var result = string.Join("\n", balanceSheet.Select(kv => $"{kv.Key}: {kv.Value}"));
                MessageBox.Show($"Бухгалтерский баланс:\n{result}", "Результат", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка формирования баланса: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
        public ICommand CalculateVATCommand => _calculateVATCommand ??= new RelayCommand(async () =>
        {
            try
            {
                var vat = await _dbService.CalculateVATAsync();
                var result = vat.Count > 0
                    ? string.Join("\n", vat.Select(kv => $"Контрагент {kv.Key}: НДС = {kv.Value:F2}"))
                    : "Нет данных для расчета НДС. Проверьте документы с типом 'Продажа'.";
                MessageBox.Show($"Расчет НДС:\n{result}", "Результат", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка расчета НДС: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
        public MainViewModel(DatabaseService dbService, JsonService jsonService)
        {
            _dbService = dbService;
            _jsonService = jsonService;
            _items = new ObservableCollection<object>();
            _selectedTableName = TableNames.First();
            _depreciationMethods = new ObservableCollection<string> { "Линейный", "Уменьшаемый остаток" };
            _selectedDepreciationMethod = _depreciationMethods.First();
        }
        public async Task LoadDataAsync()
        {
            if (string.IsNullOrEmpty(SelectedTableName))
                return;
            IsBusy = true;
            try
            {
                var data = await GetTableDataAsync(SelectedTableName);
                Items = new ObservableCollection<object>(data);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
        private async Task<IEnumerable<object>> GetTableDataAsync(string tableName)
        {
            return tableName switch
            {
                "Users" => await _dbService.ExecuteQueryAsync("SELECT * FROM Users", MapUser),
                "Roles" => await _dbService.ExecuteQueryAsync("SELECT * FROM Roles", MapRole),
                "UserRoles" => await _dbService.ExecuteQueryAsync("SELECT * FROM UserRoles", MapUserRole),
                "Permissions" => await _dbService.ExecuteQueryAsync("SELECT * FROM Permissions", MapPermission),
                "RolePermissions" => await _dbService.ExecuteQueryAsync("SELECT * FROM RolePermissions", MapRolePermission),
                "Counterparties" => await _dbService.ExecuteQueryAsync("SELECT * FROM Counterparties", MapCounterparty),
                "BankAccounts" => await _dbService.ExecuteQueryAsync("SELECT * FROM BankAccounts", MapBankAccount),
                "Contracts" => await _dbService.ExecuteQueryAsync("SELECT * FROM Contracts", MapContract),
                "ChartOfAccounts" => await _dbService.ExecuteQueryAsync("SELECT * FROM ChartOfAccounts", MapChartOfAccount),
                "Transactions" => await _dbService.ExecuteQueryAsync("SELECT * FROM Transactions", MapTransaction),
                "Documents" => await _dbService.ExecuteQueryAsync("SELECT * FROM Documents", MapDocument),
                "DocumentItems" => await _dbService.ExecuteQueryAsync("SELECT * FROM DocumentItems", MapDocumentItem),
                "FixedAssets" => await _dbService.ExecuteQueryAsync("SELECT * FROM FixedAssets", MapFixedAsset),
                "Depreciation" => await _dbService.ExecuteQueryAsync("SELECT * FROM Depreciation", MapDepreciation),
                "AssetMovements" => await _dbService.ExecuteQueryAsync("SELECT * FROM AssetMovements", MapAssetMovement),
                "Warehouses" => await _dbService.ExecuteQueryAsync("SELECT * FROM Warehouses", MapWarehouse),
                "ProductCategories" => await _dbService.ExecuteQueryAsync("SELECT * FROM ProductCategories", MapProductCategory),
                "Products" => await _dbService.ExecuteQueryAsync("SELECT * FROM Products", MapProduct),
                "Inventory" => await _dbService.ExecuteQueryAsync("SELECT * FROM Inventory", MapInventory),
                "InventoryMovements" => await _dbService.ExecuteQueryAsync("SELECT * FROM InventoryMovements", MapInventoryMovement),
                "Departments" => await _dbService.ExecuteQueryAsync("SELECT * FROM Departments", MapDepartment),
                "Employees" => await _dbService.ExecuteQueryAsync("SELECT * FROM Employees", MapEmployee),
                "SalaryPayments" => await _dbService.ExecuteQueryAsync("SELECT * FROM SalaryPayments", MapSalaryPayment),
                "Taxes" => await _dbService.ExecuteQueryAsync("SELECT * FROM Taxes", MapTax),
                "FinancialReports" => await _dbService.ExecuteQueryAsync("SELECT * FROM FinancialReports", MapFinancialReport),
                _ => throw new ArgumentException($"Неизвестная таблица: {tableName}")
            };
        }
        public async Task ExportToJsonAsync(string filePath)
        {
            IsBusy = true;
            try
            {
                var exportData = new Dictionary<string, object>();
                foreach (var table in TableNames)
                {
                    try
                    {
                        var data = await GetTableDataAsync(table);
                        exportData[table] = data;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка экспорта таблицы {table}: {ex.Message}");
                    }
                }
                await _jsonService.ExportToJsonAsync(filePath, exportData);
                MessageBox.Show("Экспорт успешно завершен!", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
        public async Task ImportFromJsonAsync(string filePath)
        {
            if (MessageBox.Show("Это перезапишет текущие данные. Продолжить?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            IsBusy = true;
            try
            {
                var importData = await _jsonService.ImportFromJsonAsync<Dictionary<string, List<Dictionary<string, object>>>>(filePath);
                if (importData == null || !importData.ContainsKey(SelectedTableName))
                {
                    MessageBox.Show($"Данные для таблицы '{SelectedTableName}' не найдены", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var tableData = importData[SelectedTableName];
                if (tableData == null || !tableData.Any())
                {
                    MessageBox.Show($"Нет данных для импорта в таблицу '{SelectedTableName}'", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var items = ConvertImportedData(SelectedTableName, tableData);
                await _dbService.SaveItemsAsync(SelectedTableName, items);
                await LoadDataAsync();
                MessageBox.Show($"Успешно импортировано {items.Count()} записей!", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка импорта: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine($"Ошибка импорта: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }
        private IEnumerable<object> ConvertImportedData(string tableName, List<Dictionary<string, object>> data)
        {
            Console.WriteLine($"Converting data for table: {tableName}");
            if (data.Any())
            {
                Console.WriteLine($"First item keys: {string.Join(", ", data.First().Keys)}");
                Console.WriteLine($"First item values: {string.Join(", ", data.First().Values)}");
            }
            return tableName switch
            {
                "Users" => data.Select(d => new User
                {
                    Id = d.ContainsKey("user_id") && d["user_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["user_id"])) : 0,
                    Username = d.ContainsKey("username") ? d["username"]?.ToString() : null,
                    PasswordHash = d.ContainsKey("password_hash") ? d["password_hash"]?.ToString() : null,
                    FullName = d.ContainsKey("full_name") ? d["full_name"]?.ToString() : null,
                    Email = d.ContainsKey("email") ? d["email"]?.ToString() : null,
                    IsActive = d.ContainsKey("is_active") && d["is_active"] != null && Convert.ToBoolean(d["is_active"])
                }),
                "Roles" => data.Select(d => new Role
                {
                    Id = d.ContainsKey("role_id") && d["role_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["role_id"])) : 0,
                    Name = d.ContainsKey("name") ? d["name"]?.ToString() : null
                }),
                "UserRoles" => data.Select(d => new UserRole
                {
                    UserId = d.ContainsKey("user_id") && d["user_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["user_id"])) : 0,
                    RoleId = d.ContainsKey("role_id") && d["role_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["role_id"])) : 0
                }),
                "Permissions" => data.Select(d => new Permission
                {
                    Id = d.ContainsKey("permission_id") && d["permission_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["permission_id"])) : 0,
                    Name = d.ContainsKey("name") ? d["name"]?.ToString() : null
                }),
                "RolePermissions" => data.Select(d => new RolePermission
                {
                    RoleId = d.ContainsKey("role_id") && d["role_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["role_id"])) : 0,
                    PermissionId = d.ContainsKey("permission_id") && d["permission_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["permission_id"])) : 0
                }),
                "Counterparties" => data.Select(d => new Counterparty
                {
                    Id = d.ContainsKey("counterparty_id") && d["counterparty_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["counterparty_id"])) : 0,
                    Name = d.ContainsKey("name") ? d["name"]?.ToString() : null,
                    Type = d.ContainsKey("type") ? d["type"]?.ToString() : null,
                    TaxNumber = d.ContainsKey("tax_number") ? d["tax_number"]?.ToString() : null,
                    BankDetails = d.ContainsKey("bank_details") ? d["bank_details"]?.ToString() : null
                }),
                "BankAccounts" => data.Select(d => new BankAccount
                {
                    Id = d.ContainsKey("account_id") && d["account_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["account_id"])) : 0,
                    CounterpartyId = d.ContainsKey("counterparty_id") && d["counterparty_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["counterparty_id"])) : 0,
                    AccountNumber = d.ContainsKey("account_number") ? d["account_number"]?.ToString() : null,
                    BankName = d.ContainsKey("bank_name") ? d["bank_name"]?.ToString() : null
                }),
                "Contracts" => data.Select(d => new Contract
                {
                    Id = d.ContainsKey("contract_id") && d["contract_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["contract_id"])) : 0,
                    CounterpartyId = d.ContainsKey("counterparty_id") && d["counterparty_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["counterparty_id"])) : 0,
                    StartDate = d.ContainsKey("start_date") && d["start_date"] != null ? Convert.ToDateTime(d["start_date"]) : default,
                    EndDate = d.ContainsKey("end_date") && d["end_date"] != null ? Convert.ToDateTime(d["end_date"]) : null,
                    Amount = d.ContainsKey("amount") && d["amount"] != null ? Convert.ToDecimal(d["amount"]) : 0
                }),
                "ChartOfAccounts" => data.Select(d => new ChartOfAccount
                {
                    Id = d.ContainsKey("account_id") && d["account_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["account_id"])) : 0,
                    Code = d.ContainsKey("code") ? d["code"]?.ToString() : null,
                    Name = d.ContainsKey("name") ? d["name"]?.ToString() : null,
                    Type = d.ContainsKey("type") ? d["type"]?.ToString() : null
                }),
                "Transactions" => data.Select(d => new Transaction
                {
                    TransactionId = d.ContainsKey("transaction_id") && d["transaction_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["transaction_id"])) : 0,
                    Date = d.ContainsKey("date") && d["date"] != null ? Convert.ToDateTime(d["date"]) : default,
                    DebitAccountId = d.ContainsKey("debit_account_id") && d["debit_account_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["debit_account_id"])) : null,
                    CreditAccountId = d.ContainsKey("credit_account_id") && d["credit_account_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["credit_account_id"])) : null,
                    Amount = d.ContainsKey("amount") && d["amount"] != null ? Convert.ToDecimal(d["amount"]) : 0,
                    Description = d.ContainsKey("description") ? d["description"]?.ToString() : null
                }),
                "Documents" => data.Select(d => new Document
                {
                    Id = d.ContainsKey("document_id") && d["document_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["document_id"])) : 0,
                    Type = d.ContainsKey("type") ? d["type"]?.ToString() : null,
                    Date = d.ContainsKey("date") && d["date"] != null ? Convert.ToDateTime(d["date"]) : default,
                    CounterpartyId = d.ContainsKey("counterparty_id") && d["counterparty_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["counterparty_id"])) : null,
                    TotalAmount = d.ContainsKey("total_amount") && d["total_amount"] != null ? Convert.ToDecimal(d["total_amount"]) : 0
                }),
                "DocumentItems" => data.Select(d => new DocumentItem
                {
                    Id = d.ContainsKey("item_id") && d["item_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["item_id"])) : 0,
                    DocumentId = d.ContainsKey("document_id") && d["document_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["document_id"])) : 0,
                    ProductId = d.ContainsKey("product_id") && d["product_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["product_id"])) : 0,
                    Quantity = d.ContainsKey("quantity") && d["quantity"] != null ? Convert.ToDecimal(d["quantity"]) : 0,
                    Price = d.ContainsKey("price") && d["price"] != null ? Convert.ToDecimal(d["price"]) : 0
                }),
                "FixedAssets" => data.Select(d => new FixedAsset
                {
                    Id = d.ContainsKey("asset_id") && d["asset_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["asset_id"])) : 0,
                    Name = d.ContainsKey("name") ? d["name"]?.ToString() : null,
                    InventoryNumber = d.ContainsKey("inventory_number") ? d["inventory_number"]?.ToString() : null,
                    AcquisitionDate = d.ContainsKey("acquisition_date") && d["acquisition_date"] != null ? Convert.ToDateTime(d["acquisition_date"]) : default,
                    InitialCost = d.ContainsKey("initial_cost") && d["initial_cost"] != null ? Convert.ToDecimal(d["initial_cost"]) : 0,
                    UsefulLife = d.ContainsKey("useful_life") && d["useful_life"] != null ? Convert.ToInt32(Convert.ToDecimal(d["useful_life"])) : 0
                }),
                "Depreciation" => data.Select(d => new Depreciation
                {
                    Id = d.ContainsKey("depreciation_id") && d["depreciation_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["depreciation_id"])) : 0,
                    AssetId = d.ContainsKey("asset_id") && d["asset_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["asset_id"])) : 0,
                    Month = d.ContainsKey("month") && d["month"] != null ? Convert.ToDateTime(d["month"]) : default,
                    Amount = d.ContainsKey("amount") && d["amount"] != null ? Convert.ToDecimal(d["amount"]) : 0,
                    Method = d.ContainsKey("method") ? d["method"]?.ToString() : null
                }),
                "AssetMovements" => data.Select(d => new AssetMovement
                {
                    Id = d.ContainsKey("movement_id") && d["movement_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["movement_id"])) : 0,
                    AssetId = d.ContainsKey("asset_id") && d["asset_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["asset_id"])) : 0,
                    FromDepartment = d.ContainsKey("from_department") ? d["from_department"]?.ToString() : null,
                    ToDepartment = d.ContainsKey("to_department") ? d["to_department"]?.ToString() : null,
                    Date = d.ContainsKey("date") && d["date"] != null ? Convert.ToDateTime(d["date"]) : default
                }),
                "Warehouses" => data.Select(d => new Warehouse
                {
                    Id = d.ContainsKey("warehouse_id") && d["warehouse_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["warehouse_id"])) : 0,
                    Name = d.ContainsKey("name") ? d["name"]?.ToString() : null,
                    Address = d.ContainsKey("address") ? d["address"]?.ToString() : null
                }),
                "ProductCategories" => data.Select(d => new ProductCategory
                {
                    Id = d.ContainsKey("category_id") && d["category_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["category_id"])) : 0,
                    Name = d.ContainsKey("name") ? d["name"]?.ToString() : null
                }),
                "Products" => data.Select(d => new Product
                {
                    Id = d.ContainsKey("product_id") && d["product_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["product_id"])) : 0,
                    Name = d.ContainsKey("name") ? d["name"]?.ToString() : null,
                    Unit = d.ContainsKey("unit") ? d["unit"]?.ToString() : null,
                    CategoryId = d.ContainsKey("category_id") && d["category_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["category_id"])) : 0
                }),
                "Inventory" => data.Select(d => new Inventory
                {
                    Id = d.ContainsKey("inventory_id") && d["inventory_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["inventory_id"])) : 0,
                    WarehouseId = d.ContainsKey("warehouse_id") && d["warehouse_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["warehouse_id"])) : 0,
                    ProductId = d.ContainsKey("product_id") && d["product_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["product_id"])) : 0,
                    Quantity = d.ContainsKey("quantity") && d["quantity"] != null ? Convert.ToDecimal(d["quantity"]) : 0
                }),
                "InventoryMovements" => data.Select(d => new InventoryMovement
                {
                    Id = d.ContainsKey("movement_id") && d["movement_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["movement_id"])) : 0,
                    ProductId = d.ContainsKey("product_id") && d["product_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["product_id"])) : 0,
                    FromWarehouseId = d.ContainsKey("from_warehouse_id") && d["from_warehouse_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["from_warehouse_id"])) : null,
                    ToWarehouseId = d.ContainsKey("to_warehouse_id") && d["to_warehouse_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["to_warehouse_id"])) : null,
                    Quantity = d.ContainsKey("quantity") && d["quantity"] != null ? Convert.ToDecimal(d["quantity"]) : 0,
                    Date = d.ContainsKey("date") && d["date"] != null ? Convert.ToDateTime(d["date"]) : default
                }),
                "Departments" => data.Select(d => new Department
                {
                    Id = d.ContainsKey("department_id") && d["department_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["department_id"])) : 0,
                    Name = d.ContainsKey("name") ? d["name"]?.ToString() : null
                }),
                "Employees" => data.Select(d => new Employee
                {
                    Id = d.ContainsKey("employee_id") && d["employee_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["employee_id"])) : 0,
                    FullName = d.ContainsKey("full_name") ? d["full_name"]?.ToString() : null,
                    Position = d.ContainsKey("position") ? d["position"]?.ToString() : null,
                    DepartmentId = d.ContainsKey("department_id") && d["department_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["department_id"])) : 0,
                    HireDate = d.ContainsKey("hire_date") && d["hire_date"] != null ? Convert.ToDateTime(d["hire_date"]) : default
                }),
                "SalaryPayments" => data.Select(d => new SalaryPayment
                {
                    Id = d.ContainsKey("payment_id") && d["payment_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["payment_id"])) : 0,
                    EmployeeId = d.ContainsKey("employee_id") && d["employee_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["employee_id"])) : 0,
                    Month = d.ContainsKey("month") && d["month"] != null ? Convert.ToDateTime(d["month"]) : default,
                    BaseSalary = d.ContainsKey("base_salary") && d["base_salary"] != null ? Convert.ToDecimal(d["base_salary"]) : 0,
                    Bonus = d.ContainsKey("bonus") && d["bonus"] != null ? Convert.ToDecimal(d["bonus"]) : 0,
                    TaxDeduction = d.ContainsKey("tax_deduction") && d["tax_deduction"] != null ? Convert.ToDecimal(d["tax_deduction"]) : 0
                }),
                "Taxes" => data.Select(d => new Tax
                {
                    Id = d.ContainsKey("tax_id") && d["tax_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["tax_id"])) : 0,
                    PaymentId = d.ContainsKey("payment_id") && d["payment_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["payment_id"])) : 0,
                    Type = d.ContainsKey("type") ? d["type"]?.ToString() : null,
                    Amount = d.ContainsKey("amount") && d["amount"] != null ? Convert.ToDecimal(d["amount"]) : 0
                }),
                "FinancialReports" => data.Select(d => new FinancialReport
                {
                    Id = d.ContainsKey("report_id") && d["report_id"] != null ? Convert.ToInt32(Convert.ToDecimal(d["report_id"])) : 0,
                    Type = d.ContainsKey("type") ? d["type"]?.ToString() : null,
                    Period = d.ContainsKey("period") ? d["period"]?.ToString() : null,
                    GeneratedBy = d.ContainsKey("generated_by") && d["generated_by"] != null ? Convert.ToInt32(Convert.ToDecimal(d["generated_by"])) : 0,
                    CreationDate = d.ContainsKey("creation_date") && d["creation_date"] != null ? Convert.ToDateTime(d["creation_date"]) : default
                }),
                _ => throw new ArgumentException($"Неизвестная таблица: {tableName}")
            };
        }
        private User MapUser(NpgsqlDataReader reader) => new User
        {
            Id = reader.GetInt32(reader.GetOrdinal("user_id")),
            Username = reader.IsDBNull(reader.GetOrdinal("username")) ? null : reader.GetString(reader.GetOrdinal("username")),
            PasswordHash = reader.IsDBNull(reader.GetOrdinal("password_hash")) ? null : reader.GetString(reader.GetOrdinal("password_hash")),
            FullName = reader.IsDBNull(reader.GetOrdinal("full_name")) ? null : reader.GetString(reader.GetOrdinal("full_name")),
            Email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("is_active"))
        };
        private Role MapRole(NpgsqlDataReader reader) => new Role
        {
            Id = reader.GetInt32(reader.GetOrdinal("role_id")),
            Name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name"))
        };
        private UserRole MapUserRole(NpgsqlDataReader reader) => new UserRole
        {
            UserId = reader.GetInt32(reader.GetOrdinal("user_id")),
            RoleId = reader.GetInt32(reader.GetOrdinal("role_id"))
        };
        private Permission MapPermission(NpgsqlDataReader reader) => new Permission
        {
            Id = reader.GetInt32(reader.GetOrdinal("permission_id")),
            Name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name"))
        };
        private RolePermission MapRolePermission(NpgsqlDataReader reader) => new RolePermission
        {
            RoleId = reader.GetInt32(reader.GetOrdinal("role_id")),
            PermissionId = reader.GetInt32(reader.GetOrdinal("permission_id"))
        };
        private Counterparty MapCounterparty(NpgsqlDataReader reader) => new Counterparty
        {
            Id = reader.GetInt32(reader.GetOrdinal("counterparty_id")),
            Name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name")),
            Type = reader.IsDBNull(reader.GetOrdinal("type")) ? null : reader.GetString(reader.GetOrdinal("type")),
            TaxNumber = reader.IsDBNull(reader.GetOrdinal("tax_number")) ? null : reader.GetString(reader.GetOrdinal("tax_number")),
            BankDetails = reader.IsDBNull(reader.GetOrdinal("bank_details")) ? null : reader.GetString(reader.GetOrdinal("bank_details"))
        };
        private BankAccount MapBankAccount(NpgsqlDataReader reader) => new BankAccount
        {
            Id = reader.GetInt32(reader.GetOrdinal("account_id")),
            CounterpartyId = reader.GetInt32(reader.GetOrdinal("counterparty_id")),
            AccountNumber = reader.IsDBNull(reader.GetOrdinal("account_number")) ? null : reader.GetString(reader.GetOrdinal("account_number")),
            BankName = reader.IsDBNull(reader.GetOrdinal("bank_name")) ? null : reader.GetString(reader.GetOrdinal("bank_name"))
        };
        private Contract MapContract(NpgsqlDataReader reader) => new Contract
        {
            Id = reader.GetInt32(reader.GetOrdinal("contract_id")),
            CounterpartyId = reader.GetInt32(reader.GetOrdinal("counterparty_id")),
            StartDate = reader.GetDateTime(reader.GetOrdinal("start_date")),
            EndDate = reader.IsDBNull(reader.GetOrdinal("end_date")) ? null : reader.GetDateTime(reader.GetOrdinal("end_date")),
            Amount = reader.GetDecimal(reader.GetOrdinal("amount"))
        };
        private ChartOfAccount MapChartOfAccount(NpgsqlDataReader reader) => new ChartOfAccount
        {
            Id = reader.GetInt32(reader.GetOrdinal("account_id")),
            Code = reader.IsDBNull(reader.GetOrdinal("code")) ? null : reader.GetString(reader.GetOrdinal("code")),
            Name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name")),
            Type = reader.IsDBNull(reader.GetOrdinal("type")) ? null : reader.GetString(reader.GetOrdinal("type"))
        };
        private Transaction MapTransaction(NpgsqlDataReader reader) => new Transaction
        {
            TransactionId = reader.GetInt32(reader.GetOrdinal("transaction_id")),
            Date = reader.GetDateTime(reader.GetOrdinal("date")),
            DebitAccountId = reader.IsDBNull(reader.GetOrdinal("debit_account_id")) ? null : reader.GetInt32(reader.GetOrdinal("debit_account_id")),
            CreditAccountId = reader.IsDBNull(reader.GetOrdinal("credit_account_id")) ? null : reader.GetInt32(reader.GetOrdinal("credit_account_id")),
            Amount = reader.GetDecimal(reader.GetOrdinal("amount")),
            Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description"))
        };
        private Document MapDocument(NpgsqlDataReader reader) => new Document
        {
            Id = reader.GetInt32(reader.GetOrdinal("document_id")),
            Type = reader.IsDBNull(reader.GetOrdinal("type")) ? null : reader.GetString(reader.GetOrdinal("type")),
            Date = reader.GetDateTime(reader.GetOrdinal("date")),
            CounterpartyId = reader.IsDBNull(reader.GetOrdinal("counterparty_id")) ? null : reader.GetInt32(reader.GetOrdinal("counterparty_id")),
            TotalAmount = reader.GetDecimal(reader.GetOrdinal("total_amount"))
        };
        private DocumentItem MapDocumentItem(NpgsqlDataReader reader) => new DocumentItem
        {
            Id = reader.GetInt32(reader.GetOrdinal("item_id")),
            DocumentId = reader.GetInt32(reader.GetOrdinal("document_id")),
            ProductId = reader.GetInt32(reader.GetOrdinal("product_id")),
            Quantity = reader.GetDecimal(reader.GetOrdinal("quantity")),
            Price = reader.GetDecimal(reader.GetOrdinal("price"))
        };
        private FixedAsset MapFixedAsset(NpgsqlDataReader reader) => new FixedAsset
        {
            Id = reader.GetInt32(reader.GetOrdinal("asset_id")),
            Name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name")),
            InventoryNumber = reader.IsDBNull(reader.GetOrdinal("inventory_number")) ? null : reader.GetString(reader.GetOrdinal("inventory_number")),
            AcquisitionDate = reader.GetDateTime(reader.GetOrdinal("acquisition_date")),
            InitialCost = reader.GetDecimal(reader.GetOrdinal("initial_cost")),
            UsefulLife = reader.GetInt32(reader.GetOrdinal("useful_life"))
        };
        private Depreciation MapDepreciation(NpgsqlDataReader reader) => new Depreciation
        {
            Id = reader.GetInt32(reader.GetOrdinal("depreciation_id")),
            AssetId = reader.GetInt32(reader.GetOrdinal("asset_id")),
            Month = reader.GetDateTime(reader.GetOrdinal("month")),
            Amount = reader.GetDecimal(reader.GetOrdinal("amount")),
            Method = reader.IsDBNull(reader.GetOrdinal("method")) ? null : reader.GetString(reader.GetOrdinal("method"))
        };
        private AssetMovement MapAssetMovement(NpgsqlDataReader reader) => new AssetMovement
        {
            Id = reader.GetInt32(reader.GetOrdinal("movement_id")),
            AssetId = reader.GetInt32(reader.GetOrdinal("asset_id")),
            FromDepartment = reader.IsDBNull(reader.GetOrdinal("from_department")) ? null : reader.GetString(reader.GetOrdinal("from_department")),
            ToDepartment = reader.IsDBNull(reader.GetOrdinal("to_department")) ? null : reader.GetString(reader.GetOrdinal("to_department")),
            Date = reader.GetDateTime(reader.GetOrdinal("date"))
        };
        private Warehouse MapWarehouse(NpgsqlDataReader reader) => new Warehouse
        {
            Id = reader.GetInt32(reader.GetOrdinal("warehouse_id")),
            Name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name")),
            Address = reader.IsDBNull(reader.GetOrdinal("address")) ? null : reader.GetString(reader.GetOrdinal("address"))
        };
        private ProductCategory MapProductCategory(NpgsqlDataReader reader) => new ProductCategory
        {
            Id = reader.GetInt32(reader.GetOrdinal("category_id")),
            Name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name"))
        };
        private Product MapProduct(NpgsqlDataReader reader) => new Product
        {
            Id = reader.GetInt32(reader.GetOrdinal("product_id")),
            Name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name")),
            Unit = reader.IsDBNull(reader.GetOrdinal("unit")) ? null : reader.GetString(reader.GetOrdinal("unit")),
            CategoryId = reader.GetInt32(reader.GetOrdinal("category_id"))
        };
        private Inventory MapInventory(NpgsqlDataReader reader) => new Inventory
        {
            Id = reader.GetInt32(reader.GetOrdinal("inventory_id")),
            WarehouseId = reader.GetInt32(reader.GetOrdinal("warehouse_id")),
            ProductId = reader.GetInt32(reader.GetOrdinal("product_id")),
            Quantity = reader.GetDecimal(reader.GetOrdinal("quantity"))
        };
        private InventoryMovement MapInventoryMovement(NpgsqlDataReader reader) => new InventoryMovement
        {
            Id = reader.GetInt32(reader.GetOrdinal("movement_id")),
            ProductId = reader.GetInt32(reader.GetOrdinal("product_id")),
            FromWarehouseId = reader.IsDBNull(reader.GetOrdinal("from_warehouse_id")) ? null : reader.GetInt32(reader.GetOrdinal("from_warehouse_id")),
            ToWarehouseId = reader.IsDBNull(reader.GetOrdinal("to_warehouse_id")) ? null : reader.GetInt32(reader.GetOrdinal("to_warehouse_id")),
            Quantity = reader.GetDecimal(reader.GetOrdinal("quantity")),
            Date = reader.GetDateTime(reader.GetOrdinal("date"))
        };
        private Department MapDepartment(NpgsqlDataReader reader) => new Department
        {
            Id = reader.GetInt32(reader.GetOrdinal("department_id")),
            Name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name"))
        };
        private Employee MapEmployee(NpgsqlDataReader reader) => new Employee
        {
            Id = reader.GetInt32(reader.GetOrdinal("employee_id")),
            FullName = reader.IsDBNull(reader.GetOrdinal("full_name")) ? null : reader.GetString(reader.GetOrdinal("full_name")),
            Position = reader.IsDBNull(reader.GetOrdinal("position")) ? null : reader.GetString(reader.GetOrdinal("position")),
            DepartmentId = reader.GetInt32(reader.GetOrdinal("department_id")),
            HireDate = reader.GetDateTime(reader.GetOrdinal("hire_date"))
        };
        private SalaryPayment MapSalaryPayment(NpgsqlDataReader reader) => new SalaryPayment
        {
            Id = reader.GetInt32(reader.GetOrdinal("payment_id")),
            EmployeeId = reader.GetInt32(reader.GetOrdinal("employee_id")),
            Month = reader.GetDateTime(reader.GetOrdinal("month")),
            BaseSalary = reader.GetDecimal(reader.GetOrdinal("base_salary")),
            Bonus = reader.GetDecimal(reader.GetOrdinal("bonus")),
            TaxDeduction = reader.GetDecimal(reader.GetOrdinal("tax_deduction"))
        };
        private Tax MapTax(NpgsqlDataReader reader) => new Tax
        {
            Id = reader.GetInt32(reader.GetOrdinal("tax_id")),
            PaymentId = reader.GetInt32(reader.GetOrdinal("payment_id")),
            Type = reader.IsDBNull(reader.GetOrdinal("type")) ? null : reader.GetString(reader.GetOrdinal("type")),
            Amount = reader.GetDecimal(reader.GetOrdinal("amount"))
        };
        private FinancialReport MapFinancialReport(NpgsqlDataReader reader) => new FinancialReport
        {
            Id = reader.GetInt32(reader.GetOrdinal("report_id")),
            Type = reader.IsDBNull(reader.GetOrdinal("type")) ? null : reader.GetString(reader.GetOrdinal("type")),
            Period = reader.IsDBNull(reader.GetOrdinal("period")) ? null : reader.GetString(reader.GetOrdinal("period")),
            GeneratedBy = reader.GetInt32(reader.GetOrdinal("generated_by")),
            CreationDate = reader.GetDateTime(reader.GetOrdinal("creation_date"))
        };
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
        public void Execute(object parameter) => _execute();
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}