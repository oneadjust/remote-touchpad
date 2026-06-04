namespace RemoteTouchpad;

public sealed class AppConfig
{
    public int Port { get; set; } = 8765;
    public string Password { get; set; } = "123456";
    public double Sensitivity { get; set; } = 1.0;
    public string SessionToken { get; set; } = Guid.NewGuid().ToString("N");
}
