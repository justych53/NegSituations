using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParticipantsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ParticipantsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Participant>>> GetAll()
    {
        return await _context.Participants.ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<Participant>> Create([FromBody] Participant participant)
    {
        _context.Participants.Add(participant);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { id = participant.Id }, participant);
    }
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Participant updated)
    {
        var participant = await _context.Participants.FindAsync(id);
        if (participant == null) return NotFound();

        participant.Name = updated.Name;
        participant.Position = updated.Position;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var participant = await _context.Participants.FindAsync(id);
        if (participant == null) return NotFound();
        _context.Participants.Remove(participant);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}