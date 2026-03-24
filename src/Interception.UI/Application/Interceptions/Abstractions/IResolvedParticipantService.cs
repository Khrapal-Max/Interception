//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Dtos;

namespace Interception.UI.Application.Interceptions.Abstractions;

/// <summary>
/// Сервіс управління встановленими особами (ResolvedParticipant).
/// Підтвердження групи створює ResolvedParticipant і прив'язує до групи.
/// </summary>
public interface IResolvedParticipantService
{
    /// <summary>
    /// Підтверджує групу кандидатів — створює ResolvedParticipant
    /// і прив'язує до групи через ResolvedParticipantId.
    /// Кидає InvalidOperationException якщо група не Open або
    /// ResolvedParticipant з таким ім'ям вже існує.
    /// </summary>
    Task<ResolvedParticipantDto> ConfirmGroupAsync(
        Guid groupId,
        ConfirmCandidateGroupDto form,
        string operatorName,
        CancellationToken ct = default);

    /// <summary>
    /// Відхиляє групу — НВ залишаються невідомими.
    /// </summary>
    Task DismissGroupAsync(
        Guid groupId,
        string operatorName,
        CancellationToken ct = default);

    /// <summary>Повертає всі встановлені особи відсортовані за Name.</summary>
    Task<IReadOnlyList<ResolvedParticipantDto>> GetAllAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Оновлює Name, Role, Division встановленої особи.
    /// Кидає InvalidOperationException якщо нова назва вже зайнята.
    /// </summary>
    Task<ResolvedParticipantDto> UpdateAsync(
        Guid id,
        ConfirmCandidateGroupDto form,
        CancellationToken ct = default);
}
