using System.Text.Json;

namespace Xke.Storage;

public sealed class LegacyStudentReader
{
    private readonly LegacyFileReader _fileReader;
    private readonly LegacyStudentRoot _studentRoot;

    public LegacyStudentReader(LegacyStudentRoot studentRoot, LegacyFileReader? fileReader = null)
    {
        _studentRoot = studentRoot;
        _fileReader = fileReader ?? new LegacyFileReader(studentRoot.DataRootPath);
    }

    public LegacyReadResult<JsonElement> ReadBasicInfo()
    {
        var primary = _fileReader.ReadJsonElement(_studentRoot.BasicInfoPath);
        return primary.Exists ? primary : _fileReader.ReadJsonElement(_studentRoot.LegacyBasicInfoPath);
    }

    public LegacyReadResult<JsonElement> ReadPreferences()
    {
        var primary = _fileReader.ReadJsonElement(_studentRoot.PreferencesPath);
        return primary.Exists ? primary : _fileReader.ReadJsonElement(_studentRoot.LegacyPreferencesPath);
    }

    public LegacyReadResult<JsonElement> ReadAppRuntime()
    {
        return _fileReader.ReadJsonElement(_studentRoot.AppRuntimePath);
    }

    public LegacyReadResult<JsonElement> ReadNetworkAuthHistory()
    {
        return _fileReader.ReadJsonElement(_studentRoot.NetworkAuthHistoryPath);
    }

    public LegacyReadResult<JsonElement> ReadRemoteSensingSnapshot()
    {
        return _fileReader.ReadJsonElement(_studentRoot.RemoteSensingSnapshotPath);
    }

    public LegacyReadResult<JsonElement> ReadCourseGrabGuard()
    {
        return _fileReader.ReadJsonElement(_studentRoot.CourseGrabGuardPath);
    }

    public LegacyReadResult<JsonElement> ReadCookieScheduleParams()
    {
        return _fileReader.ReadJsonElement(_studentRoot.CookieScheduleParamsPath);
    }

    public LegacyReadResult<JsonElement> ReadAccountCredentials()
    {
        return _fileReader.ReadJsonElement(_studentRoot.CredentialsPath);
    }

    public LegacyReadResult<string> ReadCookieHeaderText()
    {
        return _fileReader.ReadText(_studentRoot.CookieHeaderPath);
    }

    public LegacyReadResult<string> ReadPreloginCaptureText()
    {
        return _fileReader.ReadText(_studentRoot.PreloginCapturePath);
    }
}
