using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParticipantMatricesController : ControllerBase
{
    private readonly AppDbContext _context;

    public ParticipantMatricesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("by-failure/{failureId}")]
    public async Task<ActionResult<IEnumerable<object>>> GetByFailure(int failureId)
    {
        var matrix = await _context.ParticipantMatrices
            .Where(pm => pm.FailureRecordId == failureId)
            .Select(pm => new
            {
                pm.Id,
                pm.FailureRecordId,
                pm.ParticipantAId,
                pm.ParticipantBId,
                pm.Score,
                ParticipantAName = pm.ParticipantA.Name,
                ParticipantBName = pm.ParticipantB.Name
            })
            .ToListAsync();

        return matrix;
    }

    [HttpPost]
    public async Task<ActionResult> Save([FromBody] SaveParticipantMatrixDto dto)
    {
        var oldEntries = await _context.ParticipantMatrices
            .Where(pm => pm.FailureRecordId == dto.FailureRecordId)
            .ToListAsync();
        _context.ParticipantMatrices.RemoveRange(oldEntries);

        foreach (var entry in dto.Entries)
        {
            _context.ParticipantMatrices.Add(new ParticipantMatrix
            {
                FailureRecordId = dto.FailureRecordId,
                ParticipantAId = entry.participantAId,
                ParticipantBId = entry.participantBId,
                Score = entry.score
            });
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("by-failure/{failureId}")]
    public async Task<IActionResult> DeleteByFailure(int failureId)
    {
        var entries = await _context.ParticipantMatrices
            .Where(pm => pm.FailureRecordId == failureId)
            .ToListAsync();
        _context.ParticipantMatrices.RemoveRange(entries);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public class SaveParticipantMatrixDto
{
    public int FailureRecordId { get; set; }
    public List<ParticipantMatrixEntryDto> Entries { get; set; } = new();
}

public class ParticipantMatrixEntryDto
{
    public int participantAId { get; set; }
    public int participantBId { get; set; }
    public double score { get; set; }
}