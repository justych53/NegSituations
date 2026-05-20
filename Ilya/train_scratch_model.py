from __future__ import annotations

import json
import random
from pathlib import Path

import numpy as np
import pandas as pd
import torch
from sklearn.metrics import accuracy_score, classification_report, confusion_matrix, f1_score
from sklearn.model_selection import train_test_split
from torch import nn
from torch.utils.data import DataLoader, Dataset

from scratch_model import ScratchFactorClassifier, build_vocabulary, encode_text


BASE_DIR = Path(__file__).resolve().parent
DATASET_PATH = BASE_DIR / "dataset.csv"
MODEL_DIR = BASE_DIR / "scratch_factor_classifier"

LABEL2ID = {
    "ORG": 0,
    "TECH": 1,
    "PSYCHO": 2,
    "CONSEQUENCE": 3,
}
ID2LABEL = {value: key for key, value in LABEL2ID.items()}

MAX_LENGTH = 64
BATCH_SIZE = 16
EPOCHS = 45
LEARNING_RATE = 1e-3
SEED = 42


class FactorDataset(Dataset):
    def __init__(
        self,
        texts: list[str],
        labels: list[int],
        vocabulary: dict[str, int],
        max_length: int,
    ) -> None:
        self.texts = texts
        self.labels = labels
        self.vocabulary = vocabulary
        self.max_length = max_length

    def __len__(self) -> int:
        return len(self.texts)

    def __getitem__(self, index: int) -> dict[str, torch.Tensor]:
        return {
            "input_ids": torch.tensor(
                encode_text(self.texts[index], self.vocabulary, self.max_length),
                dtype=torch.long,
            ),
            "label": torch.tensor(self.labels[index], dtype=torch.long),
        }


def set_seed(seed: int) -> None:
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)
    if torch.cuda.is_available():
        torch.cuda.manual_seed_all(seed)


def load_dataset() -> pd.DataFrame:
    print("Загрузка датасета...")

    if not DATASET_PATH.exists():
        raise FileNotFoundError(f"Файл датасета не найден: {DATASET_PATH}")

    dataframe = pd.read_csv(DATASET_PATH)
    required_columns = {"text", "label"}

    if not required_columns.issubset(dataframe.columns):
        raise ValueError("dataset.csv должен содержать колонки text,label")

    dataframe = dataframe[["text", "label"]].dropna()
    dataframe["text"] = dataframe["text"].astype(str).str.strip()
    dataframe["label"] = dataframe["label"].astype(str).str.strip().str.upper()
    dataframe = dataframe[(dataframe["text"] != "") & (dataframe["label"] != "")]

    unknown_labels = sorted(set(dataframe["label"]) - set(LABEL2ID))
    if unknown_labels:
        raise ValueError(f"В датасете найдены неизвестные классы: {unknown_labels}")

    class_counts = dataframe["label"].value_counts()
    if (class_counts < 2).any():
        raise ValueError("Для stratify нужно минимум два примера каждого класса")

    dataframe["label_id"] = dataframe["label"].map(LABEL2ID)
    return dataframe


def evaluate_model(
    model: ScratchFactorClassifier,
    data_loader: DataLoader,
    device: torch.device,
) -> tuple[float, float, list[int], list[int]]:
    model.eval()
    predictions = []
    labels = []

    with torch.no_grad():
        for batch in data_loader:
            input_ids = batch["input_ids"].to(device)
            batch_labels = batch["label"].to(device)
            logits = model(input_ids)
            batch_predictions = torch.argmax(logits, dim=1)

            predictions.extend(batch_predictions.cpu().tolist())
            labels.extend(batch_labels.cpu().tolist())

    accuracy = accuracy_score(labels, predictions)
    f1_macro = f1_score(labels, predictions, average="macro")

    return accuracy, f1_macro, labels, predictions


