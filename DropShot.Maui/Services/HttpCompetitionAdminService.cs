using System.Net.Http.Json;
using DropShot.Shared;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Maui.Services;

public sealed class HttpCompetitionAdminService(HttpClient http) : ICompetitionAdminService
{
    // ── Read ─────────────────────────────────────────────────────────────────

    public async Task<CompetitionEditDto?> GetCompetitionForEditAsync(int? competitionId, CancellationToken ct = default)
    {
        var url = competitionId.HasValue && competitionId.Value > 0
            ? $"api/competitions/admin/{competitionId.Value}/edit"
            : "api/competitions/admin/new/edit";
        return await http.GetFromJsonAsync<CompetitionEditDto>(url, ct);
    }

    public async Task<List<CompetitionSeedSourceDto>> GetSeedSourceCandidatesAsync(
        int? excludeCompetitionId, CompetitionFormat format, int? hostClubId, CancellationToken ct = default)
    {
        var qs = $"format={format}";
        if (excludeCompetitionId.HasValue) qs += $"&excludeCompetitionId={excludeCompetitionId.Value}";
        if (hostClubId.HasValue) qs += $"&hostClubId={hostClubId.Value}";
        return await http.GetFromJsonAsync<List<CompetitionSeedSourceDto>>(
            $"api/competitions/admin/seed-source-candidates?{qs}", ct) ?? [];
    }

