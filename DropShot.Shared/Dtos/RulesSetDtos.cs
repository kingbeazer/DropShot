namespace DropShot.Shared.Dtos;

public record RulesSetDto(
    int RulesSetId,
    string Name,
    string? Description,
    int ItemCount,
    int ClubId);

public record RulesSetDetailDto(
    int RulesSetId,
    string Name,
    string? Description,
    int ClubId,
    List<RulesSetItemDto> Items);

public record RulesSetItemDto(
    int RulesSetItemId,
    int RulesSetId,
    int SortOrder,
    string RuleText);

public record SaveRulesSetRequest(string Name, string? Description, int? ClubId = null);

public record AddRulesSetItemRequest(string RuleText);
