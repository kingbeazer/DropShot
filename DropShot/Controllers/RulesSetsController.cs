using DropShot.Data;
using DropShot.Models;
using DropShot.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class RulesSetsController(IDbContextFactory<MyDbContext> dbFactory) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RulesSetDto>>> GetAll()
    {
        await using var db = dbFactory.CreateDbContext();
        var sets = await db.RulesSets.Include(r => r.Items).OrderBy(r => r.Name).ToListAsync();
        return sets.Select(r => new RulesSetDto(r.RulesSetId, r.Name, r.Description, r.Items.Count)).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RulesSetDetailDto>> Get(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var r = await db.RulesSets.Include(x => x.Items.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(x => x.RulesSetId == id);
        if (r is null) return NotFound();

        return new RulesSetDetailDto(r.RulesSetId, r.Name, r.Description,
            r.Items.Select(i => new RulesSetItemDto(i.RulesSetItemId, i.RulesSetId, i.SortOrder, i.RuleText)).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RulesSetDto>> Create([FromBody] SaveRulesSetRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var r = new RulesSet { Name = req.Name.Trim(), Description = req.Description };
        db.RulesSets.Add(r);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = r.RulesSetId },
            new RulesSetDto(r.RulesSetId, r.Name, r.Description, 0));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RulesSetDto>> Update(int id, [FromBody] SaveRulesSetRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var r = await db.RulesSets.Include(x => x.Items).FirstOrDefaultAsync(x => x.RulesSetId == id);
        if (r is null) return NotFound();
        r.Name = req.Name.Trim();
        r.Description = req.Description;
        await db.SaveChangesAsync();
        return new RulesSetDto(r.RulesSetId, r.Name, r.Description, r.Items.Count);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var db = dbFactory.CreateDbContext();
        var r = await db.RulesSets.FindAsync(id);
        if (r is null) return NotFound();
        db.RulesSets.Remove(r);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/items")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RulesSetItemDto>> AddItem(int id, [FromBody] AddRulesSetItemRequest req)
    {
        await using var db = dbFactory.CreateDbContext();
        var maxOrder = await db.RulesSetItems.Where(i => i.RulesSetId == id)
            .Select(i => (int?)i.SortOrder).MaxAsync() ?? 0;
        var item = new RulesSetItem { RulesSetId = id, SortOrder = maxOrder + 1, RuleText = req.RuleText.Trim() };
        db.RulesSetItems.Add(item);
        await db.SaveChangesAsync();
        return Ok(new RulesSetItemDto(item.RulesSetItemId, item.RulesSetId, item.SortOrder, item.RuleText));
    }

    [HttpDelete("{id:int}/items/{itemId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteItem(int id, int itemId)
    {
        await using var db = dbFactory.CreateDbContext();
        var item = await db.RulesSetItems.FindAsync(itemId);
        if (item is null || item.RulesSetId != id) return NotFound();
        db.RulesSetItems.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
