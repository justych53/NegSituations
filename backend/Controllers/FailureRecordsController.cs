using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Dtos;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace backend.Controllers;

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
        var response = await client.PostAsJsonAsync(endpoint, requestData);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, $"External service error: {responseBody}");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var analysis = JsonSerializer.Deserialize<ExternalAnalysisResponse>(responseBody, options);

        if (analysis == null)
            return Ok(new List<ParticipantDto>());

        var participants = analysis.Responsibility
            .Select(r => r.Participant.Trim())
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct()
            .Select(name => new ParticipantDto { Name = name, Position = name })
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
        var record = await _context.FailureRecords.FindAsync(id);
        if (record == null) return NotFound();

        _context.FailureRecords.Remove(record);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/auto-fill-matrix")]
    public async Task<IActionResult> AutoFillMatrix(int id)
    {
        try
        {
            var record = await _context.FailureRecords
                .Include(fr => fr.FailureFactors)
                .Include(fr => fr.Participants)
                .FirstOrDefaultAsync(fr => fr.Id == id);

            if (record == null) return NotFound();

            var httpClientFactory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
            var client = httpClientFactory.CreateClient("ExternalService");
            var endpoint = _configuration["ExternalService:Endpoint"];
            var requestField = _configuration["ExternalService:RequestBodyField"] ?? "text";

            var requestData = new Dictionary<string, string> { { requestField, record.DescFailure } };
            var response = await client.PostAsJsonAsync(endpoint, requestData);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, $"External service error: {responseBody}");

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var analysis = JsonSerializer.Deserialize<ExternalAnalysisResponse>(responseBody, options);

            if (analysis?.Ahp == null)
                return BadRequest("No AHP data in response");

            var ahp = analysis.Ahp;
            var allFactors = await _context.Factors.ToListAsync();

            // 1. Factor matrix
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
                        }
                    }
                }
            }

            // 2. Participant matrices per factor
            var oldParts = await _context.ParticipantMatrices
                .Where(pm => pm.FailureRecordId == id).ToListAsync();
            _context.ParticipantMatrices.RemoveRange(oldParts);

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

public class DetectParticipantsDto
{
    public string Description { get; set; } = string.Empty;
}