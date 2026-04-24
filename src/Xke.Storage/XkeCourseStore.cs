using System.Text.Json;
using Xke.Contracts.Responses;

namespace Xke.Storage;

public sealed class XkeCourseStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly XkeDataRoot _root;
    private readonly XkeStudentStore _studentStore;

    public XkeCourseStore(XkeDataRoot root, XkeStudentStore studentStore)
    {
        _root = root;
        _studentStore = studentStore;
    }

    public string CourseDirectory => _studentStore.CourseDirectory;
    public string RoundsPath => Path.Combine(CourseDirectory, "rounds.json");
    public string RoundsRawPath => Path.Combine(CourseDirectory, "rounds_raw.json");
    public string PreviewDirectory => Path.Combine(CourseDirectory, "previews");

    public string SaveRounds(CourseRoundsPayload payload, Dictionary<string, object?> rawPayload)
    {
        Directory.CreateDirectory(CourseDirectory);
        WriteJson(RoundsPath, payload);
        if (rawPayload.Count > 0)
        {
            WriteJson(RoundsRawPath, rawPayload);
        }

        return _root.RelativePath(RoundsPath);
    }

    public CourseRoundsPayload? LoadRounds()
    {
        return ReadJson<CourseRoundsPayload>(RoundsPath);
    }

    public string SavePreview(CoursePreviewPayload payload, Dictionary<string, object?> rawPayload)
    {
        var roundDirectory = ResolvePreviewRoundDirectory(payload.RoundId);
        Directory.CreateDirectory(roundDirectory);
        var previewPath = ResolvePreviewPath(payload.RoundId);
        WriteJson(previewPath, payload);
        if (rawPayload.Count > 0)
        {
            WriteJson(ResolvePreviewRawPath(payload.RoundId), rawPayload);
        }

        return _root.RelativePath(previewPath);
    }

    public CoursePreviewPayload? LoadPreview(string roundId)
    {
        return ReadJson<CoursePreviewPayload>(ResolvePreviewPath(roundId));
    }

    public string RelativeDirectory()
    {
        Directory.CreateDirectory(CourseDirectory);
        return _root.RelativePath(CourseDirectory);
    }

    private string ResolvePreviewRoundDirectory(string roundId)
    {
        return Path.Combine(PreviewDirectory, XkeDataRoot.SanitizePathSegment(roundId));
    }

    private string ResolvePreviewPath(string roundId)
    {
        return Path.Combine(ResolvePreviewRoundDirectory(roundId), "preview.json");
    }

    private string ResolvePreviewRawPath(string roundId)
    {
        return Path.Combine(ResolvePreviewRoundDirectory(roundId), "preview_raw.json");
    }

    private static T? ReadJson<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
    }

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
    }
}
