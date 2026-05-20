from fastapi import Body, FastAPI, HTTPException
from pydantic import BaseModel, Field
from gliner import GLiNER

from ahp import (
    FACTOR_ORDER,
    calculate_factor_ahp,
    calculate_factor_weights_from_fragments,
    calculate_participant_factor_scores,
    calculate_responsibility_ahp,
    factor_fragment_relevance,
)
from factor_classifier.inference import (
    classify_factor_text,
    classify_fragments,
    is_factor_model_available,
    split_text_to_fragments,
)


app = FastAPI(
    title="API нейросетевого анализа отказа",
    description=(
        "Swagger-документация для frontend-интеграции. "
        "Сервис извлекает участников через GLiNER, классифицирует факторные фрагменты "
        "через RuBERT и возвращает данные МАИ для отображения весов и матриц."
    ),
    version="2.0.0",
    openapi_tags=[
        {
            "name": "Сущности",
            "description": "Извлечение участников отказа из исходного текста.",
        },
        {
            "name": "Факторы",
            "description": "Классификация смысловых фрагментов по факторам отказа.",
        },
        {
            "name": "МАИ",
            "description": "Веса факторов и матрицы попарных сравнений.",
        },
        {
            "name": "Полный анализ",
            "description": "Единый ответ для frontend: участники, факторы, последствия и МАИ.",
        },
        {
            "name": "Service",
            "description": "Служебная проверка состояния сервиса.",
        },
    ],
)


class TextRequest(BaseModel):
    text: str = Field(
        ...,
        description="Текстовое описание отказа, из которого нужно извлечь участников.",
        examples=[
            "При ремонте насосной станции мастер участка и подрядная организация "
            "не согласовали порядок отключения оборудования."
        ],
    )


class ParticipantEntity(BaseModel):
    text: str = Field(..., description="Фрагмент текста, распознанный как участник.")
    label: str = Field(..., description="Семантическая метка GLiNER.")
    score: float = Field(..., description="Оценка уверенности модели.")
    start: int = Field(..., description="Начальная позиция фрагмента в тексте.")
    end: int = Field(..., description="Конечная позиция фрагмента в тексте.")


class ParticipantResponse(BaseModel):
    participants: list[ParticipantEntity] = Field(
        ..., description="Список участников, найденных моделью GLiNER."
    )
    count: int = Field(..., description="Количество найденных участников.")


class FactorRequest(BaseModel):
    text: str = Field(
        ...,
        description="Текст отказа или отдельный фрагмент для классификации факторов.",
        examples=[
            "Оператор не выполнил визуальный контроль. Терминал РЗА воспринял сигнал как команду."
        ],
    )
    fragments: list[str] | None = Field(
        default=None,
        description=(
            "Необязательный список готовых смысловых фрагментов. Если не передан, "
            "сервис разделит текст на фрагменты самостоятельно."
        ),
    )


class FactorEntity(BaseModel):
    text: str = Field(..., description="Фраза или смысловой фрагмент текста отказа.")
    label: str = Field(..., description="Класс фактора: ORG, TECH, PSYCHO или CONSEQUENCE.")
    score: float = Field(..., description="Уверенность модели в выбранном классе.")
    probabilities: dict[str, float] = Field(
        ..., description="Вероятности по всем классам факторов."
    )


class FactorResponse(BaseModel):
    factors: list[FactorEntity] = Field(
        ..., description="Фрагменты текста с предсказанными факторами."
    )
    count: int = Field(..., description="Количество классифицированных фрагментов.")


class FailureAnalysisRequest(BaseModel):
    text: str = Field(
        ...,
        description="Полный текст описания отказа для комплексного анализа.",
        examples=[
            "Специалисты подрядной организации выполняли обслуживание устройств РЗА. "
            "Принимающий специалист ЭТЛ не проверил наличие активных сигналов. "
            "Оперативный персонал не выполнил визуальный контроль. "
            "Оператор перевёл ключ УРОВ в рабочее положение."
        ],
    )
    fragments: list[str] | None = Field(
        default=None,
        description=(
            "Необязательный список готовых фрагментов для классификации факторов. "
            "Если список не передан, сервис разделит текст сам."
        ),
    )
    factor_pairwise_matrix: list[list[float]] | None = Field(
        default=None,
        description=(
            "Необязательная экспертная матрица попарных сравнений факторов "
            "в порядке ORG, PSYCHO, TECH. Если не передана, веса факторов "
            "рассчитываются по классифицированным RuBERT-фрагментам текста."
        ),
    )


