# Прогоны из PostgreSQL как источник данных — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Научить JSQ Viewer открывать прогоны испытаний напрямую из PostgreSQL сервиса JSQ Laboratory — исторические и идущие в данный момент — не ломая работу с папками DBF и выгруженными протоколами.

**Architecture:** Прогон из базы становится третьим типом источника наравне с папкой и `.xlsx` и кодируется строкой `jsqdb://recording/<id>` внутри существующей `FolderSpec`. За счёт этого бесплатно работают мультизагрузка до шести источников, слияние с разделением совпадающих кодов, кнопка «Обновить», список недавних источников и сохранение раскладок. Npgsql виден только внутри `Infrastructure/Database/`; прикладной слой работает через порты `IRecordingCatalog` и `IRecordingDataReader`, поэтому тестируется без сети.

**Tech Stack:** C# 7.3, .NET Framework 4.8, WinForms, Npgsql 6.0.13, MSTest 3.5.2, PostgreSQL 15.17.

**Спецификация:** `docs/superpowers/specs/2026-08-06-postgres-recording-source-design.md`

## Global Constraints

- Целевая платформа основного проекта — .NET Framework 4.8, старый формат csproj. Язык — C# 7.3: **нельзя** использовать switch-выражения, `using`-объявления, nullable reference types, целевые `new()`, интерполированные строки в константах.
- Npgsql **6.0.13** — последняя версия с таргетом `netstandard2.0`. Версия 8, лежащая в локальном кэше NuGet, собрана под `net8.0` и на 4.8 не загрузится.
- Npgsql на .NET Framework 4.8 **не работает без binding redirects**. Проверено: без них `NpgsqlConnection.Open()` падает с `System.IO.FileLoadException: не удалось загрузить файл или сборку "System.Runtime.CompilerServices.Unsafe, Version=4.0.4.1"`. Нужны три перенаправления, они приведены дословно в Задаче 1.
- Тестовый проект `JSQViewer.Tests` — SDK-формата и **сам генерирует** binding redirects. Поэтому юнит-тест никогда не поймает ошибку перенаправлений в основном приложении: проверка выполняется только запуском `JSQViewer.exe`.
- Доступ к базе — **только чтение**. Ни одного `INSERT`, `UPDATE`, `DELETE`, `CREATE`: единственный писатель базы — сервис JSQ Laboratory.
- Стиль кода: 4 пробела, фигурные скобки на своей строке, `PascalCase` для типов и публичных членов, `_camelCase` для приватных полей, явные модификаторы доступа.
- Свойства классов, сериализуемых в JSON, именуются в нижнем регистре через подчёркивание (`refresh_interval_seconds`) — так устроены существующие `ViewerSettingsModel` и `JsonHelper` на `JavaScriptSerializer`.
- Пароль базы никогда не пишется на диск в открытом виде и никогда не попадает в лог.
- Сборка: `dotnet build .\JSQViewer.csproj -c Debug`. Тесты: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`.
- Реальные параметры подключения для ручных проверок: `192.168.66.100:5432`, база `jsq_db`, пользователь `jsq_user`, пароль `<пароль>`.

## Структура файлов

**Создаются:**

| Файл | Ответственность |
| --- | --- |
| `lib/npgsql/*.dll` | Npgsql 6.0.13 и 13 транзитивных сборок, коммитятся в репозиторий ради offline-сборки |
| `App.config` | Binding redirects для Npgsql; MSBuild копирует его в `JSQViewer.exe.config` |
| `Application/Database/DatabaseConnectionSettings.cs` | Модель параметров подключения + значения по умолчанию |
| `Application/Database/RecordingSourceRef.cs` | Разбор и сборка строки `jsqdb://recording/<id>` |
| `Application/Database/RecordingSummaryItem.cs` | Строка списка прогонов для диалога выбора |
| `Application/Database/RecordingCatalogFilter.cs` | Фильтры списка прогонов |
| `Application/Database/ChannelCodeNormalizer.cs` | Срезание префикса поста из кода канала |
| `Application/Database/RecordingRowsToTestDataMapper.cs` | Разворот «длинного» результата в `TestData`; чистая функция |
| `Application/Abstractions/IDatabaseSettingsRepository.cs` | Порт хранения параметров подключения |
| `Application/Abstractions/ISecretProtector.cs` | Порт шифрования пароля |
| `Application/Workspace/Ports/IRecordingCatalog.cs` | Порт получения списка прогонов и статуса прогона |
| `Application/Workspace/Ports/IRecordingDataReader.cs` | Порт чтения рядов прогона, включая дозагрузку хвоста |
| `Infrastructure/Database/NpgsqlConnectionFactory.cs` | Единственное место, знающее про Npgsql; строит строку подключения |
| `Infrastructure/Database/PostgresRecordingCatalog.cs` | Реализация `IRecordingCatalog` |
| `Infrastructure/Database/PostgresRecordingDataSourceReader.cs` | Реализация `IRecordingDataReader` и `ITestDataSourceReader` |
| `Infrastructure/Database/RecordingCatalogQueryBuilder.cs` | Сборка SQL списка прогонов по фильтрам; чистая функция |
| `Infrastructure/Platform/DpapiSecretProtector.cs` | Реализация `ISecretProtector` через DPAPI |
| `Infrastructure/Persistence/FileDatabaseSettingsRepository.cs` | Чтение и запись `database_settings.json` |
| `UI/DatabaseSettingsDialog.cs` | Диалог параметров подключения с кнопкой «Проверить подключение» |
| `UI/OpenFromDatabaseForm.cs` | Диалог выбора прогонов с фильтрами |
| `doc/database_source_manual_checklist.md` | Чек-лист ручной проверки на живой базе |

**Изменяются:**

| Файл | Что меняется |
| --- | --- |
| `JSQViewer.csproj` | Ссылки на сборки Npgsql, `App.config` |
| `Program.cs` | Композиция новых компонентов, ключ командной строки `--dbcheck` |
| `Application/Workspace/WorkspaceLoadOrchestrationService.cs` | `IsValidSpec` и `ResolveSelectedFolderSource` понимают `jsqdb://` |
| `Application/Workspace/UseCases/LoadWorkspaceDataUseCase.cs` | Третья ветка по типу источника |
| `Infrastructure/Composition/WorkspaceLoadingComposition.cs` | Передача читателя базы в use case |
| `Infrastructure/Platform/DictionaryLocalizationService.cs` | Русские и английские строки нового интерфейса |
| `UI/MainForm.cs` | Кнопка «Из БД…», таймер автообновления, вызов диалога настроек |
| `installer/JSQViewer.iss` | Поставка сборок Npgsql и `JSQViewer.exe.config` |
| `Properties/AssemblyInfo.cs` | Версия 0.4.0 |

## Порядок выполнения

Задача 1 снимает главный технический риск и обязана быть выполнена первой. Задачи 2–5 независимы друг от друга и могут выполняться параллельно. Задача 6 требует 3, 4 и 5. Задача 7 требует 2, 5 и 6. Задача 8 требует 7. Задача 9 — последняя.

---

### Task 1: Npgsql в сборке приложения и диагностика подключения

Задача снимает риск binding redirects: по её завершении команда `JSQViewer.exe --dbcheck` реально подключается к базе из настоящего приложения, а не из тестового проекта.

**Files:**
- Create: `lib/npgsql/` (14 файлов `.dll`)
- Create: `App.config`
- Create: `Application/Database/DatabaseConnectionSettings.cs`
- Create: `Infrastructure/Database/NpgsqlConnectionFactory.cs`
- Modify: `JSQViewer.csproj` (блок `<ItemGroup>` со ссылками, строки 36–49)
- Modify: `Program.cs` (метод `Main`, строка 34)
- Test: `JSQViewer.Tests/DatabaseConnectionSettingsTests.cs`

**Interfaces:**
- Consumes: ничего.
- Produces:
  - `JSQViewer.Application.Database.DatabaseConnectionSettings` со свойствами `string host`, `int port`, `string database`, `string username`, `string password_protected`, `int refresh_interval_seconds`, `int connect_timeout_seconds`, `int command_timeout_seconds` и статическим методом `DatabaseConnectionSettings CreateDefault()`.
  - `JSQViewer.Infrastructure.Database.NpgsqlConnectionFactory` с конструктором `NpgsqlConnectionFactory()`, методом `string BuildConnectionString(DatabaseConnectionSettings settings, string password)`, методом `NpgsqlConnection Create(DatabaseConnectionSettings settings, string password)` и методом `string TestConnection(DatabaseConnectionSettings settings, string password)`, возвращающим `null` при успехе и текст ошибки при неудаче.

- [ ] **Step 1: Получить сборки Npgsql**

Во временной папке (не в репозитории) создать проект-заготовку и собрать его, чтобы NuGet разложил нужный набор сборок:

```bash
mkdir /tmp/npgprobe && cd /tmp/npgprobe
cat > probe.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <OutputType>Library</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql" Version="6.0.13" />
  </ItemGroup>
</Project>
EOF
dotnet build
```

Скопировать в `lib/npgsql/` ровно эти 14 файлов из `bin/Debug/net48/` (файлы `probe.*` не копировать):

```
Microsoft.Bcl.AsyncInterfaces.dll
Microsoft.Bcl.HashCode.dll
Npgsql.dll
System.Buffers.dll
System.Collections.Immutable.dll
System.Diagnostics.DiagnosticSource.dll
System.Memory.dll
System.Numerics.Vectors.dll
System.Runtime.CompilerServices.Unsafe.dll
System.Text.Encodings.Web.dll
System.Text.Json.dll
System.Threading.Channels.dll
System.Threading.Tasks.Extensions.dll
System.ValueTuple.dll
```

- [ ] **Step 2: Прописать ссылки в JSQViewer.csproj**

В существующий `<ItemGroup>` со ссылками (там, где `<Reference Include="System" />`) добавить:

```xml
    <Reference Include="Npgsql">
      <HintPath>lib\npgsql\Npgsql.dll</HintPath>
      <Private>True</Private>
    </Reference>
    <Reference Include="Microsoft.Bcl.AsyncInterfaces">
      <HintPath>lib\npgsql\Microsoft.Bcl.AsyncInterfaces.dll</HintPath>
      <Private>True</Private>
    </Reference>
    <Reference Include="Microsoft.Bcl.HashCode">
      <HintPath>lib\npgsql\Microsoft.Bcl.HashCode.dll</HintPath>
      <Private>True</Private>
    </Reference>
    <Reference Include="System.Buffers">
      <HintPath>lib\npgsql\System.Buffers.dll</HintPath>
      <Private>True</Private>
    </Reference>
    <Reference Include="System.Collections.Immutable">
      <HintPath>lib\npgsql\System.Collections.Immutable.dll</HintPath>
      <Private>True</Private>
    </Reference>
    <Reference Include="System.Diagnostics.DiagnosticSource">
      <HintPath>lib\npgsql\System.Diagnostics.DiagnosticSource.dll</HintPath>
      <Private>True</Private>
    </Reference>
    <Reference Include="System.Memory">
      <HintPath>lib\npgsql\System.Memory.dll</HintPath>
      <Private>True</Private>
    </Reference>
    <Reference Include="System.Numerics.Vectors">
      <HintPath>lib\npgsql\System.Numerics.Vectors.dll</HintPath>
      <Private>True</Private>
    </Reference>
    <Reference Include="System.Runtime.CompilerServices.Unsafe">
      <HintPath>lib\npgsql\System.Runtime.CompilerServices.Unsafe.dll</HintPath>
      <Private>True</Private>
    </Reference>
    <Reference Include="System.Text.Encodings.Web">
      <HintPath>lib\npgsql\System.Text.Encodings.Web.dll</HintPath>
      <Private>True</Private>
    </Reference>
    <Reference Include="System.Text.Json">
      <HintPath>lib\npgsql\System.Text.Json.dll</HintPath>
      <Private>True</Private>
    </Reference>
    <Reference Include="System.Threading.Channels">
      <HintPath>lib\npgsql\System.Threading.Channels.dll</HintPath>
      <Private>True</Private>
    </Reference>
    <Reference Include="System.Threading.Tasks.Extensions">
      <HintPath>lib\npgsql\System.Threading.Tasks.Extensions.dll</HintPath>
      <Private>True</Private>
    </Reference>
    <Reference Include="System.ValueTuple">
      <HintPath>lib\npgsql\System.ValueTuple.dll</HintPath>
      <Private>True</Private>
    </Reference>
```

В `<ItemGroup>` с `<Compile Include=...>` добавить строку, чтобы `App.config` попадал в вывод как `JSQViewer.exe.config`:

```xml
    <None Include="App.config" />
```

- [ ] **Step 3: Создать App.config**

Создать `App.config` в корне проекта ровно с этим содержимым. Значения версий взяты из проверенной сборки Npgsql 6.0.13 — менять их нельзя:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <startup>
    <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" />
  </startup>
  <runtime>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="System.Buffers" publicKeyToken="cc7b13ffcd2ddd51" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-4.0.3.0" newVersion="4.0.3.0" />
      </dependentAssembly>
      <dependentAssembly>
        <assemblyIdentity name="System.Runtime.CompilerServices.Unsafe" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-6.0.0.0" newVersion="6.0.0.0" />
      </dependentAssembly>
      <dependentAssembly>
        <assemblyIdentity name="System.Text.Json" publicKeyToken="cc7b13ffcd2ddd51" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-6.0.0.10" newVersion="6.0.0.10" />
      </dependentAssembly>
    </assemblyBinding>
  </runtime>
</configuration>
```

- [ ] **Step 4: Написать падающий тест на параметры подключения**

Создать `JSQViewer.Tests/DatabaseConnectionSettingsTests.cs`:

```csharp
using JSQViewer.Application.Database;
using JSQViewer.Infrastructure.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class DatabaseConnectionSettingsTests
    {
        [TestMethod]
        public void CreateDefault_UsesLaboratoryServer()
        {
            DatabaseConnectionSettings settings = DatabaseConnectionSettings.CreateDefault();

            Assert.AreEqual("192.168.66.100", settings.host);
            Assert.AreEqual(5432, settings.port);
            Assert.AreEqual("jsq_db", settings.database);
            Assert.AreEqual("jsq_user", settings.username);
            Assert.AreEqual(30, settings.refresh_interval_seconds);
            Assert.AreEqual(10, settings.connect_timeout_seconds);
            Assert.AreEqual(120, settings.command_timeout_seconds);
        }

        [TestMethod]
        public void BuildConnectionString_IncludesTimeoutsAndCredentials()
        {
            DatabaseConnectionSettings settings = DatabaseConnectionSettings.CreateDefault();
            var factory = new NpgsqlConnectionFactory();

            string connectionString = factory.BuildConnectionString(settings, "secret");

            StringAssert.Contains(connectionString, "Host=192.168.66.100");
            StringAssert.Contains(connectionString, "Port=5432");
            StringAssert.Contains(connectionString, "Database=jsq_db");
            StringAssert.Contains(connectionString, "Username=jsq_user");
            StringAssert.Contains(connectionString, "Password=secret");
            StringAssert.Contains(connectionString, "Timeout=10");
            StringAssert.Contains(connectionString, "Command Timeout=120");
        }
    }
}
```

- [ ] **Step 5: Убедиться, что тест падает**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter DatabaseConnectionSettingsTests`

Ожидается: ошибка компиляции — типы `DatabaseConnectionSettings` и `NpgsqlConnectionFactory` не существуют.

- [ ] **Step 6: Реализовать DatabaseConnectionSettings**

Создать `Application/Database/DatabaseConnectionSettings.cs`:

```csharp
namespace JSQViewer.Application.Database
{
    public sealed class DatabaseConnectionSettings
    {
        public string host { get; set; }
        public int port { get; set; }
        public string database { get; set; }
        public string username { get; set; }
        public string password_protected { get; set; }
        public int refresh_interval_seconds { get; set; }
        public int connect_timeout_seconds { get; set; }
        public int command_timeout_seconds { get; set; }

        public static DatabaseConnectionSettings CreateDefault()
        {
            return new DatabaseConnectionSettings
            {
                host = "192.168.66.100",
                port = 5432,
                database = "jsq_db",
                username = "jsq_user",
                password_protected = string.Empty,
                refresh_interval_seconds = 30,
                connect_timeout_seconds = 10,
                command_timeout_seconds = 120
            };
        }
    }
}
```

- [ ] **Step 7: Реализовать NpgsqlConnectionFactory**

Создать `Infrastructure/Database/NpgsqlConnectionFactory.cs`:

```csharp
using System;
using JSQViewer.Application.Database;
using Npgsql;

namespace JSQViewer.Infrastructure.Database
{
    public sealed class NpgsqlConnectionFactory
    {
        public string BuildConnectionString(DatabaseConnectionSettings settings, string password)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = settings.host ?? string.Empty,
                Port = settings.port,
                Database = settings.database ?? string.Empty,
                Username = settings.username ?? string.Empty,
                Password = password ?? string.Empty,
                Timeout = settings.connect_timeout_seconds,
                CommandTimeout = settings.command_timeout_seconds,
                ApplicationName = "JSQViewer"
            };

            return builder.ToString();
        }

        public NpgsqlConnection Create(DatabaseConnectionSettings settings, string password)
        {
            return new NpgsqlConnection(BuildConnectionString(settings, password));
        }

        public string TestConnection(DatabaseConnectionSettings settings, string password)
        {
            try
            {
                using (NpgsqlConnection connection = Create(settings, password))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT 1", connection))
                    {
                        command.ExecuteScalar();
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
```

- [ ] **Step 8: Убедиться, что тест проходит**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter DatabaseConnectionSettingsTests`

Ожидается: 2 теста пройдены.

- [ ] **Step 9: Добавить ключ --dbcheck в Program.cs**

Этот ключ — единственный способ проверить binding redirects в настоящем приложении, потому что тестовый проект генерирует свои перенаправления и ошибку не воспроизводит.

В `Program.cs` заменить сигнатуру `private static void Main()` на `private static void Main(string[] args)` и первой строкой тела метода вставить:

```csharp
            if (args != null && args.Length > 0 && string.Equals(args[0], "--dbcheck", StringComparison.OrdinalIgnoreCase))
            {
                RunDatabaseCheck();
                return;
            }
```

Добавить в класс `Program` метод:

```csharp
        private static void RunDatabaseCheck()
        {
            DatabaseConnectionSettings settings = DatabaseConnectionSettings.CreateDefault();
            string password = Environment.GetEnvironmentVariable("JSQ_DB_PASSWORD") ?? string.Empty;
            string error = new NpgsqlConnectionFactory().TestConnection(settings, password);
            string message = error == null
                ? "Подключение выполнено: " + settings.host + ":" + settings.port + "/" + settings.database
                : "Не удалось подключиться: " + error;
            MessageBox.Show(message, "JSQViewer --dbcheck", MessageBoxButtons.OK,
                error == null ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }
```

Добавить в начало файла `using JSQViewer.Application.Database;` и `using JSQViewer.Infrastructure.Database;`.

- [ ] **Step 10: Проверить подключение из настоящего приложения**

Выполнить:

```powershell
dotnet build .\JSQViewer.csproj -c Debug
$env:JSQ_DB_PASSWORD = "<пароль от jsq_user>"
.\bin\Debug\JSQViewer.exe --dbcheck
```

Ожидается: окно с текстом «Подключение выполнено: 192.168.66.100:5432/jsq_db».

Если вместо этого появилось сообщение с `FileLoadException` и упоминанием `System.Runtime.CompilerServices.Unsafe` — значит `App.config` не попал в вывод. Проверить наличие `bin\Debug\JSQViewer.exe.config` и строку `<None Include="App.config" />` в csproj.

Отдельно убедиться, что рядом с `JSQViewer.exe` лежат все 14 сборок из `lib/npgsql`.

- [ ] **Step 11: Коммит**

```bash
git add lib/npgsql App.config JSQViewer.csproj Program.cs Application/Database/DatabaseConnectionSettings.cs Infrastructure/Database/NpgsqlConnectionFactory.cs JSQViewer.Tests/DatabaseConnectionSettingsTests.cs
git commit -m "Подключён Npgsql 6.0.13 и добавлена диагностика --dbcheck"
```

---

### Task 2: Хранение параметров подключения с шифрованием пароля

**Files:**
- Create: `Application/Abstractions/ISecretProtector.cs`
- Create: `Application/Abstractions/IDatabaseSettingsRepository.cs`
- Create: `Infrastructure/Platform/DpapiSecretProtector.cs`
- Create: `Infrastructure/Persistence/FileDatabaseSettingsRepository.cs`
- Test: `JSQViewer.Tests/DatabaseSettingsRepositoryTests.cs`

**Interfaces:**
- Consumes: `DatabaseConnectionSettings` из Задачи 1.
- Produces:
  - `JSQViewer.Application.Abstractions.ISecretProtector` с методами `string Protect(string plainText)` и `string Unprotect(string protectedText)`.
  - `JSQViewer.Application.Abstractions.IDatabaseSettingsRepository` с методами `DatabaseConnectionSettings Load()`, `bool Save(DatabaseConnectionSettings settings)`, `string LoadPassword()`, `bool SavePassword(DatabaseConnectionSettings settings, string password)`.
  - `JSQViewer.Infrastructure.Persistence.FileDatabaseSettingsRepository` с конструктором `FileDatabaseSettingsRepository(IAppPaths appPaths, ISecretProtector secretProtector)`.
  - `JSQViewer.Infrastructure.Platform.DpapiSecretProtector` с конструктором без параметров.

- [ ] **Step 1: Написать падающие тесты**

Создать `JSQViewer.Tests/DatabaseSettingsRepositoryTests.cs`:

```csharp
using System;
using System.IO;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Database;
using JSQViewer.Application.Exporting;
using JSQViewer.Infrastructure.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class DatabaseSettingsRepositoryTests
    {
        private sealed class ReversingProtector : ISecretProtector
        {
            public string Protect(string plainText)
            {
                if (string.IsNullOrEmpty(plainText)) return string.Empty;
                char[] chars = plainText.ToCharArray();
                Array.Reverse(chars);
                return new string(chars);
            }

            public string Unprotect(string protectedText)
            {
                return Protect(protectedText);
            }
        }

        private sealed class FailingProtector : ISecretProtector
        {
            public string Protect(string plainText)
            {
                return "broken";
            }

            public string Unprotect(string protectedText)
            {
                throw new InvalidOperationException("Ciphertext from another user.");
            }
        }

        private sealed class TempPaths : IAppPaths, IDisposable
        {
            public TempPaths()
            {
                ApplicationBaseDirectory = Path.Combine(Path.GetTempPath(), "jsq_db_settings_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(ApplicationBaseDirectory);
            }

            public string ApplicationBaseDirectory { get; }

            public string ProjectRoot
            {
                get { return ApplicationBaseDirectory; }
            }

            public string LogDirectory
            {
                get { return Path.Combine(ApplicationBaseDirectory, "log"); }
            }

            public string GetProtocolTemplatePath(ProtocolTemplateMode mode)
            {
                return Path.Combine(ApplicationBaseDirectory, "template.xlsx");
            }

            public void Dispose()
            {
                try { Directory.Delete(ApplicationBaseDirectory, true); } catch { }
            }
        }

        [TestMethod]
        public void Load_WithoutFile_ReturnsDefaults()
        {
            using (var paths = new TempPaths())
            {
                var repository = new FileDatabaseSettingsRepository(paths, new ReversingProtector());

                DatabaseConnectionSettings settings = repository.Load();

                Assert.AreEqual("192.168.66.100", settings.host);
                Assert.AreEqual("jsq_db", settings.database);
                Assert.AreEqual(string.Empty, repository.LoadPassword());
            }
        }

        [TestMethod]
        public void SavePassword_RoundTripsThroughProtector()
        {
            using (var paths = new TempPaths())
            {
                var repository = new FileDatabaseSettingsRepository(paths, new ReversingProtector());
                DatabaseConnectionSettings settings = DatabaseConnectionSettings.CreateDefault();
                settings.host = "10.0.0.5";

                Assert.IsTrue(repository.SavePassword(settings, "s3cr3t-test-value"));

                var reopened = new FileDatabaseSettingsRepository(paths, new ReversingProtector());
                Assert.AreEqual("10.0.0.5", reopened.Load().host);
                Assert.AreEqual("s3cr3t-test-value", reopened.LoadPassword());
            }
        }

        [TestMethod]
        public void SavePassword_DoesNotWritePlainTextToDisk()
        {
            using (var paths = new TempPaths())
            {
                var repository = new FileDatabaseSettingsRepository(paths, new ReversingProtector());

                repository.SavePassword(DatabaseConnectionSettings.CreateDefault(), "s3cr3t-test-value");

                string json = File.ReadAllText(Path.Combine(paths.ProjectRoot, "database_settings.json"));
                Assert.IsFalse(json.Contains("s3cr3t-test-value"), "Пароль не должен попадать на диск в открытом виде.");
            }
        }

        [TestMethod]
        public void LoadPassword_WhenDecryptionFails_ReturnsEmptyInsteadOfThrowing()
        {
            using (var paths = new TempPaths())
            {
                new FileDatabaseSettingsRepository(paths, new ReversingProtector())
                    .SavePassword(DatabaseConnectionSettings.CreateDefault(), "s3cr3t-test-value");

                var repository = new FileDatabaseSettingsRepository(paths, new FailingProtector());

                Assert.AreEqual(string.Empty, repository.LoadPassword());
            }
        }
    }
}
```

- [ ] **Step 2: Убедиться, что тесты падают**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter DatabaseSettingsRepositoryTests`

Ожидается: ошибка компиляции — типы `ISecretProtector` и `FileDatabaseSettingsRepository` не существуют.

- [ ] **Step 3: Создать порты**

Создать `Application/Abstractions/ISecretProtector.cs`:

```csharp
namespace JSQViewer.Application.Abstractions
{
    public interface ISecretProtector
    {
        string Protect(string plainText);

        string Unprotect(string protectedText);
    }
}
```

Создать `Application/Abstractions/IDatabaseSettingsRepository.cs`:

```csharp
using JSQViewer.Application.Database;

namespace JSQViewer.Application.Abstractions
{
    public interface IDatabaseSettingsRepository
    {
        DatabaseConnectionSettings Load();

        bool Save(DatabaseConnectionSettings settings);

        string LoadPassword();

        bool SavePassword(DatabaseConnectionSettings settings, string password);
    }
}
```

- [ ] **Step 4: Реализовать репозиторий**

Создать `Infrastructure/Persistence/FileDatabaseSettingsRepository.cs`:

```csharp
using System;
using System.IO;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Database;
using JSQViewer.Settings;

namespace JSQViewer.Infrastructure.Persistence
{
    public sealed class FileDatabaseSettingsRepository : IDatabaseSettingsRepository
    {
        private readonly string _filePath;
        private readonly ISecretProtector _secretProtector;

        public FileDatabaseSettingsRepository(IAppPaths appPaths, ISecretProtector secretProtector)
        {
            if (appPaths == null) throw new ArgumentNullException(nameof(appPaths));

            _secretProtector = secretProtector ?? throw new ArgumentNullException(nameof(secretProtector));
            _filePath = Path.Combine(appPaths.ProjectRoot, "database_settings.json");
        }

        public DatabaseConnectionSettings Load()
        {
            DatabaseConnectionSettings settings = JsonHelper.LoadFromFile(_filePath, DatabaseConnectionSettings.CreateDefault());
            return Sanitize(settings);
        }

        public bool Save(DatabaseConnectionSettings settings)
        {
            return JsonHelper.SaveToFile(_filePath, Sanitize(settings));
        }

        public string LoadPassword()
        {
            string protectedPassword = Load().password_protected;
            if (string.IsNullOrEmpty(protectedPassword))
            {
                return string.Empty;
            }

            try
            {
                return _secretProtector.Unprotect(protectedPassword) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public bool SavePassword(DatabaseConnectionSettings settings, string password)
        {
            DatabaseConnectionSettings sanitized = Sanitize(settings);
            sanitized.password_protected = string.IsNullOrEmpty(password)
                ? string.Empty
                : _secretProtector.Protect(password);
            return JsonHelper.SaveToFile(_filePath, sanitized);
        }

        private static DatabaseConnectionSettings Sanitize(DatabaseConnectionSettings settings)
        {
            DatabaseConnectionSettings defaults = DatabaseConnectionSettings.CreateDefault();
            if (settings == null)
            {
                return defaults;
            }

            if (string.IsNullOrWhiteSpace(settings.host)) settings.host = defaults.host;
            if (string.IsNullOrWhiteSpace(settings.database)) settings.database = defaults.database;
            if (string.IsNullOrWhiteSpace(settings.username)) settings.username = defaults.username;
            if (settings.password_protected == null) settings.password_protected = string.Empty;
            if (settings.port <= 0 || settings.port > 65535) settings.port = defaults.port;
            if (settings.refresh_interval_seconds < 5) settings.refresh_interval_seconds = defaults.refresh_interval_seconds;
            if (settings.connect_timeout_seconds <= 0) settings.connect_timeout_seconds = defaults.connect_timeout_seconds;
            if (settings.command_timeout_seconds <= 0) settings.command_timeout_seconds = defaults.command_timeout_seconds;
            return settings;
        }
    }
}
```

- [ ] **Step 5: Реализовать DPAPI-шифрование**

Создать `Infrastructure/Platform/DpapiSecretProtector.cs`:

```csharp
using System;
using System.Security.Cryptography;
using System.Text;
using JSQViewer.Application.Abstractions;

namespace JSQViewer.Infrastructure.Platform
{
    public sealed class DpapiSecretProtector : ISecretProtector
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("JSQViewer.DatabaseConnection.v1");

        public string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }

            byte[] encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(plainText), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        public string Unprotect(string protectedText)
        {
            if (string.IsNullOrEmpty(protectedText))
            {
                return string.Empty;
            }

            byte[] decrypted = ProtectedData.Unprotect(
                Convert.FromBase64String(protectedText), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
    }
}
```

В `JSQViewer.csproj` в `<ItemGroup>` со ссылками добавить `<Reference Include="System.Security" />` — там живёт `ProtectedData`.

- [ ] **Step 6: Убедиться, что тесты проходят**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter DatabaseSettingsRepositoryTests`

Ожидается: 4 теста пройдены.

- [ ] **Step 7: Добавить database_settings.json в .gitignore**

Дописать в `.gitignore` строку `database_settings.json` — файл содержит зашифрованный пароль конкретного пользователя и в репозитории не нужен.

- [ ] **Step 8: Коммит**

```bash
git add Application/Abstractions/ISecretProtector.cs Application/Abstractions/IDatabaseSettingsRepository.cs Infrastructure/Platform/DpapiSecretProtector.cs Infrastructure/Persistence/FileDatabaseSettingsRepository.cs JSQViewer.Tests/DatabaseSettingsRepositoryTests.cs JSQViewer.csproj .gitignore
git commit -m "Добавлено хранение параметров подключения к БД с шифрованием пароля"
```

---

### Task 3: Ссылка на прогон jsqdb://recording/&lt;id&gt;

**Files:**
- Create: `Application/Database/RecordingSourceRef.cs`
- Test: `JSQViewer.Tests/RecordingSourceRefTests.cs`

**Interfaces:**
- Consumes: ничего.
- Produces: `JSQViewer.Application.Database.RecordingSourceRef` со статическими членами `const string Scheme = "jsqdb://recording/"`, `bool IsRecordingSource(string source)`, `string Build(string recordingId)`, `bool TryParse(string source, out string recordingId)`.

- [ ] **Step 1: Написать падающий тест**

Создать `JSQViewer.Tests/RecordingSourceRefTests.cs`:

```csharp
using JSQViewer.Application.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class RecordingSourceRefTests
    {
        [TestMethod]
        public void Build_ProducesCanonicalSource()
        {
            Assert.AreEqual("jsqdb://recording/21edc519ba594f94", RecordingSourceRef.Build("21edc519ba594f94"));
        }

        [TestMethod]
        public void TryParse_ExtractsRecordingId()
        {
            string recordingId;

            Assert.IsTrue(RecordingSourceRef.TryParse("jsqdb://recording/21edc519ba594f94", out recordingId));
            Assert.AreEqual("21edc519ba594f94", recordingId);
        }

        [TestMethod]
        public void TryParse_IgnoresSurroundingWhitespaceAndSchemeCase()
        {
            string recordingId;

            Assert.IsTrue(RecordingSourceRef.TryParse("  JSQDB://RECORDING/abc123  ", out recordingId));
            Assert.AreEqual("abc123", recordingId);
        }

        [TestMethod]
        public void TryParse_RejectsFoldersProtocolsAndEmptyIds()
        {
            string recordingId;

            Assert.IsFalse(RecordingSourceRef.TryParse(@"C:\data\test", out recordingId));
            Assert.IsFalse(RecordingSourceRef.TryParse(@"C:\data\protocol.xlsx", out recordingId));
            Assert.IsFalse(RecordingSourceRef.TryParse("jsqdb://recording/", out recordingId));
            Assert.IsFalse(RecordingSourceRef.TryParse(null, out recordingId));
            Assert.IsFalse(RecordingSourceRef.TryParse("jsqdb://session/abc", out recordingId));
        }

        [TestMethod]
        public void IsRecordingSource_MatchesTryParse()
        {
            Assert.IsTrue(RecordingSourceRef.IsRecordingSource("jsqdb://recording/abc"));
            Assert.IsFalse(RecordingSourceRef.IsRecordingSource(@"C:\data\test"));
        }
    }
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter RecordingSourceRefTests`

Ожидается: ошибка компиляции — тип `RecordingSourceRef` не существует.

- [ ] **Step 3: Реализовать**

Создать `Application/Database/RecordingSourceRef.cs`:

```csharp
using System;

namespace JSQViewer.Application.Database
{
    public static class RecordingSourceRef
    {
        public const string Scheme = "jsqdb://recording/";

        public static string Build(string recordingId)
        {
            return Scheme + (recordingId ?? string.Empty).Trim();
        }

        public static bool IsRecordingSource(string source)
        {
            string recordingId;
            return TryParse(source, out recordingId);
        }

        public static bool TryParse(string source, out string recordingId)
        {
            recordingId = null;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            string trimmed = source.Trim().Trim('"');
            if (!trimmed.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string id = trimmed.Substring(Scheme.Length).Trim();
            if (id.Length == 0)
            {
                return false;
            }

            recordingId = id;
            return true;
        }
    }
}
```

- [ ] **Step 4: Убедиться, что тест проходит**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter RecordingSourceRefTests`

Ожидается: 5 тестов пройдены.

- [ ] **Step 5: Коммит**

```bash
git add Application/Database/RecordingSourceRef.cs JSQViewer.Tests/RecordingSourceRefTests.cs
git commit -m "Добавлена ссылка на прогон jsqdb://recording"
```

---

### Task 4: Преобразование строк агрегатов в TestData

Ядро задачи: развернуть «длинный» результат SQL (канал, время, значение) в «широкую» структуру `TestData`, которой пользуется всё приложение.

**Files:**
- Create: `Application/Database/ChannelCodeNormalizer.cs`
- Create: `Application/Database/RecordingRowsToTestDataMapper.cs`
- Test: `JSQViewer.Tests/RecordingRowsToTestDataMapperTests.cs`

**Interfaces:**
- Consumes: `Core.TestData`, `Core.ChannelInfo`.
- Produces:
  - `JSQViewer.Application.Database.ChannelCodeNormalizer` со статическим методом `string StripPostPrefix(string channelId, string postId)`.
  - `JSQViewer.Application.Database.RecordingAggregateRow` — класс со свойствами `string ChannelId`, `long TimestampMs`, `double Value`.
  - `JSQViewer.Application.Database.RecordingRowsToTestDataMapper` с методами:
    - `TestData Map(string source, string postId, IList<RecordingAggregateRow> rows, IDictionary<string, ChannelInfo> channels, IDictionary<string, string> metadata)`
    - `TestData Append(TestData existing, string postId, IList<RecordingAggregateRow> rows)`
    - `long GetLastTimestampMs(TestData data)`

- [ ] **Step 1: Написать падающие тесты**

Создать `JSQViewer.Tests/RecordingRowsToTestDataMapperTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using JSQViewer.Application.Database;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class RecordingRowsToTestDataMapperTests
    {
        private const string Source = "jsqdb://recording/abc";

        private static RecordingAggregateRow Row(string channelId, long timestampMs, double value)
        {
            return new RecordingAggregateRow { ChannelId = channelId, TimestampMs = timestampMs, Value = value };
        }

        private static Dictionary<string, ChannelInfo> Channels()
        {
            return new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase)
            {
                { "T1", new ChannelInfo { Code = "T1", Name = "В морозилке", Unit = "C" } },
                { "Pe", new ChannelInfo { Code = "Pe", Name = "Давление всасывания", Unit = "bar" } }
            };
        }

        [TestMethod]
        public void StripPostPrefix_RemovesOnlyLeadingPostId()
        {
            Assert.AreEqual("T1", ChannelCodeNormalizer.StripPostPrefix("B-T1", "B"));
            Assert.AreEqual("MaxI", ChannelCodeNormalizer.StripPostPrefix("A-MaxI", "A"));
            Assert.AreEqual("T-avg", ChannelCodeNormalizer.StripPostPrefix("C-T-avg", "C"));
            Assert.AreEqual("T1", ChannelCodeNormalizer.StripPostPrefix("T1", "B"));
            Assert.AreEqual("A-T1", ChannelCodeNormalizer.StripPostPrefix("A-T1", "B"));
        }

        [TestMethod]
        public void Map_BuildsWideTableSortedByTimestamp()
        {
            var rows = new List<RecordingAggregateRow>
            {
                Row("B-T1", 2000, 10.5), Row("B-Pe", 2000, 1.4),
                Row("B-T1", 1000, 10.0), Row("B-Pe", 1000, 1.3)
            };

            TestData data = new RecordingRowsToTestDataMapper().Map(
                Source, "B", rows, Channels(), new Dictionary<string, string>());

            Assert.AreEqual(Source, data.Root);
            Assert.AreEqual(2, data.RowCount);
            CollectionAssert.AreEqual(new long[] { 1000, 2000 }, data.TimestampsMs);
            CollectionAssert.AreEqual(new double?[] { 10.0, 10.5 }, data.Columns["T1"]);
            CollectionAssert.AreEqual(new double?[] { 1.3, 1.4 }, data.Columns["Pe"]);
        }

        [TestMethod]
        public void Map_LeavesGapsForWindowsMissingFromResult()
        {
            var rows = new List<RecordingAggregateRow>
            {
                Row("B-T1", 1000, 10.0),
                Row("B-Pe", 1000, 1.3),
                Row("B-Pe", 2000, 1.4)
            };

            TestData data = new RecordingRowsToTestDataMapper().Map(
                Source, "B", rows, Channels(), new Dictionary<string, string>());

            Assert.AreEqual(2, data.RowCount);
            CollectionAssert.AreEqual(new double?[] { 10.0, null }, data.Columns["T1"]);
            CollectionAssert.AreEqual(new double?[] { 1.3, 1.4 }, data.Columns["Pe"]);
        }

        [TestMethod]
        public void Map_FillsSourceBoundsAndCodeSources()
        {
            var rows = new List<RecordingAggregateRow> { Row("B-T1", 1000, 10.0), Row("B-T1", 5000, 11.0) };

            TestData data = new RecordingRowsToTestDataMapper().Map(
                Source, "B", rows, Channels(), new Dictionary<string, string> { { "Модель", "LIDER" } });

            Assert.AreEqual(1000L, data.SourceStartMs[Source]);
            Assert.AreEqual(5000L, data.SourceEndMs[Source]);
            Assert.AreEqual(Source, data.CodeSources["T1"]);
            Assert.AreEqual("LIDER", data.Meta["Модель"]);
            CollectionAssert.Contains(data.SourceColumns[Source], "T1");
        }

        [TestMethod]
        public void Map_WithoutRows_ProducesEmptyButValidTestData()
        {
            TestData data = new RecordingRowsToTestDataMapper().Map(
                Source, "B", new List<RecordingAggregateRow>(), Channels(), new Dictionary<string, string>());

            Assert.AreEqual(0, data.RowCount);
            Assert.AreEqual(0, data.TimestampsMs.Length);
            Assert.AreEqual(0L, data.SourceStartMs[Source]);
        }

        [TestMethod]
        public void GetLastTimestampMs_ReturnsTailOrMinusOneWhenEmpty()
        {
            var mapper = new RecordingRowsToTestDataMapper();
            TestData filled = mapper.Map(Source, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 1000, 10.0), Row("B-T1", 4000, 12.0) },
                Channels(), new Dictionary<string, string>());
            TestData empty = mapper.Map(Source, "B",
                new List<RecordingAggregateRow>(), Channels(), new Dictionary<string, string>());

            Assert.AreEqual(4000L, mapper.GetLastTimestampMs(filled));
            Assert.AreEqual(-1L, mapper.GetLastTimestampMs(empty));
        }

        [TestMethod]
        public void Append_AddsNewWindowsToTail()
        {
            var mapper = new RecordingRowsToTestDataMapper();
            TestData data = mapper.Map(Source, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 1000, 10.0), Row("B-Pe", 1000, 1.3) },
                Channels(), new Dictionary<string, string>());

            TestData appended = mapper.Append(data, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 2000, 10.5), Row("B-Pe", 2000, 1.4) });

            Assert.AreEqual(2, appended.RowCount);
            CollectionAssert.AreEqual(new long[] { 1000, 2000 }, appended.TimestampsMs);
            CollectionAssert.AreEqual(new double?[] { 10.0, 10.5 }, appended.Columns["T1"]);
            Assert.AreEqual(2000L, appended.SourceEndMs[Source]);
        }

        [TestMethod]
        public void Append_IgnoresWindowsAlreadyLoaded()
        {
            var mapper = new RecordingRowsToTestDataMapper();
            TestData data = mapper.Map(Source, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 1000, 10.0), Row("B-T1", 2000, 10.5) },
                Channels(), new Dictionary<string, string>());

            TestData appended = mapper.Append(data, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 2000, 99.9), Row("B-T1", 3000, 11.0) });

            Assert.AreEqual(3, appended.RowCount);
            CollectionAssert.AreEqual(new double?[] { 10.0, 10.5, 11.0 }, appended.Columns["T1"]);
        }

        [TestMethod]
        public void Append_WithoutNewRows_ReturnsSameInstance()
        {
            var mapper = new RecordingRowsToTestDataMapper();
            TestData data = mapper.Map(Source, "B",
                new List<RecordingAggregateRow> { Row("B-T1", 1000, 10.0) },
                Channels(), new Dictionary<string, string>());

            TestData appended = mapper.Append(data, "B", new List<RecordingAggregateRow>());

            Assert.AreSame(data, appended);
        }
    }
}
```

- [ ] **Step 2: Убедиться, что тесты падают**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter RecordingRowsToTestDataMapperTests`

Ожидается: ошибка компиляции — типы `ChannelCodeNormalizer`, `RecordingAggregateRow`, `RecordingRowsToTestDataMapper` не существуют.

- [ ] **Step 3: Реализовать нормализацию кода канала**

Создать `Application/Database/ChannelCodeNormalizer.cs`:

```csharp
using System;

namespace JSQViewer.Application.Database
{
    public static class ChannelCodeNormalizer
    {
        public static string StripPostPrefix(string channelId, string postId)
        {
            string code = (channelId ?? string.Empty).Trim();
            if (code.Length == 0 || string.IsNullOrWhiteSpace(postId))
            {
                return code;
            }

            string prefix = postId.Trim() + "-";
            if (code.Length > prefix.Length && code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return code.Substring(prefix.Length);
            }

            return code;
        }
    }
}
```

- [ ] **Step 4: Реализовать преобразователь**

Создать `Application/Database/RecordingRowsToTestDataMapper.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Core;

namespace JSQViewer.Application.Database
{
    public sealed class RecordingAggregateRow
    {
        public string ChannelId { get; set; }

        public long TimestampMs { get; set; }

        public double Value { get; set; }
    }

    public sealed class RecordingRowsToTestDataMapper
    {
        public TestData Map(
            string source,
            string postId,
            IList<RecordingAggregateRow> rows,
            IDictionary<string, ChannelInfo> channels,
            IDictionary<string, string> metadata)
        {
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Source is required.", nameof(source));

            IList<RecordingAggregateRow> safeRows = rows ?? new List<RecordingAggregateRow>();

            long[] timestamps = safeRows
                .Select(row => row.TimestampMs)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();

            var indexByTimestamp = new Dictionary<long, int>(timestamps.Length);
            for (int i = 0; i < timestamps.Length; i++)
            {
                indexByTimestamp[timestamps[i]] = i;
            }

            var columns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            var columnOrder = new List<string>();
            for (int i = 0; i < safeRows.Count; i++)
            {
                RecordingAggregateRow row = safeRows[i];
                string code = ChannelCodeNormalizer.StripPostPrefix(row.ChannelId, postId);
                double?[] column;
                if (!columns.TryGetValue(code, out column))
                {
                    column = new double?[timestamps.Length];
                    columns[code] = column;
                    columnOrder.Add(code);
                }

                column[indexByTimestamp[row.TimestampMs]] = row.Value;
            }

            string[] columnNames = columnOrder.ToArray();
            var normalizedChannels = new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase);
            if (channels != null)
            {
                foreach (KeyValuePair<string, ChannelInfo> pair in channels)
                {
                    normalizedChannels[pair.Key] = pair.Value;
                }
            }

            return new TestData
            {
                Root = source,
                Meta = metadata == null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase),
                Channels = normalizedChannels,
                CodeSources = columnNames.ToDictionary(code => code, code => source, StringComparer.OrdinalIgnoreCase),
                SourceStartMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    { source, timestamps.Length > 0 ? timestamps[0] : 0L }
                },
                SourceEndMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    { source, timestamps.Length > 0 ? timestamps[timestamps.Length - 1] : 0L }
                },
                TimestampsMs = timestamps,
                Columns = columns,
                ColumnNames = columnNames,
                SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { source, columnNames.ToArray() }
                },
                RowCount = timestamps.Length
            };
        }

        public long GetLastTimestampMs(TestData data)
        {
            if (data == null || data.TimestampsMs == null || data.TimestampsMs.Length == 0)
            {
                return -1L;
            }

            return data.TimestampsMs[data.TimestampsMs.Length - 1];
        }

        public TestData Append(TestData existing, string postId, IList<RecordingAggregateRow> rows)
        {
            if (existing == null) throw new ArgumentNullException(nameof(existing));

            long lastTimestamp = GetLastTimestampMs(existing);
            List<RecordingAggregateRow> freshRows = (rows ?? new List<RecordingAggregateRow>())
                .Where(row => row.TimestampMs > lastTimestamp)
                .ToList();

            if (freshRows.Count == 0)
            {
                return existing;
            }

            long[] newTimestamps = freshRows
                .Select(row => row.TimestampMs)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();

            int oldLength = existing.TimestampsMs.Length;
            int newLength = oldLength + newTimestamps.Length;

            var mergedTimestamps = new long[newLength];
            Array.Copy(existing.TimestampsMs, mergedTimestamps, oldLength);
            Array.Copy(newTimestamps, 0, mergedTimestamps, oldLength, newTimestamps.Length);

            var indexByTimestamp = new Dictionary<long, int>(newTimestamps.Length);
            for (int i = 0; i < newTimestamps.Length; i++)
            {
                indexByTimestamp[newTimestamps[i]] = oldLength + i;
            }

            var mergedColumns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            var columnOrder = new List<string>(existing.ColumnNames);
            foreach (KeyValuePair<string, double?[]> pair in existing.Columns)
            {
                var extended = new double?[newLength];
                Array.Copy(pair.Value, extended, Math.Min(pair.Value.Length, oldLength));
                mergedColumns[pair.Key] = extended;
            }

            for (int i = 0; i < freshRows.Count; i++)
            {
                RecordingAggregateRow row = freshRows[i];
                string code = ChannelCodeNormalizer.StripPostPrefix(row.ChannelId, postId);
                double?[] column;
                if (!mergedColumns.TryGetValue(code, out column))
                {
                    column = new double?[newLength];
                    mergedColumns[code] = column;
                    columnOrder.Add(code);
                }

                column[indexByTimestamp[row.TimestampMs]] = row.Value;
            }

            string[] mergedColumnNames = columnOrder.ToArray();
            string source = existing.Root;

            return new TestData
            {
                Root = source,
                Meta = existing.Meta,
                Channels = existing.Channels,
                CodeSources = mergedColumnNames.ToDictionary(code => code, code => source, StringComparer.OrdinalIgnoreCase),
                SourceStartMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    { source, mergedTimestamps[0] }
                },
                SourceEndMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    { source, mergedTimestamps[newLength - 1] }
                },
                TimestampsMs = mergedTimestamps,
                Columns = mergedColumns,
                ColumnNames = mergedColumnNames,
                SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { source, mergedColumnNames.ToArray() }
                },
                RowCount = newLength
            };
        }
    }
}
```

- [ ] **Step 5: Убедиться, что тесты проходят**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter RecordingRowsToTestDataMapperTests`

Ожидается: 9 тестов пройдены.

- [ ] **Step 6: Коммит**

```bash
git add Application/Database/ChannelCodeNormalizer.cs Application/Database/RecordingRowsToTestDataMapper.cs JSQViewer.Tests/RecordingRowsToTestDataMapperTests.cs
git commit -m "Добавлено преобразование агрегатов прогона в TestData"
```

---

### Task 5: Каталог прогонов

**Files:**
- Create: `Application/Database/RecordingSummaryItem.cs`
- Create: `Application/Database/RecordingCatalogFilter.cs`
- Create: `Application/Workspace/Ports/IRecordingCatalog.cs`
- Create: `Infrastructure/Database/RecordingCatalogQueryBuilder.cs`
- Create: `Infrastructure/Database/PostgresRecordingCatalog.cs`
- Test: `JSQViewer.Tests/RecordingCatalogQueryBuilderTests.cs`

**Interfaces:**
- Consumes: `DatabaseConnectionSettings`, `NpgsqlConnectionFactory` из Задачи 1.
- Produces:
  - `JSQViewer.Application.Database.RecordingSummaryItem` со свойствами `string Id`, `string PostId`, `string Title`, `string Status`, `DateTime? StartedAt`, `DateTime? StoppedAt`, `string EquipmentModel`, `string ExperimentType`, `double DurationHours`, `bool IsActive`, и методом `string ToSourceString()`, возвращающим `RecordingSourceRef.Build(Id)`.
  - `JSQViewer.Application.Database.RecordingCatalogFilter` со свойствами `string PostId`, `DateTime? From`, `DateTime? To`, `string ExperimentType`, `string TitleContains`, `int Limit`.
  - `JSQViewer.Application.Workspace.Ports.IRecordingCatalog` с методами `IList<RecordingSummaryItem> List(RecordingCatalogFilter filter)`, `IList<string> ListPosts()`, `IList<string> ListExperimentTypes()`, `string GetStatus(string recordingId)`.
  - `JSQViewer.Infrastructure.Database.RecordingCatalogQueryBuilder` с методом `string Build(RecordingCatalogFilter filter, IList<string> parameterNames)`, дописывающим имена использованных параметров в `parameterNames`.

- [ ] **Step 1: Написать падающий тест на сборку SQL**

Создать `JSQViewer.Tests/RecordingCatalogQueryBuilderTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using JSQViewer.Application.Database;
using JSQViewer.Infrastructure.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class RecordingCatalogQueryBuilderTests
    {
        [TestMethod]
        public void Build_WithoutFilters_SelectsAllOrderedByStartDescending()
        {
            var parameters = new List<string>();

            string sql = new RecordingCatalogQueryBuilder().Build(new RecordingCatalogFilter(), parameters);

            StringAssert.Contains(sql, "FROM recordings r");
            StringAssert.Contains(sql, "ORDER BY r.started_at DESC NULLS LAST");
            StringAssert.Contains(sql, "LIMIT @limit");
            CollectionAssert.AreEqual(new[] { "limit" }, parameters);
        }

        [TestMethod]
        public void Build_WithEveryFilter_AddsOneConditionPerParameter()
        {
            var parameters = new List<string>();
            var filter = new RecordingCatalogFilter
            {
                PostId = "B",
                From = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Local),
                To = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Local),
                ExperimentType = "FUNC",
                TitleContains = "LIDER"
            };

            string sql = new RecordingCatalogQueryBuilder().Build(filter, parameters);

            StringAssert.Contains(sql, "r.post_id = @post_id");
            StringAssert.Contains(sql, "r.started_at >= @from");
            StringAssert.Contains(sql, "r.started_at < @to");
            StringAssert.Contains(sql, "r.experiment_type = @experiment_type");
            StringAssert.Contains(sql, "r.title ILIKE @title");
            CollectionAssert.AreEquivalent(
                new[] { "post_id", "from", "to", "experiment_type", "title", "limit" }, parameters);
        }

        [TestMethod]
        public void Build_IgnoresBlankFilterValues()
        {
            var parameters = new List<string>();
            var filter = new RecordingCatalogFilter { PostId = "   ", ExperimentType = "", TitleContains = null };

            string sql = new RecordingCatalogQueryBuilder().Build(filter, parameters);

            Assert.IsFalse(sql.Contains("@post_id"));
            Assert.IsFalse(sql.Contains("@experiment_type"));
            Assert.IsFalse(sql.Contains("@title"));
            CollectionAssert.AreEqual(new[] { "limit" }, parameters);
        }

        [TestMethod]
        public void Build_NeverEmitsWriteStatements()
        {
            string sql = new RecordingCatalogQueryBuilder().Build(new RecordingCatalogFilter(), new List<string>());

            foreach (string forbidden in new[] { "INSERT", "UPDATE", "DELETE", "CREATE", "DROP" })
            {
                Assert.IsFalse(sql.ToUpperInvariant().Contains(forbidden), "SQL не должен содержать " + forbidden);
            }
        }
    }
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter RecordingCatalogQueryBuilderTests`

Ожидается: ошибка компиляции — типы `RecordingCatalogFilter` и `RecordingCatalogQueryBuilder` не существуют.

- [ ] **Step 3: Создать модели каталога**

Создать `Application/Database/RecordingSummaryItem.cs`:

```csharp
using System;

namespace JSQViewer.Application.Database
{
    public sealed class RecordingSummaryItem
    {
        public string Id { get; set; }
        public string PostId { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? StoppedAt { get; set; }
        public string EquipmentModel { get; set; }
        public string ExperimentType { get; set; }

        public bool IsActive
        {
            get { return string.Equals(Status, "recording", StringComparison.OrdinalIgnoreCase); }
        }

        public double DurationHours
        {
            get
            {
                if (!StartedAt.HasValue)
                {
                    return 0.0;
                }

                DateTime end = StoppedAt.HasValue ? StoppedAt.Value : DateTime.Now;
                double hours = (end - StartedAt.Value).TotalHours;
                return hours < 0.0 ? 0.0 : hours;
            }
        }

        public string ToSourceString()
        {
            return RecordingSourceRef.Build(Id);
        }
    }
}
```

Создать `Application/Database/RecordingCatalogFilter.cs`:

```csharp
using System;

namespace JSQViewer.Application.Database
{
    public sealed class RecordingCatalogFilter
    {
        public RecordingCatalogFilter()
        {
            Limit = 500;
        }

        public string PostId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string ExperimentType { get; set; }
        public string TitleContains { get; set; }
        public int Limit { get; set; }
    }
}
```

Создать `Application/Workspace/Ports/IRecordingCatalog.cs`:

```csharp
using System.Collections.Generic;
using JSQViewer.Application.Database;

namespace JSQViewer.Application.Workspace.Ports
{
    public interface IRecordingCatalog
    {
        IList<RecordingSummaryItem> List(RecordingCatalogFilter filter);

        IList<string> ListPosts();

        IList<string> ListExperimentTypes();

        string GetStatus(string recordingId);
    }
}
```

- [ ] **Step 4: Реализовать сборщик SQL**

Создать `Infrastructure/Database/RecordingCatalogQueryBuilder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using JSQViewer.Application.Database;

namespace JSQViewer.Infrastructure.Database
{
    public sealed class RecordingCatalogQueryBuilder
    {
        public string Build(RecordingCatalogFilter filter, IList<string> parameterNames)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            if (parameterNames == null) throw new ArgumentNullException(nameof(parameterNames));

            var conditions = new List<string>();
            if (!string.IsNullOrWhiteSpace(filter.PostId))
            {
                conditions.Add("r.post_id = @post_id");
                parameterNames.Add("post_id");
            }

            if (filter.From.HasValue)
            {
                conditions.Add("r.started_at >= @from");
                parameterNames.Add("from");
            }

            if (filter.To.HasValue)
            {
                conditions.Add("r.started_at < @to");
                parameterNames.Add("to");
            }

            if (!string.IsNullOrWhiteSpace(filter.ExperimentType))
            {
                conditions.Add("r.experiment_type = @experiment_type");
                parameterNames.Add("experiment_type");
            }

            if (!string.IsNullOrWhiteSpace(filter.TitleContains))
            {
                conditions.Add("r.title ILIKE @title");
                parameterNames.Add("title");
            }

            var sql = new StringBuilder();
            sql.AppendLine("SELECT r.id, r.post_id, r.title, r.status, r.started_at, r.stopped_at,");
            sql.AppendLine("       r.equipment_model, r.experiment_type");
            sql.AppendLine("FROM recordings r");
            if (conditions.Count > 0)
            {
                sql.AppendLine("WHERE " + string.Join(" AND ", conditions.ToArray()));
            }

            sql.AppendLine("ORDER BY r.started_at DESC NULLS LAST");
            sql.AppendLine("LIMIT @limit");
            parameterNames.Add("limit");
            return sql.ToString();
        }
    }
}
```

- [ ] **Step 5: Убедиться, что тесты проходят**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter RecordingCatalogQueryBuilderTests`

Ожидается: 4 теста пройдены.

- [ ] **Step 6: Реализовать каталог поверх Npgsql**

Создать `Infrastructure/Database/PostgresRecordingCatalog.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Data;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Database;
using JSQViewer.Application.Workspace.Ports;
using Npgsql;

namespace JSQViewer.Infrastructure.Database
{
    public sealed class PostgresRecordingCatalog : IRecordingCatalog
    {
        private readonly NpgsqlConnectionFactory _connectionFactory;
        private readonly IDatabaseSettingsRepository _settingsRepository;
        private readonly RecordingCatalogQueryBuilder _queryBuilder;

        public PostgresRecordingCatalog(
            NpgsqlConnectionFactory connectionFactory,
            IDatabaseSettingsRepository settingsRepository)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
            _queryBuilder = new RecordingCatalogQueryBuilder();
        }

        public IList<RecordingSummaryItem> List(RecordingCatalogFilter filter)
        {
            RecordingCatalogFilter safeFilter = filter ?? new RecordingCatalogFilter();
            var parameterNames = new List<string>();
            string sql = _queryBuilder.Build(safeFilter, parameterNames);
            var result = new List<RecordingSummaryItem>();

            using (NpgsqlConnection connection = OpenConnection())
            using (var command = new NpgsqlCommand(sql, connection))
            {
                for (int i = 0; i < parameterNames.Count; i++)
                {
                    string name = parameterNames[i];
                    if (name == "post_id") command.Parameters.AddWithValue(name, safeFilter.PostId.Trim());
                    else if (name == "from") command.Parameters.AddWithValue(name, safeFilter.From.Value);
                    else if (name == "to") command.Parameters.AddWithValue(name, safeFilter.To.Value);
                    else if (name == "experiment_type") command.Parameters.AddWithValue(name, safeFilter.ExperimentType.Trim());
                    else if (name == "title") command.Parameters.AddWithValue(name, "%" + safeFilter.TitleContains.Trim() + "%");
                    else if (name == "limit") command.Parameters.AddWithValue(name, safeFilter.Limit <= 0 ? 500 : safeFilter.Limit);
                }

                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new RecordingSummaryItem
                        {
                            Id = ReadString(reader, 0),
                            PostId = ReadString(reader, 1),
                            Title = ReadString(reader, 2),
                            Status = ReadString(reader, 3),
                            StartedAt = ReadLocalDateTime(reader, 4),
                            StoppedAt = ReadLocalDateTime(reader, 5),
                            EquipmentModel = ReadString(reader, 6),
                            ExperimentType = ReadString(reader, 7)
                        });
                    }
                }
            }

            return result;
        }

        public IList<string> ListPosts()
        {
            return ReadSingleColumn("SELECT id FROM posts WHERE is_active ORDER BY id");
        }

        public IList<string> ListExperimentTypes()
        {
            return ReadSingleColumn("SELECT name FROM experiment_types WHERE is_active ORDER BY name");
        }

        public string GetStatus(string recordingId)
        {
            using (NpgsqlConnection connection = OpenConnection())
            using (var command = new NpgsqlCommand("SELECT status FROM recordings WHERE id = @id", connection))
            {
                command.Parameters.AddWithValue("id", recordingId ?? string.Empty);
                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? null : Convert.ToString(value);
            }
        }

        private IList<string> ReadSingleColumn(string sql)
        {
            var result = new List<string>();
            using (NpgsqlConnection connection = OpenConnection())
            using (var command = new NpgsqlCommand(sql, connection))
            using (NpgsqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    result.Add(ReadString(reader, 0));
                }
            }

            return result;
        }

        private NpgsqlConnection OpenConnection()
        {
            DatabaseConnectionSettings settings = _settingsRepository.Load();
            NpgsqlConnection connection = _connectionFactory.Create(settings, _settingsRepository.LoadPassword());
            connection.Open();
            return connection;
        }

        private static string ReadString(IDataRecord record, int index)
        {
            return record.IsDBNull(index) ? string.Empty : Convert.ToString(record.GetValue(index));
        }

        private static DateTime? ReadLocalDateTime(IDataRecord record, int index)
        {
            if (record.IsDBNull(index))
            {
                return null;
            }

            var value = (DateTime)record.GetValue(index);
            return value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
        }
    }
}
```

- [ ] **Step 7: Собрать и проверить каталог на живой базе**

Выполнить `dotnet build .\JSQViewer.csproj -c Debug` — ожидается 0 ошибок.

Проверка каталога данными выполняется в Задаче 7 вместе с диалогом. Здесь достаточно успешной сборки: класс ещё никем не вызывается.

- [ ] **Step 8: Коммит**

```bash
git add Application/Database/RecordingSummaryItem.cs Application/Database/RecordingCatalogFilter.cs Application/Workspace/Ports/IRecordingCatalog.cs Infrastructure/Database/RecordingCatalogQueryBuilder.cs Infrastructure/Database/PostgresRecordingCatalog.cs JSQViewer.Tests/RecordingCatalogQueryBuilderTests.cs
git commit -m "Добавлен каталог прогонов из PostgreSQL"
```

---

### Task 6: Чтение прогона и включение в загрузку рабочей области

**Files:**
- Create: `Application/Workspace/Ports/IRecordingDataReader.cs`
- Create: `Infrastructure/Database/PostgresRecordingDataSourceReader.cs`
- Modify: `Application/Workspace/UseCases/LoadWorkspaceDataUseCase.cs` (конструктор, `Execute`, `ResolveSourceRoot`)
- Modify: `Application/Workspace/WorkspaceLoadOrchestrationService.cs` (`IsValidSpec`, `ResolveSelectedFolderSource`)
- Modify: `Infrastructure/Composition/WorkspaceLoadingComposition.cs`
- Test: `JSQViewer.Tests/RecordingWorkspaceLoadingTests.cs`

**Interfaces:**
- Consumes: `RecordingSourceRef` (Задача 3), `RecordingRowsToTestDataMapper` и `RecordingAggregateRow` (Задача 4), `NpgsqlConnectionFactory` и `IDatabaseSettingsRepository` (Задачи 1–2).
- Produces:
  - `JSQViewer.Application.Workspace.Ports.IRecordingDataReader` с методами `TestData ReadRecording(string recordingId)` и `TestData AppendNewWindows(TestData existing, string recordingId)`.
  - `LoadWorkspaceDataUseCase` получает новый необязательный параметр конструктора `IRecordingDataReader recordingDataReader = null`, идущий последним.
  - `WorkspaceLoadingComposition.CreateLoadWorkspaceDataUseCase` получает новый необязательный параметр `IRecordingDataReader recordingDataReader = null`.

- [ ] **Step 1: Написать падающие тесты**

Создать `JSQViewer.Tests/RecordingWorkspaceLoadingTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Database;
using JSQViewer.Application.Workspace;
using JSQViewer.Application.Workspace.Ports;
using JSQViewer.Application.Workspace.UseCases;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class RecordingWorkspaceLoadingTests
    {
        private sealed class FakeRecordingDataReader : IRecordingDataReader
        {
            public List<string> RequestedIds = new List<string>();

            public TestData ReadRecording(string recordingId)
            {
                RequestedIds.Add(recordingId);
                var mapper = new RecordingRowsToTestDataMapper();
                return mapper.Map(
                    RecordingSourceRef.Build(recordingId),
                    "B",
                    new List<RecordingAggregateRow>
                    {
                        new RecordingAggregateRow { ChannelId = "B-T1", TimestampMs = 1000, Value = 10.0 },
                        new RecordingAggregateRow { ChannelId = "B-T1", TimestampMs = 2000, Value = 11.0 }
                    },
                    new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            }

            public TestData AppendNewWindows(TestData existing, string recordingId)
            {
                return existing;
            }
        }

        private sealed class ThrowingCanaliReader : ICanaliDefinitionReader
        {
            public Dictionary<string, ChannelInfo> Read(string root)
            {
                throw new InvalidOperationException("Файловый читатель не должен вызываться для источника из БД.");
            }
        }

        private sealed class ThrowingMetadataReader : ITestMetadataReader
        {
            public Dictionary<string, string> Read(string root)
            {
                throw new InvalidOperationException("Файловый читатель не должен вызываться для источника из БД.");
            }
        }

        private sealed class ThrowingDataSourceReader : ITestDataSourceReader
        {
            public TestData Read(string root, Dictionary<string, ChannelInfo> channels, Dictionary<string, string> metadata)
            {
                throw new InvalidOperationException("Файловый читатель не должен вызываться для источника из БД.");
            }
        }

        private sealed class PassThroughRootLocator : ITestRootLocator
        {
            public string FindRoot(string folder)
            {
                return folder;
            }
        }

        private sealed class StubFileSystem : IFileSystem
        {
            public bool FileExists(string path) { return false; }
            public bool DirectoryExists(string path) { return false; }
            public string[] GetFiles(string path, string searchPattern, System.IO.SearchOption searchOption) { return new string[0]; }
            public DateTime GetLastWriteTime(string path) { return DateTime.MinValue; }
            public void WriteAllBytes(string path, byte[] contents) { }
            public void CreateDirectory(string path) { }
            public void AppendAllText(string path, string contents, System.Text.Encoding encoding) { }
        }

        private static LoadWorkspaceDataUseCase CreateUseCase(IRecordingDataReader recordingDataReader)
        {
            return new LoadWorkspaceDataUseCase(
                new WorkspaceFolderSpecParser(),
                new PassThroughRootLocator(),
                new ThrowingMetadataReader(),
                new ThrowingCanaliReader(),
                new ThrowingDataSourceReader(),
                new MergeLoadedSourcesUseCase(),
                null,
                recordingDataReader);
        }

        [TestMethod]
        public void Execute_WithRecordingSource_UsesRecordingReader()
        {
            var reader = new FakeRecordingDataReader();

            WorkspaceLoadResult result = CreateUseCase(reader)
                .Execute(new WorkspaceLoadRequest("jsqdb://recording/abc123"));

            CollectionAssert.AreEqual(new[] { "abc123" }, reader.RequestedIds);
            Assert.AreEqual(2, result.Data.RowCount);
            Assert.AreEqual("jsqdb://recording/abc123", result.Data.Root);
        }

        [TestMethod]
        public void Execute_WithRecordingSource_KeepsSourceStringUnchanged()
        {
            WorkspaceLoadResult result = CreateUseCase(new FakeRecordingDataReader())
                .Execute(new WorkspaceLoadRequest("jsqdb://recording/abc123"));

            CollectionAssert.AreEqual(new[] { "jsqdb://recording/abc123" }, new List<string>(result.Folders));
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Execute_WithRecordingSource_WithoutReader_Throws()
        {
            CreateUseCase(null).Execute(new WorkspaceLoadRequest("jsqdb://recording/abc123"));
        }

        [TestMethod]
        public void IsValidSpec_AcceptsRecordingSources()
        {
            var service = new WorkspaceLoadOrchestrationService(new WorkspaceFolderSpecParser(), new StubFileSystem());

            Assert.IsTrue(service.IsValidSpec("jsqdb://recording/abc123"));
            Assert.IsFalse(service.IsValidSpec("jsqdb://recording/"));
            Assert.IsFalse(service.IsValidSpec(@"C:\does\not\exist"));
        }

        [TestMethod]
        public void ResolveSelectedFolderSource_LeavesRecordingSourceIntact()
        {
            var service = new WorkspaceLoadOrchestrationService(new WorkspaceFolderSpecParser(), new StubFileSystem());

            Assert.AreEqual("jsqdb://recording/abc123", service.ResolveSelectedFolderSource("jsqdb://recording/abc123"));
        }
    }
}
```

Заглушка `StubFileSystem` реализует все семь членов `IFileSystem` в текущей редакции `Application/Abstractions/IFileSystem.cs`. Если сборка ругается на нереализованный член — значит интерфейс с тех пор изменился; дописать недостающие члены пустыми телами, тест от этого не зависит.

- [ ] **Step 2: Убедиться, что тесты падают**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter RecordingWorkspaceLoadingTests`

Ожидается: ошибка компиляции — тип `IRecordingDataReader` не существует и у `LoadWorkspaceDataUseCase` нет восьмого параметра.

- [ ] **Step 3: Создать порт чтения прогона**

Создать `Application/Workspace/Ports/IRecordingDataReader.cs`:

```csharp
using JSQViewer.Core;

namespace JSQViewer.Application.Workspace.Ports
{
    public interface IRecordingDataReader
    {
        TestData ReadRecording(string recordingId);

        TestData AppendNewWindows(TestData existing, string recordingId);
    }
}
```

- [ ] **Step 4: Добавить ветку в LoadWorkspaceDataUseCase**

В `Application/Workspace/UseCases/LoadWorkspaceDataUseCase.cs`:

Добавить `using JSQViewer.Application.Database;`, поле `private readonly IRecordingDataReader _recordingDataReader;`, восьмой параметр конструктора `IRecordingDataReader recordingDataReader = null` и присваивание `_recordingDataReader = recordingDataReader;`.

В методе `ResolveSourceRoot` первой проверкой вставить:

```csharp
            if (RecordingSourceRef.IsRecordingSource(source))
            {
                return source.Trim().Trim('"');
            }
```

В методе `Execute`, в цикле по `resolvedRoots`, перед проверкой `IsExportedProtocolPath(root)` вставить:

```csharp
                string recordingId;
                if (RecordingSourceRef.TryParse(root, out recordingId))
                {
                    if (_recordingDataReader == null)
                    {
                        throw new InvalidOperationException("Recording data reader is not configured.");
                    }

                    loadedSources.Add(_recordingDataReader.ReadRecording(recordingId));
                    continue;
                }
```

Важно: в `Execute` список `resolvedRoots` строится через `Distinct(StringComparer.OrdinalIgnoreCase)` — источники `jsqdb://` при этом дедуплицируются корректно, дополнительных правок не нужно.

- [ ] **Step 5: Научить оркестратор понимать jsqdb://**

В `Application/Workspace/WorkspaceLoadOrchestrationService.cs` добавить `using JSQViewer.Application.Database;`.

В методе `IsValidSpec` в цикле заменить условие на:

```csharp
                if (RecordingSourceRef.IsRecordingSource(folders[i]))
                {
                    continue;
                }

                if (!_fileSystem.DirectoryExists(folders[i])
                    && !(IsExportedProtocolPath(folders[i]) && _fileSystem.FileExists(folders[i])))
                {
                    return false;
                }
```

В начало метода `ResolveSelectedFolderSource` вставить:

```csharp
            if (RecordingSourceRef.IsRecordingSource(selectedFolder))
            {
                return selectedFolder.Trim().Trim('"');
            }
```

- [ ] **Step 6: Убедиться, что тесты проходят**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter RecordingWorkspaceLoadingTests`

Ожидается: 5 тестов пройдены.

Затем выполнить весь набор тестов: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj`

Ожидается: все существующие тесты по-прежнему проходят. Особое внимание — `WorkspaceLoadingTests` и `WorkspaceLoadOrchestrationServiceTests`.

- [ ] **Step 7: Реализовать чтение прогона из PostgreSQL**

Создать `Infrastructure/Database/PostgresRecordingDataSourceReader.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Data;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Database;
using JSQViewer.Application.Workspace.Ports;
using JSQViewer.Core;
using Npgsql;

namespace JSQViewer.Infrastructure.Database
{
    public sealed class PostgresRecordingDataSourceReader : IRecordingDataReader
    {
        private const string AggregatesSql =
            "SELECT a.channel_id, a.window_start, a.avg_value " +
            "FROM recording_aggregates a " +
            "WHERE a.recording_id = @recording_id " +
            "  AND a.avg_value IS NOT NULL " +
            "  AND a.window_start > @since " +
            "  AND NOT EXISTS (SELECT 1 FROM recording_aggregate_exclusions e " +
            "                  WHERE e.recording_id = a.recording_id " +
            "                    AND e.channel_id = a.channel_id " +
            "                    AND e.window_start = a.window_start " +
            "                    AND e.restored_at IS NULL) " +
            "ORDER BY a.window_start";

        private static readonly DateTime EpochUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly NpgsqlConnectionFactory _connectionFactory;
        private readonly IDatabaseSettingsRepository _settingsRepository;
        private readonly RecordingRowsToTestDataMapper _mapper;

        public PostgresRecordingDataSourceReader(
            NpgsqlConnectionFactory connectionFactory,
            IDatabaseSettingsRepository settingsRepository)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
            _mapper = new RecordingRowsToTestDataMapper();
        }

        public TestData ReadRecording(string recordingId)
        {
            if (string.IsNullOrWhiteSpace(recordingId))
            {
                throw new ArgumentException("Recording id is required.", nameof(recordingId));
            }

            using (NpgsqlConnection connection = OpenConnection())
            {
                string postId;
                Dictionary<string, string> metadata = ReadMetadata(connection, recordingId, out postId);
                if (postId == null)
                {
                    throw new InvalidOperationException("Прогон " + recordingId + " не найден в базе данных.");
                }

                Dictionary<string, ChannelInfo> channels = ReadChannels(connection, postId);
                List<RecordingAggregateRow> rows = ReadRows(connection, recordingId, DateTime.MinValue);
                return _mapper.Map(RecordingSourceRef.Build(recordingId), postId, rows, channels, metadata);
            }
        }

        public TestData AppendNewWindows(TestData existing, string recordingId)
        {
            if (existing == null) throw new ArgumentNullException(nameof(existing));

            long lastMs = _mapper.GetLastTimestampMs(existing);
            DateTime since = lastMs < 0 ? DateTime.MinValue : EpochUtc.AddMilliseconds(lastMs);

            using (NpgsqlConnection connection = OpenConnection())
            {
                string postId;
                ReadMetadata(connection, recordingId, out postId);
                if (postId == null)
                {
                    return existing;
                }

                List<RecordingAggregateRow> rows = ReadRows(connection, recordingId, since);
                return _mapper.Append(existing, postId, rows);
            }
        }

        private NpgsqlConnection OpenConnection()
        {
            DatabaseConnectionSettings settings = _settingsRepository.Load();
            NpgsqlConnection connection = _connectionFactory.Create(settings, _settingsRepository.LoadPassword());
            connection.Open();
            return connection;
        }

        private static List<RecordingAggregateRow> ReadRows(NpgsqlConnection connection, string recordingId, DateTime since)
        {
            var rows = new List<RecordingAggregateRow>();
            using (var command = new NpgsqlCommand(AggregatesSql, connection))
            {
                command.Parameters.AddWithValue("recording_id", recordingId);
                command.Parameters.AddWithValue("since",
                    since == DateTime.MinValue ? EpochUtc.AddYears(-1) : DateTime.SpecifyKind(since, DateTimeKind.Utc));

                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var windowStart = (DateTime)reader.GetValue(1);
                        DateTime utc = windowStart.Kind == DateTimeKind.Utc
                            ? windowStart
                            : windowStart.ToUniversalTime();

                        rows.Add(new RecordingAggregateRow
                        {
                            ChannelId = Convert.ToString(reader.GetValue(0)),
                            TimestampMs = (long)(utc - EpochUtc).TotalMilliseconds,
                            Value = reader.GetDouble(2)
                        });
                    }
                }
            }

            return rows;
        }

        private static Dictionary<string, string> ReadMetadata(NpgsqlConnection connection, string recordingId, out string postId)
        {
            postId = null;
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            const string sql =
                "SELECT r.post_id, p.name, r.title, r.status, r.started_at, r.stopped_at, r.operator, " +
                "       r.equipment_model, r.experiment_type, r.refrigerant, r.charge_mass_grams, " +
                "       r.compressor_model, r.modification, r.climate_class, r.temperature_class, r.notes " +
                "FROM recordings r LEFT JOIN posts p ON p.id = r.post_id WHERE r.id = @id";

            using (var command = new NpgsqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("id", recordingId);
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return metadata;
                    }

                    postId = Text(reader, 0);
                    Put(metadata, "Пост", Text(reader, 1));
                    Put(metadata, "Название", Text(reader, 2));
                    Put(metadata, "Статус", Text(reader, 3));
                    Put(metadata, "Начало", Moment(reader, 4));
                    Put(metadata, "Окончание", Moment(reader, 5));
                    Put(metadata, "Оператор", Text(reader, 6));
                    Put(metadata, "Модель", Text(reader, 7));
                    Put(metadata, "Тип испытания", Text(reader, 8));
                    Put(metadata, "Хладагент", Text(reader, 9));
                    Put(metadata, "Масса заправки, г", Text(reader, 10));
                    Put(metadata, "Компрессор", Text(reader, 11));
                    Put(metadata, "Модификация", Text(reader, 12));
                    Put(metadata, "Климатический класс", Text(reader, 13));
                    Put(metadata, "Температурный класс", Text(reader, 14));
                    Put(metadata, "Примечания", Text(reader, 15));
                }
            }

            return metadata;
        }

        private static Dictionary<string, ChannelInfo> ReadChannels(NpgsqlConnection connection, string postId)
        {
            var channels = new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase);
            const string sql =
                "SELECT channel_id, alias, unit FROM channel_config " +
                "WHERE post_id = @post_id AND NOT is_hidden";

            using (var command = new NpgsqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("post_id", postId);
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string code = ChannelCodeNormalizer.StripPostPrefix(Text(reader, 0), postId);
                        if (code.Length == 0)
                        {
                            continue;
                        }

                        channels[code] = new ChannelInfo
                        {
                            Code = code,
                            Name = Text(reader, 1),
                            Unit = Text(reader, 2)
                        };
                    }
                }
            }

            return channels;
        }

        private static void Put(IDictionary<string, string> metadata, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                metadata[key] = value;
            }
        }

        private static string Text(IDataRecord record, int index)
        {
            return record.IsDBNull(index) ? string.Empty : Convert.ToString(record.GetValue(index));
        }

        private static string Moment(IDataRecord record, int index)
        {
            if (record.IsDBNull(index))
            {
                return string.Empty;
            }

            var value = (DateTime)record.GetValue(index);
            DateTime local = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
            return local.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
```

- [ ] **Step 8: Пробросить читатель через композицию**

В `Infrastructure/Composition/WorkspaceLoadingComposition.cs` изменить сигнатуру и тело:

```csharp
        public static LoadWorkspaceDataUseCase CreateLoadWorkspaceDataUseCase(
            WorkspaceFolderSpecParser folderSpecParser,
            IRecordingDataReader recordingDataReader = null)
        {
            return new LoadWorkspaceDataUseCase(
                folderSpecParser ?? CreateFolderSpecParser(),
                CreateTestRootLocator(),
                new ProvaMetadataReader(),
                new CanaliDefinitionReader(),
                new DbfTestDataSourceReader(),
                new MergeLoadedSourcesUseCase(),
                new ExportedProtocolDataSourceReader(),
                recordingDataReader);
        }
```

- [ ] **Step 9: Собрать и прогнать все тесты**

Выполнить:

```powershell
dotnet build .\JSQViewer.csproj -c Debug
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj
```

Ожидается: 0 ошибок сборки, все тесты пройдены.

- [ ] **Step 10: Коммит**

```bash
git add Application/Workspace/Ports/IRecordingDataReader.cs Infrastructure/Database/PostgresRecordingDataSourceReader.cs Application/Workspace/UseCases/LoadWorkspaceDataUseCase.cs Application/Workspace/WorkspaceLoadOrchestrationService.cs Infrastructure/Composition/WorkspaceLoadingComposition.cs JSQViewer.Tests/RecordingWorkspaceLoadingTests.cs
git commit -m "Добавлено чтение прогонов из PostgreSQL в загрузку рабочей области"
```

---

### Task 7: Интерфейс выбора прогона и настроек подключения

**Files:**
- Create: `UI/DatabaseSettingsDialog.cs`
- Create: `UI/OpenFromDatabaseForm.cs`
- Modify: `Infrastructure/Platform/DictionaryLocalizationService.cs`
- Modify: `UI/MainForm.cs` (поля около строки 35, конструктор около строки 181, панель кнопок около строки 268, обработчики около строки 1790, метод `SetBusy` около строки 4351)
- Modify: `Program.cs`
- Test: `JSQViewer.Tests/RecordingSummaryItemTests.cs`

**Interfaces:**
- Consumes: `IRecordingCatalog` (Задача 5), `IDatabaseSettingsRepository` (Задача 2), `NpgsqlConnectionFactory` (Задача 1), `RecordingSummaryItem` (Задача 5).
- Produces:
  - `JSQViewer.UI.OpenFromDatabaseForm` с конструктором `OpenFromDatabaseForm(IRecordingCatalog catalog, int maxSelectionCount)` и свойством `IList<string> SelectedSources { get; }`, содержащим строки `jsqdb://recording/<id>`.
  - `JSQViewer.UI.DatabaseSettingsDialog` с конструктором `DatabaseSettingsDialog(IDatabaseSettingsRepository repository, NpgsqlConnectionFactory connectionFactory)`.
  - `MainForm` получает два новых параметра конструктора, идущих последними: `IRecordingCatalog recordingCatalog`, `IDatabaseSettingsRepository databaseSettingsRepository`.

Примечание к спецификации: спека называла настройки подключения «вкладкой в `SettingsDialog`». Существующий `SettingsDialog` — это диалог стилей с плоской компоновкой и результатом типа `ViewerSettingsModel`; встраивание в него вкладки потребовало бы перестройки всей формы. Вместо этого создаётся отдельный `DatabaseSettingsDialog`, вызываемый из той же панели кнопок. Функциональность та же, вмешательство в существующий код минимально.

- [ ] **Step 1: Написать падающий тест на строку списка**

Создать `JSQViewer.Tests/RecordingSummaryItemTests.cs`:

```csharp
using System;
using JSQViewer.Application.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class RecordingSummaryItemTests
    {
        [TestMethod]
        public void ToSourceString_ProducesLoadableSource()
        {
            var item = new RecordingSummaryItem { Id = "21edc519ba594f94" };

            Assert.AreEqual("jsqdb://recording/21edc519ba594f94", item.ToSourceString());
            Assert.IsTrue(RecordingSourceRef.IsRecordingSource(item.ToSourceString()));
        }

        [TestMethod]
        public void DurationHours_ForFinishedRecording_UsesStoppedAt()
        {
            var item = new RecordingSummaryItem
            {
                StartedAt = new DateTime(2026, 8, 5, 10, 0, 0),
                StoppedAt = new DateTime(2026, 8, 5, 13, 30, 0)
            };

            Assert.AreEqual(3.5, item.DurationHours, 0.001);
        }

        [TestMethod]
        public void DurationHours_WithoutStart_IsZero()
        {
            Assert.AreEqual(0.0, new RecordingSummaryItem().DurationHours, 0.001);
        }

        [TestMethod]
        public void IsActive_TrueOnlyForRecordingStatus()
        {
            Assert.IsTrue(new RecordingSummaryItem { Status = "recording" }.IsActive);
            Assert.IsFalse(new RecordingSummaryItem { Status = "stopped" }.IsActive);
            Assert.IsFalse(new RecordingSummaryItem().IsActive);
        }
    }
}
```

- [ ] **Step 2: Убедиться, что тест падает или проходит**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter RecordingSummaryItemTests`

Ожидается: 4 теста пройдены — `RecordingSummaryItem` уже создан в Задаче 5. Если хотя бы один тест падает, исправить `RecordingSummaryItem`, а не тест.

- [ ] **Step 3: Добавить строки локализации**

В `Infrastructure/Platform/DictionaryLocalizationService.cs` в русский словарь добавить:

```csharp
            { "OpenFromDatabase", "Из БД…" },
            { "TipOpenFromDatabase", "Открыть прогон из базы данных JSQ Laboratory" },
            { "DatabaseSettings", "Настройки БД…" },
            { "DatabaseSettingsTitle", "Подключение к базе данных" },
            { "DatabaseHost", "Сервер" },
            { "DatabasePort", "Порт" },
            { "DatabaseName", "База" },
            { "DatabaseUser", "Пользователь" },
            { "DatabasePassword", "Пароль" },
            { "DatabaseRefreshInterval", "Интервал автообновления, с" },
            { "DatabaseTestConnection", "Проверить подключение" },
            { "DatabaseConnectionOk", "Подключение выполнено." },
            { "DatabaseConnectionFailed", "Не удалось подключиться: {0}" },
            { "DatabaseAuthFailed", "Не удалось пройти проверку подлинности. Проверьте пользователя и пароль в настройках БД." },
            { "OpenFromDatabaseTitle", "Прогоны в базе данных" },
            { "RecordingColumnStarted", "Начало" },
            { "RecordingColumnPost", "Пост" },
            { "RecordingColumnTitle", "Название" },
            { "RecordingColumnModel", "Модель" },
            { "RecordingColumnExperiment", "Тип испытания" },
            { "RecordingColumnDuration", "Длительность, ч" },
            { "RecordingColumnStatus", "Статус" },
            { "RecordingStatusActive", "идёт запись" },
            { "RecordingStatusStopped", "завершён" },
            { "RecordingFilterPost", "Пост" },
            { "RecordingFilterFrom", "С" },
            { "RecordingFilterTo", "По" },
            { "RecordingFilterExperiment", "Тип" },
            { "RecordingFilterTitle", "Название содержит" },
            { "RecordingFilterApply", "Показать" },
            { "RecordingFilterAny", "любой" },
            { "RecordingSelectAtLeastOne", "Выберите хотя бы один прогон." },
            { "RecordingTooManySelected", "Одновременно можно загрузить не более {0} источников." },
            { "RecordingLiveUpdating", "Идёт запись: данные обновляются автоматически." },
            { "RecordingConnectionLost", "Нет связи с базой данных, автообновление продолжает попытки." },
            { "RecordingConnectionRestored", "Связь с базой данных восстановлена." },
```

В английский словарь добавить те же ключи со значениями: `"From DB…"`, `"Open a recording from the JSQ Laboratory database"`, `"Database settings…"`, `"Database connection"`, `"Host"`, `"Port"`, `"Database"`, `"User"`, `"Password"`, `"Auto-refresh interval, s"`, `"Test connection"`, `"Connected."`, `"Connection failed: {0}"`, `"Authentication failed. Check the user and password in database settings."`, `"Recordings in database"`, `"Started"`, `"Post"`, `"Title"`, `"Model"`, `"Experiment"`, `"Duration, h"`, `"Status"`, `"recording"`, `"finished"`, `"Post"`, `"From"`, `"To"`, `"Type"`, `"Title contains"`, `"Show"`, `"any"`, `"Select at least one recording."`, `"No more than {0} sources can be loaded at once."`, `"Recording in progress: data refreshes automatically."`, `"No database connection, auto-refresh keeps retrying."`, `"Database connection restored."`.

- [ ] **Step 4: Создать диалог настроек подключения**

Создать `UI/DatabaseSettingsDialog.cs`:

```csharp
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Database;
using JSQViewer.Infrastructure.Database;

namespace JSQViewer.UI
{
    public sealed class DatabaseSettingsDialog : Form
    {
        private readonly IDatabaseSettingsRepository _repository;
        private readonly NpgsqlConnectionFactory _connectionFactory;
        private readonly TextBox _hostBox = new TextBox();
        private readonly NumericUpDown _portBox = new NumericUpDown();
        private readonly TextBox _databaseBox = new TextBox();
        private readonly TextBox _userBox = new TextBox();
        private readonly TextBox _passwordBox = new TextBox();
        private readonly NumericUpDown _intervalBox = new NumericUpDown();
        private readonly Label _statusLabel = new Label();

        public DatabaseSettingsDialog(IDatabaseSettingsRepository repository, NpgsqlConnectionFactory connectionFactory)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

            Text = Loc.Get("DatabaseSettingsTitle");
            Width = 460;
            Height = 320;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.Padding = new Padding(10);
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            Controls.Add(layout);

            _portBox.Minimum = 1;
            _portBox.Maximum = 65535;
            _intervalBox.Minimum = 5;
            _intervalBox.Maximum = 3600;
            _passwordBox.UseSystemPasswordChar = true;
            _hostBox.Width = 220;
            _databaseBox.Width = 220;
            _userBox.Width = 220;
            _passwordBox.Width = 220;

            AddRow(layout, Loc.Get("DatabaseHost"), _hostBox);
            AddRow(layout, Loc.Get("DatabasePort"), _portBox);
            AddRow(layout, Loc.Get("DatabaseName"), _databaseBox);
            AddRow(layout, Loc.Get("DatabaseUser"), _userBox);
            AddRow(layout, Loc.Get("DatabasePassword"), _passwordBox);
            AddRow(layout, Loc.Get("DatabaseRefreshInterval"), _intervalBox);

            var testButton = new Button();
            testButton.Text = Loc.Get("DatabaseTestConnection");
            testButton.AutoSize = true;
            testButton.Click += TestButtonOnClick;
            AddRow(layout, string.Empty, testButton);

            _statusLabel.AutoSize = false;
            _statusLabel.Width = 260;
            _statusLabel.Height = 40;
            AddRow(layout, string.Empty, _statusLabel);

            var buttons = new FlowLayoutPanel();
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Dock = DockStyle.Bottom;
            buttons.Height = 40;
            var cancelButton = new Button { Text = Loc.Get("Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
            var okButton = new Button { Text = Loc.Get("Ok"), AutoSize = true };
            okButton.Click += OkButtonOnClick;
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(okButton);
            Controls.Add(buttons);
            CancelButton = cancelButton;

            LoadIntoEditors();
        }

        private static void AddRow(TableLayoutPanel layout, string caption, Control editor)
        {
            var label = new Label();
            label.Text = caption;
            label.AutoSize = true;
            label.Anchor = AnchorStyles.Left;
            label.Margin = new Padding(0, 6, 6, 0);
            layout.Controls.Add(label);
            layout.Controls.Add(editor);
        }

        private void LoadIntoEditors()
        {
            DatabaseConnectionSettings settings = _repository.Load();
            _hostBox.Text = settings.host;
            _portBox.Value = settings.port;
            _databaseBox.Text = settings.database;
            _userBox.Text = settings.username;
            _passwordBox.Text = _repository.LoadPassword();
            _intervalBox.Value = settings.refresh_interval_seconds;
        }

        private DatabaseConnectionSettings ReadFromEditors()
        {
            DatabaseConnectionSettings settings = _repository.Load();
            settings.host = _hostBox.Text.Trim();
            settings.port = (int)_portBox.Value;
            settings.database = _databaseBox.Text.Trim();
            settings.username = _userBox.Text.Trim();
            settings.refresh_interval_seconds = (int)_intervalBox.Value;
            return settings;
        }

        private void TestButtonOnClick(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                string error = _connectionFactory.TestConnection(ReadFromEditors(), _passwordBox.Text);
                if (error == null)
                {
                    _statusLabel.ForeColor = Color.DarkGreen;
                    _statusLabel.Text = Loc.Get("DatabaseConnectionOk");
                }
                else
                {
                    _statusLabel.ForeColor = Color.Firebrick;
                    _statusLabel.Text = string.Format(CultureInfo.CurrentCulture, Loc.Get("DatabaseConnectionFailed"), error);
                }
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void OkButtonOnClick(object sender, EventArgs e)
        {
            _repository.SavePassword(ReadFromEditors(), _passwordBox.Text);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
```

Перед сборкой проверить, что ключи `Ok` и `Cancel` есть в `DictionaryLocalizationService`. Если их нет — добавить `{ "Ok", "ОК" }` и `{ "Cancel", "Отмена" }` в русский словарь и `{ "Ok", "OK" }`, `{ "Cancel", "Cancel" }` в английский.

- [ ] **Step 5: Создать диалог выбора прогонов**

Создать `UI/OpenFromDatabaseForm.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using JSQViewer.Application.Database;
using JSQViewer.Application.Workspace.Ports;

namespace JSQViewer.UI
{
    public sealed class OpenFromDatabaseForm : Form
    {
        private readonly IRecordingCatalog _catalog;
        private readonly int _maxSelectionCount;
        private readonly ListView _list = new ListView();
        private readonly ComboBox _postFilter = new ComboBox();
        private readonly ComboBox _experimentFilter = new ComboBox();
        private readonly DateTimePicker _fromFilter = new DateTimePicker();
        private readonly DateTimePicker _toFilter = new DateTimePicker();
        private readonly TextBox _titleFilter = new TextBox();
        private readonly List<RecordingSummaryItem> _items = new List<RecordingSummaryItem>();

        public OpenFromDatabaseForm(IRecordingCatalog catalog, int maxSelectionCount)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _maxSelectionCount = maxSelectionCount < 1 ? 1 : maxSelectionCount;
            SelectedSources = new List<string>();

            Text = Loc.Get("OpenFromDatabaseTitle");
            Width = 1000;
            Height = 600;
            StartPosition = FormStartPosition.CenterParent;

            var filters = new FlowLayoutPanel();
            filters.Dock = DockStyle.Top;
            filters.Height = 40;
            filters.Padding = new Padding(6, 6, 6, 0);
            Controls.Add(filters);

            _postFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _postFilter.Width = 90;
            _experimentFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _experimentFilter.Width = 120;
            _fromFilter.Format = DateTimePickerFormat.Short;
            _fromFilter.Width = 110;
            _fromFilter.ShowCheckBox = true;
            _fromFilter.Checked = false;
            _toFilter.Format = DateTimePickerFormat.Short;
            _toFilter.Width = 110;
            _toFilter.ShowCheckBox = true;
            _toFilter.Checked = false;
            _titleFilter.Width = 180;

            AddFilter(filters, Loc.Get("RecordingFilterPost"), _postFilter);
            AddFilter(filters, Loc.Get("RecordingFilterExperiment"), _experimentFilter);
            AddFilter(filters, Loc.Get("RecordingFilterFrom"), _fromFilter);
            AddFilter(filters, Loc.Get("RecordingFilterTo"), _toFilter);
            AddFilter(filters, Loc.Get("RecordingFilterTitle"), _titleFilter);

            var applyButton = new Button { Text = Loc.Get("RecordingFilterApply"), AutoSize = true };
            applyButton.Click += ApplyButtonOnClick;
            filters.Controls.Add(applyButton);

            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.MultiSelect = true;
            _list.HideSelection = false;
            _list.Columns.Add(Loc.Get("RecordingColumnStarted"), 150);
            _list.Columns.Add(Loc.Get("RecordingColumnPost"), 60);
            _list.Columns.Add(Loc.Get("RecordingColumnTitle"), 260);
            _list.Columns.Add(Loc.Get("RecordingColumnModel"), 120);
            _list.Columns.Add(Loc.Get("RecordingColumnExperiment"), 110);
            _list.Columns.Add(Loc.Get("RecordingColumnDuration"), 110);
            _list.Columns.Add(Loc.Get("RecordingColumnStatus"), 110);
            _list.DoubleClick += ListOnDoubleClick;
            Controls.Add(_list);
            _list.BringToFront();

            var buttons = new FlowLayoutPanel();
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Dock = DockStyle.Bottom;
            buttons.Height = 44;
            var cancelButton = new Button { Text = Loc.Get("Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
            var okButton = new Button { Text = Loc.Get("Ok"), AutoSize = true };
            okButton.Click += OkButtonOnClick;
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(okButton);
            Controls.Add(buttons);
            CancelButton = cancelButton;

            LoadFilterValues();
            Reload();
        }

        public IList<string> SelectedSources { get; private set; }

        private static void AddFilter(Control parent, string caption, Control editor)
        {
            var label = new Label { Text = caption, AutoSize = true, Margin = new Padding(6, 6, 2, 0) };
            parent.Controls.Add(label);
            parent.Controls.Add(editor);
        }

        private void LoadFilterValues()
        {
            _postFilter.Items.Add(Loc.Get("RecordingFilterAny"));
            _experimentFilter.Items.Add(Loc.Get("RecordingFilterAny"));
            try
            {
                foreach (string post in _catalog.ListPosts())
                {
                    _postFilter.Items.Add(post);
                }

                foreach (string experiment in _catalog.ListExperimentTypes())
                {
                    _experimentFilter.Items.Add(experiment);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(CultureInfo.CurrentCulture, Loc.Get("DatabaseConnectionFailed"), ex.Message),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            _postFilter.SelectedIndex = 0;
            _experimentFilter.SelectedIndex = 0;
        }

        private void ApplyButtonOnClick(object sender, EventArgs e)
        {
            Reload();
        }

        private void Reload()
        {
            var filter = new RecordingCatalogFilter();
            if (_postFilter.SelectedIndex > 0) filter.PostId = Convert.ToString(_postFilter.SelectedItem);
            if (_experimentFilter.SelectedIndex > 0) filter.ExperimentType = Convert.ToString(_experimentFilter.SelectedItem);
            if (_fromFilter.Checked) filter.From = _fromFilter.Value.Date;
            if (_toFilter.Checked) filter.To = _toFilter.Value.Date.AddDays(1);
            if (!string.IsNullOrWhiteSpace(_titleFilter.Text)) filter.TitleContains = _titleFilter.Text.Trim();

            Cursor = Cursors.WaitCursor;
            try
            {
                _items.Clear();
                _list.Items.Clear();
                foreach (RecordingSummaryItem item in _catalog.List(filter))
                {
                    _items.Add(item);
                    var row = new ListViewItem(item.StartedAt.HasValue
                        ? item.StartedAt.Value.ToString("yyyy-MM-dd HH:mm")
                        : string.Empty);
                    row.SubItems.Add(item.PostId);
                    row.SubItems.Add(item.Title);
                    row.SubItems.Add(item.EquipmentModel);
                    row.SubItems.Add(item.ExperimentType);
                    row.SubItems.Add(item.DurationHours.ToString("0.0", CultureInfo.CurrentCulture));
                    row.SubItems.Add(item.IsActive
                        ? Loc.Get("RecordingStatusActive")
                        : Loc.Get("RecordingStatusStopped"));
                    row.Tag = item;
                    if (item.IsActive)
                    {
                        row.Font = new System.Drawing.Font(_list.Font, System.Drawing.FontStyle.Bold);
                    }

                    _list.Items.Add(row);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(CultureInfo.CurrentCulture, Loc.Get("DatabaseConnectionFailed"), ex.Message),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void ListOnDoubleClick(object sender, EventArgs e)
        {
            OkButtonOnClick(sender, e);
        }

        private void OkButtonOnClick(object sender, EventArgs e)
        {
            if (_list.SelectedItems.Count == 0)
            {
                MessageBox.Show(Loc.Get("RecordingSelectAtLeastOne"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_list.SelectedItems.Count > _maxSelectionCount)
            {
                MessageBox.Show(
                    string.Format(CultureInfo.CurrentCulture, Loc.Get("RecordingTooManySelected"), _maxSelectionCount),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var sources = new List<string>();
            for (int i = 0; i < _list.SelectedItems.Count; i++)
            {
                var item = (RecordingSummaryItem)_list.SelectedItems[i].Tag;
                sources.Add(item.ToSourceString());
            }

            SelectedSources = sources;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
```

- [ ] **Step 6: Подключить кнопки в MainForm**

В `UI/MainForm.cs`:

Добавить `using JSQViewer.Application.Database;` и `using JSQViewer.Application.Workspace.Ports;`.

Рядом с полем `private readonly Button _refreshButton;` добавить:

```csharp
        private readonly Button _openFromDatabaseButton;
        private readonly Button _databaseSettingsButton;
        private readonly IRecordingCatalog _recordingCatalog;
        private readonly IDatabaseSettingsRepository _databaseSettingsRepository;
```

В конец списка параметров конструктора добавить `IRecordingCatalog recordingCatalog, IDatabaseSettingsRepository databaseSettingsRepository` и присвоить поля.

После строки создания `_refreshButton` (около строки 270) добавить:

```csharp
            _openFromDatabaseButton = new Button(); _openFromDatabaseButton.Text = Loc.Get("OpenFromDatabase"); _openFromDatabaseButton.AutoSize = true; _openFromDatabaseButton.Click += OpenFromDatabaseButtonOnClick; folderRow.Controls.Add(_openFromDatabaseButton);
            _databaseSettingsButton = new Button(); _databaseSettingsButton.Text = Loc.Get("DatabaseSettings"); _databaseSettingsButton.AutoSize = true; _databaseSettingsButton.Click += DatabaseSettingsButtonOnClick; folderRow.Controls.Add(_databaseSettingsButton);
```

Рядом с `_toolTip.SetToolTip(_refreshButton, ...)` (около строки 786) добавить:

```csharp
            _toolTip.SetToolTip(_openFromDatabaseButton, Loc.Get("TipOpenFromDatabase"));
```

В метод, где обновляются подписи при смене языка (рядом со строкой 709), добавить:

```csharp
            _openFromDatabaseButton.Text = Loc.Get("OpenFromDatabase");
            _databaseSettingsButton.Text = Loc.Get("DatabaseSettings");
```

В `SetBusy` рядом с `_refreshButton.Enabled = !busy;` (около строки 4353) добавить:

```csharp
            _openFromDatabaseButton.Enabled = !busy;
            _databaseSettingsButton.Enabled = !busy;
```

Рядом с `AddDataButtonOnClick` добавить обработчики:

```csharp
        private void OpenFromDatabaseButtonOnClick(object sender, EventArgs e)
        {
            try
            {
                List<string> current = ParseFolderSpec(_folderBox.Text);
                int available = WorkspaceFolderSpecParser.MaxFolderCount - current.Count;
                if (available <= 0)
                {
                    available = WorkspaceFolderSpecParser.MaxFolderCount;
                    current.Clear();
                }

                using (var dialog = new OpenFromDatabaseForm(_recordingCatalog, available))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedSources.Count == 0)
                    {
                        return;
                    }

                    var combined = new List<string>();
                    for (int i = 0; i < dialog.SelectedSources.Count; i++)
                    {
                        combined.Add(dialog.SelectedSources[i]);
                    }

                    string spec = JoinFolderSpec(combined);
                    _folderBox.Text = spec;
                    LoadFolder(spec, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Open recording from database failed.", ex);
                NotifyError(Loc.Get("LoadFailed"));
            }
        }

        private void DatabaseSettingsButtonOnClick(object sender, EventArgs e)
        {
            using (var dialog = new DatabaseSettingsDialog(_databaseSettingsRepository, new NpgsqlConnectionFactory()))
            {
                dialog.ShowDialog(this);
            }
        }
```

Добавить `using JSQViewer.Infrastructure.Database;` в начало `MainForm.cs`.

- [ ] **Step 7: Собрать композицию в Program.cs**

В `Program.cs` перед созданием `loadWorkspaceDataUseCase` вставить:

```csharp
            ISecretProtector secretProtector = new DpapiSecretProtector();
            IDatabaseSettingsRepository databaseSettingsRepository = new FileDatabaseSettingsRepository(appPaths, secretProtector);
            var npgsqlConnectionFactory = new NpgsqlConnectionFactory();
            IRecordingCatalog recordingCatalog = new PostgresRecordingCatalog(npgsqlConnectionFactory, databaseSettingsRepository);
            IRecordingDataReader recordingDataReader = new PostgresRecordingDataSourceReader(npgsqlConnectionFactory, databaseSettingsRepository);
```

Заменить создание use case на:

```csharp
            var loadWorkspaceDataUseCase = WorkspaceLoadingComposition.CreateLoadWorkspaceDataUseCase(
                workspaceFolderSpecParser, recordingDataReader);
```

В конец списка аргументов `new MainForm(...)` добавить `recordingCatalog, databaseSettingsRepository`.

Добавить `using JSQViewer.Application.Workspace.Ports;` в начало файла.

- [ ] **Step 8: Собрать и прогнать тесты**

Выполнить:

```powershell
dotnet build .\JSQViewer.csproj -c Debug
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj
```

Ожидается: 0 ошибок, все тесты пройдены.

- [ ] **Step 9: Проверить вручную на живой базе**

Запустить `.\bin\Debug\JSQViewer.exe` и выполнить:

1. Нажать «Настройки БД…», ввести пароль `<пароль>`, нажать «Проверить подключение» — ожидается зелёная надпись «Подключение выполнено.». Нажать «ОК».
2. Нажать «Из БД…» — ожидается список из примерно 190 прогонов, отсортированный по дате начала по убыванию; идущий прогон выделен полужирным.
3. Отфильтровать по посту `B` и нажать «Показать» — в списке остаются только прогоны поста B.
4. Выбрать завершённый прогон поста B и нажать «ОК» — прогон загружается, в списке каналов видны коды без префикса (`T1`, `Pe`, `Tc`), графики строятся.
5. Открыть сведения о записи — в карточке видны модель, хладагент, оператор, тип испытания.
6. Перезапустить приложение — пароль сохранён, повторный ввод не требуется.

- [ ] **Step 10: Коммит**

```bash
git add UI/DatabaseSettingsDialog.cs UI/OpenFromDatabaseForm.cs UI/MainForm.cs Program.cs Infrastructure/Platform/DictionaryLocalizationService.cs JSQViewer.Tests/RecordingSummaryItemTests.cs
git commit -m "Добавлены диалоги выбора прогона из БД и настроек подключения"
```

---

### Task 8: Автообновление активного прогона

**Files:**
- Modify: `UI/MainForm.cs` (поля, конструктор, `LoadFolder`, новый обработчик таймера)
- Test: `JSQViewer.Tests/RecordingLiveRefreshTests.cs`

**Interfaces:**
- Consumes: `IRecordingDataReader` и `IRecordingCatalog` из Задач 5–6, `RecordingRowsToTestDataMapper.Append` из Задачи 4.
- Produces: `MainForm` получает ещё один параметр конструктора, идущий последним: `IRecordingDataReader recordingDataReader`.

- [ ] **Step 1: Написать падающий тест на дозагрузку через порт**

Создать `JSQViewer.Tests/RecordingLiveRefreshTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using JSQViewer.Application.Database;
using JSQViewer.Application.Workspace.Ports;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class RecordingLiveRefreshTests
    {
        private sealed class GrowingRecordingReader : IRecordingDataReader
        {
            private readonly RecordingRowsToTestDataMapper _mapper = new RecordingRowsToTestDataMapper();
            private long _nextTimestamp = 2000;

            public TestData ReadRecording(string recordingId)
            {
                return _mapper.Map(
                    RecordingSourceRef.Build(recordingId),
                    "B",
                    new List<RecordingAggregateRow>
                    {
                        new RecordingAggregateRow { ChannelId = "B-T1", TimestampMs = 1000, Value = 10.0 }
                    },
                    new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            }

            public TestData AppendNewWindows(TestData existing, string recordingId)
            {
                var rows = new List<RecordingAggregateRow>
                {
                    new RecordingAggregateRow { ChannelId = "B-T1", TimestampMs = _nextTimestamp, Value = 11.0 }
                };
                _nextTimestamp += 1000;
                return _mapper.Append(existing, "B", rows);
            }
        }

        [TestMethod]
        public void AppendNewWindows_ExtendsSeriesOnEveryTick()
        {
            var reader = new GrowingRecordingReader();
            TestData data = reader.ReadRecording("abc");
            Assert.AreEqual(1, data.RowCount);

            data = reader.AppendNewWindows(data, "abc");
            Assert.AreEqual(2, data.RowCount);

            data = reader.AppendNewWindows(data, "abc");
            Assert.AreEqual(3, data.RowCount);
            CollectionAssert.AreEqual(new long[] { 1000, 2000, 3000 }, data.TimestampsMs);
            CollectionAssert.AreEqual(new double?[] { 10.0, 11.0, 11.0 }, data.Columns["T1"]);
        }

        [TestMethod]
        public void SingleRecordingSource_IsDetectedFromSpec()
        {
            string recordingId;

            Assert.IsTrue(RecordingSourceRef.TryParse("jsqdb://recording/abc", out recordingId));
            Assert.AreEqual("abc", recordingId);
            Assert.IsFalse(RecordingSourceRef.IsRecordingSource(@"C:\data ; jsqdb://recording/abc"));
        }
    }
}
```

- [ ] **Step 2: Убедиться, что тесты падают или проходят**

Выполнить: `dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj --filter RecordingLiveRefreshTests`

Ожидается: 2 теста пройдены — вся используемая механика создана в Задачах 3–6. Тест фиксирует контракт, на который опирается таймер.

- [ ] **Step 3: Добавить таймер в MainForm**

В `UI/MainForm.cs` рядом с полем `_recordingCatalog` добавить:

```csharp
        private readonly IRecordingDataReader _recordingDataReader;
        private readonly Timer _liveRefreshTimer = new Timer();
        private string _liveRecordingId;
        private bool _liveRefreshInProgress;
        private bool _liveConnectionLost;
```

В конец списка параметров конструктора добавить `IRecordingDataReader recordingDataReader`, присвоить поле и настроить таймер:

```csharp
            _liveRefreshTimer.Tick += LiveRefreshTimerOnTick;
            _liveRefreshTimer.Enabled = false;
```

- [ ] **Step 4: Включать таймер после загрузки активного прогона**

В методе `LoadFolder`, сразу после строки `NotifySuccess(string.Format(Loc.Get("LoadedTest"), data.RowCount));` вставить:

```csharp
                UpdateLiveRefreshState(spec);
```

Добавить в `MainForm` методы:

```csharp
        private void UpdateLiveRefreshState(string spec)
        {
            _liveRefreshTimer.Enabled = false;
            _liveRecordingId = null;
            _liveConnectionLost = false;

            List<string> sources = ParseFolderSpec(spec);
            if (sources.Count != 1)
            {
                return;
            }

            string recordingId;
            if (!RecordingSourceRef.TryParse(sources[0], out recordingId))
            {
                return;
            }

            string status;
            try
            {
                status = _recordingCatalog.GetStatus(recordingId);
            }
            catch (Exception ex)
            {
                _logger.LogError("Reading recording status failed.", ex);
                return;
            }

            if (!string.Equals(status, "recording", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int intervalSeconds = _databaseSettingsRepository.Load().refresh_interval_seconds;
            _liveRecordingId = recordingId;
            _liveRefreshTimer.Interval = intervalSeconds * 1000;
            _liveRefreshTimer.Enabled = true;
            NotifySuccess(Loc.Get("RecordingLiveUpdating"));
        }

        private async void LiveRefreshTimerOnTick(object sender, EventArgs e)
        {
            if (_liveRefreshInProgress || string.IsNullOrEmpty(_liveRecordingId))
            {
                return;
            }

            _liveRefreshInProgress = true;
            string recordingId = _liveRecordingId;
            TestData current = _viewerSession.Data;
            if (current == null)
            {
                _liveRefreshInProgress = false;
                return;
            }

            try
            {
                TestData updated = await Task.Run(() => _recordingDataReader.AppendNewWindows(current, recordingId));
                if (IsDisposed || !string.Equals(recordingId, _liveRecordingId, StringComparison.Ordinal))
                {
                    return;
                }

                if (_liveConnectionLost)
                {
                    _liveConnectionLost = false;
                    NotifySuccess(Loc.Get("RecordingConnectionRestored"));
                }

                if (!ReferenceEquals(updated, current))
                {
                    _viewerSession.SetData(_folderBox.Text, updated);
                    BindLoadedData(updated, true);
                }

                string status = await Task.Run(() => _recordingCatalog.GetStatus(recordingId));
                if (!string.Equals(status, "recording", StringComparison.OrdinalIgnoreCase))
                {
                    _liveRefreshTimer.Enabled = false;
                    _liveRecordingId = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Live recording refresh failed.", ex);
                if (!_liveConnectionLost)
                {
                    _liveConnectionLost = true;
                    NotifyError(Loc.Get("RecordingConnectionLost"));
                }
            }
            finally
            {
                _liveRefreshInProgress = false;
            }
        }
```

Свойство `_viewerSession.Data` существует: `IViewerSession` объявляет `int DataVersion`, `bool IsLoaded`, `string Folder`, `TestData Data` и метод `SetData(string folder, TestData data)`. Дополнительное поле для хранения текущих данных не нужно.

- [ ] **Step 5: Останавливать таймер при закрытии формы**

В обработчике `FormClosing` (или в `Dispose`) добавить:

```csharp
            _liveRefreshTimer.Enabled = false;
            _liveRefreshTimer.Tick -= LiveRefreshTimerOnTick;
```

- [ ] **Step 6: Передать читатель в MainForm**

В `Program.cs` в конец списка аргументов `new MainForm(...)` добавить `recordingDataReader`.

- [ ] **Step 7: Собрать и прогнать тесты**

Выполнить:

```powershell
dotnet build .\JSQViewer.csproj -c Debug
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj
```

Ожидается: 0 ошибок, все тесты пройдены.

- [ ] **Step 8: Проверить автообновление вручную**

Запустить `.\bin\Debug\JSQViewer.exe` и выполнить:

1. Открыть из БД прогон со статусом «идёт запись» — в статусной строке появляется «Идёт запись: данные обновляются автоматически.».
2. Подождать три интервала обновления (по умолчанию 90 с) — правый край графика должен продвинуться, число точек вырасти. Зафиксировать значения до и после.
3. Открыть завершённый прогон — таймер не включается, сообщение об автообновлении не появляется.
4. Отключить сеть на 40 с — появляется «Нет связи с базой данных…», приложение не падает, уже загруженные данные остаются на экране. Вернуть сеть — появляется «Связь с базой данных восстановлена.» и график продолжает расти.

- [ ] **Step 9: Коммит**

```bash
git add UI/MainForm.cs Program.cs JSQViewer.Tests/RecordingLiveRefreshTests.cs
git commit -m "Добавлено автообновление активного прогона из БД"
```

---

### Task 9: Поставка, документация и версия

**Files:**
- Modify: `installer/JSQViewer.iss` (секция `[Files]`, строка 31)
- Modify: `Properties/AssemblyInfo.cs`
- Create: `doc/database_source_manual_checklist.md`
- Modify: `AGENTS.md`

**Interfaces:**
- Consumes: всё предыдущее.
- Produces: установочный пакет `artifacts/JSQViewer-Setup-0.4.0.exe`.

- [ ] **Step 1: Добавить сборки в инсталлятор**

В `installer/JSQViewer.iss` в секцию `[Files]` после строки с `{#MyAppExeName}` добавить:

```
Source: "..\artifacts\installer\JSQViewer.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\installer\Npgsql.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\installer\Microsoft.Bcl.AsyncInterfaces.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\installer\Microsoft.Bcl.HashCode.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\installer\System.Buffers.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\installer\System.Collections.Immutable.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\installer\System.Diagnostics.DiagnosticSource.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\installer\System.Memory.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\installer\System.Numerics.Vectors.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\installer\System.Runtime.CompilerServices.Unsafe.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\installer\System.Text.Encodings.Web.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\installer\System.Text.Json.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\installer\System.Threading.Channels.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\installer\System.Threading.Tasks.Extensions.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\installer\System.ValueTuple.dll"; DestDir: "{app}"; Flags: ignoreversion
```

Изменить строку версии на `#define MyAppVersion "0.4.0"`.

- [ ] **Step 2: Поднять версию сборки**

В `Properties/AssemblyInfo.cs` поднять `AssemblyVersion` и `AssemblyFileVersion` до `0.4.0.0`.

- [ ] **Step 3: Написать чек-лист ручной проверки**

Создать `doc/database_source_manual_checklist.md`:

```markdown
# Чек-лист ручной проверки источника данных PostgreSQL

Проверка выполняется на живой базе `192.168.66.100:5432 / jsq_db`.
Автоматических тестов на сеть нет намеренно: юнит-тесты работают на заглушках.

## Подготовка

1. Запустить `JSQViewer.exe`, открыть «Настройки БД…».
2. Проверить, что поля предзаполнены: `192.168.66.100`, `5432`, `jsq_db`, `jsq_user`.
3. Ввести пароль, нажать «Проверить подключение» — ожидается «Подключение выполнено.».
4. Нажать «ОК», закрыть и снова открыть приложение — пароль должен сохраниться.

## Исторический прогон

5. «Из БД…» — открывается список прогонов, отсортированный по дате начала по убыванию.
6. Фильтр по посту `B` — в списке только прогоны поста B.
7. Фильтр по диапазону дат — в списке только прогоны из диапазона.
8. Выбрать завершённый прогон, нажать «ОК» — данные загружаются, графики строятся.
9. Коды каналов в списке — без префикса поста: `T1`, `Pe`, `Tc`, а не `B-T1`.
10. Сверить график канала `T1` с тем же прогоном в веб-интерфейсе: форма кривой
    и разрывы в местах исключённых оператором окон должны совпадать.
11. Открыть сведения о записи — видны модель, хладагент, оператор, тип испытания.
12. Экспортировать в шаблон — файл формируется, каналы попадают в свои столбцы.

## Активный прогон

13. Открыть прогон со статусом «идёт запись» — появляется сообщение об
    автообновлении, строка выделена в списке полужирным.
14. Записать число точек, подождать три интервала (по умолчанию 90 с) — число
    точек выросло, правый край графика продвинулся.
15. Отключить сеть на 40 с — появляется «Нет связи с базой данных…», приложение
    не падает, загруженные данные остаются. Вернуть сеть — появляется сообщение
    о восстановлении, график продолжает расти.
16. Дождаться остановки прогона в веб-сервисе — автообновление прекращается само.

## Смешанная рабочая область

17. Загрузить прогон из БД, затем добавить папку с DBF — оба источника видны,
    совпавшие коды каналов разведены по источникам.
18. Загрузить два прогона разных постов — коды не конфликтуют.
19. Нажать «Обновить» — рабочая область перечитывается целиком без ошибок.

## Проверка поставки

20. Собрать инсталлятор, установить на машину без .NET-разработки.
21. Запустить установленное приложение и повторить пункты 1–8.
    Если появляется `FileLoadException` с упоминанием
    `System.Runtime.CompilerServices.Unsafe` — в поставку не попал
    `JSQViewer.exe.config` с binding redirects.
```

- [ ] **Step 4: Обновить AGENTS.md**

В раздел «Project Structure & Module Organization» дописать:

```markdown
Доступ к базе данных JSQ Laboratory живёт в `Infrastructure/Database/`; прикладной слой работает через порты `IRecordingCatalog` и `IRecordingDataReader` из `Application/Workspace/Ports/`. Сборки Npgsql лежат в `lib/npgsql/` и подключены через `HintPath`; `App.config` содержит обязательные binding redirects — без них Npgsql не загружается на .NET Framework 4.8.
```

В раздел «Testing Guidelines» дописать:

```markdown
Тесты работы с базой данных не обращаются к сети: порты подменяются заглушками. Проверка на живой базе выполняется вручную по `doc/database_source_manual_checklist.md`.
```

- [ ] **Step 5: Собрать релиз и пройти чек-лист**

Выполнить:

```powershell
dotnet build .\JSQViewer.csproj -c Release
dotnet test .\JSQViewer.Tests\JSQViewer.Tests.csproj
```

Скопировать содержимое `bin\Release\` в `artifacts\installer\`, собрать инсталлятор и пройти пункты 20–21 чек-листа.

- [ ] **Step 6: Коммит**

```bash
git add installer/JSQViewer.iss Properties/AssemblyInfo.cs doc/database_source_manual_checklist.md AGENTS.md
git commit -m "Подготовлена поставка источника данных PostgreSQL, версия 0.4.0"
```

---

## Самопроверка плана

**Покрытие спецификации:**

| Требование спецификации | Задача |
| --- | --- |
| Источник `jsqdb://recording/<id>` как третий тип | 3, 6 |
| Чтение карточки прогона в `Meta` | 6 |
| Чтение каналов из `channel_config` | 6 |
| Чтение рядов по `avg_value` с отсечением исключений | 6 |
| Срезание префикса поста | 4 |
| Разворот «длинного» результата в `TestData` с пропусками | 4 |
| Метки времени в epoch ms UTC | 4, 6 |
| Живое обновление по таймеру, только хвост | 8 |
| Автоостановка таймера при `status = stopped` | 8 |
| Пометка «нет связи» без модальных окон | 8 |
| Диалог выбора прогонов с фильтрами | 7 |
| Настройки подключения с дефолтами и «Проверить подключение» | 7 |
| Шифрование пароля DPAPI | 2 |
| Npgsql через `HintPath`, offline-сборка | 1 |
| Таймауты 10 с и 120 с | 1 |
| Обработка ошибок | 5, 6, 7, 8 |
| Юнит-тесты без сети | 1–8 |
| Ручной чек-лист | 9 |
| Поставка в инсталляторе | 9 |

Расхождение с текстом спецификации одно и отмечено в Задаче 7: настройки подключения оформлены отдельным `DatabaseSettingsDialog`, а не вкладкой в существующем `SettingsDialog`. Причина — `SettingsDialog` спроектирован под один результат типа `ViewerSettingsModel` и плоскую компоновку; вкладка потребовала бы его перестройки без пользы для задачи.

**Проверено при написании плана:** состав `IFileSystem` (семь членов) и наличие `IViewerSession.Data` сверены с кодом, заглушки и обработчик таймера приведены в соответствие. Работоспособность связки .NET Framework 4.8 + Npgsql 6.0.13 подтверждена запуском против живой базы; без binding redirects из Задачи 1 она не работает.