    public async Task<List<ClubSchedulingTemplateDto>> GetClubTemplatesAsync(int clubId, CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<ClubSchedulingTemplateDto>>(
            $"api/competitions/admin/clubs/{clubId}/scheduling-templates", ct) ?? [];

    public async Task<List<ClubEmailTemplateDto>> GetEmailTemplatesAsync(int clubId, CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<ClubEmailTemplateDto>>(
            $"api/competitions/admin/clubs/{clubId}/email-templates", ct) ?? [];

    public async Task<bool> CanEditCompetitionAsync(int? competitionId, CancellationToken ct = default)
    {
        var qs = competitionId.HasValue ? $"?competitionId={competitionId.Value}" : "";
        return await http.GetFromJsonAsync<bool>($"api/competitions/admin/can-edit{qs}", ct);
    }

    public async Task<bool> IsSuperAdminAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<bool>("api/competitions/admin/is-super-admin", ct);

    public async Task<List<CompetitionAdminRowDto>> GetCompetitionAdminsAsync(int competitionId, CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<CompetitionAdminRowDto>>(
            $"api/competitions/admin/{competitionId}/admins", ct) ?? [];

    // ── Competition lifecycle ────────────────────────────────────────────────

    public async Task<int> SaveCompetitionAsync(
        int? competitionId, SaveCompetitionEditRequest request, CancellationToken ct = default)
    {
        var resp = competitionId.HasValue && competitionId.Value > 0
            ? await http.PutAsJsonAsync($"api/competitions/admin/{competitionId.Value}", request, ct)
            : await http.PostAsJsonAsync("api/competitions/admin", request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrEmpty(body) ? resp.ReasonPhrase ?? "Save failed." : body);
        }
        return await resp.Content.ReadFromJsonAsync<int>(cancellationToken: ct);
    }

    public async Task<CloneCompetitionResultDto> CloneCompetitionAsync(
        int sourceCompetitionId, CloneCompetitionRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{sourceCompetitionId}/clone", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CloneCompetitionResultDto>(cancellationToken: ct))!;
    }

    public async Task<bool> ToggleStartedAsync(int competitionId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"api/competitions/admin/{competitionId}/toggle-started", null, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<bool>(cancellationToken: ct);
    }

    // ── Stages ───────────────────────────────────────────────────────────────

    public async Task ApplyStageFollowUpAsync(
        int competitionId, ApplyStageFollowUpRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/stages/follow-up", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    // ── Admins ───────────────────────────────────────────────────────────────

    public async Task AddCompetitionAdminAsync(
        int competitionId, AddCompetitionAdminRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/admins", request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrEmpty(body) ? resp.ReasonPhrase ?? "Failed to add admin." : body);
        }
    }

    public async Task RemoveCompetitionAdminAsync(int competitionId, string userId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync(
            $"api/competitions/admin/{competitionId}/admins/{Uri.EscapeDataString(userId)}", ct);
        resp.EnsureSuccessStatusCode();
    }

    // ── Rubber template ──────────────────────────────────────────────────────

    public async Task<RubberTemplateStateDto> LoadRubberTemplateStateAsync(int competitionId, CancellationToken ct = default)
    {
        var dto = await http.GetFromJsonAsync<RubberTemplateStateDto>(
            $"api/competitions/admin/{competitionId}/rubber-template", ct)
            ?? throw new InvalidOperationException("Rubber template not found.");
        return dto;
    }

    public async Task ApplyRubberPresetAsync(
        int competitionId, ApplyRubberPresetRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/rubber-template/preset", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task ConvertToCustomTemplateAsync(int competitionId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync(
            $"api/competitions/admin/{competitionId}/rubber-template/convert-to-custom", null, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task AddCustomRubberRowAsync(int competitionId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync(
            $"api/competitions/admin/{competitionId}/rubber-template/rows", null, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task SaveRubberRowAsync(
        int competitionId, SaveRubberRowRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync(
            $"api/competitions/admin/{competitionId}/rubber-template/rows", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteCustomRubberRowAsync(int competitionId, int order, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync(
            $"api/competitions/admin/{competitionId}/rubber-template/rows/{order}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task RevertToDefaultTemplateAsync(int competitionId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync(
            $"api/competitions/admin/{competitionId}/rubber-template/revert", null, ct);
        resp.EnsureSuccessStatusCode();
    }

    // ── Email ────────────────────────────────────────────────────────────────

    public async Task SendMatchEmailAsync(
        int competitionId, SendMatchEmailRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/email/match", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task SendCompetitionEmailAsync(
        int competitionId, SendCompetitionEmailRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/email/competition", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    // ── Participants ─────────────────────────────────────────────────────────

    public async Task<List<PlayerSearchResultDto>> SearchPlayersAsync(
        int competitionId, SearchPlayersForAddRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/participants/search", request, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<PlayerSearchResultDto>>(cancellationToken: ct) ?? [];
    }

    public async Task AddParticipantAsync(int competitionId, AddParticipantRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/participants", request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrEmpty(body) ? resp.ReasonPhrase ?? "Failed to add participant." : body);
        }
    }

    public async Task<BulkAddParticipantsResultDto> BulkAddParticipantsAsync(
        int competitionId, BulkAddParticipantsRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/participants/bulk-add", request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrEmpty(body) ? resp.ReasonPhrase ?? "Failed to bulk-add participants." : body);
        }
        return await resp.Content.ReadFromJsonAsync<BulkAddParticipantsResultDto>(cancellationToken: ct)
            ?? new BulkAddParticipantsResultDto(0, 0);
    }

    public async Task RemoveParticipantAsync(int competitionId, int playerId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"api/competitions/admin/{competitionId}/participants/{playerId}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task UpdateParticipantStatusAsync(
        int competitionId, int playerId, UpdateParticipantStatusRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync(
            $"api/competitions/admin/{competitionId}/participants/{playerId}/status", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task AssignParticipantTeamAsync(
        int competitionId, int playerId, AssignParticipantTeamRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync(
            $"api/competitions/admin/{competitionId}/participants/{playerId}/team", request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(ExtractErrorMessage(body) ?? resp.ReasonPhrase ?? "Failed to assign team.");
        }
    }

    public async Task AssignParticipantRoleAsync(
        int competitionId, int playerId, SetParticipantRoleRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync(
            $"api/competitions/admin/{competitionId}/participants/{playerId}/role", request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(ExtractErrorMessage(body) ?? resp.ReasonPhrase ?? "Failed to assign role.");
        }
    }

    /// <summary>
    /// Parse the <c>{ "message": "..." }</c> body the API returns on
    /// BadRequest/NotFound so the surfaced error is the friendly message,
    /// not the raw JSON envelope. Returns null when the body isn't that
    /// shape so callers fall back to ReasonPhrase.
    /// </summary>
    private static string? ExtractErrorMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && doc.RootElement.TryGetProperty("message", out var msg)
                && msg.ValueKind == System.Text.Json.JsonValueKind.String)
                return msg.GetString();
        }
        catch (System.Text.Json.JsonException) { }
        return body;
    }

    public async Task AssignParticipantDivisionAsync(
        int competitionId, int playerId, SetParticipantDivisionRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync(
            $"api/competitions/admin/{competitionId}/participants/{playerId}/division", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task SetParticipantInitialRatingAsync(
        int competitionId, int playerId, SetParticipantInitialRatingRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync(
            $"api/competitions/admin/{competitionId}/participants/{playerId}/initial-rating", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<PlayerRatingSuggestionDto?> AcceptParticipantRatingAsync(
        int competitionId, int playerId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync(
            $"api/competitions/admin/{competitionId}/participants/{playerId}/accept-rating", content: null, ct);
        resp.EnsureSuccessStatusCode();
        if (resp.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        return await resp.Content.ReadFromJsonAsync<PlayerRatingSuggestionDto>(cancellationToken: ct);
    }

    public async Task<List<PlayerRatingSuggestionDto>> AcceptAllParticipantRatingsAsync(
        int competitionId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync(
            $"api/competitions/admin/{competitionId}/ratings/apply-all", content: null, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<PlayerRatingSuggestionDto>>(cancellationToken: ct)
            ?? new List<PlayerRatingSuggestionDto>();
    }

    public async Task ApplyDivisionPlacementAsync(
        int competitionId, int playerId, ApplyDivisionPlacementRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync(
            $"api/competitions/admin/{competitionId}/participants/{playerId}/division-placement", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task ApplyRolePlacementAsync(
        int competitionId, int playerId, ApplyRolePlacementRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync(
            $"api/competitions/admin/{competitionId}/participants/{playerId}/role-placement", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<int> ApplyAllPlacementsAsync(int competitionId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync(
            $"api/competitions/admin/{competitionId}/placements/apply-all", content: null, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<int>(cancellationToken: ct);
    }

    public async Task<int> CreateLightPlayerAsync(
        int competitionId, CreateLightPlayerForCompetitionRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/light-players", request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrEmpty(body) ? resp.ReasonPhrase ?? "Failed to create light player." : body);
        }
        return await resp.Content.ReadFromJsonAsync<int>(cancellationToken: ct);
    }

    public async Task SaveLightPlayerAsync(
        int competitionId, int playerId, SaveLightPlayerRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync(
            $"api/competitions/admin/{competitionId}/light-players/{playerId}", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    // ── Divisions ────────────────────────────────────────────────────────────

    public async Task<int> SaveDivisionAsync(
        int competitionId, int? divisionId, SaveDivisionRequest request, CancellationToken ct = default)
    {
        var resp = divisionId.HasValue
            ? await http.PutAsJsonAsync($"api/competitions/admin/{competitionId}/divisions/{divisionId.Value}", request, ct)
            : await http.PostAsJsonAsync($"api/competitions/admin/{competitionId}/divisions", request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrEmpty(body) ? resp.ReasonPhrase ?? "Failed to save division." : body);
        }
        return await resp.Content.ReadFromJsonAsync<int>(cancellationToken: ct);
    }

    public async Task DeleteDivisionAsync(int competitionId, int divisionId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync(
            $"api/competitions/admin/{competitionId}/divisions/{divisionId}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task RunSeedDivisionsAsync(
        int competitionId, SeedDivisionsFromPreviousRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/divisions/seed", request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrEmpty(body) ? resp.ReasonPhrase ?? "Failed to seed divisions." : body);
        }
    }

    public async Task AssignTeamDivisionAsync(
        int competitionId, int teamId, AssignTeamDivisionRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync(
            $"api/competitions/admin/{competitionId}/teams/{teamId}/division", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    // ── Teams ────────────────────────────────────────────────────────────────

    public async Task<int> SaveTeamAsync(
        int competitionId, int? teamId, SaveTeamRequest request, CancellationToken ct = default)
    {
        var resp = teamId.HasValue
            ? await http.PutAsJsonAsync($"api/competitions/admin/{competitionId}/teams/{teamId.Value}", request, ct)
            : await http.PostAsJsonAsync($"api/competitions/admin/{competitionId}/teams", request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrEmpty(body) ? resp.ReasonPhrase ?? "Failed to save team." : body);
        }
        return await resp.Content.ReadFromJsonAsync<int>(cancellationToken: ct);
    }

    public async Task DeleteTeamAsync(int competitionId, int teamId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"api/competitions/admin/{competitionId}/teams/{teamId}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteAllTeamsAsync(int competitionId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"api/competitions/admin/{competitionId}/teams", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task AssignCaptainAsync(
        int competitionId, AssignCaptainRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/teams/captain", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<int> AutoAssignCaptainsAsync(int competitionId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"api/competitions/admin/{competitionId}/teams/auto-captain", null, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<int>(cancellationToken: ct);
    }

    public async Task<GenerateTeamsResultDto> GenerateTeamsPreviewAsync(
        int competitionId, GenerateTeamsRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/teams/generate-preview", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<GenerateTeamsResultDto>(cancellationToken: ct))!;
    }

    public async Task ConfirmGenerateTeamsAsync(
        int competitionId, ConfirmGenerateTeamsRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/teams/generate-confirm", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<ValidateTeamResultDto> ValidateTeamAsync(
        int competitionId, int teamId, CancellationToken ct = default)
        => (await http.GetFromJsonAsync<ValidateTeamResultDto>(
            $"api/competitions/admin/{competitionId}/teams/{teamId}/validate", ct))!;

    // ── Fixtures ─────────────────────────────────────────────────────────────

    public async Task<int> SaveFixtureAsync(
        int competitionId, int? fixtureId, SaveFixtureRequest request, CancellationToken ct = default)
    {
        var resp = fixtureId.HasValue
            ? await http.PutAsJsonAsync($"api/competitions/admin/{competitionId}/fixtures/{fixtureId.Value}", request, ct)
            : await http.PostAsJsonAsync($"api/competitions/admin/{competitionId}/fixtures", request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrEmpty(body) ? resp.ReasonPhrase ?? "Failed to save fixture." : body);
        }
        return await resp.Content.ReadFromJsonAsync<int>(cancellationToken: ct);
    }

    public async Task DeleteFixtureAsync(int competitionId, int fixtureId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"api/competitions/admin/{competitionId}/fixtures/{fixtureId}", ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrEmpty(body) ? resp.ReasonPhrase ?? "Failed to delete fixture." : body);
        }
    }

    public async Task DeleteAllFixturesAsync(int competitionId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"api/competitions/admin/{competitionId}/fixtures", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<CompetitionFixtureDto?> LoadFixtureForDialogAsync(
        int competitionId, int fixtureId, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"api/competitions/admin/{competitionId}/fixtures/{fixtureId}/dialog", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CompetitionFixtureDto>(cancellationToken: ct);
    }

    public async Task<ConfirmFixtureAssignmentResultDto> ConfirmFixtureAssignmentAsync(
        int competitionId, ConfirmFixtureAssignmentRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/fixtures/confirm-assignment", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ConfirmFixtureAssignmentResultDto>(cancellationToken: ct))!;
    }

    public async Task<ScheduleFixturesResultDto> ScheduleFixturesAsync(
        int competitionId, ScheduleFixturesAdminRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/fixtures/schedule", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ScheduleFixturesResultDto>(cancellationToken: ct))!;
    }

    public async Task<SimulateRoundRobinResultDto> SimulateRoundRobinAsync(int competitionId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync(
            $"api/competitions/admin/{competitionId}/fixtures/simulate-rr", null, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<SimulateRoundRobinResultDto>(cancellationToken: ct))!;
    }

    public async Task<SeedKnockoutFromStandingsResultDto> SeedKnockoutFromStandingsAsync(
        int competitionId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync(
            $"api/competitions/admin/{competitionId}/fixtures/seed-knockout", null, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<SeedKnockoutFromStandingsResultDto>(cancellationToken: ct))!;
    }

    // ── Match windows ────────────────────────────────────────────────────────

    public async Task<int> AddMatchWindowAsync(
        int competitionId, SaveMatchWindowRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/match-windows", request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrEmpty(body) ? resp.ReasonPhrase ?? "Failed to add window." : body);
        }
        return await resp.Content.ReadFromJsonAsync<int>(cancellationToken: ct);
    }

    public async Task DeleteMatchWindowAsync(int competitionId, int matchWindowId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync(
            $"api/competitions/admin/{competitionId}/match-windows/{matchWindowId}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<int> AddDivisionMatchWindowAsync(
        int competitionId, int divisionId, SaveMatchWindowRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/divisions/{divisionId}/match-windows", request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrEmpty(body) ? resp.ReasonPhrase ?? "Failed to add division window." : body);
        }
        return await resp.Content.ReadFromJsonAsync<int>(cancellationToken: ct);
    }

    public async Task<int> ImportMatchWindowsFromTemplateAsync(
        int competitionId, ImportMatchWindowsFromTemplateRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/match-windows/import-from-template", request, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<int>(cancellationToken: ct);
    }

    // ── Calendar exceptions ──────────────────────────────────────────────────

    public async Task<int> AddCalendarExceptionAsync(
        int competitionId, SaveCalendarExceptionRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"api/competitions/admin/{competitionId}/calendar-exceptions", request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrEmpty(body) ? resp.ReasonPhrase ?? "Failed to add calendar exception." : body);
        }
        return await resp.Content.ReadFromJsonAsync<int>(cancellationToken: ct);
    }

    public async Task DeleteCalendarExceptionAsync(int competitionId, int exceptionId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync(
            $"api/competitions/admin/{competitionId}/calendar-exceptions/{exceptionId}", ct);
        resp.EnsureSuccessStatusCode();
    }

    // ── Fixture reminder emails ──────────────────────────────────────────────

    public async Task<List<CompetitionFixtureReminderDto>> GetFixtureRemindersAsync(
        int competitionId, CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<CompetitionFixtureReminderDto>>(
            $"api/competitions/admin/{competitionId}/fixture-reminders", ct) ?? [];

    public async Task<List<ScheduledReminderEmailDto>> GetScheduledReminderEmailsAsync(
        int competitionId, CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<ScheduledReminderEmailDto>>(
            $"api/competitions/admin/{competitionId}/scheduled-reminder-emails", ct) ?? [];

    public async Task<int> SaveFixtureReminderAsync(
        int competitionId, int? reminderId, SaveFixtureReminderRequest request, CancellationToken ct = default)
    {
        HttpResponseMessage resp;
        if (reminderId.HasValue)
            resp = await http.PutAsJsonAsync(
                $"api/competitions/admin/{competitionId}/fixture-reminders/{reminderId.Value}", request, ct);
        else
            resp = await http.PostAsJsonAsync(
                $"api/competitions/admin/{competitionId}/fixture-reminders", request, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<int>(ct);
    }

    public async Task DeleteFixtureReminderAsync(int competitionId, int reminderId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync(
            $"api/competitions/admin/{competitionId}/fixture-reminders/{reminderId}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task SendFixtureReminderManualAsync(
        int competitionId, int fixtureId, int reminderId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync(
            $"api/competitions/admin/{competitionId}/fixtures/{fixtureId}/send-reminder/{reminderId}", null, ct);
        resp.EnsureSuccessStatusCode();
    }
}