class AnalysisParticipant(BaseModel):
    text: str = Field(..., description="Найденный участник.")
    score: float = Field(..., description="Уверенность GLiNER.")
    start: int = Field(..., description="Начало фрагмента в тексте.")
    end: int = Field(..., description="Конец фрагмента в тексте.")


class AnalysisFragment(BaseModel):
    text: str = Field(..., description="Фрагмент описания отказа.")
    factor: str = Field(..., description="Класс: ORG, PSYCHO, TECH или CONSEQUENCE.")
    score: float = Field(..., description="Уверенность RuBERT.")


class ResponsibilityItem(BaseModel):
    participant: str = Field(..., description="Участник.")
    weight: float = Field(..., description="Итоговый предварительный вес участника.")
    factor_scores: dict[str, float] = Field(..., description="Баллы по ORG, PSYCHO, TECH.")
    matched_fragments: list[dict] = Field(..., description="Фрагменты, связанные с участником.")


class AhpMatrix(BaseModel):
    labels: list[str] = Field(..., description="Подписи строк и столбцов матрицы.")
    matrix: list[list[float]] = Field(
        ..., description="Числовая матрица попарных сравнений по шкале Саати."
    )
    saaty_scale: list[float] = Field(
        ...,
        description=(
            "Допустимые числовые значения шкалы Саати для таблицы: "
            "1/9, 1/8, 1/7, 1/6, 1/5, 1/4, 1/3, 1/2, 1, 2, 3, 4, 5, 6, 7, 8, 9."
        ),
    )
    weights: dict[str, float] = Field(..., description="Веса, рассчитанные по этой матрице.")
    source_scores: dict[str, float] = Field(
        default_factory=dict,
        description="Исходные баллы, из которых была собрана матрица.",
    )
    lambda_max: float = Field(..., description="Максимальное собственное значение матрицы.")
    consistency_index: float = Field(..., description="Индекс согласованности.")
    consistency_ratio: float | None = Field(..., description="Отношение согласованности.")


class FactorWeightsResponse(BaseModel):
    factors: list[FactorEntity] = Field(
        ..., description="Фрагменты ORG, PSYCHO и TECH, участвующие в расчете весов."
    )
    count: int = Field(..., description="Количество факторных фрагментов без CONSEQUENCE.")
    weights: dict[str, float] = Field(..., description="Веса факторов первого уровня.")
    source_scores: dict[str, float] = Field(..., description="Исходные суммы confidence по факторам.")
    fragment_counts: dict[str, int] = Field(..., description="Количество фрагментов по каждому фактору.")
    matrix: AhpMatrix = Field(..., description="Матрица попарных сравнений факторов.")


class AhpSummary(BaseModel):
    level_1_factors: dict[str, float] = Field(..., description="Веса факторов первого уровня.")
    level_2_participants: dict[str, float] = Field(..., description="Итоговые веса участников.")
    factor_weight_source: str = Field(..., description="Источник весов факторов: text-derived или expert-matrix.")
    factor_source_scores: dict[str, float] = Field(..., description="Суммы confidence по факторам из текста.")
    factor_fragment_counts: dict[str, int] = Field(..., description="Количество фрагментов по каждому фактору.")
    factor_consistency_ratio: float | None = Field(
        ..., description="Коэффициент согласованности матрицы факторов, если использовалась экспертная матрица."
    )
    local_weights_by_factor: dict[str, dict[str, float]] = Field(
        ..., description="Локальные веса участников внутри каждого фактора."
    )
    factor_matrix: AhpMatrix = Field(
        ..., description="Матрица первого уровня для факторов ORG, PSYCHO, TECH."
    )
    participant_matrices_by_factor: dict[str, AhpMatrix] = Field(
        ..., description="Матрицы второго уровня: участники внутри каждого фактора."
    )


class FailureAnalysisResponse(BaseModel):
    participants: list[AnalysisParticipant] = Field(..., description="Участники без лишней служебной информации.")
    factors: list[AnalysisFragment] = Field(..., description="Факторы ORG, PSYCHO и TECH.")
    consequences: list[AnalysisFragment] = Field(..., description="Последствия, не участвующие в МАИ.")
    responsibility: list[ResponsibilityItem] = Field(..., description="Предварительные веса участников.")
    ahp: AhpSummary = Field(..., description="Двухуровневая структура МАИ.")


