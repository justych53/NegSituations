using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ComparisonMatricesController : ControllerBase
{
    private readonly AppDbContext _context;

    public ComparisonMatricesController(AppDbContext context)
    {
        _context = context;
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
    public async Task<ActionResult> Save([FromBody] SaveMatrixDto dto)
    {
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
        return Ok();
    }

    [HttpDelete("by-failure/{failureId}")]
    public async Task<IActionResult> DeleteByFailure(int failureId)
    {
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