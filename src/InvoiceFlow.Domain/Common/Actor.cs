namespace InvoiceFlow.Domain.Common;

public enum ActorType
{
    User,
    System,
}

/// <summary>
/// Who performed an action. For users, <see cref="Id"/> is the Keycloak subject id and <see cref="Name"/>
/// a snapshot of the display name at that time.
/// </summary>
public sealed record Actor
{
    private Actor(ActorType type, string id, string name)
    {
        Type = type;
        Id = id;
        Name = name;
    }

    public ActorType Type { get; }

    public string Id { get; }

    public string Name { get; }

    public static Actor User(string subjectId, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new Actor(ActorType.User, subjectId, displayName);
    }

    public static Actor System(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Actor(ActorType.System, name, name);
    }

    // Display names change; identity is the type and id only.
    public bool IsSameAs(Actor other) => other.Type == Type && other.Id == Id;
}