PARTICIPANT_LABELS = [
    "участник происшествия",
    "ответственное лицо",
    "ответственный сотрудник",
    "человек",
    "сотрудник",
    "работник",
    "роль человека в происшествии",
    "исполнитель работ",
    "оператор",
    "принимающий специалист",
    "должность сотрудника",
    "группа персонала",
    "оперативный персонал",
    "подрядная организация",
    "организационное подразделение",
]

_gliner_model: GLiNER | None = None


def get_gliner_model() -> GLiNER:
    global _gliner_model

    if _gliner_model is None:
        _gliner_model = GLiNER.from_pretrained("urchade/gliner_multi-v2.1")

    return _gliner_model


def split_text_with_offsets(text: str) -> list[tuple[str, int]]:
    fragments = []
    start = None

    def add_fragment(end: int) -> None:
        nonlocal start

        if start is None:
            return

        source_fragment = text[start:end]
        stripped_fragment = source_fragment.strip()
        if stripped_fragment:
            leading_spaces = len(source_fragment) - len(source_fragment.lstrip())
            fragments.append((stripped_fragment, start + leading_spaces))

        start = None

    for index, char in enumerate(text):
        if start is None and not char.isspace():
            start = index

        if start is not None and char in ".!?;\n":
            add_fragment(index + 1)

    add_fragment(len(text))

    return fragments


def extract_participants(text: str, threshold: float = 0.55) -> list[dict]:
    model = get_gliner_model()

    participants = []
    seen = set()
    text_fragments = split_text_with_offsets(text)

    def add_entity(entity: dict, offset: int = 0) -> None:
        entity_text = entity["text"].strip()

        if not entity_text:
            return

        start = int(entity["start"]) + offset
        end = int(entity["end"]) + offset
        key = (entity_text.casefold(), start, end)

        if key in seen:
            return

        seen.add(key)
        participants.append(
            {
                "text": entity_text,
                "label": entity["label"].strip(),
                "score": round(float(entity["score"]), 3),
                "start": start,
                "end": end,
            }
        )

    prediction_sources = []
    if len(text) <= 700:
        prediction_sources.append((text, 0))
    prediction_sources.extend(text_fragments)

    predictions = model.batch_predict_entities(
        [fragment for fragment, _ in prediction_sources],
        PARTICIPANT_LABELS,
        threshold=threshold,
        batch_size=4,
    )

    for (_, fragment_start), fragment_entities in zip(prediction_sources, predictions):
        for entity in fragment_entities:
            add_entity(entity, fragment_start)

    return sorted(participants, key=lambda item: item["start"])


def is_parenthetical_alias(source_text: str, left: dict, right: dict) -> bool:
    if left["end"] > right["start"]:
        return False

    between = source_text[left["end"]:right["start"]]
    open_index = between.find("(")
    if open_index == -1 or open_index > 3:
        return False

    close_index = source_text.find(")", left["end"])

    return close_index != -1 and right["start"] < close_index


def merge_participant_aliases(source_text: str, participants: list[dict]) -> list[dict]:
    merged = []

    for participant in sorted(participants, key=lambda item: item["start"]):
        merged_into_existing = False

        for existing in merged:
            same_span = (
                participant["start"] >= existing["start"]
                and participant["end"] <= existing["end"]
            )
            parenthetical_alias = is_parenthetical_alias(source_text, existing, participant)
            adjacent_continuation = (
                existing["end"] <= participant["start"] <= existing["end"] + 2
                and not source_text[existing["end"]:participant["start"]].strip()
                and participant["text"][:1].islower()
            )

            if not same_span and not parenthetical_alias and not adjacent_continuation:
                continue

            aliases = existing.setdefault("aliases", [])
            if adjacent_continuation and existing["text"] not in aliases:
                aliases.append(existing["text"])
            if participant["text"] not in aliases:
                aliases.append(participant["text"])

            if adjacent_continuation:
                existing["text"] = source_text[existing["start"]:participant["end"]].strip()
                existing["end"] = participant["end"]
            existing["score"] = round(max(existing["score"], participant["score"]), 3)
            merged_into_existing = True
            break

        if not merged_into_existing:
            item = participant.copy()
            item["aliases"] = [participant["text"]]
            merged.append(item)

    return merged


