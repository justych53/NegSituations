using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Dtos;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace backend.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FailureRecordsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    private static readonly Dictionary<string, string> FactorMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { "ORG", "Организационный" },
        { "PSYCHO", "Психофизиологический" },
        { "TECH", "Технический" }
    };

    public FailureRecordsController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }
    private int GetCurrentUserId()
{
    var claim = User.FindFirst(ClaimTypes.NameIdentifier);
    if (claim == null)
        throw new UnauthorizedAccessException("Недействительный токен: отсутствует идентификатор пользователя");
    return int.Parse(claim.Value);
}

        [HttpGet]
public async Task<ActionResult<IEnumerable<object>>> GetAll()
{
    return await _context.FailureRecords
        .Include(fr => fr.Participants)
        .Include(fr => fr.CreatedBy)
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
            }),
            fr.CreatedAt,
            fr.UpdatedAt,
            CreatedBy = fr.CreatedBy != null ? fr.CreatedBy.Username : null,
            CreatedByUserId = fr.CreatedByUserId   // нужно для проверки на фронте
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
            }),
                record.CreatedAt,
            record.UpdatedAt,
            CreatedBy = record.CreatedBy != null ? record.CreatedBy.Username : null,
            CreatedByUserId = record.CreatedByUserId 
        };
    }
/// <summary>
/// Возвращает список участников конкретного отказа.
/// </summary>
/// <param name="id">Идентификатор отказа</param>
/// <returns>Список участников с Id, Name, Position</returns>
    [HttpGet("{id}/participants")]
public async Task<ActionResult<IEnumerable<object>>> GetParticipants(int id)
{
    var record = await _context.FailureRecords
        .Include(fr => fr.Participants)
        .FirstOrDefaultAsync(fr => fr.Id == id);

    if (record == null) return NotFound();

    var participants = record.Participants.Select(p => new
    {
        p.Id,
        p.Name,
        p.Position
    }).ToList();

    return Ok(participants);
}

    [HttpPost("detect-participants")]
    [Authorize]
public async Task<ActionResult<List<ParticipantDto>>> DetectParticipants([FromBody] DetectParticipantsDto dto)
{
    var httpClientFactory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
    var client = httpClientFactory.CreateClient("ExternalService");
    var endpoint = _configuration["ExternalService:ParticipantsEndpoint"];
    var requestField = _configuration["ExternalService:RequestBodyField"] ?? "text";

    // Конкатенируем описание и результат
    var combinedText = (dto.Description?.Trim() ?? "") + 
                       (string.IsNullOrWhiteSpace(dto.Result) ? "" : " " + dto.Result.Trim());

    if (string.IsNullOrWhiteSpace(combinedText))
        return BadRequest("Необходимо указать описание или результат расследования");

    var requestData = new Dictionary<string, string> { { requestField, combinedText } };
    var response = await client.PostAsJsonAsync(endpoint, requestData);
    var responseBody = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
        return StatusCode((int)response.StatusCode, $"External service error: {responseBody}");

    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var detectionResult = JsonSerializer.Deserialize<ParticipantsDetectionResponse>(responseBody, options);

    if (detectionResult == null || detectionResult.Participants.Count == 0)
        return Ok(new List<ParticipantDto>());

    var participants = detectionResult.Participants
        .Select(p => p.Label ?? p.Text)
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Distinct()
        .Select(name => new ParticipantDto { Name = name.Trim(), Position = name.Trim() })
        .ToList();

    return Ok(participants);
}

    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CreateFailureRecordDto dto)
    {
        var userId = GetCurrentUserId();
        var record = new FailureRecord
        {
            DescFailure = dto.DescFailure,
            ResInvest = dto.ResInvest,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
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
        return CreatedAtAction(nameof(GetById), new { id = record.Id }, new
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
                record.CreatedAt,
                record.UpdatedAt,
                CreatedBy = record.CreatedBy?.Username
            });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateFailureRecordDto dto)
    {
        var userId = GetCurrentUserId();
        var record = await _context.FailureRecords
            .Include(fr => fr.Participants)
            .Include(fr => fr.FailureFactors)
            .FirstOrDefaultAsync(fr => fr.Id == id);

        if (record == null) return NotFound();

        if (!User.IsInRole("Admin") && record.CreatedByUserId != userId)
            return Forbid("Вы не можете редактировать чужой отказ");

        record.UpdatedAt = DateTime.UtcNow;

        record.DescFailure = dto.DescFailure;
        record.ResInvest = dto.ResInvest;

        _context.Participants.RemoveRange(record.Participants);
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

        _context.FailureFactors.RemoveRange(record.FailureFactors);
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
        var userId = GetCurrentUserId();
        var record = await _context.FailureRecords.FindAsync(id);
        if (record == null) return NotFound();

        if (!User.IsInRole("Admin") && record.CreatedByUserId != userId)
            return Forbid();

        _context.FailureRecords.Remove(record);
        await _context.SaveChangesAsync();
        return NoContent();
    }

