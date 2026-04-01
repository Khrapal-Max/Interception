//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.Application.Analytics.Services.Builders.LinkMap;

internal sealed class PersonAccumulator(string name)
{
    public string Name { get; } = name;
    public string? Role { get; set; }
    public DateTime RoleObservedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public int Mentions { get; set; }
    public int ConnectionWeight { get; set; }
    public int UniquePartnerCount { get; set; }
    public HashSet<string> Frequencies { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterMention(DateTime observedAt, string? role, string? frequency)
    {
        Mentions++;

        if (observedAt > LastSeenAt)
            LastSeenAt = observedAt;

        if (!string.IsNullOrWhiteSpace(role) && observedAt >= RoleObservedAt)
        {
            Role = role.Trim();
            RoleObservedAt = observedAt;
        }

        if (!string.IsNullOrWhiteSpace(frequency))
            Frequencies.Add(frequency.Trim());
    }

    public void AddConnectionWeight(int increment = 1)
    {
        if (increment > 0)
            ConnectionWeight += increment;
    }

    public void SetUniquePartnerCount(int count)
    {
        UniquePartnerCount = Math.Max(UniquePartnerCount, count);
    }

    public void MergeFrom(PersonAccumulator other)
    {
        Mentions += other.Mentions;
        ConnectionWeight += other.ConnectionWeight;
        UniquePartnerCount = Math.Max(UniquePartnerCount, other.UniquePartnerCount);

        if (other.LastSeenAt > LastSeenAt)
            LastSeenAt = other.LastSeenAt;

        if (!string.IsNullOrWhiteSpace(other.Role) && other.RoleObservedAt >= RoleObservedAt)
        {
            Role = other.Role;
            RoleObservedAt = other.RoleObservedAt;
        }

        foreach (var frequency in other.Frequencies)
            Frequencies.Add(frequency);
    }

    public PersonAccumulator Clone()
    {
        var copy = new PersonAccumulator(Name);
        copy.MergeFrom(this);
        return copy;
    }
}
