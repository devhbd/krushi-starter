namespace MotorStarter.Models;

public enum UserRole
{
    Master,
    Regular
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Regular;
    public bool CanStart => Role == UserRole.Master || AllowedActions.Contains(MotorActionType.Start);
    public bool CanStop => Role == UserRole.Master || AllowedActions.Contains(MotorActionType.Stop);
    public bool CanViewStatus => Role == UserRole.Master || AllowedActions.Contains(MotorActionType.Status);
    public ICollection<MotorActionType> AllowedActions { get; set; } = new HashSet<MotorActionType>();
}
