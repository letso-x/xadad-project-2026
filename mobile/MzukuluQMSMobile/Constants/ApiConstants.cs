namespace MzukuluQMSMobile.Constants;

public static class ApiConstants
{
#if ANDROID
    public const string BaseUrl = "https://10.0.2.2:7044/";
#elif IOS
    public const string BaseUrl = "https://localhost:7044/";
#elif WINDOWS
    public const string BaseUrl = "http://localhost:5163/";
#else
    public const string BaseUrl = "http://localhost:5163/";
#endif
}