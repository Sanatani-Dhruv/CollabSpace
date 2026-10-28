namespace CollabSpace.Models;

public class CanvasNote
{
    public int Id { get; set; }

    public int RoomId { get; set; }

    public string Content { get; set; } = string.Empty;

    public double X { get; set; } = 100;

    public double Y { get; set; } = 100;

    public double Width { get; set; } = 240;

    public double Height { get; set; } = 180;

    public string Color { get; set; } = "#fff3a3";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Room Room { get; set; } = null!;
}
