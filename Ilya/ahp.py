from __future__ import annotations

from math import isclose


FACTOR_ORDER = ["ORG", "PSYCHO", "TECH"]
SAATY_VALUES = [1, 2, 3, 4, 5, 6, 7, 8, 9]
SAATY_SCALE_VALUES = [
    round(1 / 9, 4),
    round(1 / 8, 4),
    round(1 / 7, 4),
    round(1 / 6, 4),
    round(1 / 5, 4),
    round(1 / 4, 4),
    round(1 / 3, 4),
    round(1 / 2, 4),
    1.0,
    2.0,
    3.0,
    4.0,
    5.0,
    6.0,
    7.0,
    8.0,
    9.0,
]

# Default expert matrix for level 1.
# The neutral matrix is deliberate: real AHP weights should come from an expert.
DEFAULT_FACTOR_PAIRWISE_MATRIX = [
    [1.0, 1.0, 1.0],
    [1.0, 1.0, 1.0],
    [1.0, 1.0, 1.0],
]

RANDOM_INDEX = {
    1: 0.0,
    2: 0.0,
    3: 0.58,
    4: 0.9,
    5: 1.12,
    6: 1.24,
    7: 1.32,
    8: 1.41,
    9: 1.45,
    10: 1.49,
}


def normalize(values: dict[str, float]) -> dict[str, float]:
    total = sum(max(value, 0.0) for value in values.values())

    if isclose(total, 0.0):
        return {key: 0.0 for key in values}

    return {
        key: round(max(value, 0.0) / total, 4)
        for key, value in values.items()
    }


def round_matrix(matrix: list[list[float]]) -> list[list[float]]:
    return [[round(value, 4) for value in row] for row in matrix]


def ratio_to_saaty_value(ratio: float) -> float:
    normalized_ratio = max(ratio, 0.0001)

    if normalized_ratio < 1.0:
        return 1 / ratio_to_saaty_value(1 / normalized_ratio)

    return float(min(SAATY_VALUES, key=lambda value: abs(value - normalized_ratio)))


def calculate_ahp(matrix: list[list[float]], labels: list[str]) -> dict:
    if len(matrix) != len(labels):
        raise ValueError("Matrix size must match labels count")

    size = len(labels)
    if size == 0:
        return {
            "labels": [],
            "matrix": [],
            "saaty_scale": SAATY_SCALE_VALUES,
            "weights": {},
            "lambda_max": 0.0,
            "consistency_index": 0.0,
            "consistency_ratio": 0.0,
        }

    for row in matrix:
        if len(row) != size:
            raise ValueError("Pairwise matrix must be square")

    column_sums = [
        sum(matrix[row_index][column_index] for row_index in range(size))
        for column_index in range(size)
    ]
    normalized_matrix = [
        [
            matrix[row_index][column_index] / column_sums[column_index]
            for column_index in range(size)
        ]
        for row_index in range(size)
    ]
    weight_values = [
        sum(normalized_matrix[row_index]) / size
        for row_index in range(size)
    ]

    weighted_sums = [
        sum(matrix[row_index][column_index] * weight_values[column_index] for column_index in range(size))
        for row_index in range(size)
    ]
    lambda_values = [
        weighted_sums[index] / weight_values[index]
        for index in range(size)
        if not isclose(weight_values[index], 0.0)
    ]
    lambda_max = sum(lambda_values) / len(lambda_values) if lambda_values else 0.0
    consistency_index = (lambda_max - size) / (size - 1) if size > 2 else 0.0
    random_index = RANDOM_INDEX.get(size, 1.49)
    consistency_ratio = consistency_index / random_index if random_index else 0.0
    rounded_matrix = round_matrix(matrix)

    return {
        "labels": labels,
        "matrix": rounded_matrix,
        "saaty_scale": SAATY_SCALE_VALUES,
        "weights": {
            label: round(weight_values[index], 4)
            for index, label in enumerate(labels)
        },
        "lambda_max": round(lambda_max, 4),
        "consistency_index": round(consistency_index, 4),
        "consistency_ratio": round(consistency_ratio, 4),
    }


def score_ratio(left_score: float, right_score: float) -> float:
    left = max(left_score, 0.0001)
    right = max(right_score, 0.0001)

    return left / right


