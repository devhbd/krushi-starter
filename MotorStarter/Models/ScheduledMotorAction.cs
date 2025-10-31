namespace MotorStarter.Models;

public class ScheduledMotorAction
{
    public int Id { get; set; }
    public DateTime ExecuteAt { get; set; }
    public MotorActionType ActionType { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public bool Executed { get; set; }
}
