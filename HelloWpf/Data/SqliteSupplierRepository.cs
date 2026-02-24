using System.Globalization;
using HelloWpf.Models;
using Microsoft.Data.Sqlite;

namespace HelloWpf.Data;

public sealed class SqliteSupplierRepository : ISupplierRepository
{
    private readonly string _connectionString;

    public SqliteSupplierRepository(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadWriteCreate, Cache = SqliteCacheMode.Shared }.ToString();
        Initialize();
    }

    public SupplierPageResult GetPage(string? search, int pageNumber, int pageSize)
    {
        using var c = new SqliteConnection(_connectionString);
        c.Open();
        var safePage = Math.Max(1, pageNumber);
        var safeSize = Math.Max(10, pageSize);
        var offset = (safePage - 1) * safeSize;
        var (where, term) = BuildWhere(search);

        using var count = c.CreateCommand();
        count.CommandText = $"SELECT COUNT(*) FROM Suppliers {where};";
        if (term is not null) count.Parameters.AddWithValue("$search", term);
        var total = Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture);

        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"SELECT Code,Name,Type,Contact,OnboardDate,OpeningPayable,IsActive,UpdatedAt FROM Suppliers {where} ORDER BY UpdatedAt DESC LIMIT $limit OFFSET $offset;";
        if (term is not null) cmd.Parameters.AddWithValue("$search", term);
        cmd.Parameters.AddWithValue("$limit", safeSize);
        cmd.Parameters.AddWithValue("$offset", offset);

        var items = new List<Supplier>(safeSize);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            items.Add(new Supplier
            {
                Code = r.GetString(0), Name = r.GetString(1), Type = r.GetString(2), Contact = r.GetString(3),
                OnboardDate = DateTime.Parse(r.GetString(4), CultureInfo.InvariantCulture),
                OpeningPayable = Convert.ToDecimal(r.GetDouble(5), CultureInfo.InvariantCulture),
                IsActive = r.GetInt64(6) == 1,
                UpdatedAt = DateTime.Parse(r.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            });
        }

        return new SupplierPageResult(items, total, safePage, safeSize);
    }

    public Supplier? GetByCode(string code)
    {
        using var c = new SqliteConnection(_connectionString);
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Code,Name,Type,Contact,OnboardDate,OpeningPayable,IsActive,UpdatedAt FROM Suppliers WHERE Code=$code LIMIT 1;";
        cmd.Parameters.AddWithValue("$code", code.Trim());
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new Supplier
        {
            Code = r.GetString(0), Name = r.GetString(1), Type = r.GetString(2), Contact = r.GetString(3),
            OnboardDate = DateTime.Parse(r.GetString(4), CultureInfo.InvariantCulture),
            OpeningPayable = Convert.ToDecimal(r.GetDouble(5), CultureInfo.InvariantCulture),
            IsActive = r.GetInt64(6) == 1,
            UpdatedAt = DateTime.Parse(r.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };
    }

    public void Upsert(Supplier supplier)
    {
        using var c = new SqliteConnection(_connectionString);
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"INSERT INTO Suppliers(Code,Name,Type,Contact,OnboardDate,OpeningPayable,IsActive,UpdatedAt)
VALUES($code,$name,$type,$contact,$onboard,$payable,$active,$updatedAt)
ON CONFLICT(Code) DO UPDATE SET Name=excluded.Name,Type=excluded.Type,Contact=excluded.Contact,OnboardDate=excluded.OnboardDate,OpeningPayable=excluded.OpeningPayable,IsActive=excluded.IsActive,UpdatedAt=excluded.UpdatedAt;";
        cmd.Parameters.AddWithValue("$code", supplier.Code.Trim());
        cmd.Parameters.AddWithValue("$name", supplier.Name.Trim());
        cmd.Parameters.AddWithValue("$type", supplier.Type.Trim());
        cmd.Parameters.AddWithValue("$contact", supplier.Contact.Trim());
        cmd.Parameters.AddWithValue("$onboard", supplier.OnboardDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$payable", Convert.ToDouble(supplier.OpeningPayable, CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$active", supplier.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
    }

    public bool Delete(string code)
    {
        using var c = new SqliteConnection(_connectionString);
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM Suppliers WHERE Code=$code;";
        cmd.Parameters.AddWithValue("$code", code.Trim());
        return cmd.ExecuteNonQuery() > 0;
    }

    private void Initialize()
    {
        using var c = new SqliteConnection(_connectionString);
        c.Open();
        using var create = c.CreateCommand();
        create.CommandText = @"CREATE TABLE IF NOT EXISTS Suppliers(Code TEXT PRIMARY KEY,Name TEXT NOT NULL,Type TEXT NOT NULL,Contact TEXT NOT NULL,OnboardDate TEXT NOT NULL,OpeningPayable REAL NOT NULL,IsActive INTEGER NOT NULL,UpdatedAt TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS IX_Suppliers_Name ON Suppliers(Name);
CREATE INDEX IF NOT EXISTS IX_Suppliers_UpdatedAt ON Suppliers(UpdatedAt DESC);";
        create.ExecuteNonQuery();

        using var count = c.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM Suppliers;";
        var total = Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (total > 0) return;

        using var tx = c.BeginTransaction();
        using var ins = c.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = @"INSERT INTO Suppliers(Code,Name,Type,Contact,OnboardDate,OpeningPayable,IsActive,UpdatedAt) VALUES ($code,$name,$type,$contact,$date,$payable,1,$updated);";
        var pCode = ins.Parameters.Add("$code", SqliteType.Text);
        var pName = ins.Parameters.Add("$name", SqliteType.Text);
        var pType = ins.Parameters.Add("$type", SqliteType.Text);
        var pContact = ins.Parameters.Add("$contact", SqliteType.Text);
        var pDate = ins.Parameters.Add("$date", SqliteType.Text);
        var pPayable = ins.Parameters.Add("$payable", SqliteType.Real);
        var pUpdated = ins.Parameters.Add("$updated", SqliteType.Text);

        var seed = new[] { ("SUP-301", "HealthLine Pharma", "Distributor", "0300-1111111", 4200d), ("SUP-302", "Global Medics", "Wholesaler", "0300-2222222", 7860d), ("SUP-303", "Sterile Supply Co", "Distributor", "0300-3333333", 2410d) };
        foreach (var s in seed)
        {
            pCode.Value = s.Item1; pName.Value = s.Item2; pType.Value = s.Item3; pContact.Value = s.Item4; pPayable.Value = s.Item5;
            pDate.Value = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            pUpdated.Value = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            ins.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private static (string where, string? term) BuildWhere(string? search)
        => string.IsNullOrWhiteSpace(search)
            ? (string.Empty, null)
            : ("WHERE Code LIKE $search OR Name LIKE $search OR Contact LIKE $search", $"%{search.Trim()}%");
}
