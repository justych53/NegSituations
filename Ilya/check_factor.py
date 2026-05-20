from __future__ import annotations

import argparse
from pathlib import Path

from inference import classify_factor_text, is_factor_model_available
from test_scratch_model import load_artifacts, predict_text


CLASS_DESCRIPTIONS = {
    "ORG": "организационный фактор",
    "TECH": "технический фактор",
    "PSYCHO": "психофизиологический фактор / ошибка человека",
    "CONSEQUENCE": "последствие",
}


def print_prediction(prediction: dict) -> None:
    label = prediction["label"]
    confidence = prediction.get("confidence", prediction.get("score"))

    print("-" * 80)
    print(f"Текст: {prediction['text']}")
    print(f"Класс: {label} — {CLASS_DESCRIPTIONS.get(label, 'неизвестный класс')}")
    print(f"Уверенность: {confidence:.4f}")
    print("Вероятности по классам:")

    for class_label, probability in prediction["probabilities"].items():
        description = CLASS_DESCRIPTIONS.get(class_label, "")
        print(f"  {class_label:11s} {probability:.4f}  {description}")


def predict_with_rubert(text: str) -> dict:
    if not is_factor_model_available():
        raise FileNotFoundError(
            "Модель RuBERT не найдена. Запустите обучение: "
            "cd factor_classifier && python train_factor_model.py"
        )

    prediction = classify_factor_text(text)
    prediction["confidence"] = prediction["score"]
    return prediction


def run_single_check(model_name: str, text: str) -> None:
    if model_name == "rubert":
        prediction = predict_with_rubert(text)
    else:
        model, vocabulary, id2label, max_length = load_artifacts()
        prediction = predict_text(text, model, vocabulary, id2label, max_length)

    print_prediction(prediction)


def run_interactive_check(model_name: str) -> None:
    scratch_artifacts = None

    print("Интерактивная проверка фактора.")
    print("Введите фразу или предложение. Для выхода нажмите Enter на пустой строке.")
    print(f"Модель: {model_name}")

    if model_name == "rubert" and not is_factor_model_available():
        raise FileNotFoundError(
            "Модель RuBERT не найдена. Запустите обучение: "
            "cd factor_classifier && python train_factor_model.py"
        )

    if model_name == "scratch":
        scratch_artifacts = load_artifacts()

    while True:
        text = input("\nФактор/фрагмент: ").strip()
        if not text:
            print("Проверка завершена.")
            return

        if model_name == "rubert":
            prediction = predict_with_rubert(text)
        else:
            model, vocabulary, id2label, max_length = scratch_artifacts
            prediction = predict_text(text, model, vocabulary, id2label, max_length)

        print_prediction(prediction)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Проверка классификации фактора по введенному тексту.",
    )
    parser.add_argument(
        "text",
        nargs="*",
        help="Фраза для проверки. Если не передана, включается интерактивный режим.",
    )
    parser.add_argument(
        "--model",
        choices=["rubert", "scratch"],
        default="rubert",
        help="Какую модель использовать: рабочий RuBERT или модель, обученную с нуля.",
    )

    return parser.parse_args()


def main() -> None:
    args = parse_args()
    text = " ".join(args.text).strip()

    if text:
        run_single_check(args.model, text)
    else:
        run_interactive_check(args.model)


if __name__ == "__main__":
    main()
