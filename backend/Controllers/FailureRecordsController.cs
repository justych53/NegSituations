using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FailureRecordsController : ControllerBase
{
    private readonly AppDbContext _context;

    public FailureRecordsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{id}")]
public async Task<ActionResult<object>> GetById(int id)
{
    var record = await _context.FailureRecords
        .Include(fr => fr.FailureParticipants)
            .ThenInclude(fp => fp.Participant)
        .FirstOrDefaultAsync(fr => fr.Id == id);

    if (record == null) return NotFound();

    return new
    {
        record.Id,
        record.DescFailure,
        record.ResInvest,
        FailureParticipants = record.FailureParticipants.Select(fp => new
        {
            fp.FailureRecordId,
            fp.ParticipantId,
            Participant = fp.Participant != null ? new
            {
                fp.Participant.Id,
                fp.Participant.Name,
                fp.Participant.Position
            } : null
        })
    };
}
[HttpGet]
public async Task<ActionResult<IEnumerable<object>>> GetAll()
{
    return await _context.FailureRecords
        .Include(fr => fr.FailureParticipants)
            .ThenInclude(fp => fp.Participant)
        .Select(fr => new
        {
            fr.Id,
            fr.DescFailure,
            fr.ResInvest,
            FailureParticipants = fr.FailureParticipants.Select(fp => new
            {
                fp.FailureRecordId,
                fp.ParticipantId,
                Participant = fp.Participant != null ? new
                {
                    fp.Participant.Id,
                    fp.Participant.Name,
                    fp.Participant.Position
                } : null
            })
        })
        .ToListAsync();
}


    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CreateFailureRecordDto dto)
    {
        var record = new FailureRecord
        {
            DescFailure = dto.DescFailure,
            ResInvest = dto.ResInvest
        };

        if (dto.ParticipantIds != null)
        {
            foreach (var pid in dto.ParticipantIds)
            {
                record.FailureParticipants.Add(new FailureParticipant { ParticipantId = pid });
            }
        }

        _context.FailureRecords.Add(record);
        await _context.SaveChangesAsync();

        var rnd = new Random();
        int org = rnd.Next(0, 101);
        int tech = rnd.Next(0, 101 - org);
        int psycho = 100 - org - tech;

        return CreatedAtAction(nameof(GetAll), new { id = record.Id }, new
        {
            record.Id,
            record.DescFailure,
            record.ResInvest,
            OrganizationalPercent = org,
            TechnicalPercent = tech,
            PsychophysiologicalPercent = psycho
        });
    }
    [HttpPut("{id}")]
public async Task<IActionResult> Update(int id, [FromBody] CreateFailureRecordDto dto)
{
    var record = await _context.FailureRecords
        .Include(fr => fr.FailureParticipants)
        .FirstOrDefaultAsync(fr => fr.Id == id);

    if (record == null) return NotFound();

    record.DescFailure = dto.DescFailure;
    record.ResInvest = dto.ResInvest;

    // Удаляем старые связи
    record.FailureParticipants.Clear();

    // Добавляем новые
    if (dto.ParticipantIds != null)
    {
        foreach (var pid in dto.ParticipantIds)
        {
            record.FailureParticipants.Add(new FailureParticipant
            {
                FailureRecordId = id,
                ParticipantId = pid
            });
        }
    }

    await _context.SaveChangesAsync();
    return NoContent();
}

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var record = await _context.FailureRecords.FindAsync(id);
        if (record == null) return NotFound();
        _context.FailureRecords.Remove(record);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public class CreateFailureRecordDto
{
    public string DescFailure { get; set; } = string.Empty;
    public string ResInvest { get; set; } = string.Empty;
    public List<int>? ParticipantIds { get; set; }
}