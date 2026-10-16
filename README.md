# AcutStudio

Настільна гра для Windows: **симулятор розвитку міста** + **покрокові бої**.
Командний проєкт з дисципліни «Програмна інженерія. Частина 1».

Документація: [Docs](Docs): [вимоги](Docs/Requirements_Document.md), [архітектура](Docs/Architecture.md), [завдання етапів 5-6](Docs/Tasks_Stage5-6.md).

## Технології

C#, .NET 10, WPF (MVVM), SQLite (`Microsoft.Data.Sqlite`), xUnit.

## Структура

| Проєкт | Призначення |
| :--- | :--- |
| `acut` | WPF-застосунок (Views, ViewModels) |
| `Acut.Core` | Ігрова логіка |
| `Acut.Data` | SQLite: схема, каталог, репозиторії |
| `Acut.Tests` | Тести (xUnit) |

## Запуск

Потрібні Windows і [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet build acut.slnx
dotnet run --project acut
dotnet test acut.slnx
```

Збереження зберігаються в `%LocalAppData%\AcutStudio\acut.db` (створюється автоматично; щоб почати з нуля, видаліть файл).