def build_pairwise_matrix_from_scores(scores: list[float]) -> list[list[float]]:
    size = len(scores)
    matrix = [[1.0 for _ in range(size)] for _ in range(size)]

    for row_index in range(size):
        for column_index in range(row_index + 1, size):
            ratio = score_ratio(scores[row_index], scores[column_index])
            saaty_value = ratio_to_saaty_value(ratio)
            matrix[row_index][column_index] = saaty_value
            matrix[column_index][row_index] = 1 / saaty_value

    return matrix


def calculate_factor_ahp(matrix: list[list[float]] | None = None) -> dict:
    return calculate_ahp(matrix or DEFAULT_FACTOR_PAIRWISE_MATRIX, FACTOR_ORDER)


def factor_probability(fragment: dict, factor: str) -> float:
    probabilities = fragment.get("probabilities") or {}

    if factor in probabilities:
        return float(probabilities[factor])

    if fragment.get("label") == factor:
        return float(fragment.get("score", 0.0))

    return 0.0


def factor_fragment_relevance(text: str) -> float:
    normalized_text = f" {text.casefold()} "

    strong_markers = [
        " не ",
        " не выпол",
        " не провер",
        " не содерж",
        " не предус",
        " отсутств",
        " наруш",
        " ошиб",
        " некоррект",
        " неисправ",
        " сбой",
        " отказ",
        " авар",
        " останов",
        " вывед",
    ]
    medium_markers = [
        " команд",
        " отключ",
        " квитир",
        " перев",
        " действовал",
    ]
    weak_markers = [
        " сигнал",
        " защит",
        " терминал",
        " устройство",
        " сработ",
    ]

    if any(marker in normalized_text for marker in strong_markers):
        return 1.0

    if any(marker in normalized_text for marker in medium_markers):
        return 0.6

    if any(marker in normalized_text for marker in weak_markers):
        return 0.2

    return 0.1


def calculate_factor_weights_from_fragments(factor_fragments: list[dict]) -> dict:
    scores = {factor: 0.0 for factor in FACTOR_ORDER}
    counts = {factor: 0 for factor in FACTOR_ORDER}
    used_scores = {factor: 0.0 for factor in FACTOR_ORDER}

    for fragment in factor_fragments:
        relevance = factor_fragment_relevance(fragment["text"])

        for factor in FACTOR_ORDER:
            score = factor_probability(fragment, factor)
            scores[factor] += score
            used_scores[factor] += score * relevance

        label = fragment.get("label")
        if label in counts:
            counts[label] += 1

    if isclose(sum(used_scores.values()), 0.0):
        factor_result = calculate_ahp(DEFAULT_FACTOR_PAIRWISE_MATRIX, FACTOR_ORDER)
    else:
        factor_matrix = build_pairwise_matrix_from_scores(
            [used_scores[factor] for factor in FACTOR_ORDER]
        )
        factor_result = calculate_ahp(factor_matrix, FACTOR_ORDER)

    return {
        "labels": FACTOR_ORDER,
        "matrix": factor_result["matrix"],
        "saaty_scale": factor_result["saaty_scale"],
        "weights": factor_result["weights"],
        "lambda_max": factor_result["lambda_max"],
        "consistency_index": factor_result["consistency_index"],
        "consistency_ratio": factor_result["consistency_ratio"],
        "source_scores": {
            factor: round(score, 4)
            for factor, score in used_scores.items()
        },
        "raw_scores": {
            factor: round(score, 4)
            for factor, score in scores.items()
        },
        "fragment_counts": counts,
    }


