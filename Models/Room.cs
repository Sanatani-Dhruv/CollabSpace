namespace CollabSpace.Models;

public class Room
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AccessCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    public RoomNote? Note { get; set; }

    public ICollection<DrawingStroke> DrawingStrokes { get; set; }
        = new List<DrawingStroke>();
}
