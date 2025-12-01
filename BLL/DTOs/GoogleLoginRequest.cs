namespace BLL.DTOs
{
    public class GoogleLoginRequest
    {
        public string IdToken { get; set; }
        public string DeviceInfo { get; set; }
    }

    public class GoogleLoginResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public UserProfile User { get; set; }
        public string Message { get; set; }
        public bool IsNewUser { get; set; }
    }
}