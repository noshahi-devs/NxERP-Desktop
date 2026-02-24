using System.Globalization;
using System.IO;
using HelloWpf.Models;
using Microsoft.Data.Sqlite;

namespace HelloWpf.Data;

public sealed class SqliteCustomerRepository : ICustomerRepository
{
    private readonly string _connectionString;

    public SqliteCustomerRepository(string databasePath)
    {
        var folder = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        Initialize();
    }

    public CustomerPageResult GetPage(string? search, int pageNumber, int pageSize)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var safePage = Math.Max(1, pageNumber);
        var safeSize = Math.Max(10, pageSize);
        var offset = (safePage - 1) * safeSize;

        var where = BuildWhere(search);

        using var countCmd = connection.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM Customers {where.clause};";
        AddSearchParam(countCmd, where.term);
        var total = Convert.ToInt32(countCmd.ExecuteScalar(), CultureInfo.InvariantCulture);

        using var pageCmd = connection.CreateCommand();
        pageCmd.CommandText = $@"
SELECT Code, Name, Type, Contact, OpeningDate, OpeningBalance, IsActive, UpdatedAt
FROM Customers
{where.clause}
ORDER BY UpdatedAt DESC
LIMIT $limit OFFSET $offset;";
        AddSearchParam(pageCmd, where.term);
        pageCmd.Parameters.AddWithValue("$limit", safeSize);
        pageCmd.Parameters.AddWithValue("$offset", offset);

        var items = new List<Customer>(safeSize);
        using var reader = pageCmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new Customer
            {
                Code = reader.GetString(0),
                Name = reader.GetString(1),
                Type = reader.GetString(2),
                Contact = reader.GetString(3),
                OpeningDate = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                OpeningBalance = Convert.ToDecimal(reader.GetDouble(5), CultureInfo.InvariantCulture),
                IsActive = reader.GetInt64(6) == 1,
                UpdatedAt = DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            });
        }

        return new CustomerPageResult(items, total, safePage, safeSize);
    }

    public Customer? GetByCode(string code)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
SELECT Code, Name, Type, Contact, OpeningDate, OpeningBalance, IsActive, UpdatedAt
FROM Customers WHERE Code = $code LIMIT 1;";
        cmd.Parameters.AddWithValue("$code", code.Trim());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new Customer
        {
            Code = reader.GetString(0),
            Name = reader.GetString(1),
            Type = reader.GetString(2),
            Contact = reader.GetString(3),
            OpeningDate = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
            OpeningBalance = Convert.ToDecimal(reader.GetDouble(5), CultureInfo.InvariantCulture),
            IsActive = reader.GetInt64(6) == 1,
            UpdatedAt = DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };
    }

    public void Upsert(Customer customer)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
INSERT INTO Customers (Code, Name, Type, Contact, OpeningDate, OpeningBalance, IsActive, UpdatedAt)
VALUES ($code, $name, $type, $contact, $openingDate, $openingBalance, $isActive, $updatedAt)
ON CONFLICT(Code) DO UPDATE SET
    Name = excluded.Name,
    Type = excluded.Type,
    Contact = excluded.Contact,
    OpeningDate = excluded.OpeningDate,
    OpeningBalance = excluded.OpeningBalance,
    IsActive = excluded.IsActive,
    UpdatedAt = excluded.UpdatedAt;";

        cmd.Parameters.AddWithValue("$code", customer.Code.Trim());
        cmd.Parameters.AddWithValue("$name", customer.Name.Trim());
        cmd.Parameters.AddWithValue("$type", customer.Type.Trim());
        cmd.Parameters.AddWithValue("$contact", customer.Contact.Trim());
        cmd.Parameters.AddWithValue("$openingDate", customer.OpeningDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$openingBalance", Convert.ToDouble(customer.OpeningBalance, CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$isActive", customer.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));

        cmd.ExecuteNonQuery();
    }

    public bool Delete(string code)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Customers WHERE Code = $code;";
        cmd.Parameters.AddWithValue("$code", code.Trim());
        return cmd.ExecuteNonQuery() > 0;
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var create = connection.CreateCommand();
        create.CommandText = @"
CREATE TABLE IF NOT EXISTS Customers(
    Code TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Type TEXT NOT NULL,
    Contact TEXT NOT NULL,
    OpeningDate TEXT NOT NULL,
    OpeningBalance REAL NOT NULL,
    IsActive INTEGER NOT NULL,
    UpdatedAt TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_Customers_Name ON Customers(Name);
CREATE INDEX IF NOT EXISTS IX_Customers_Contact ON Customers(Contact);
CREATE INDEX IF NOT EXISTS IX_Customers_UpdatedAt ON Customers(UpdatedAt DESC);
";
        create.ExecuteNonQuery();

        using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM Customers;";
        var total = Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (total == 0)
        {
            Seed(connection);
        }
    }

    private static void Seed(SqliteConnection connection)
    {
        var random = new Random(42);
        var types = new[] { "Retail", "Corporate", "Insurance" };

        using var tx = connection.BeginTransaction();
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
INSERT INTO Customers(Code, Name, Type, Contact, OpeningDate, OpeningBalance, IsActive, UpdatedAt)
VALUES($code,$name,$type,$contact,$openingDate,$openingBalance,1,$updatedAt);";

        var code = cmd.Parameters.Add("$code", SqliteType.Text);
        var name = cmd.Parameters.Add("$name", SqliteType.Text);
        var type = cmd.Parameters.Add("$type", SqliteType.Text);
        var contact = cmd.Parameters.Add("$contact", SqliteType.Text);
        var openingDate = cmd.Parameters.Add("$openingDate", SqliteType.Text);
        var openingBalance = cmd.Parameters.Add("$openingBalance", SqliteType.Real);
        var updatedAt = cmd.Parameters.Add("$updatedAt", SqliteType.Text);

        for (var i = 1; i <= 5000; i++)
        {
            code.Value = $"CUS-{i:00000}";
            name.Value = $"Customer {i:00000}";
            type.Value = types[i % types.Length];
            contact.Value = $"03{random.Next(10, 49)}-{random.Next(1000000, 9999999)}";
            openingDate.Value = DateTime.Today.AddDays(-(i % 365)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            openingBalance.Value = random.Next(0, 15000);
            updatedAt.Value = DateTime.UtcNow.AddMinutes(-i).ToString("O", CultureInfo.InvariantCulture);
            cmd.ExecuteNonQuery();
        }

        // Known sample customers for demos.
        cmd.CommandText = @"
INSERT INTO Customers(Code, Name, Type, Contact, OpeningDate, OpeningBalance, IsActive, UpdatedAt)
VALUES('CUS-1002','City Clinic','Corporate','0300-1234567','2026-02-19',2150,1,$updatedAt)
ON CONFLICT(Code) DO UPDATE SET
Name='City Clinic', Type='Corporate', Contact='0300-1234567', OpeningDate='2026-02-19', OpeningBalance=2150, IsActive=1, UpdatedAt=$updatedAt;";
        updatedAt.Value = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        cmd.ExecuteNonQuery();

        tx.Commit();
    }

    private static (string clause, string? term) BuildWhere(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return (string.Empty, null);
        }

        return ("WHERE Code LIKE $search OR Name LIKE $search OR Contact LIKE $search", $"%{search.Trim()}%");
    }

    private static void AddSearchParam(SqliteCommand command, string? term)
    {
        if (!string.IsNullOrWhiteSpace(term))
        {
            command.Parameters.AddWithValue("$search", term);
        }
    }
}
