from __future__ import annotations

import inspect
import json
from pathlib import Path

import numpy as np
import pandas as pd
from datasets import Dataset
from sklearn.metrics import accuracy_score, f1_score
from sklearn.model_selection import train_test_split
from transformers import (
    AutoModelForSequenceClassification,
    AutoTokenizer,
    DataCollatorWithPadding,
    Trainer,
    TrainingArguments,
)


MODEL_NAME = "DeepPavlov/rubert-base-cased"
BASE_DIR = Path(__file__).resolve().parent
DATASET_PATH = BASE_DIR / "dataset.csv"
MODEL_DIR = BASE_DIR / "rubert_factor_classifier"

LABEL2ID = {
    "ORG": 0,
    "TECH": 1,
    "PSYCHO": 2,
    "CONSEQUENCE": 3,
}
ID2LABEL = {value: key for key, value in LABEL2ID.items()}


def load_factor_dataset() -> tuple[Dataset, Dataset]:
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

    dataframe["labels"] = dataframe["label"].map(LABEL2ID)
    train_df, test_df = train_test_split(
        dataframe[["text", "labels"]],
        test_size=0.2,
        random_state=42,
        stratify=dataframe["label"],
    )

    train_dataset = Dataset.from_pandas(train_df.reset_index(drop=True))
    test_dataset = Dataset.from_pandas(test_df.reset_index(drop=True))

    return train_dataset, test_dataset


def build_training_args() -> TrainingArguments:
    args = {
        "output_dir": str(MODEL_DIR),
        "num_train_epochs": 10,
        "per_device_train_batch_size": 8,
        "per_device_eval_batch_size": 8,
        "learning_rate": 3e-5,
        "weight_decay": 0.01,
        "logging_steps": 10,
        "save_strategy": "epoch",
        "save_total_limit": 2,
        "load_best_model_at_end": True,
        "metric_for_best_model": "f1_macro",
        "greater_is_better": True,
        "report_to": "none",
        "seed": 42,
        "dataloader_num_workers": 0,
    }

    signature = inspect.signature(TrainingArguments.__init__)
    if "eval_strategy" in signature.parameters:
        args["eval_strategy"] = "epoch"
    else:
        args["evaluation_strategy"] = "epoch"

    return TrainingArguments(**args)


def compute_metrics(eval_prediction):
    predictions = np.argmax(eval_prediction.predictions, axis=1)
    labels = eval_prediction.label_ids

    return {
        "accuracy": accuracy_score(labels, predictions),
        "f1_macro": f1_score(labels, predictions, average="macro"),
    }


def main() -> None:
    train_dataset, test_dataset = load_factor_dataset()

    print("Загрузка RuBERT...")
    tokenizer = AutoTokenizer.from_pretrained(MODEL_NAME)
    model = AutoModelForSequenceClassification.from_pretrained(
        MODEL_NAME,
        num_labels=len(LABEL2ID),
        id2label=ID2LABEL,
        label2id=LABEL2ID,
    )

    def tokenize_batch(batch):
        return tokenizer(batch["text"], truncation=True, max_length=128)

    tokenized_train = train_dataset.map(tokenize_batch, batched=True)
    tokenized_test = test_dataset.map(tokenize_batch, batched=True)

    trainer_args = {
        "model": model,
        "args": build_training_args(),
        "train_dataset": tokenized_train,
        "eval_dataset": tokenized_test,
        "data_collator": DataCollatorWithPadding(tokenizer=tokenizer),
        "compute_metrics": compute_metrics,
    }

    trainer_signature = inspect.signature(Trainer.__init__)
    if "processing_class" in trainer_signature.parameters:
        trainer_args["processing_class"] = tokenizer
    else:
        trainer_args["tokenizer"] = tokenizer

    trainer = Trainer(**trainer_args)

    print("Начало обучения...")
    trainer.train()
    metrics = trainer.evaluate()

    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    trainer.save_model(MODEL_DIR)
    tokenizer.save_pretrained(MODEL_DIR)

    mapping_path = MODEL_DIR / "label_mapping.json"
    mapping_path.write_text(
        json.dumps(LABEL2ID, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )

    print(f"Метрики на тестовой выборке: {metrics}")
    print(f"Модель сохранена в папку: {MODEL_DIR}")


if __name__ == "__main__":
    main()
