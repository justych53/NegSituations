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

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetAll()
    {
        return await _context.FailureRecords
            .Include(fr => fr.Participants)
            .Select(fr => new
            {
                fr.Id,
                fr.DescFailure,
                fr.ResInvest,
                Participants = fr.Participants.Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Position
                })
            })
            .ToListAsync();
    }

    [HttpGet("{id}")]
public async Task<ActionResult<object>> GetById(int id)
{
    var record = await _context.FailureRecords
        .Include(fr => fr.Participants)
        .Include(fr => fr.FailureFactors)
            .ThenInclude(ff => ff.Factor)
        .FirstOrDefaultAsync(fr => fr.Id == id);

    if (record == null) return NotFound();

    return new
    {
        record.Id,
        record.DescFailure,
        record.ResInvest,
        Participants = record.Participants.Select(p => new
        {
            p.Id,
            p.Name,
            p.Position
        }),
        Factors = record.FailureFactors.Select(ff => new
        {
            ff.Factor.Id,
            ff.Factor.Name
        })
    };
}

    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CreateFailureRecordDto dto)
    {
        var record = new FailureRecord
        {
            DescFailure = dto.DescFailure,
            ResInvest = dto.ResInvest
        };

        if (dto.Participants != null)
        {
            foreach (var p in dto.Participants)
            {
                record.Participants.Add(new Participant
                {
                    Name = p.Name,
                    Position = p.Position
                });
            }
        }
        if (dto.FactorIds != null)
        {
            foreach (var fid in dto.FactorIds)
            {
                record.FailureFactors.Add(new FailureFactor { FactorId = fid });
            }
        }

        _context.FailureRecords.Add(record);
        await _context.SaveChangesAsync();

        var rnd = new Random();
        int org = rnd.Next(0, 101);
        int tech = rnd.Next(0, 101 - org);
        int psycho = 100 - org - tech;

        return CreatedAtAction(nameof(GetById), new { id = record.Id }, new
        {
            record.Id,
            record.DescFailure,
            record.ResInvest,
            OrganizationalPercent = org,
            TechnicalPercent = tech,
            PsychophysiologicalPercent = psycho,
            Participants = record.Participants.Select(p => new
            {
                p.Id,
                p.Name,
                p.Position
            })
        });
    }

    [HttpPut("{id}")]
public async Task<IActionResult> Update(int id, [FromBody] CreateFailureRecordDto dto)
{
    var record = await _context.FailureRecords
        .Include(fr => fr.Participants)
        .Include(fr => fr.FailureFactors)
        .FirstOrDefaultAsync(fr => fr.Id == id);

    if (record == null) return NotFound();

    record.DescFailure = dto.DescFailure;
    record.ResInvest = dto.ResInvest;

    // Удаляем старых участников
    _context.Participants.RemoveRange(record.Participants);

    // Добавляем новых участников
    if (dto.Participants != null)
    {
        foreach (var p in dto.Participants)
        {
            record.Participants.Add(new Participant
            {
                Name = p.Name,
                Position = p.Position,
                FailureRecordId = id
            });
        }
    }

    // Очищаем старые связи с факторами
    _context.FailureFactors.RemoveRange(record.FailureFactors);

    // Добавляем новые связи с факторами
    if (dto.FactorIds != null)
    {
        foreach (var fid in dto.FactorIds)
        {
            _context.FailureFactors.Add(new FailureFactor
            {
                FailureRecordId = id,
                FactorId = fid
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
    public List<ParticipantDto>? Participants { get; set; }
    public List<int>? FactorIds { get; set; }  // ← новое поле
}

public class ParticipantDto
{
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
}