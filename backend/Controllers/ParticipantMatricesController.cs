using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

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
        private int GetCurrentUserId()
{
    var claim = User.FindFirst(ClaimTypes.NameIdentifier);
    if (claim == null)
        throw new UnauthorizedAccessException("Недействительный токен: отсутствует идентификатор пользователя");
    return int.Parse(claim.Value);
}

    // Получить матрицу для отказа по фактору
    [HttpGet("by-failure/{failureId}/factor/{factorId}")]
    public async Task<ActionResult<IEnumerable<object>>> GetByFailureAndFactor(int failureId, int factorId)
    {
        var matrix = await _context.ParticipantMatrices
            .Where(pm => pm.FailureRecordId == failureId && pm.FactorId == factorId)
            .Select(pm => new
            {
                pm.Id,
                pm.FailureRecordId,
                pm.FactorId,
                pm.ParticipantAId,
                pm.ParticipantBId,
                pm.Score
            })
            .ToListAsync();

        return Ok(matrix);
    }

    // Сохранить матрицу для отказа по фактору
    [HttpPost("by-factor")]
    [Authorize]
    public async Task<ActionResult> SaveByFactor([FromBody] SaveParticipantMatrixDto dto)
    {
        var userId = GetCurrentUserId();
        var record = await _context.FailureRecords.FindAsync(dto.FailureRecordId);
        if (record == null) return NotFound();
        if (!User.IsInRole("Admin") && record.CreatedByUserId != userId)
        return Forbid();
        // Удаляем старые записи для этого отказа и фактора
        var oldEntries = await _context.ParticipantMatrices
            .Where(pm => pm.FailureRecordId == dto.FailureRecordId && pm.FactorId == dto.FactorId)
            .ToListAsync();
        _context.ParticipantMatrices.RemoveRange(oldEntries);

        // Добавляем новые
        foreach (var entry in dto.Entries)
        {
            _context.ParticipantMatrices.Add(new ParticipantMatrix
            {
                FailureRecordId = dto.FailureRecordId,
                FactorId = dto.FactorId,
                ParticipantAId = entry.participantAId,
                ParticipantBId = entry.participantBId,
                Score = entry.score
            });
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    // Удалить все матрицы для отказа
    [HttpDelete("by-failure/{failureId}")]
    [Authorize]
    public async Task<IActionResult> DeleteByFailure(int failureId)
    {
        var userId = GetCurrentUserId();
        var record = await _context.FailureRecords.FindAsync(failureId);
        if (record == null) return NotFound();
        if (!User.IsInRole("Admin") && record.CreatedByUserId != userId)
            return Forbid();

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
    public int FactorId { get; set; }
    public List<ParticipantMatrixEntryDto> Entries { get; set; } = new();
}

public class ParticipantMatrixEntryDto
{
    public int participantAId { get; set; }
    public int participantBId { get; set; }
    public double score { get; set; }
}