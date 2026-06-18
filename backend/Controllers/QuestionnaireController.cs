using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuestionnaireController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly LogService _logService;

    public QuestionnaireController(AppDbContext context, LogService logService)
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

    // Получить ответы для конкретного отказа
    [HttpGet("by-failure/{failureId}")]
    public async Task<ActionResult<IEnumerable<object>>> GetAnswers(int failureId)
    {
        var answers = await _context.QuestionnaireAnswers
            .Where(qa => qa.Participant.FailureRecordId == failureId)
            .Select(qa => new
            {
                qa.Id,
                qa.ParticipantId,
                qa.Answer
            })
            .ToListAsync();

        return Ok(answers);
    }

    // Сохранить массив ответов для отказа
    [HttpPost("save")]
    [Authorize]
    public async Task<IActionResult> SaveAnswers([FromBody] SaveAnswersDto dto)
    {
        var userId = GetCurrentUserId();
        var record = await _context.FailureRecords.FindAsync(dto.FailureRecordId);
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        if (record == null) return NotFound();
        if (!User.IsInRole("Admin") && record.CreatedByUserId != userId)
        return Forbid();
        // Получаем участников, привязанных к этому отказу
        var participantIds = await _context.Participants
            .Where(p => p.FailureRecordId == dto.FailureRecordId)
            .Select(p => p.Id)
            .ToListAsync();

        foreach (var item in dto.Answers)
        {
            if (!participantIds.Contains(item.ParticipantId))
                return BadRequest($"Участник с id={item.ParticipantId} не принадлежит отказу {dto.FailureRecordId}");

            var existing = await _context.QuestionnaireAnswers
                .FirstOrDefaultAsync(qa => qa.ParticipantId == item.ParticipantId);

            if (existing != null)
            {
                existing.Answer = item.Answer;
            }
            else
            {
                _context.QuestionnaireAnswers.Add(new QuestionnaireAnswer
                {
                    ParticipantId = item.ParticipantId,
                    Answer = item.Answer
                });
            }
        }

        await _context.SaveChangesAsync();
        await _logService.LogAsync("Info", username, "Сохранение анкеты", $"FailureId={dto.FailureRecordId}");
        return Ok();
    }
}

public class SaveAnswersDto
{
    public int FailureRecordId { get; set; }
    public List<AnswerItem> Answers { get; set; } = new();
}

public class AnswerItem
{
    public int ParticipantId { get; set; }
    public string Answer { get; set; } = string.Empty;
}