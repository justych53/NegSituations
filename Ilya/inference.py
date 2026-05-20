from __future__ import annotations

from pathlib import Path

import torch
from transformers import AutoModelForSequenceClassification, AutoTokenizer


BASE_DIR = Path(__file__).resolve().parent
MODEL_DIR = BASE_DIR / "rubert_factor_classifier"
FACTOR_LABELS = ["ORG", "TECH", "PSYCHO", "CONSEQUENCE"]

_tokenizer = None
_model = None
_device = torch.device("cuda" if torch.cuda.is_available() else "cpu")


def is_factor_model_available() -> bool:
    return MODEL_DIR.exists() and (MODEL_DIR / "config.json").exists()


def get_factor_model():
    global _tokenizer, _model

    if not is_factor_model_available():
        raise FileNotFoundError(
            "Модель факторов не найдена. Сначала запустите обучение: "
            "cd factor_classifier && python train_factor_model.py"
        )

    if _tokenizer is None or _model is None:
        _tokenizer = AutoTokenizer.from_pretrained(MODEL_DIR)
        _model = AutoModelForSequenceClassification.from_pretrained(MODEL_DIR)
        _model.to(_device)
        _model.eval()

    return _tokenizer, _model


def get_label(model, label_id: int) -> str:
    label = model.config.id2label.get(label_id)

    if label is None:
        label = model.config.id2label.get(str(label_id), str(label_id))

    return str(label)


def prepare_text_for_classification(text: str) -> str:
    prepared_text = text.strip()
    prefixes = [
        "следовательно,",
        "следовательно",
        "в результате",
        "таким образом,",
        "таким образом",
    ]

    changed = True
    while changed:
        changed = False
        lowered_text = prepared_text.casefold()

        for prefix in prefixes:
            if not lowered_text.startswith(prefix):
                continue

            prepared_text = prepared_text[len(prefix):].strip(" ,—")
            changed = True
            break

    colon_index = prepared_text.find(":")
    if 0 <= colon_index <= 40 and len(prepared_text[colon_index + 1:].strip()) >= 25:
        prepared_text = prepared_text[colon_index + 1:].strip()

    return prepared_text or text


def split_text_to_fragments(text: str) -> list[str]:
    fragments = []
    buffer = []
    pending_heading = ""
    active_heading = ""

    def split_long_fragment(fragment: str) -> list[str]:
        if len(fragment) <= 180:
            return [fragment]

        parts = []
        start = 0
        bracket_depth = 0

        for index, char in enumerate(fragment):
            if char == "(":
                bracket_depth += 1
                continue

            if char == ")":
                bracket_depth = max(0, bracket_depth - 1)
                continue

            if bracket_depth > 0:
                continue

            if char not in ",—":
                continue

            part = fragment[start:index].strip(" ,—")
            rest = fragment[index + 1:].strip(" ,—")

            if len(part) >= 45 and len(rest) >= 35:
                parts.append(part)
                start = index + 1

        tail = fragment[start:].strip(" ,—")
        if tail:
            parts.append(tail)

        result = []
        for part in parts or [fragment]:
            and_index = part.find(" и ")
            if len(part) > 140 and and_index >= 60:
                left = part[:and_index].strip()
                right = part[and_index + 3:].strip()
                if len(left) >= 45 and len(right) >= 35:
                    result.extend([left, right])
                    continue

            result.append(part)

        return result

    def add_fragment(fragment: str) -> None:
        nonlocal active_heading, pending_heading

        fragment = fragment.strip()
        if not fragment:
            return

        if fragment.endswith(":"):
            pending_heading = fragment[:-1].strip()
            active_heading = pending_heading
            return

        if pending_heading and fragment[:1].islower():
            fragment = f"{pending_heading} {fragment}"
            pending_heading = ""
        elif active_heading and fragment[:1].islower():
            fragment = f"{active_heading} {fragment}"
        else:
            pending_heading = ""
            if fragment[:1].isupper():
                active_heading = ""

        fragments.extend(split_long_fragment(fragment))

    for line in text.splitlines():
        line = line.strip()
        if not line:
            continue

        for char in line:
            buffer.append(char)
            if char in ".!?;":
                add_fragment("".join(buffer))
                buffer = []

        if buffer:
            add_fragment("".join(buffer))
            buffer = []

    fragment = "".join(buffer).strip()
    if fragment:
        add_fragment(fragment)

    return fragments


def classify_factor_text(text: str) -> dict:
    tokenizer, model = get_factor_model()
    model_text = prepare_text_for_classification(text)
    inputs = tokenizer(
        model_text,
        return_tensors="pt",
        truncation=True,
        max_length=128,
    ).to(_device)

    with torch.no_grad():
        outputs = model(**inputs)
        probabilities_tensor = torch.softmax(outputs.logits, dim=1)[0]

    predicted_id = int(torch.argmax(probabilities_tensor).item())
    predicted_label = get_label(model, predicted_id)
    probabilities = {
        get_label(model, label_id): round(float(probability.item()), 4)
        for label_id, probability in enumerate(probabilities_tensor)
    }

    return {
        "text": text,
        "label": predicted_label,
        "score": round(float(probabilities_tensor[predicted_id].item()), 4),
        "probabilities": probabilities,
    }


def classify_fragments(text: str, fragments: list[str] | None = None) -> list[dict]:
    source_fragments = fragments if fragments else split_text_to_fragments(text)

    return [
        classify_factor_text(fragment)
        for fragment in source_fragments
        if fragment.strip()
    ]
