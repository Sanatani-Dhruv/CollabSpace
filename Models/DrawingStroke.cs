namespace CollabSpace.Models;

public class DrawingStroke
{
    public int Id { get; set; }

    public int RoomId { get; set; }

    // JSON-serialized drawing points, color, and stroke width.
    public string StrokeData { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Room Room { get; set; } = null!;
}
