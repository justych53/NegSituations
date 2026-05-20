from __future__ import annotations

import re
from collections import Counter

import torch
from torch import nn


PAD_TOKEN = "<PAD>"
UNK_TOKEN = "<UNK>"


def tokenize_text(text: str) -> list[str]:
    return re.findall(r"[а-яёa-z0-9]+", text.casefold())


def build_vocabulary(texts: list[str], min_frequency: int = 1) -> dict[str, int]:
    token_counts: Counter[str] = Counter()

    for text in texts:
        token_counts.update(tokenize_text(text))

    vocabulary = {
        PAD_TOKEN: 0,
        UNK_TOKEN: 1,
    }

    for token, frequency in sorted(token_counts.items()):
        if frequency >= min_frequency:
            vocabulary[token] = len(vocabulary)

    return vocabulary


def encode_text(text: str, vocabulary: dict[str, int], max_length: int) -> list[int]:
    token_ids = [
        vocabulary.get(token, vocabulary[UNK_TOKEN])
        for token in tokenize_text(text)
    ][:max_length]

    if len(token_ids) < max_length:
        token_ids.extend([vocabulary[PAD_TOKEN]] * (max_length - len(token_ids)))

    return token_ids


class ScratchFactorClassifier(nn.Module):
    def __init__(
        self,
        vocab_size: int,
        num_labels: int,
        embedding_dim: int = 128,
        hidden_channels: int = 96,
        dense_dim: int = 64,
        dropout: float = 0.35,
    ) -> None:
        super().__init__()

        self.embedding = nn.Embedding(
            num_embeddings=vocab_size,
            embedding_dim=embedding_dim,
            padding_idx=0,
        )
        self.conv3 = nn.Conv1d(
            in_channels=embedding_dim,
            out_channels=hidden_channels,
            kernel_size=3,
            padding=1,
        )
        self.conv5 = nn.Conv1d(
            in_channels=embedding_dim,
            out_channels=hidden_channels,
            kernel_size=5,
            padding=2,
        )
        self.activation = nn.ReLU()
        self.dropout = nn.Dropout(dropout)
        self.classifier = nn.Sequential(
            nn.Linear(hidden_channels * 2, dense_dim),
            nn.ReLU(),
            nn.Dropout(dropout),
            nn.Linear(dense_dim, num_labels),
        )

    def forward(self, input_ids: torch.Tensor) -> torch.Tensor:
        mask = input_ids.ne(0).unsqueeze(1)
        embeddings = self.embedding(input_ids).transpose(1, 2)

        conv3_output = self.activation(self.conv3(embeddings)).masked_fill(~mask, -1e9)
        conv5_output = self.activation(self.conv5(embeddings)).masked_fill(~mask, -1e9)

        pooled3 = torch.max(conv3_output, dim=2).values
        pooled5 = torch.max(conv5_output, dim=2).values
        features = torch.cat([pooled3, pooled5], dim=1)

        return self.classifier(self.dropout(features))
