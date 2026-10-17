using System.Text.Json;

namespace Xke.Storage;

/// <summary>Only dates and opaque delivery fingerprints; no course text or credentials.</summary>
public sealed class XkeLessonReminderStore
{
    private readonly string _path;
    public XkeLessonReminderStore(XkeStudentStore student) => _path = Path.Combine(student.InfoDirectory, "lesson_reminders.json");

    public bool Contains(DateOnly date, string fingerprint)
        => Read().TryGetValue(date.ToString("yyyy-MM-dd"), out var sent) && sent.Contains(fingerprint);

    public void Record(DateOnly date, string fingerprint, DateOnly today)
    {
        var receipts = Read();
        foreach (var key in receipts.Keys.Where(key => string.CompareOrdinal(key, today.AddDays(-1).ToString("yyyy-MM-dd")) < 0).ToArray())
            receipts.Remove(key);
        var day = date.ToString("yyyy-MM-dd");
        if (!receipts.TryGetValue(day, out var sent)) receipts[day] = sent = [];
        sent.Add(fingerprint);
        AtomicFile.WriteText(_path, JsonSerializer.Serialize(receipts));
    }

    private Dictionary<string, HashSet<string>> Read()
    {
        if (!File.Exists(_path)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, HashSet<string>>>(File.ReadAllText(_path)) ?? new(); }
        catch (JsonException) { return new(); }
    }
}