@app.get(
    "/health",
    tags=["Service"],
    summary="Проверить состояние сервиса",
    description=(
        "Возвращает технический статус микросервиса. Используется, чтобы быстро "
        "убедиться, что FastAPI-приложение запущено и готово принимать запросы."
    ),
)
def health():
    return {
        "status": "ok",
        "message": "Сервис извлечения участников работает",
    }


@app.post(
    "/participants-ai",
    response_model=ParticipantResponse,
    tags=["Сущности"],
    summary="Извлечь сущности-участников из текста отказа",
    description=(
        "(Арсений, это тебе) ) "
        "Метод для frontend: принимает JSON с полем text и возвращает список сущностей-участников, "
        "которых GLiNER нашел в описании отказа. Поле score показывает уверенность модели, "
        "а start и end позволяют подсветить найденный фрагмент в исходном тексте."
    ),
)
def participants_ai(request: TextRequest):
    participants = extract_participants(request.text)

    return {
        "participants": participants,
        "count": len(participants),
    }


@app.post(
    "/participants-ai-plain",
    response_model=ParticipantResponse,
    tags=["Сущности"],
    summary="Извлечь участников из обычного текста",
    description=(
        "Принимает тело запроса как обычный text/plain без JSON-обертки. "
        "Метод удобен для Swagger и frontend-форм, где пользователь просто вставляет "
        "полное описание отказа. Возвращает участников, найденных GLiNER, а также "
        "score, start и end для подсветки фрагментов в исходном тексте."
    ),
)
def participants_ai_plain(text: str = Body(..., media_type="text/plain")):
    participants = extract_participants(text)

    return {
        "participants": participants,
        "count": len(participants),
    }


def classify_factor_fragments_or_503(text: str, fragments: list[str] | None = None) -> list[dict]:
    if not is_factor_model_available():
        raise HTTPException(
            status_code=503,
            detail=(
                "Модель классификации факторов не найдена. "
                "Запустите обучение: cd factor_classifier && python train_factor_model.py"
            ),
        )

    try:
        return classify_fragments(text, fragments)
    except FileNotFoundError as error:
        raise HTTPException(status_code=503, detail=str(error)) from error


@app.post(
    "/factors-ai",
    response_model=FactorResponse,
    tags=["Факторы"],
    summary="Классифицировать факторы отказа",
    description=(
        "Принимает JSON с полем text и, при необходимости, массивом готовых fragments. "
        "Каждый фрагмент классифицируется обученной моделью RuBERT по классам ORG, TECH, "
        "PSYCHO или CONSEQUENCE. Метод нужен фронту, чтобы показать какие части текста "
        "относятся к организационным, техническим, психофизиологическим факторам и последствиям."
    ),
)
def factors_ai(request: FactorRequest):
    factors = classify_factor_fragments_or_503(request.text, request.fragments)

    return {
        "factors": factors,
        "count": len(factors),
    }


def build_factor_matrix_response(factor_ahp: dict, factor_items: list[dict]) -> dict:
    return {
        "factors": factor_items,
        "count": len(factor_items),
        "weights": factor_ahp["weights"],
        "source_scores": factor_ahp["source_scores"],
        "fragment_counts": factor_ahp["fragment_counts"],
        "matrix": {
            "labels": factor_ahp["labels"],
            "matrix": factor_ahp["matrix"],
            "saaty_scale": factor_ahp["saaty_scale"],
            "weights": factor_ahp["weights"],
            "source_scores": factor_ahp["source_scores"],
            "lambda_max": factor_ahp["lambda_max"],
            "consistency_index": factor_ahp["consistency_index"],
            "consistency_ratio": factor_ahp["consistency_ratio"],
        },
    }


@app.post(
    "/factor-weights-ai",
    response_model=FactorWeightsResponse,
    tags=["МАИ"],
    summary="Рассчитать веса факторов отказа",
    description=(
        "Принимает JSON с текстом отказа или готовыми фрагментами. Сервис классифицирует "
        "фрагменты через RuBERT, исключает CONSEQUENCE из расчета ответственности и строит "
        "матрицу попарных сравнений для факторов первого уровня: ORG, PSYCHO, TECH. "
        "Ответ содержит веса факторов, исходные баллы, количество фрагментов и готовую "
        "матрицу для отображения на frontend."
    ),
)
def factor_weights_ai(request: FactorRequest):
    factor_fragments = classify_factor_fragments_or_503(request.text, request.fragments)
    factor_items = [
        fragment
        for fragment in factor_fragments
        if fragment["label"] in FACTOR_ORDER
    ]
    factor_ahp = calculate_factor_weights_from_fragments(factor_items)

    return build_factor_matrix_response(factor_ahp, factor_items)


