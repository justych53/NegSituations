from __future__ import annotations

import argparse

from check_factor import run_interactive_check, run_single_check


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Ручная проверка модели факторов, обученной с нуля.",
    )
    parser.add_argument(
        "text",
        nargs="*",
        help="Фраза для проверки. Если не передана, включается интерактивный режим.",
    )

    return parser.parse_args()


def main() -> None:
    args = parse_args()
    text = " ".join(args.text).strip()

    if text:
        run_single_check("scratch", text)
    else:
        run_interactive_check("scratch")


if __name__ == "__main__":
    main()