[HttpPost("{id}/auto-fill-matrix")]
[Authorize]
public async Task<IActionResult> AutoFillMatrix(int id)
{
    try
    {
        var userId = GetCurrentUserId();
        var record = await _context.FailureRecords
            .Include(fr => fr.FailureFactors)
            .Include(fr => fr.Participants)
            .FirstOrDefaultAsync(fr => fr.Id == id);

        if (record == null) return NotFound();
        if (!User.IsInRole("Admin") && record.CreatedByUserId != userId)
            return Forbid();

        var httpClientFactory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
        var client = httpClientFactory.CreateClient("ExternalService");
        var endpoint = _configuration["ExternalService:AnalysisEndpoint"];

        var content = new StringContent(record.DescFailure, System.Text.Encoding.UTF8, "text/plain");
        var response = await client.PostAsync(endpoint, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, $"External service error: {responseBody}");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var analysis = JsonSerializer.Deserialize<ExternalAnalysisResponse>(responseBody, options);

        if (analysis?.Ahp == null)
            return BadRequest("No AHP data in response");

        var ahp = analysis.Ahp;
        var allFactors = await _context.Factors.ToListAsync();

        // 1. Матрица факторов
        var factorLabels = ahp.FactorMatrix.Labels;
        var factorMatrix = ahp.FactorMatrix.Matrix;
        if (factorLabels.Count > 1 && factorMatrix.Count == factorLabels.Count &&
            factorMatrix.All(row => row.Count == factorLabels.Count))
        {
            var oldFactors = await _context.ComparisonMatrices
                .Where(cm => cm.FailureRecordId == id).ToListAsync();
            _context.ComparisonMatrices.RemoveRange(oldFactors);

            for (int i = 0; i < factorLabels.Count; i++)
            {
                for (int j = i + 1; j < factorLabels.Count; j++)
                {
                    var factorA = FindFactor(allFactors, factorLabels[i]);
                    var factorB = FindFactor(allFactors, factorLabels[j]);
                    if (factorA != null && factorB != null)
                    {
                        _context.ComparisonMatrices.Add(new ComparisonMatrix
                        {
                            FailureRecordId = id,
                            FactorAId = factorA.Id,
                            FactorBId = factorB.Id,
                            Score = factorMatrix[i][j]
                        });
                        Console.WriteLine($"{factorLabels[i]} -> {factorLabels[j]}: {factorMatrix[i][j]}");
                    }
                }
            }
        }

        // 2. Матрицы участников по факторам
        var oldParts = await _context.ParticipantMatrices
            .Where(pm => pm.FailureRecordId == id).ToListAsync();
        _context.ParticipantMatrices.RemoveRange(oldParts);
        await _context.SaveChangesAsync();

        foreach (var (factorCode, matrixData) in ahp.ParticipantMatricesByFactor)
        {
            var factor = FindFactor(allFactors, factorCode);
            if (factor == null) continue;

            var labels = matrixData.Labels;
            var matrix = matrixData.Matrix;
            if (labels.Count == 0 || matrix.Count == 0) continue;

            if (labels.Count > 1 && matrix.Count == labels.Count &&
                matrix.All(row => row.Count == labels.Count))
            {
                for (int i = 0; i < labels.Count; i++)
                {
                    for (int j = i + 1; j < labels.Count; j++)
                    {
                        var pA = record.Participants.FirstOrDefault(p =>
                            p.Name.Equals(labels[i], StringComparison.OrdinalIgnoreCase));
                        var pB = record.Participants.FirstOrDefault(p =>
                            p.Name.Equals(labels[j], StringComparison.OrdinalIgnoreCase));

                        if (pA != null && pB != null)
                        {
                            _context.ParticipantMatrices.Add(new ParticipantMatrix
                            {
                                FailureRecordId = id,
                                FactorId = factor.Id,
                                ParticipantAId = pA.Id,
                                ParticipantBId = pB.Id,
                                Score = matrix[i][j]
                            });
                            Console.WriteLine($"Participant: {pA.Name} -> {pB.Name}: {matrix[i][j]}");
                        }
                    }
                }
            }
        }

        await _context.SaveChangesAsync();
        return Ok();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"AutoFillMatrix error: {ex}");
        return StatusCode(500, $"Internal error: {ex.Message}");
    }
}

// Метод FindFactor 
private static Factor? FindFactor(List<Factor> factors, string labelOrCode)
{
    var factor = factors.FirstOrDefault(f =>
        f.Name.Equals(labelOrCode, StringComparison.OrdinalIgnoreCase));
    if (factor != null) return factor;

    if (FactorMapping.TryGetValue(labelOrCode, out var rus))
        return factors.FirstOrDefault(f =>
            f.Name.Equals(rus, StringComparison.OrdinalIgnoreCase));

    return null;
}
}

public class CreateFailureRecordDto
{
    public string DescFailure { get; set; } = string.Empty;
    public string ResInvest { get; set; } = string.Empty;
    public List<ParticipantDto>? Participants { get; set; }
    public List<int>? FactorIds { get; set; }
}

public class ParticipantDto
{
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
}