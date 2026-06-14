using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StatisticsController : ControllerBase
{
    private readonly AppDbContext _context;

    public StatisticsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult> GetDashboard()
    {
        var failures = await _context.FailureRecords
            .Include(fr => fr.Participants)
            .Include(fr => fr.FailureFactors).ThenInclude(ff => ff.Factor)
            .Include(fr => fr.ComparisonMatrices)
            .Include(fr => fr.ParticipantMatrices)
            .ToListAsync();

        if (failures.Count == 0)
            return Ok(new {
                totalFailures = 0,
                avgParticipants = 0.0,
                topParticipants = Array.Empty<object>(),
                factorAverages = Array.Empty<object>()
            });

        var participantWeightsAcc = new Dictionary<string, List<double>>();
        var factorWeightsAcc = new Dictionary<string, List<double>>();

        foreach (var record in failures)
        {
            var factors = record.FailureFactors.Select(ff => ff.Factor).ToList();
            var participants = record.Participants.ToList();
            if (factors.Count < 2 || participants.Count < 2) continue;

            var factorComparisons = record.ComparisonMatrices.ToList();
            if (!factorComparisons.Any()) continue;

            // 1. Веса факторов
            var factorWeights = AhpHelper.CalculateFactorWeights(factors, factorComparisons);
            foreach (var fw in factorWeights)
            {
                if (!factorWeightsAcc.ContainsKey(fw.Factor.Name))
                    factorWeightsAcc[fw.Factor.Name] = new List<double>();
                factorWeightsAcc[fw.Factor.Name].Add(fw.Weight);
            }

            // 2. Локальные веса участников по факторам
            var participantLocalWeights = new Dictionary<int, Dictionary<int, double>>();
            foreach (var factor in factors)
            {
                var matrixEntries = record.ParticipantMatrices
                    .Where(pm => pm.FactorId == factor.Id)
                    .ToList();
                if (matrixEntries.Any())
                {
                    var localWeights = AhpHelper.CalculateLocalWeights(participants, matrixEntries);
                    foreach (var pw in localWeights)
                    {
                        if (!participantLocalWeights.ContainsKey(factor.Id))
                            participantLocalWeights[factor.Id] = new Dictionary<int, double>();
                        participantLocalWeights[factor.Id][pw.Participant.Id] = pw.Weight;
                    }
                }
            }

            // 3. Синтез
            foreach (var participant in participants)
            {
                double totalWeight = 0;
                foreach (var factor in factors)
                {
                    var factorWeight = factorWeights.First(fw => fw.Factor.Id == factor.Id).Weight;
                    double localWeight = 0;
                    if (participantLocalWeights.ContainsKey(factor.Id) &&
                        participantLocalWeights[factor.Id].ContainsKey(participant.Id))
                        localWeight = participantLocalWeights[factor.Id][participant.Id];
                    totalWeight += factorWeight * localWeight;
                }

                if (!participantWeightsAcc.ContainsKey(participant.Name))
                    participantWeightsAcc[participant.Name] = new List<double>();
                participantWeightsAcc[participant.Name].Add(totalWeight);
            }
        }

        // Агрегация
        var topParticipants = participantWeightsAcc
            .Select(p => new {
                name = p.Key,
                avgWeight = p.Value.Average(),
                maxWeight = p.Value.Max(),
                count = p.Value.Count
            })
            .OrderByDescending(x => x.avgWeight)
            .Take(5)
            .Select(x => new {
                name = x.name,
                avgWeight = x.avgWeight,
                topCount = x.count
            })
            .ToList();

        var factorAverages = factorWeightsAcc
            .Select(f => new { name = f.Key, avgWeight = f.Value.Average() })
            .OrderByDescending(x => x.avgWeight)
            .ToList();

        return Ok(new {
            totalFailures = failures.Count,
            avgParticipants = failures.Average(fr => fr.Participants.Count),
            topParticipants,
            factorAverages
        });
    }
}

// Вспомогательный класс для МАИ
public static class AhpHelper
{
    public static List<(Factor Factor, double Weight)> CalculateFactorWeights(
        List<Factor> factors, List<ComparisonMatrix> comparisons)
    {
        int n = factors.Count;
        double[,] matrix = new double[n, n];
        for (int i = 0; i < n; i++) matrix[i, i] = 1;

        foreach (var c in comparisons)
        {
            int i = factors.FindIndex(f => f.Id == c.FactorAId);
            int j = factors.FindIndex(f => f.Id == c.FactorBId);
            if (i >= 0 && j >= 0 && i != j)
            {
                matrix[i, j] = c.Score;
                matrix[j, i] = 1.0 / c.Score;
            }
        }

        double[] colSums = new double[n];
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
                colSums[j] += matrix[i, j];

        double[] weights = new double[n];
        for (int i = 0; i < n; i++)
        {
            double sum = 0;
            for (int j = 0; j < n; j++)
                sum += matrix[i, j] / colSums[j];
            weights[i] = sum / n;
        }

        var result = new List<(Factor, double)>();
        for (int i = 0; i < n; i++)
            result.Add((factors[i], weights[i]));
        return result;
    }

    public static List<(Participant Participant, double Weight)> CalculateLocalWeights(
        List<Participant> participants, List<ParticipantMatrix> matrixEntries)
    {
        int n = participants.Count;
        double[,] matrix = new double[n, n];
        for (int i = 0; i < n; i++) matrix[i, i] = 1;

        foreach (var pm in matrixEntries)
        {
            int i = participants.FindIndex(p => p.Id == pm.ParticipantAId);
            int j = participants.FindIndex(p => p.Id == pm.ParticipantBId);
            if (i >= 0 && j >= 0 && i != j)
            {
                matrix[i, j] = pm.Score;
                matrix[j, i] = 1.0 / pm.Score;
            }
        }

        double[] colSums = new double[n];
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
                colSums[j] += matrix[i, j];

        double[] weights = new double[n];
        for (int i = 0; i < n; i++)
        {
            double sum = 0;
            for (int j = 0; j < n; j++)
                sum += matrix[i, j] / colSums[j];
            weights[i] = sum / n;
        }

        var result = new List<(Participant, double)>();
        for (int i = 0; i < n; i++)
            result.Add((participants[i], weights[i]));
        return result;
    }
}