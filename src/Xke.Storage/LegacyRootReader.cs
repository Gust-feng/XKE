using System.Text.Json;

namespace Xke.Storage;

public sealed class LegacyRootReader
{
    // Kept for Python-format fixture/reference reads only. Do not wire this into
    // active .NET Auth/Schedule runtime because it can point at legacy data/.env.
    public LegacyDataRoot DataRoot { get; }
    public LegacyFileReader FileReader { get; }
    public LegacyNoticeCacheReader NoticeCacheReader { get; }

    public LegacyRootReader(LegacyDataRoot dataRoot, LegacyFileReader? fileReader = null)
    {
        DataRoot = dataRoot;
        FileReader = fileReader ?? new LegacyFileReader(dataRoot.RootPath);
        NoticeCacheReader = new LegacyNoticeCacheReader(dataRoot.NoticeCache());
    }

    public LegacyStudentReader ForStudent(string studentId)
    {
        return new LegacyStudentReader(DataRoot.ForStudent(studentId), FileReader);
    }

    public LegacyReadResult<JsonElement> ReadNoticeIndex()
    {
        return NoticeCacheReader.ReadIndex();
    }

    public LegacyNoticeValidationResult<JsonElement> ReadAndValidateNoticeIndex(string appRoot)
    {
        return NoticeCacheReader.ReadAndValidateIndex(appRoot);
    }

    public LegacyReadResult<string> ReadNoticeIndexSignature()
    {
        return NoticeCacheReader.ReadIndexSignature();
    }

    public LegacyReadResult<string> ReadTelemetryInstallation()
    {
        return FileReader.ReadText(DataRoot.Telemetry().InstallationPath);
    }

    public LegacyReadResult<string> ReadTelemetryAesKey()
    {
        return FileReader.ReadText(DataRoot.Telemetry().TelemetryAesKeyPath);
    }
}
