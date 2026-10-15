namespace MzukuluQMSMobile.Constants;

public static class ApiConstants
{
#if DEBUG && ANDROID
    // Local Android development only. Release builds always use the public API.
    public const string BaseUrl = "https://10.0.2.2:7044/";
#elif DEBUG
    // Preserve local Windows/Visual Studio development.
    public const string BaseUrl = "https://localhost:7044/";
#else
    // One shared production origin for identity, reads and durable sync writes.
    public const string BaseUrl = "https://xadad-project-2026.onrender.com/";
#endif
}
