namespace CollabSpace.Models;

public class Room
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AccessCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    // Legacy shared note, retained during data migration.
    public RoomNote? Note { get; set; }

    public ICollection<CanvasNote> CanvasNotes { get; set; }
        = new List<CanvasNote>();

    public ICollection<DrawingStroke> DrawingStrokes { get; set; }
        = new List<DrawingStroke>();
}
