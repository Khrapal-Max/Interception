//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain.Policies;

/// <summary>
/// Доменна policy для рішень пріоритезації/класифікації під час групування
/// невідомих учасників у Candidate Group.
/// </summary>
public static class ParticipantCandidateGroupingPolicy
{
    /// <summary>
    /// Мінімальна кількість збігів, потрібна для приєднання до групи.
    /// </summary>
    public static int RequiredStrongMatches(int existingMembers)
        => existingMembers <= 1
            ? 1
            : (int)Math.Ceiling(existingMembers / 2.0);

    /// <summary>
    /// Чи може кандидат бути доданий до групи на основі кількості сильних збігів.
    /// </summary>
    public static bool CanJoinGroup(int strongMatches, int existingMembers)
        => strongMatches >= RequiredStrongMatches(existingMembers);

    /// <summary>
    /// Порогове рішення щодо впевненості score.
    /// </summary>
    public static bool IsConfident(double score, double minConfidenceScore)
        => score >= minConfidenceScore;

    /// <summary>
    /// Мінімальний валідний розмір групи для створення/збереження.
    /// </summary>
    public static bool IsValidGroupSize(int membersCount)
        => membersCount >= 2;
}
