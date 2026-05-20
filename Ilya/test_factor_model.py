from __future__ import annotations

from collections import defaultdict
from pathlib import Path

import torch
from transformers import AutoModelForSequenceClassification, AutoTokenizer

from inference import prepare_text_for_classification


BASE_DIR = Path(__file__).resolve().parent
MODEL_DIR = BASE_DIR / "rubert_factor_classifier"
MIN_ACCEPTABLE_ACCURACY = 0.85

TEST_CASES = [
    ("исполнитель не выполнил квитирование активных сигналов", "PSYCHO"),
    ("принимающий специалист не проверил наличие несброшенных сигналов", "PSYCHO"),
    ("оператор не выполнил визуальный контроль дисплея", "PSYCHO"),
    ("дежурная смена не сверила показания датчиков с журналом", "PSYCHO"),
    ("мастер преждевременно разрешил запуск оборудования", "PSYCHO"),
    ("сотрудник поддержки закрыл инцидент без проверки восстановления", "PSYCHO"),
    ("водитель не проверил крепление груза перед отправлением", "PSYCHO"),
    ("администратор ошибочно применил настройки к рабочему серверу", "PSYCHO"),
    ("диспетчер неверно выбрал режим управления объектом", "PSYCHO"),
    ("лаборант перепутал образцы при регистрации результата", "PSYCHO"),
    ("руководитель смены не проконтролировал подготовку персонала", "PSYCHO"),
    ("исполнитель не сверил фактическую схему с оперативной документацией", "PSYCHO"),
    ("оператор перевел ключ УРОВ в рабочее положение", "PSYCHO"),
    ("оператор ввел защиту в работу без подтверждения сброса", "PSYCHO"),
    ("Ключевое нарушение: исполнитель не выполнил обязательное квитирование активных сигналов на терминале РЗА", "PSYCHO"),
    ("Приемка ячейки после работ: принимающий специалист не проверил наличие несброшенных сигналов на дисплее", "PSYCHO"),
    ("оперативный персонал действовал по бланку и не выполнил визуальный контроль терминала", "PSYCHO"),
    ("бланк переключений не содержал операции проверки", "ORG"),
    ("регламент не предусматривал контроль активных сигналов", "ORG"),
    ("отсутствовал порядок взаимодействия между подрядчиком и принимающей стороной", "ORG"),
    ("не был назначен ответственный за подтверждение готовности оборудования", "ORG"),
    ("инструкция не описывала действия при отказе канала связи", "ORG"),
    ("график обслуживания не включал проверку резервного питания", "ORG"),
    ("в договоре не было требования к контролю температуры груза", "ORG"),
    ("порядок передачи смены не содержал контрольного листа", "ORG"),
    ("в IT службе не был утвержден регламент восстановления сервиса", "ORG"),
    ("не была определена зона ответственности между подразделениями", "ORG"),
    ("план работ не включал этап согласования отключения", "ORG"),
    ("персонал не прошел обучение по новой процедуре допуска", "ORG"),
    ("бланк переключений не содержал проверку дисплея и квитирование защит", "ORG"),
    ("регламентные работы не включали обязательную сверку журнала событий", "ORG"),
    ("терминал РЗА воспринял сигнал как команду отключения", "TECH"),
    ("датчик передал некорректные данные", "TECH"),
    ("система защиты подала команду на отключение ввода", "TECH"),
    ("активные сигналы защит остались в состоянии сработало", "TECH"),
    ("контроллер не обработал сигнал блокировки из-за ошибки прошивки", "TECH"),
    ("модуль связи потерял пакеты телеметрии", "TECH"),
    ("насос работал с повышенной вибрацией подшипника", "TECH"),
    ("сервер базы данных вернул ошибку при записи журнала", "TECH"),
    ("медицинский аппарат выдал ошибку калибровки сенсора", "TECH"),
    ("привод задвижки не подтвердил конечное положение", "TECH"),
    ("блок питания перешел в режим защиты от перегрузки", "TECH"),
    ("алгоритм автоматики ошибочно активировал резервный канал", "TECH"),
    ("терминал РЗА воспринял положение ключа как команду ввода защиты", "TECH"),
    ("активный сигнал УРОВ остался несброшенным на терминале", "TECH"),
    ("сигналы МТЗ и УРОВ остались в состоянии сработало на дисплее РЗА", "TECH"),
    ("терминал Sepam воспринял ввод защиты как команду отключения", "TECH"),
    ("произошло отключение ввода номер два", "CONSEQUENCE"),
    ("объект был выведен из работы", "CONSEQUENCE"),
    ("производственная линия остановилась до окончания смены", "CONSEQUENCE"),
    ("клиенты временно потеряли доступ к сервису", "CONSEQUENCE"),
    ("питание потребителей было прервано", "CONSEQUENCE"),
    ("пациенту перенесли процедуру из-за недоступности оборудования", "CONSEQUENCE"),
    ("участок сети остался без телеметрии", "CONSEQUENCE"),
    ("поезд задержался на станции до восстановления маршрута", "CONSEQUENCE"),
    ("партия продукции была отправлена на повторную проверку", "CONSEQUENCE"),
    ("операция обслуживания была приостановлена", "CONSEQUENCE"),
    ("часть данных мониторинга была потеряна", "CONSEQUENCE"),
    ("защита отключила присоединение от сети", "CONSEQUENCE"),
    ("произошло отключение ввода номер два по сигналу УРОВ", "CONSEQUENCE"),
    ("ввод номер два отключился по активному сигналу защиты", "CONSEQUENCE"),
    ("в результате произошло аварийное отключение ввода номер два", "CONSEQUENCE"),
    ("ячейка не была введена в работу в плановый срок", "CONSEQUENCE"),
]


