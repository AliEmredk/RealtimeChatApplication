namespace dataaccess.Entities;

public class Room
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public bool IsArchived { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}