@app.post(
    "/factor-weights-ai-plain",
    response_model=FactorWeightsResponse,
    tags=["МАИ"],
    summary="Рассчитать веса факторов из обычного текста",
    description=(
        "Принимает полное описание отказа как обычный text/plain без JSON-обертки. "
        "Сервис сам разбивает текст на смысловые фрагменты, классифицирует их через "
        "RuBERT, исключает CONSEQUENCE из расчета ответственности и возвращает веса "
        "факторов первого уровня ORG, PSYCHO, TECH вместе с матрицей попарных сравнений."
    ),
)
def factor_weights_ai_plain(text: str = Body(..., media_type="text/plain")):
    factor_fragments = classify_factor_fragments_or_503(text)
    factor_items = [
        fragment
        for fragment in factor_fragments
        if fragment["label"] in FACTOR_ORDER
    ]
    factor_ahp = calculate_factor_weights_from_fragments(factor_items)

    return build_factor_matrix_response(factor_ahp, factor_items)


@app.post(
    "/factors-ai-plain",
    response_model=FactorResponse,
    tags=["Факторы"],
    summary="Классифицировать факторы из обычного текста",
    description=(
        "Принимает текст отказа или отдельный фрагмент как обычный text/plain без JSON-обертки. "
        "Если передан большой текст, сервис сам разбивает его на фрагменты и для каждого "
        "возвращает класс фактора: ORG, TECH, PSYCHO или CONSEQUENCE."
    ),
)
def factors_ai_plain(text: str = Body(..., media_type="text/plain")):
    factors = classify_factor_fragments_or_503(text)

    return {
        "factors": factors,
        "count": len(factors),
    }


@app.post(
    "/factor-ai-plain",
    response_model=FactorEntity,
    include_in_schema=False,
    summary="Классифицировать один текстовый фрагмент",
    description=(
        "Скрытый совместимый alias для старой ручной проверки одного фрагмента. "
        "Для Swagger используйте /factors-ai-plain."
    ),
)
def factor_ai_plain(text: str = Body(..., media_type="text/plain")):
    if not is_factor_model_available():
        raise HTTPException(
            status_code=503,
            detail=(
                "Модель классификации факторов не найдена. "
                "Запустите обучение: cd factor_classifier && python train_factor_model.py"
            ),
        )

    try:
        return classify_factor_text(text)
    except FileNotFoundError as error:
        raise HTTPException(status_code=503, detail=str(error)) from error


def compact_participant(participant: dict) -> dict:
    return {
        "text": participant["text"],
        "score": participant["score"],
        "start": participant["start"],
        "end": participant["end"],
    }


def compact_fragment(fragment: dict) -> dict:
    return {
        "text": fragment["text"],
        "factor": fragment["label"],
        "score": fragment["score"],
    }


