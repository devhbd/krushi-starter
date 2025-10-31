namespace MotorStarter.Models;

public class MotorActionLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public MotorActionType ActionType { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}
