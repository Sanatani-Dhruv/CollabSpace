namespace CollabSpace.Models;

public class RoomNote
{
    public int Id { get; set; }

    public int RoomId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Room Room { get; set; } = null!;
}
