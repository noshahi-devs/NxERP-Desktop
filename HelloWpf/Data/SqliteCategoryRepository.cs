using System.Globalization;
using HelloWpf.Models;
using Microsoft.Data.Sqlite;

namespace HelloWpf.Data;

public sealed class SqliteCategoryRepository : ICategoryRepository
{
    private readonly string _connectionString;

    public SqliteCategoryRepository(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadWriteCreate, Cache = SqliteCacheMode.Shared }.ToString();
        Initialize();
    }

    public CategoryPageResult GetPage(string? search, int pageNumber, int pageSize)
    {
        using var c = new SqliteConnection(_connectionString);
        c.Open();
        var safePage = Math.Max(1, pageNumber);
        var safeSize = Math.Max(10, pageSize);
        var offset = (safePage - 1) * safeSize;
        var (where, term) = BuildWhere(search);

        using var count = c.CreateCommand();
        count.CommandText = $"SELECT COUNT(*) FROM Categories {where};";
        if (term is not null) count.Parameters.AddWithValue("$search", term);
        var total = Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture);

        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"SELECT Code,Name,Type,ParentCategory,CreatedDate,IsActive,UpdatedAt FROM Categories {where} ORDER BY UpdatedAt DESC LIMIT $limit OFFSET $offset;";
        if (term is not null) cmd.Parameters.AddWithValue("$search", term);
        cmd.Parameters.AddWithValue("$limit", safeSize);
        cmd.Parameters.AddWithValue("$offset", offset);

        var items = new List<Category>(safeSize);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            items.Add(new Category
            {
                Code = r.GetString(0),
                Name = r.GetString(1),
                Type = r.GetString(2),
                ParentCategory = r.GetString(3),
                CreatedDate = DateTime.Parse(r.GetString(4), CultureInfo.InvariantCulture),
                IsActive = r.GetInt64(5) == 1,
                UpdatedAt = DateTime.Parse(r.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            });
        }

        return new CategoryPageResult(items, total, safePage, safeSize);
    }

    public Category? GetByCode(string code)
    {
        using var c = new SqliteConnection(_connectionString);
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Code,Name,Type,ParentCategory,CreatedDate,IsActive,UpdatedAt FROM Categories WHERE Code=$code LIMIT 1;";
        cmd.Parameters.AddWithValue("$code", code.Trim());
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new Category
        {
            Code = r.GetString(0), Name = r.GetString(1), Type = r.GetString(2), ParentCategory = r.GetString(3),
            CreatedDate = DateTime.Parse(r.GetString(4), CultureInfo.InvariantCulture), IsActive = r.GetInt64(5) == 1,
            UpdatedAt = DateTime.Parse(r.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };
    }

    public void Upsert(Category category)
    {
        using var c = new SqliteConnection(_connectionString);
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"INSERT INTO Categories(Code,Name,Type,ParentCategory,CreatedDate,IsActive,UpdatedAt)
VALUES($code,$name,$type,$parent,$createdDate,$active,$updatedAt)
ON CONFLICT(Code) DO UPDATE SET Name=excluded.Name,Type=excluded.Type,ParentCategory=excluded.ParentCategory,CreatedDate=excluded.CreatedDate,IsActive=excluded.IsActive,UpdatedAt=excluded.UpdatedAt;";
        cmd.Parameters.AddWithValue("$code", category.Code.Trim());
        cmd.Parameters.AddWithValue("$name", category.Name.Trim());
        cmd.Parameters.AddWithValue("$type", category.Type.Trim());
        cmd.Parameters.AddWithValue("$parent", category.ParentCategory.Trim());
        cmd.Parameters.AddWithValue("$createdDate", category.CreatedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$active", category.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
    }

    public bool Delete(string code)
    {
        using var c = new SqliteConnection(_connectionString);
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM Categories WHERE Code=$code;";
        cmd.Parameters.AddWithValue("$code", code.Trim());
        return cmd.ExecuteNonQuery() > 0;
    }

    private void Initialize()
    {
        using var c = new SqliteConnection(_connectionString);
        c.Open();
        using var create = c.CreateCommand();
        create.CommandText = @"CREATE TABLE IF NOT EXISTS Categories(Code TEXT PRIMARY KEY,Name TEXT NOT NULL,Type TEXT NOT NULL,ParentCategory TEXT NOT NULL,CreatedDate TEXT NOT NULL,IsActive INTEGER NOT NULL,UpdatedAt TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS IX_Categories_Name ON Categories(Name);
CREATE INDEX IF NOT EXISTS IX_Categories_UpdatedAt ON Categories(UpdatedAt DESC);";
        create.ExecuteNonQuery();

        using var count = c.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM Categories;";
        var total = Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (total > 0) return;

        using var tx = c.BeginTransaction();
        using var ins = c.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = @"INSERT INTO Categories(Code,Name,Type,ParentCategory,CreatedDate,IsActive,UpdatedAt) VALUES ($code,$name,$type,$parent,$date,1,$updated);";
        var pCode = ins.Parameters.Add("$code", SqliteType.Text);
        var pName = ins.Parameters.Add("$name", SqliteType.Text);
        var pType = ins.Parameters.Add("$type", SqliteType.Text);
        var pParent = ins.Parameters.Add("$parent", SqliteType.Text);
        var pDate = ins.Parameters.Add("$date", SqliteType.Text);
        var pUpdated = ins.Parameters.Add("$updated", SqliteType.Text);

        var seed = new[] { ("CAT-101", "Antibiotic", "Medicine", ""), ("CAT-102", "Drip/Infusion", "Medicine", ""), ("CAT-103", "Syrup", "Medicine", ""), ("CAT-104", "Painkiller", "Medicine", "") };
        foreach (var s in seed)
        {
            pCode.Value = s.Item1; pName.Value = s.Item2; pType.Value = s.Item3; pParent.Value = s.Item4;
            pDate.Value = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            pUpdated.Value = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            ins.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private static (string where, string? term) BuildWhere(string? search)
        => string.IsNullOrWhiteSpace(search)
            ? (string.Empty, null)
            : ("WHERE Code LIKE $search OR Name LIKE $search", $"%{search.Trim()}%");
}
