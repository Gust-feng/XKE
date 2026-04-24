namespace Xke.Storage;

public sealed class LegacyDataRoot
{
    public string RootPath { get; }
    public string TelemetryDirectory => Path.Combine(RootPath, ".telemetry");
    public string NoticeCacheDirectory => Path.Combine(RootPath, ".notice_cache", "v1");

    public LegacyDataRoot(string rootPath)
    {
        RootPath = rootPath;
    }

    public LegacyStudentRoot ForStudent(string studentId)
    {
        return new LegacyStudentRoot(RootPath, studentId);
    }

    public LegacyTelemetryRoot Telemetry()
    {
        return new LegacyTelemetryRoot(TelemetryDirectory);
    }

    public LegacyNoticeCacheRoot NoticeCache()
    {
        return new LegacyNoticeCacheRoot(NoticeCacheDirectory);
    }
}
