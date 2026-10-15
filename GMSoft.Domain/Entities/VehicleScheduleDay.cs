namespace GMSoft.Domain.Entities;

public class VehicleScheduleDay
{
    public Guid VehicleId { get; set; }
    public int Day { get; set; }
    public Guid? ZoneId { get; set; }
    public Zone? Zone { get; set; }
}
