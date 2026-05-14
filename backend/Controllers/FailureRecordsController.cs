using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Dtos;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FailureRecordsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public FailureRecordsController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
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
[HttpPost("detect-participants")]
public async Task<ActionResult<List<ParticipantDto>>> DetectParticipants([FromBody] DetectParticipantsDto dto)
{
    var httpClientFactory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
    var client = httpClientFactory.CreateClient("ExternalService");
    var endpoint = _configuration["ExternalService:Endpoint"];
    var requestField = _configuration["ExternalService:RequestBodyField"] ?? "text";

    var requestData = new Dictionary<string, string> { { requestField, dto.Description } };
    var requestJson = System.Text.Json.JsonSerializer.Serialize(requestData);
    Console.WriteLine($"→ External request: POST {endpoint} | Body: {requestJson}");

    var response = await client.PostAsJsonAsync(endpoint, requestData);
    var responseBody = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"← External response: {(int)response.StatusCode} | Body: {responseBody}");

    if (!response.IsSuccessStatusCode)
        return StatusCode((int)response.StatusCode, $"External service error: {responseBody}");

    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var externalParticipants = System.Text.Json.JsonSerializer.Deserialize<List<ExternalParticipantDto>>(responseBody, options);

    if (externalParticipants == null)
        return Ok(new List<ParticipantDto>());

    // Парсим каждого участника, извлекаем имя и должность, затем дедублицируем по имени
    var participants = externalParticipants
        .Select(p =>
        {
            var full = p.Participant?.Trim() ?? "";
            string name = full;
            string position = "";

            if (!string.IsNullOrEmpty(full))
            {
                // Разделяем по длинному тире или обычному тире
                var parts = full.Split(new[] { '—', '-' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    name = parts[0].Trim();
                    position = parts[1].Trim();
                }
            }

            return new ParticipantDto { Name = name, Position = position };
        })
        .GroupBy(p => p.Name)          // группируем по имени
        .Select(g => g.First())       // берём первое вхождение для каждого имени
        .ToList();

    return Ok(participants);
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