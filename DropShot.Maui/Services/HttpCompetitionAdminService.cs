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
}