def train_one_epoch(
    model: ScratchFactorClassifier,
    data_loader: DataLoader,
    optimizer: torch.optim.Optimizer,
    criterion: nn.Module,
    device: torch.device,
) -> float:
    model.train()
    total_loss = 0.0

    for batch in data_loader:
        input_ids = batch["input_ids"].to(device)
        labels = batch["label"].to(device)

        optimizer.zero_grad()
        logits = model(input_ids)
        loss = criterion(logits, labels)
        loss.backward()
        optimizer.step()

        total_loss += float(loss.item())

    return total_loss / max(len(data_loader), 1)


def main() -> None:
    set_seed(SEED)
    dataframe = load_dataset()

    print("Кодирование меток...")
    train_df, test_df = train_test_split(
        dataframe[["text", "label", "label_id"]],
        test_size=0.2,
        random_state=SEED,
        stratify=dataframe["label"],
    )

    print("Создание словаря по обучающей выборке...")
    vocabulary = build_vocabulary(train_df["text"].tolist())

    train_dataset = FactorDataset(
        train_df["text"].tolist(),
        train_df["label_id"].tolist(),
        vocabulary,
        MAX_LENGTH,
    )
    test_dataset = FactorDataset(
        test_df["text"].tolist(),
        test_df["label_id"].tolist(),
        vocabulary,
        MAX_LENGTH,
    )

    train_loader = DataLoader(train_dataset, batch_size=BATCH_SIZE, shuffle=True)
    test_loader = DataLoader(test_dataset, batch_size=BATCH_SIZE)

    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    print(f"Создание модели с нуля. Устройство: {device}")

    model = ScratchFactorClassifier(
        vocab_size=len(vocabulary),
        num_labels=len(LABEL2ID),
    ).to(device)
    optimizer = torch.optim.Adam(model.parameters(), lr=LEARNING_RATE)
    criterion = nn.CrossEntropyLoss()

    print("Начало обучения...")
    best_f1 = -1.0
    best_state = None

    for epoch in range(1, EPOCHS + 1):
        train_loss = train_one_epoch(model, train_loader, optimizer, criterion, device)
        accuracy, f1_macro, _, _ = evaluate_model(model, test_loader, device)

        if f1_macro > best_f1:
            best_f1 = f1_macro
            best_state = {
                key: value.detach().cpu().clone()
                for key, value in model.state_dict().items()
            }

        print(
            f"Эпоха {epoch:02d}/{EPOCHS}: "
            f"loss={train_loss:.4f}, accuracy={accuracy:.4f}, f1_macro={f1_macro:.4f}"
        )

    if best_state is not None:
        model.load_state_dict(best_state)

    accuracy, f1_macro, labels, predictions = evaluate_model(model, test_loader, device)
    report = classification_report(
        labels,
        predictions,
        labels=list(ID2LABEL),
        target_names=[ID2LABEL[index] for index in ID2LABEL],
        digits=4,
        zero_division=0,
    )
    matrix = confusion_matrix(labels, predictions, labels=list(ID2LABEL))

    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    torch.save(model.state_dict(), MODEL_DIR / "model.pt")
    (MODEL_DIR / "vocab.json").write_text(
        json.dumps(vocabulary, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    (MODEL_DIR / "config.json").write_text(
        json.dumps(
            {
                "label2id": LABEL2ID,
                "id2label": {str(key): value for key, value in ID2LABEL.items()},
                "max_length": MAX_LENGTH,
                "vocab_size": len(vocabulary),
                "embedding_dim": 128,
                "hidden_channels": 96,
                "dense_dim": 64,
                "dropout": 0.35,
                "test_size": 0.2,
                "seed": SEED,
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )
    (MODEL_DIR / "metrics.json").write_text(
        json.dumps(
            {
                "accuracy": round(float(accuracy), 4),
                "f1_macro": round(float(f1_macro), 4),
                "confusion_matrix": matrix.tolist(),
                "labels": [ID2LABEL[index] for index in ID2LABEL],
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )
    (MODEL_DIR / "classification_report.txt").write_text(report, encoding="utf-8")

    print("Итоговые метрики на тестовой выборке:")
    print(f"accuracy={accuracy:.4f}")
    print(f"f1_macro={f1_macro:.4f}")
    print("Classification report:")
    print(report)
    print(f"Модель сохранена в папку: {MODEL_DIR}")


if __name__ == "__main__":
    main()