def get_label(model, label_id: int) -> str:
    label = model.config.id2label.get(label_id)

    if label is None:
        label = model.config.id2label.get(str(label_id), str(label_id))

    return str(label)


def predict(text: str, tokenizer, model, device) -> tuple[str, float, dict[str, float]]:
    model_text = prepare_text_for_classification(text)
    inputs = tokenizer(
        model_text,
        return_tensors="pt",
        truncation=True,
        max_length=128,
    ).to(device)

    with torch.no_grad():
        outputs = model(**inputs)
        probabilities = torch.softmax(outputs.logits, dim=1)[0]

    predicted_id = int(torch.argmax(probabilities).item())
    predicted_label = get_label(model, predicted_id)
    confidence = float(probabilities[predicted_id].item())
    all_probabilities = {
        get_label(model, label_id): float(probability.item())
        for label_id, probability in enumerate(probabilities)
    }

    return predicted_label, confidence, all_probabilities


def main() -> None:
    if not MODEL_DIR.exists():
        raise FileNotFoundError(
            "Папка rubert_factor_classifier не найдена. "
            "Сначала запустите python train_factor_model.py"
        )

    tokenizer = AutoTokenizer.from_pretrained(MODEL_DIR)
    model = AutoModelForSequenceClassification.from_pretrained(MODEL_DIR)
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    model.to(device)
    model.eval()

    correct = 0
    class_totals = defaultdict(int)
    class_correct = defaultdict(int)
    mistakes = []

    for text, expected_label in TEST_CASES:
        predicted_label, confidence, probabilities = predict(text, tokenizer, model, device)
        is_correct = predicted_label == expected_label
        correct += int(is_correct)
        class_totals[expected_label] += 1
        class_correct[expected_label] += int(is_correct)

        if not is_correct:
            mistakes.append((text, expected_label, predicted_label, confidence, probabilities))

        print("=" * 80)
        print(f"Текст: {text}")
        print(f"Ожидаемый класс: {expected_label}")
        print(f"Предсказанный класс: {predicted_label}")
        print(f"Уверенность: {confidence:.4f}")
        print(f"Результат: {'OK' if is_correct else 'ОШИБКА'}")
        print("Вероятности по всем классам:")

        for label, probability in probabilities.items():
            print(f"  {label}: {probability:.4f}")

    accuracy = correct / len(TEST_CASES)
    print("=" * 80)
    print(f"Всего тестов: {len(TEST_CASES)}")
    print(f"Верных ответов: {correct}")
    print(f"Итоговая точность: {accuracy:.4f}")
    print("Точность по классам:")

    for label in sorted(class_totals):
        label_accuracy = class_correct[label] / class_totals[label]
        print(f"  {label}: {class_correct[label]}/{class_totals[label]} = {label_accuracy:.4f}")

    if mistakes:
        print("Ошибки для ручного анализа:")
        for text, expected_label, predicted_label, confidence, probabilities in mistakes:
            print(
                f"  {text} | ожидалось {expected_label}, получено {predicted_label}, "
                f"уверенность {confidence:.4f}, вероятности {probabilities}"
            )

    if accuracy < MIN_ACCEPTABLE_ACCURACY:
        raise SystemExit(
            f"Точность ниже допустимой границы {MIN_ACCEPTABLE_ACCURACY:.2f}. "
            "Нужно расширить датасет и переобучить модель."
        )


if __name__ == "__main__":
    main()
