# TweenAnimatorV2 — Code Style

Соглашения по коду для новой версии плагина. Держаться единообразно во всех файлах.

## Namespace
- Весь код — под `namespace Nano3.TweenAnimator` (для исключения конфликтов нейминга).

## Именование
- **Методы** — с большой буквы (PascalCase): `PlayAnimation()`, `Apply()`.
- **Публичные** поля/свойства — с большой буквы (PascalCase): `public float Duration`, `public TweenState State`.
- **Приватные / protected** поля — с маленькой буквы и префиксом `_`: `_duration`, `_state`, `_value`.
- **Локальные переменные** внутри методов — с маленькой буквы, без подчёркивания: `float scale`, `Vector2 pos`.

## Свойства
- Использовать полную запись `get`/`set` в блоках, а НЕ expression-bodied (`=>`).
  ```csharp
  // так:
  public float Duration { get { return _duration; } }

  // не так:
  public float Duration => _duration;
  ```
- Если сеттер не нужен (конфиг-параметр только на чтение) — оставлять только `get`-блок.

## Сериализация
- Приватные поля под инспектор помечать `[SerializeField]`.
- Узлы дерева — `[Serializable]`-классы (не MonoBehaviour); дерево хранится через `[SerializeReference]`.
