using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuestionnaireController : ControllerBase
{
    private readonly AppDbContext _context;

    public QuestionnaireController(AppDbContext context)
    {
        _context = context;
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
    public async Task<IActionResult> SaveAnswers([FromBody] SaveAnswersDto dto)
    {
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