def calculate_participant_factor_scores(
    participants: list[dict],
    factor_fragments: list[dict],
) -> list[dict]:
    results = []
    unique_participants = {}

    for participant in participants:
        participant_text = participant["text"]
        participant_key = participant_text.casefold()

        if participant_key not in unique_participants:
            unique_participants[participant_key] = {
                "text": participant_text,
                "aliases": set(),
                "occurrences": [],
            }

        unique_participants[participant_key]["aliases"].add(participant_text.casefold())
        for alias in participant.get("aliases", []):
            unique_participants[participant_key]["aliases"].add(alias.casefold())

        unique_participants[participant_key]["occurrences"].append(participant)

    for participant_item in unique_participants.values():
        participant_text = participant_item["text"]
        participant_aliases = {
            alias
            for alias in participant_item["aliases"]
            if alias
        }
        factor_scores = {factor: 0.0 for factor in FACTOR_ORDER}
        matched_fragments = []

        for fragment in factor_fragments:
            if fragment.get("label") not in FACTOR_ORDER:
                continue
            relevance = factor_fragment_relevance(fragment["text"])
            if relevance < 0.5:
                continue

            fragment_text = fragment["text"]
            fragment_key = fragment_text.casefold()
            if not any(alias in fragment_key for alias in participant_aliases):
                continue

            for factor in FACTOR_ORDER:
                score = factor_probability(fragment, factor) * relevance
                if score < 0.05:
                    continue

                factor_scores[factor] += score
                matched_fragments.append(
                    {
                        "text": fragment_text,
                        "factor": factor,
                        "score": round(score, 4),
                    }
                )

        results.append(
            {
                "participant": participant_text,
                "occurrences_count": len(participant_item["occurrences"]),
                "factor_scores": {
                    factor: round(score, 4)
                    for factor, score in factor_scores.items()
                },
                "matched_fragments": matched_fragments,
            }
        )

    return results


def calculate_responsibility_ahp(
    participant_factor_scores: list[dict],
    factor_weights: dict[str, float],
) -> dict:
    participants = [item["participant"] for item in participant_factor_scores]

    if not participants:
        return {
            "local_weights_by_factor": {},
            "participant_matrices_by_factor": {},
            "global_weights": {},
            "local_consistency_ratio": {},
        }

    local_weights_by_factor = {}
    participant_matrices_by_factor = {}
    local_consistency_ratio = {}
    global_scores = {participant: 0.0 for participant in participants}

    for factor in FACTOR_ORDER:
        scores = [
            item["factor_scores"].get(factor, 0.0)
            for item in participant_factor_scores
        ]
        active_items = [
            (participants[index], score)
            for index, score in enumerate(scores)
            if score > 0.0
        ]

        if not active_items:
            local_weights_by_factor[factor] = {
                participant: 0.0
                for participant in participants
            }
            participant_matrices_by_factor[factor] = {
                "labels": [],
                "matrix": [],
                "saaty_scale": SAATY_SCALE_VALUES,
                "weights": {},
                "source_scores": {},
                "lambda_max": 0.0,
                "consistency_index": 0.0,
                "consistency_ratio": None,
            }
            local_consistency_ratio[factor] = None
            continue

        if len(active_items) == 1:
            only_participant = active_items[0][0]
            only_score = active_items[0][1]
            local_weights_by_factor[factor] = {
                participant: 1.0 if participant == only_participant else 0.0
                for participant in participants
            }
            participant_matrices_by_factor[factor] = {
                "labels": [only_participant],
                "matrix": [[1.0]],
                "saaty_scale": SAATY_SCALE_VALUES,
                "weights": {only_participant: 1.0},
                "source_scores": {only_participant: round(only_score, 4)},
                "lambda_max": 1.0,
                "consistency_index": 0.0,
                "consistency_ratio": 0.0,
            }
            local_consistency_ratio[factor] = 0.0
        else:
            active_participants = [participant for participant, _ in active_items]
            active_scores = [score for _, score in active_items]
            matrix = build_pairwise_matrix_from_scores(active_scores)
            factor_result = calculate_ahp(matrix, active_participants)
            local_weights_by_factor[factor] = {
                participant: factor_result["weights"].get(participant, 0.0)
                for participant in participants
            }
            participant_matrices_by_factor[factor] = {
                "labels": factor_result["labels"],
                "matrix": factor_result["matrix"],
                "saaty_scale": factor_result["saaty_scale"],
                "weights": factor_result["weights"],
                "source_scores": {
                    participant: round(score, 4)
                    for participant, score in active_items
                },
                "lambda_max": factor_result["lambda_max"],
                "consistency_index": factor_result["consistency_index"],
                "consistency_ratio": factor_result["consistency_ratio"],
            }
            local_consistency_ratio[factor] = factor_result["consistency_ratio"]

        for participant in participants:
            global_scores[participant] += (
                factor_weights.get(factor, 0.0)
                * local_weights_by_factor[factor].get(participant, 0.0)
            )

    return {
        "local_weights_by_factor": local_weights_by_factor,
        "participant_matrices_by_factor": participant_matrices_by_factor,
        "global_weights": normalize(global_scores),
        "local_consistency_ratio": local_consistency_ratio,
    }
