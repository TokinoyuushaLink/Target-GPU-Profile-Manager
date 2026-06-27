using System.Text.Json;
using System.Text.Json.Serialization;

namespace GpuPreference;

public class Category
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("entries")]
    public List<string> Entries { get; set; } = [];
}

public class CategoryData
{
    [JsonPropertyName("categories")]
    public List<Category> Categories { get; set; } = [];

    [JsonPropertyName("notes")]
    public Dictionary<string, string> Notes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class CategoryStore
{
    static readonly string DataPath = Path.Combine(
        AppContext.BaseDirectory, "categories.json");

    static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    static CategoryData _data = new();

    public static IReadOnlyList<Category> Categories => _data.Categories;

    public static void Load()
    {
        try
        {
            if (!File.Exists(DataPath)) return;
            var json = File.ReadAllText(DataPath);
            _data = JsonSerializer.Deserialize<CategoryData>(json) ?? new();
        }
        catch { _data = new(); }
    }

    // 清理 json 中注册表已不存在的条目
    public static void PurgeStale(IEnumerable<string> existingExes)
    {
        var existing = new HashSet<string>(existingExes, StringComparer.OrdinalIgnoreCase);
        bool dirty = false;

        foreach (var cat in _data.Categories)
        {
            int before = cat.Entries.Count;
            cat.Entries.RemoveAll(e => !existing.Contains(e));
            if (cat.Entries.Count != before) dirty = true;
        }

        foreach (var key in _data.Notes.Keys.Where(k => !existing.Contains(k)).ToList())
        {
            _data.Notes.Remove(key);
            dirty = true;
        }

        RemoveEmptyCategories();
        if (dirty) Save();
    }

    public static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);
        File.WriteAllText(DataPath, JsonSerializer.Serialize(_data, JsonOpts));
    }

    // 返回某个 exe 所属分类名，未归类返回 null
    public static string? GetCategory(string exe) =>
        _data.Categories.FirstOrDefault(c =>
            c.Entries.Any(e => e.Equals(exe, StringComparison.OrdinalIgnoreCase)))?.Name;

    // 将 exe 移入指定分类（自动从旧分类移除）
    public static void SetCategory(string exe, string categoryName)
    {
        // 先从所有分类移除
        foreach (var c in _data.Categories)
            c.Entries.RemoveAll(e => e.Equals(exe, StringComparison.OrdinalIgnoreCase));

        var target = _data.Categories.FirstOrDefault(c =>
            c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            target = new Category { Name = categoryName };
            _data.Categories.Add(target);
        }
        target.Entries.Add(exe);
        Save();
    }

    // 将 exe 设为未分类（从所有分类移除）
    public static void ClearCategory(string exe)
    {
        foreach (var c in _data.Categories)
            c.Entries.RemoveAll(e => e.Equals(exe, StringComparison.OrdinalIgnoreCase));
        RemoveEmptyCategories();
        Save();
    }

    // 删除整个分类（条目变为未分类，不删注册表）
    public static void DeleteCategory(string categoryName)
    {
        _data.Categories.RemoveAll(c =>
            c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    // 重命名分类
    public static void RenameCategory(string oldName, string newName)
    {
        var cat = _data.Categories.FirstOrDefault(c =>
            c.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase));
        if (cat is not null) { cat.Name = newName; Save(); }
    }

    // 添加空分类
    public static void AddCategory(string name)
    {
        if (_data.Categories.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return;
        _data.Categories.Add(new Category { Name = name });
        Save();
    }

    public static string? GetNote(string exe) =>
        _data.Notes.TryGetValue(exe, out var n) ? n : null;

    public static void SetNote(string exe, string note)
    {
        if (string.IsNullOrWhiteSpace(note))
            _data.Notes.Remove(exe);
        else
            _data.Notes[exe] = note;
        Save();
    }

    // 删除 exe 时同步清理
    public static void RemoveExe(string exe)
    {
        foreach (var c in _data.Categories)
            c.Entries.RemoveAll(e => e.Equals(exe, StringComparison.OrdinalIgnoreCase));
        _data.Notes.Remove(exe);
        RemoveEmptyCategories();
        Save();
    }

    static void RemoveEmptyCategories() =>
        _data.Categories.RemoveAll(c => c.Entries.Count == 0);
}
