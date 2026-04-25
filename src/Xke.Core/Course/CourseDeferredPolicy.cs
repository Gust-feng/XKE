namespace Xke.Core.Course;

internal static class CourseDeferredPolicy
{
    public const string TraditionalJwCaptchaReason = "traditional-jw-captcha-deferred";
    public const string TraditionalJwCaptchaMessage = "课程预览需要先复用已连接的传统教务会话";
    public const string GrabReason = "course-grab-deferred";
    public const string GrabMessage = "当前只开放课程预览，快速抢课被禁用";
}
