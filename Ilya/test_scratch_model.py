from __future__ import annotations

import json
from pathlib import Path

import torch

from scratch_model import ScratchFactorClassifier, encode_text


BASE_DIR = Path(__file__).resolve().parent
MODEL_DIR = BASE_DIR / "scratch_factor_classifier"

TEST_EXAMPLES = [
    "исполнитель не выполнил квитирование активных сигналов",
    "бланк переключений не содержал операции проверки",
    "терминал РЗА воспринял сигнал как команду отключения",
    "произошло отключение ввода номер два",
    "оператор не выполнил визуальный контроль дисплея",
    "руководитель не организовал контроль персонала",
    "датчик передал некорректные данные",
]


def load_artifacts() -> tuple[ScratchFactorClassifier, dict[str, int], dict[int, str], int]:
    if not (MODEL_DIR / "model.pt").exists():
        raise FileNotFoundError(
            "Модель с нуля не найдена. Запустите обучение: "
            "cd factor_classifier && python train_scratch_model.py"
        )

    config = json.loads((MODEL_DIR / "config.json").read_text(encoding="utf-8"))
    vocabulary = json.loads((MODEL_DIR / "vocab.json").read_text(encoding="utf-8"))
    id2label = {
        int(label_id): label
        for label_id, label in config["id2label"].items()
    }

    model = ScratchFactorClassifier(
        vocab_size=config["vocab_size"],
        num_labels=len(config["label2id"]),
        embedding_dim=config["embedding_dim"],
        hidden_channels=config["hidden_channels"],
        dense_dim=config["dense_dim"],
        dropout=config["dropout"],
    )
    model.load_state_dict(torch.load(MODEL_DIR / "model.pt", map_location="cpu"))
    model.eval()

    return model, vocabulary, id2label, config["max_length"]


def predict_text(
    text: str,
    model: ScratchFactorClassifier,
    vocabulary: dict[str, int],
    id2label: dict[int, str],
    max_length: int,
) -> dict:
    input_ids = torch.tensor(
        [encode_text(text, vocabulary, max_length)],
        dtype=torch.long,
    )

    with torch.no_grad():
        logits = model(input_ids)
        probabilities_tensor = torch.softmax(logits, dim=1)[0]

    predicted_id = int(torch.argmax(probabilities_tensor).item())
    probabilities = {
        id2label[label_id]: round(float(probabilities_tensor[label_id].item()), 4)
        for label_id in sorted(id2label)
    }

    return {
        "text": text,
        "label": id2label[predicted_id],
        "confidence": round(float(probabilities_tensor[predicted_id].item()), 4),
        "probabilities": probabilities,
    }


def main() -> None:
    model, vocabulary, id2label, max_length = load_artifacts()

    print("Проверка модели, обученной с нуля:")
    for example in TEST_EXAMPLES:
        prediction = predict_text(example, model, vocabulary, id2label, max_length)
        print("-" * 80)
        print(f"Текст: {prediction['text']}")
        print(f"Класс: {prediction['label']}")
        print(f"Уверенность: {prediction['confidence']}")
        print(f"Вероятности: {prediction['probabilities']}")


if __name__ == "__main__":
    main()
