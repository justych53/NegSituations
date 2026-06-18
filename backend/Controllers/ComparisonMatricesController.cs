using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ComparisonMatricesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly LogService _logService;

    public ComparisonMatricesController(AppDbContext context, LogService logService)
    {
        _context = context;
        _logService = logService;
    }

    private int GetCurrentUserId()
{
    var claim = User.FindFirst(ClaimTypes.NameIdentifier);
    if (claim == null)
        throw new UnauthorizedAccessException("Недействительный токен: отсутствует идентификатор пользователя");
    return int.Parse(claim.Value);
}

    // Получить матрицу для конкретного отказа
    [HttpGet("by-failure/{failureId}")]
    public async Task<ActionResult<IEnumerable<object>>> GetByFailure(int failureId)
    {
        var matrix = await _context.ComparisonMatrices
            .Where(cm => cm.FailureRecordId == failureId)
            .Select(cm => new
            {
                cm.Id,
                cm.FailureRecordId,
                cm.FactorAId,
                cm.FactorBId,
                cm.Score,
                FactorAName = cm.FactorA.Name,
                FactorBName = cm.FactorB.Name
            })
            .ToListAsync();

        return matrix;
    }

    // Сохранить или обновить матрицу
    [HttpPost]
    [Authorize]
    public async Task<ActionResult> Save([FromBody] SaveMatrixDto dto)
{
    var userId = GetCurrentUserId();
    var record = await _context.FailureRecords.FindAsync(dto.FailureRecordId);
    var username = User.FindFirst(ClaimTypes.Name)?.Value;
    if (record == null) return NotFound();
    if (!User.IsInRole("Admin") && record.CreatedByUserId != userId)
        return Forbid();
        // Удаляем старую матрицу для этого отказа
        var oldEntries = await _context.ComparisonMatrices
            .Where(cm => cm.FailureRecordId == dto.FailureRecordId)
            .ToListAsync();
        _context.ComparisonMatrices.RemoveRange(oldEntries);

        // Добавляем новые оценки
        foreach (var entry in dto.Entries)
        {
            _context.ComparisonMatrices.Add(new ComparisonMatrix
            {
                FailureRecordId = dto.FailureRecordId,
                FactorAId = entry.factorAId,
                FactorBId = entry.factorBId,
                Score = entry.score
            });
        }

        await _context.SaveChangesAsync();
        await _logService.LogAsync("Info", username, "Сохранение матрицы факторов", $"FailureId={dto.FailureRecordId}");
        return Ok();
    }

    [HttpDelete("by-failure/{failureId}")]
    [Authorize]
    public async Task<IActionResult> DeleteByFailure(int failureId)
    {
        var userId = GetCurrentUserId();
        var record = await _context.FailureRecords.FindAsync(failureId);
        if (record == null) return NotFound();
        if (!User.IsInRole("Admin") && record.CreatedByUserId != userId)
            return Forbid();

        var entries = await _context.ComparisonMatrices
            .Where(cm => cm.FailureRecordId == failureId)
            .ToListAsync();
        _context.ComparisonMatrices.RemoveRange(entries);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public class SaveMatrixDto
{
    public int FailureRecordId { get; set; }
    public List<MatrixEntryDto> Entries { get; set; } = new();
}

public class MatrixEntryDto
{
    public int factorAId { get; set; }
    public int factorBId { get; set; }
    public double score { get; set; } 
}