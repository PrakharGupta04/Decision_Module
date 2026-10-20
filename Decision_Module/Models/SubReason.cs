namespace Decision_Module.Models;

public class SubReason
{
    public int SubReasonId { get; set; }
    public int ReasonId { get; set; }
    public string SubReasonText { get; set; } = "";
}

public class CoverSubReason
{
    public int UwId { get; set; }
    public int SubReasonId { get; set; }
}