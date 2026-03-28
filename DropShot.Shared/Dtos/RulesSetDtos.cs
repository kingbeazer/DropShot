namespace DropShot.Shared.Dtos;

public record RulesSetDto(
    int RulesSetId,
    string Name,
    string? Description,
    int ItemCount);

public record RulesSetDetailDto(
    int RulesSetId,
    string Name,
    string? Description,
    List<RulesSetItemDto> Items);

public record RulesSetItemDto(
    int RulesSetItemId,
    int RulesSetId,
    int SortOrder,
    string RuleText);

public record SaveRulesSetRequest(string Name, string? Description);

public record AddRulesSetItemRequest(string RuleText);
