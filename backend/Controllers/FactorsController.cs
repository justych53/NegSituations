using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FactorsController : ControllerBase
{
    private readonly AppDbContext _context;

    public FactorsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Factor>>> GetAll()
    {
        return await _context.Factors.ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<Factor>> Create([FromBody] Factor factor)
    {
        _context.Factors.Add(factor);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { id = factor.Id }, factor);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Factor updated)
    {
        var factor = await _context.Factors.FindAsync(id);
        if (factor == null) return NotFound();
        factor.Name = updated.Name;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var factor = await _context.Factors.FindAsync(id);
        if (factor == null) return NotFound();
        _context.Factors.Remove(factor);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}