def build_failure_analysis(
    text: str,
    fragments: list[str] | None = None,
    factor_pairwise_matrix: list[list[float]] | None = None,
) -> dict:
    participants = merge_participant_aliases(text, extract_participants(text))
    factor_fragments = classify_factor_fragments_or_503(text, fragments)
    factor_items = [
        fragment
        for fragment in factor_fragments
        if fragment["label"] in FACTOR_ORDER
    ]
    consequence_items = [
        fragment
        for fragment in factor_fragments
        if fragment["label"] == "CONSEQUENCE"
    ]
    participant_factor_scores = calculate_participant_factor_scores(
        participants,
        factor_items,
    )
    participant_factor_scores = [
        item
        for item in participant_factor_scores
        if any(item["factor_scores"].get(factor, 0.0) > 0.0 for factor in FACTOR_ORDER)
    ]
    responsibility_participants = {
        item["participant"].casefold()
        for item in participant_factor_scores
    }
    participants = [
        participant
        for participant in participants
        if participant["text"].casefold() in responsibility_participants
    ]
    if factor_pairwise_matrix:
        factor_ahp = calculate_factor_ahp(factor_pairwise_matrix)
        factor_weight_source = "expert-matrix"
        factor_source_scores = {
            factor: round(
                sum(
                    float(fragment["score"])
                    for fragment in factor_items
                    if fragment["label"] == factor
                ),
                4,
            )
            for factor in FACTOR_ORDER
        }
        factor_fragment_counts = {
            factor: sum(1 for fragment in factor_items if fragment["label"] == factor)
            for factor in FACTOR_ORDER
        }
    else:
        factor_ahp = calculate_factor_weights_from_fragments(factor_items)
        factor_weight_source = "text-derived"
        factor_source_scores = factor_ahp["source_scores"]
        factor_fragment_counts = factor_ahp["fragment_counts"]

    participant_ahp = calculate_responsibility_ahp(
        participant_factor_scores,
        factor_ahp["weights"],
    )
    participant_weights = participant_ahp["global_weights"]
    responsibility = []

    for item in participant_factor_scores:
        participant = item["participant"]
        responsibility.append(
            {
                "participant": participant,
                "weight": participant_weights.get(participant, 0.0),
                "factor_scores": item["factor_scores"],
                "matched_fragments": item["matched_fragments"],
            }
        )

    responsibility.sort(key=lambda item: item["weight"], reverse=True)

    visible_factor_items = [
        fragment
        for fragment in factor_items
        if factor_fragment_relevance(fragment["text"]) >= 0.5
    ]
    visible_consequence_items = [
        fragment
        for fragment in consequence_items
        if factor_fragment_relevance(fragment["text"]) >= 0.5
    ]

    return {
        "participants": [compact_participant(participant) for participant in participants],
        "factors": [compact_fragment(fragment) for fragment in visible_factor_items],
        "consequences": [compact_fragment(fragment) for fragment in visible_consequence_items],
        "responsibility": responsibility,
        "ahp": {
            "level_1_factors": factor_ahp["weights"],
            "level_2_participants": participant_weights,
            "factor_weight_source": factor_weight_source,
            "factor_source_scores": factor_source_scores,
            "factor_fragment_counts": factor_fragment_counts,
            "factor_consistency_ratio": factor_ahp["consistency_ratio"],
            "local_weights_by_factor": participant_ahp["local_weights_by_factor"],
            "factor_matrix": {
                "labels": factor_ahp["labels"],
                "matrix": factor_ahp["matrix"],
                "saaty_scale": factor_ahp["saaty_scale"],
                "weights": factor_ahp["weights"],
                "source_scores": factor_source_scores,
                "lambda_max": factor_ahp["lambda_max"],
                "consistency_index": factor_ahp["consistency_index"],
                "consistency_ratio": factor_ahp["consistency_ratio"],
            },
            "participant_matrices_by_factor": participant_ahp["participant_matrices_by_factor"],
        },
    }


@app.post(
    "/analysis-ai",
    response_model=FailureAnalysisResponse,
    tags=["Полный анализ"],
    summary="Получить полный предварительный анализ отказа из JSON",
    description=(
        "Главный метод для frontend. Принимает JSON с полем text и, при необходимости, "
        "готовым списком fragments или экспертной factor_pairwise_matrix. Возвращает "
        "единый JSON: участников из GLiNER, факторные фрагменты из RuBERT, последствия, "
        "предварительные веса участников, веса факторов, матрицу факторов и матрицы "
        "участников внутри каждого фактора. Результат является предварительным и "
        "предназначен для экспертной проверки."
    ),
)
def analysis_ai(request: FailureAnalysisRequest):
    return build_failure_analysis(
        request.text,
        request.fragments,
        request.factor_pairwise_matrix,
    )


@app.post(
    "/analysis-ai-plain",
    response_model=FailureAnalysisResponse,
    summary="Получить полный предварительный анализ отказа из обычного текста",
    include_in_schema=False,
    description=(
        "Скрытый совместимый alias для полного анализа text/plain. "
        "Для Swagger используйте /analysis-plain."
    ),
)
@app.post(
    "/analysis-plain",
    response_model=FailureAnalysisResponse,
    tags=["Полный анализ"],
    summary="Получить анализ отказа из обычного текста",
    description=(
        "Принимает тело запроса как text/plain без JSON-обертки. Метод удобен для "
        "ручной проверки в Swagger: можно вставить полное описание отказа и получить "
        "участников, факторные фрагменты, последствия, веса факторов, веса участников "
        "и матрицы МАИ в одном ответе. Если нужны заранее подготовленные фрагменты "
        "или экспертная матрица факторов, используйте JSON-метод /analysis-ai."
    ),
)
def analysis_ai_plain(text: str = Body(..., media_type="text/plain")):
    return build_failure_analysis(text)
