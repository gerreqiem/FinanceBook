using DatabaseManager.Models;
using DatabaseManager.Strategies;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
namespace DatabaseManager.Services
{
    public class DatabaseService : IDisposable
    {
        private readonly string _connectionString = "Host=localhost;Username=postgres;Password=12345678;Database=financebook";
        private NpgsqlConnection _connection;
        public DatabaseService()
        {
            _connection = new NpgsqlConnection(_connectionString);
        }
        public async Task ConnectAsync()
        {
            if (_connection.State != ConnectionState.Open)
                await _connection.OpenAsync();
        }
        public async Task<List<T>> ExecuteQueryAsync<T>(string query, Func<NpgsqlDataReader, T> map)
        {
            var items = new List<T>();
            try
            {
                await ConnectAsync();
                using var cmd = new NpgsqlCommand(query, _connection);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(map(reader));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка выполнения запроса: {ex.Message}");
                throw;
            }
            return items;
        }
        public async Task<int> ExecuteNonQueryAsync(string query, Action<NpgsqlCommand> setParameters)
        {
            try
            {
                await ConnectAsync();
                using var cmd = new NpgsqlCommand(query, _connection);
                setParameters(cmd);
                return await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка выполнения команды: {ex.Message}");
                throw;
            }
        }
        public async Task SaveItemsAsync<T>(string tableName, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                var query = GetInsertOrUpdateQuery(tableName);
                await ExecuteNonQueryAsync(query, cmd => SetParameters(cmd, item));
            }
        }
        public async Task CalculateAndSaveDepreciationAsync(FixedAsset asset, DateTime month, IDepreciationStrategy strategy)
        {
            try
            {
                await ConnectAsync();
                var depreciation = new Depreciation
                {
                    Id = await GetNextDepreciationId(),
                    AssetId = asset.Id,
                    Month = month,
                    Amount = strategy.CalculateDepreciation(asset, month),
                    Method = strategy.GetMethodName()
                };

                var query = GetInsertOrUpdateQuery("Depreciation");
                await ExecuteNonQueryAsync(query, cmd => SetParameters(cmd, depreciation));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка расчета и сохранения амортизации: {ex.Message}");
                throw;
            }
        }
        public async Task RegisterTransactionAsync(Transaction transaction)
        {
            if (transaction.DebitAccountId == transaction.CreditAccountId)
                throw new ArgumentException("Дебетовый и кредитовый счет не должны совпадать.");

            if (transaction.Amount <= 0)
                throw new ArgumentException("Сумма должна быть положительной.");

            await SaveItemsAsync("Transactions", new[] { transaction });
        }
        public async Task<Dictionary<int, (decimal Debit, decimal Credit, decimal Balance)>> GenerateTrialBalanceAsync()
        {
            var result = new Dictionary<int, (decimal Debit, decimal Credit, decimal Balance)>();
            var query = @"SELECT debit_account_id, credit_account_id, amount 
                          FROM Transactions";
            await ConnectAsync();
            using var cmd = new NpgsqlCommand(query, _connection);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var debitAccountId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
                var creditAccountId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                var amount = reader.GetDecimal(2);
                if (debitAccountId.HasValue)
                {
                    if (!result.ContainsKey(debitAccountId.Value))
                        result[debitAccountId.Value] = (Debit: 0, Credit: 0, Balance: 0);
                    var current = result[debitAccountId.Value];
                    result[debitAccountId.Value] = (current.Debit + amount, current.Credit, current.Debit + amount - current.Credit);
                }
                if (creditAccountId.HasValue)
                {
                    if (!result.ContainsKey(creditAccountId.Value))
                        result[creditAccountId.Value] = (Debit: 0, Credit: 0, Balance: 0);
                    var current = result[creditAccountId.Value];
                    result[creditAccountId.Value] = (current.Debit, current.Credit + amount, current.Debit - (current.Credit + amount));
                }
            }
            return result;
        }
        public async Task<Dictionary<int, decimal>> CalculateReceivablesPayablesAsync()
        {
            var result = new Dictionary<int, decimal>();
            var query = @"SELECT counterparty_id, SUM(total_amount) as balance 
                          FROM Documents 
                          WHERE counterparty_id IS NOT NULL 
                          GROUP BY counterparty_id";
            await ConnectAsync();
            using var cmd = new NpgsqlCommand(query, _connection);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var counterpartyId = reader.GetInt32(0);
                var balance = reader.GetDecimal(1);
                result[counterpartyId] = balance;
            }
            return result;
        }
        public async Task<Dictionary<int, decimal>> PerformInventoryCheckAsync(int warehouseId)
        {
            var result = new Dictionary<int, decimal>();
            var query = @"SELECT product_id, quantity 
                          FROM Inventory 
                          WHERE warehouse_id = @warehouse_id";
            await ConnectAsync();
            using var cmd = new NpgsqlCommand(query, _connection);
            cmd.Parameters.AddWithValue("warehouse_id", warehouseId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var productId = reader.GetInt32(0);
                var quantity = reader.GetDecimal(1);
                result[productId] = quantity;
            }
            return result;
        }
        public async Task CalculateAndSaveSalaryAsync(Employee employee, DateTime month, decimal baseSalary, decimal bonus, ITaxStrategy taxStrategy)
        {
            var salaryPayment = new SalaryPayment
            {
                Id = await GetNextSalaryPaymentId(),
                EmployeeId = employee.Id,
                Month = month,
                BaseSalary = baseSalary,
                Bonus = bonus,
                TaxDeduction = taxStrategy.CalculateTax(new SalaryPayment { BaseSalary = baseSalary, Bonus = bonus })
            };
            await SaveItemsAsync("SalaryPayments", new[] { salaryPayment });
            var tax = new Tax
            {
                Id = await GetNextTaxId(),
                PaymentId = salaryPayment.Id,
                Type = taxStrategy.GetTaxType(),
                Amount = salaryPayment.TaxDeduction
            };
            await SaveItemsAsync("Taxes", new[] { tax });
        }
        public async Task<Dictionary<string, decimal>> GenerateBalanceSheetAsync()
        {
            var result = new Dictionary<string, decimal>();
            var query = @"SELECT coa.name, SUM(t.amount) as balance 
                          FROM Transactions t 
                          JOIN ChartOfAccounts coa ON t.debit_account_id = coa.account_id OR t.credit_account_id = coa.account_id 
                          GROUP BY coa.name";
            await ConnectAsync();
            using var cmd = new NpgsqlCommand(query, _connection);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var accountName = reader.GetString(0);
                var balance = reader.GetDecimal(1);
                result[accountName] = balance;
            }
            return result;
        }
        public async Task<Dictionary<int, decimal>> CalculateVATAsync()
        {
            var result = new Dictionary<int, decimal>();
            var query = @"SELECT counterparty_id, SUM(total_amount * 0.20) as vat 
                  FROM Documents 
                  WHERE type IN ('Продажа', 'Sale', 'Invoice') AND counterparty_id IS NOT NULL 
                  GROUP BY counterparty_id";
            await ConnectAsync();
            using var cmd = new NpgsqlCommand(query, _connection);
            using var reader = await cmd.ExecuteReaderAsync();
            int rowCount = 0;
            while (await reader.ReadAsync())
            {
                var counterpartyId = reader.GetInt32(0);
                var vat = reader.GetDecimal(1);
                result[counterpartyId] = vat;
                rowCount++;
                Console.WriteLine($"Контрагент {counterpartyId}: НДС = {vat:F2}");
            }
            Console.WriteLine($"Обработано строк: {rowCount}");
            return result;
        }
        private async Task<int> GetNextDepreciationId()
        {
            var query = "SELECT COALESCE(MAX(depreciation_id), 0) + 1 FROM Depreciation";
            using var cmd = new NpgsqlCommand(query, _connection);
            await ConnectAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }
        private async Task<int> GetNextSalaryPaymentId()
        {
            var query = "SELECT COALESCE(MAX(payment_id), 0) + 1 FROM SalaryPayments";
            using var cmd = new NpgsqlCommand(query, _connection);
            await ConnectAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }
        private async Task<int> GetNextTaxId()
        {
            var query = "SELECT COALESCE(MAX(tax_id), 0) + 1 FROM Taxes";
            using var cmd = new NpgsqlCommand(query, _connection);
            await ConnectAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }
        private string GetInsertOrUpdateQuery(string tableName)
        {
            return tableName switch
            {
                "Users" => @"INSERT INTO Users (user_id, username, password_hash, full_name, email, is_active) 
                            VALUES (@user_id, @username, @password_hash, @full_name, @email, @is_active)
                            ON CONFLICT (user_id) DO UPDATE SET 
                            username = EXCLUDED.username, 
                            password_hash = EXCLUDED.password_hash,
                            full_name = EXCLUDED.full_name,
                            email = EXCLUDED.email,
                            is_active = EXCLUDED.is_active",
                "Roles" => @"INSERT INTO Roles (role_id, name) VALUES (@role_id, @name)
                           ON CONFLICT (role_id) DO UPDATE SET name = EXCLUDED.name",
                "UserRoles" => @"INSERT INTO UserRoles (user_id, role_id) 
                               VALUES (@user_id, @role_id)
                               ON CONFLICT (user_id, role_id) DO NOTHING",
                "Permissions" => @"INSERT INTO Permissions (permission_id, name) VALUES (@permission_id, @name)
                                ON CONFLICT (permission_id) DO UPDATE SET name = EXCLUDED.name",
                "RolePermissions" => @"INSERT INTO RolePermissions (role_id, permission_id) 
                                    VALUES (@role_id, @permission_id)
                                    ON CONFLICT (role_id, permission_id) DO NOTHING",
                "Counterparties" => @"INSERT INTO Counterparties (counterparty_id, name, type, tax_number, bank_details) 
                                    VALUES (@counterparty_id, @name, @type, @tax_number, @bank_details)
                                    ON CONFLICT (counterparty_id) DO UPDATE SET 
                                    name = EXCLUDED.name, 
                                    type = EXCLUDED.type,
                                    tax_number = EXCLUDED.tax_number,
                                    bank_details = EXCLUDED.bank_details",
                "BankAccounts" => @"INSERT INTO BankAccounts (account_id, counterparty_id, account_number, bank_name) 
                                  VALUES (@account_id, @counterparty_id, @account_number, @bank_name)
                                  ON CONFLICT (account_id) DO UPDATE SET 
                                  counterparty_id = EXCLUDED.counterparty_id, 
                                  account_number = EXCLUDED.account_number,
                                  bank_name = EXCLUDED.bank_name",
                "Contracts" => @"INSERT INTO Contracts (contract_id, counterparty_id, start_date, end_date, amount) 
                               VALUES (@contract_id, @counterparty_id, @start_date, @end_date, @amount)
                               ON CONFLICT (contract_id) DO UPDATE SET 
                               counterparty_id = EXCLUDED.counterparty_id, 
                               start_date = EXCLUDED.start_date,
                               end_date = EXCLUDED.end_date,
                               amount = EXCLUDED.amount",
                "ChartOfAccounts" => @"INSERT INTO ChartOfAccounts (account_id, code, name, type) 
                                    VALUES (@account_id, @code, @name, @type)
                                    ON CONFLICT (account_id) DO UPDATE SET 
                                    code = EXCLUDED.code, 
                                    name = EXCLUDED.name,
                                    type = EXCLUDED.type",
                "Transactions" => @"INSERT INTO Transactions (transaction_id, date, debit_account_id, credit_account_id, amount, description) 
                                 VALUES (@transaction_id, @date, @debit_account_id, @credit_account_id, @amount, @description)
                                 ON CONFLICT (transaction_id) DO UPDATE SET 
                                 date = EXCLUDED.date, 
                                 debit_account_id = EXCLUDED.debit_account_id,
                                 credit_account_id = EXCLUDED.credit_account_id,
                                 amount = EXCLUDED.amount,
                                 description = EXCLUDED.description",
                "Documents" => @"INSERT INTO Documents (document_id, type, date, counterparty_id, total_amount) 
                              VALUES (@document_id, @type, @date, @counterparty_id, @total_amount)
                              ON CONFLICT (document_id) DO UPDATE SET 
                              type = EXCLUDED.type, 
                              date = EXCLUDED.date,
                              counterparty_id = EXCLUDED.counterparty_id,
                              total_amount = EXCLUDED.total_amount",
                "DocumentItems" => @"INSERT INTO DocumentItems (item_id, document_id, product_id, quantity, price) 
                                  VALUES (@item_id, @document_id, @product_id, @quantity, @price)
                                  ON CONFLICT (item_id) DO UPDATE SET 
                                  document_id = EXCLUDED.document_id, 
                                  product_id = EXCLUDED.product_id,
                                  quantity = EXCLUDED.quantity,
                                  price = EXCLUDED.price",
                "FixedAssets" => @"INSERT INTO FixedAssets (asset_id, name, inventory_number, acquisition_date, initial_cost, useful_life) 
                                VALUES (@asset_id, @name, @inventory_number, @acquisition_date, @initial_cost, @useful_life)
                                ON CONFLICT (asset_id) DO UPDATE SET 
                                name = EXCLUDED.name, 
                                inventory_number = EXCLUDED.inventory_number,
                                acquisition_date = EXCLUDED.acquisition_date,
                                initial_cost = EXCLUDED.initial_cost,
                                useful_life = EXCLUDED.useful_life",
                "Depreciation" => @"INSERT INTO Depreciation (depreciation_id, asset_id, month, amount, method) 
                                 VALUES (@depreciation_id, @asset_id, @month, @amount, @method)
                                 ON CONFLICT (depreciation_id) DO UPDATE SET 
                                 asset_id = EXCLUDED.asset_id, 
                                 month = EXCLUDED.month,
                                 amount = EXCLUDED.amount,
                                 method = EXCLUDED.method",
                "AssetMovements" => @"INSERT INTO AssetMovements (movement_id, asset_id, from_department, to_department, date) 
                                   VALUES (@movement_id, @asset_id, @from_department, @to_department, @date)
                                   ON CONFLICT (movement_id) DO UPDATE SET 
                                   asset_id = EXCLUDED.asset_id, 
                                   from_department = EXCLUDED.from_department,
                                   to_department = EXCLUDED.to_department,
                                   date = EXCLUDED.date",
                "Warehouses" => @"INSERT INTO Warehouses (warehouse_id, name, address) 
                                VALUES (@warehouse_id, @name, @address)
                                ON CONFLICT (warehouse_id) DO UPDATE SET 
                                name = EXCLUDED.name, 
                                address = EXCLUDED.address",
                "ProductCategories" => @"INSERT INTO ProductCategories (category_id, name) 
                                      VALUES (@category_id, @name)
                                      ON CONFLICT (category_id) DO UPDATE SET 
                                      name = EXCLUDED.name",
                "Products" => @"INSERT INTO Products (product_id, name, unit, category_id) 
                             VALUES (@product_id, @name, @unit, @category_id)
                             ON CONFLICT (product_id) DO UPDATE SET 
                             name = EXCLUDED.name, 
                             unit = EXCLUDED.unit,
                             category_id = EXCLUDED.category_id",
                "Inventory" => @"INSERT INTO Inventory (inventory_id, warehouse_id, product_id, quantity) 
                              VALUES (@inventory_id, @warehouse_id, @product_id, @quantity)
                              ON CONFLICT (inventory_id) DO UPDATE SET 
                              warehouse_id = EXCLUDED.warehouse_id, 
                              product_id = EXCLUDED.product_id,
                              quantity = EXCLUDED.quantity",
                "InventoryMovements" => @"INSERT INTO InventoryMovements (movement_id, product_id, from_warehouse_id, to_warehouse_id, quantity, date) 
                                        VALUES (@movement_id, @product_id, @from_warehouse_id, @to_warehouse_id, @quantity, @date)
                                        ON CONFLICT (movement_id) DO UPDATE SET 
                                        product_id = EXCLUDED.product_id, 
                                        from_warehouse_id = EXCLUDED.from_warehouse_id,
                                        to_warehouse_id = EXCLUDED.to_warehouse_id,
                                        quantity = EXCLUDED.quantity,
                                        date = EXCLUDED.date",
                "Departments" => @"INSERT INTO Departments (department_id, name) 
                                VALUES (@department_id, @name)
                                ON CONFLICT (department_id) DO UPDATE SET 
                                name = EXCLUDED.name",
                "Employees" => @"INSERT INTO Employees (employee_id, full_name, position, department_id, hire_date) 
                              VALUES (@employee_id, @full_name, @position, @department_id, @hire_date)
                              ON CONFLICT (employee_id) DO UPDATE SET 
                              full_name = EXCLUDED.full_name, 
                              position = EXCLUDED.position,
                              department_id = EXCLUDED.department_id,
                              hire_date = EXCLUDED.hire_date",
                "SalaryPayments" => @"INSERT INTO SalaryPayments (payment_id, employee_id, month, base_salary, bonus, tax_deduction) 
                                   VALUES (@payment_id, @employee_id, @month, @base_salary, @bonus, @tax_deduction)
                                   ON CONFLICT (payment_id) DO UPDATE SET 
                                   employee_id = EXCLUDED.employee_id, 
                                   month = EXCLUDED.month,
                                   base_salary = EXCLUDED.base_salary,
                                   bonus = EXCLUDED.bonus,
                                   tax_deduction = EXCLUDED.tax_deduction",
                "Taxes" => @"INSERT INTO Taxes (tax_id, payment_id, type, amount) 
                          VALUES (@tax_id, @payment_id, @type, @amount)
                          ON CONFLICT (tax_id) DO UPDATE SET 
                          payment_id = EXCLUDED.payment_id, 
                          type = EXCLUDED.type,
                          amount = EXCLUDED.amount",
                "FinancialReports" => @"INSERT INTO FinancialReports (report_id, type, period, generated_by, creation_date) 
                                     VALUES (@report_id, @type, @period, @generated_by, @creation_date)
                                     ON CONFLICT (report_id) DO UPDATE SET 
                                     type = EXCLUDED.type, 
                                     period = EXCLUDED.period,
                                     generated_by = EXCLUDED.generated_by,
                                     creation_date = EXCLUDED.creation_date",
                _ => throw new ArgumentException($"Неизвестная таблица: {tableName}")
            };
        }
        private void SetParameters<T>(NpgsqlCommand cmd, T item)
        {
            switch (item)
            {
                case User u:
                    cmd.Parameters.AddWithValue("user_id", u.Id);
                    cmd.Parameters.AddWithValue("username", u.Username ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("password_hash", u.PasswordHash ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("full_name", u.FullName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("email", u.Email ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("is_active", u.IsActive);
                    break;
                case Role r:
                    cmd.Parameters.AddWithValue("role_id", r.Id);
                    cmd.Parameters.AddWithValue("name", r.Name ?? (object)DBNull.Value);
                    break;
                case UserRole ur:
                    cmd.Parameters.AddWithValue("user_id", ur.UserId);
                    cmd.Parameters.AddWithValue("role_id", ur.RoleId);
                    break;
                case Permission p:
                    cmd.Parameters.AddWithValue("permission_id", p.Id);
                    cmd.Parameters.AddWithValue("name", p.Name ?? (object)DBNull.Value);
                    break;
                case RolePermission rp:
                    cmd.Parameters.AddWithValue("role_id", rp.RoleId);
                    cmd.Parameters.AddWithValue("permission_id", rp.PermissionId);
                    break;
                case Counterparty c:
                    cmd.Parameters.AddWithValue("counterparty_id", c.Id);
                    cmd.Parameters.AddWithValue("name", c.Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("type", c.Type ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("tax_number", c.TaxNumber ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("bank_details", c.BankDetails ?? (object)DBNull.Value);
                    break;
                case BankAccount ba:
                    cmd.Parameters.AddWithValue("account_id", ba.Id);
                    cmd.Parameters.AddWithValue("counterparty_id", ba.CounterpartyId);
                    cmd.Parameters.AddWithValue("account_number", ba.AccountNumber ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("bank_name", ba.BankName ?? (object)DBNull.Value);
                    break;
                case Contract c:
                    cmd.Parameters.AddWithValue("contract_id", c.Id);
                    cmd.Parameters.AddWithValue("counterparty_id", c.CounterpartyId);
                    cmd.Parameters.AddWithValue("start_date", c.StartDate);
                    cmd.Parameters.AddWithValue("end_date", c.EndDate ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("amount", c.Amount);
                    break;
                case ChartOfAccount ca:
                    cmd.Parameters.AddWithValue("account_id", ca.Id);
                    cmd.Parameters.AddWithValue("code", ca.Code ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("name", ca.Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("type", ca.Type ?? (object)DBNull.Value);
                    break;
                case Transaction t:
                    cmd.Parameters.AddWithValue("transaction_id", t.TransactionId);
                    cmd.Parameters.AddWithValue("date", t.Date);
                    cmd.Parameters.AddWithValue("debit_account_id", t.DebitAccountId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("credit_account_id", t.CreditAccountId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("amount", t.Amount);
                    cmd.Parameters.AddWithValue("description", t.Description ?? (object)DBNull.Value);
                    break;
                case Document d:
                    cmd.Parameters.AddWithValue("document_id", d.Id);
                    cmd.Parameters.AddWithValue("type", d.Type ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("date", d.Date);
                    cmd.Parameters.AddWithValue("counterparty_id", d.CounterpartyId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("total_amount", d.TotalAmount);
                    break;
                case DocumentItem di:
                    cmd.Parameters.AddWithValue("item_id", di.Id);
                    cmd.Parameters.AddWithValue("document_id", di.DocumentId);
                    cmd.Parameters.AddWithValue("product_id", di.ProductId);
                    cmd.Parameters.AddWithValue("quantity", di.Quantity);
                    cmd.Parameters.AddWithValue("price", di.Price);
                    break;
                case FixedAsset fa:
                    cmd.Parameters.AddWithValue("asset_id", fa.Id);
                    cmd.Parameters.AddWithValue("name", fa.Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("inventory_number", fa.InventoryNumber ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("acquisition_date", fa.AcquisitionDate);
                    cmd.Parameters.AddWithValue("initial_cost", fa.InitialCost);
                    cmd.Parameters.AddWithValue("useful_life", fa.UsefulLife);
                    break;
                case Depreciation d:
                    cmd.Parameters.AddWithValue("depreciation_id", d.Id);
                    cmd.Parameters.AddWithValue("asset_id", d.AssetId);
                    cmd.Parameters.AddWithValue("month", d.Month);
                    cmd.Parameters.AddWithValue("amount", d.Amount);
                    cmd.Parameters.AddWithValue("method", d.Method ?? (object)DBNull.Value);
                    break;
                case AssetMovement am:
                    cmd.Parameters.AddWithValue("movement_id", am.Id);
                    cmd.Parameters.AddWithValue("asset_id", am.AssetId);
                    cmd.Parameters.AddWithValue("from_department", am.FromDepartment ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("to_department", am.ToDepartment ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("date", am.Date);
                    break;
                case Warehouse w:
                    cmd.Parameters.AddWithValue("warehouse_id", w.Id);
                    cmd.Parameters.AddWithValue("name", w.Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("address", w.Address ?? (object)DBNull.Value);
                    break;
                case ProductCategory pc:
                    cmd.Parameters.AddWithValue("category_id", pc.Id);
                    cmd.Parameters.AddWithValue("name", pc.Name ?? (object)DBNull.Value);
                    break;
                case Product p:
                    cmd.Parameters.AddWithValue("product_id", p.Id);
                    cmd.Parameters.AddWithValue("name", p.Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("unit", p.Unit ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("category_id", p.CategoryId);
                    break;
                case Inventory i:
                    cmd.Parameters.AddWithValue("inventory_id", i.Id);
                    cmd.Parameters.AddWithValue("warehouse_id", i.WarehouseId);
                    cmd.Parameters.AddWithValue("product_id", i.ProductId);
                    cmd.Parameters.AddWithValue("quantity", i.Quantity);
                    break;
                case InventoryMovement im:
                    cmd.Parameters.AddWithValue("movement_id", im.Id);
                    cmd.Parameters.AddWithValue("product_id", im.ProductId);
                    cmd.Parameters.AddWithValue("from_warehouse_id", im.FromWarehouseId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("to_warehouse_id", im.ToWarehouseId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("quantity", im.Quantity);
                    cmd.Parameters.AddWithValue("date", im.Date);
                    break;
                case Department d:
                    cmd.Parameters.AddWithValue("department_id", d.Id);
                    cmd.Parameters.AddWithValue("name", d.Name ?? (object)DBNull.Value);
                    break;
                case Employee e:
                    cmd.Parameters.AddWithValue("employee_id", e.Id);
                    cmd.Parameters.AddWithValue("full_name", e.FullName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("position", e.Position ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("department_id", e.DepartmentId);
                    cmd.Parameters.AddWithValue("hire_date", e.HireDate);
                    break;
                case SalaryPayment sp:
                    cmd.Parameters.AddWithValue("payment_id", sp.Id);
                    cmd.Parameters.AddWithValue("employee_id", sp.EmployeeId);
                    cmd.Parameters.AddWithValue("month", sp.Month);
                    cmd.Parameters.AddWithValue("base_salary", sp.BaseSalary);
                    cmd.Parameters.AddWithValue("bonus", sp.Bonus);
                    cmd.Parameters.AddWithValue("tax_deduction", sp.TaxDeduction);
                    break;
                case Tax t:
                    cmd.Parameters.AddWithValue("tax_id", t.Id);
                    cmd.Parameters.AddWithValue("payment_id", t.PaymentId);
                    cmd.Parameters.AddWithValue("type", t.Type ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("amount", t.Amount);
                    break;
                case FinancialReport fr:
                    cmd.Parameters.AddWithValue("report_id", fr.Id);
                    cmd.Parameters.AddWithValue("type", fr.Type ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("period", fr.Period ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("generated_by", fr.GeneratedBy);
                    cmd.Parameters.AddWithValue("creation_date", fr.CreationDate);
                    break;
                default:
                    throw new ArgumentException($"Неподдерживаемый тип: {typeof(T).Name}");
            }
        }
        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}