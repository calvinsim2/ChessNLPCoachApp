---
name: implement-unit-test
description: Audit or implement positive and negative xUnit tests for ASP.NET Core projects using FakeItEasy exclusively, placing all tests in the designated <ProjectName>Test project.
argument-hint: "[audit|fix] [optional file, folder, project, or solution]"
disable-model-invocation: true
---

# Implement Unit Tests — ASP.NET Core (Generic)

## Purpose

Find application methods that lack adequate unit tests and, when requested, implement tests that cover meaningful positive and negative scenarios. This skill works across ASP.NET Core MVC, Web API, and other C#/.NET application projects, whether the repository contains a monolith or multiple microservices.

**Non-negotiable requirements:**

1. **All unit tests must be in the designated test project named `<ProjectName>Test`.** Never place tests inside an application project or create a second test project.
2. **Use FakeItEasy exclusively** for fakes, stubs, call configuration, and interaction assertions. Do not use Moq, NSubstitute, hand-written mock classes, or any other mocking framework.
3. Mirror the relevant application project's folder structure under its own folder within `<ProjectName>Test`.
4. Never alter production application logic to make a test pass.
5. Do not add or download NuGet packages. Report missing dependencies for manual review.
6. **Keep unit tests simple and readable.** Whenever a fake dependency call needs configuration or verification, prefer a direct, strongly typed `A.CallTo(() => _dependencyMock.Method(...))` expression written inside that individual test. Do not invent complex call-matching helpers to avoid this rule.

## Invocation and scope

Support two modes:

- `audit`: Inspect the requested scope, report missing/inadequate tests, and **make no file changes**.
- `fix`: Inspect the requested scope, add or improve safe unit tests **only in `<ProjectName>Test`**, and validate the results.

Interpret `$ARGUMENTS` as follows:

1. First argument: `audit` or `fix`.
2. Remaining arguments, if any: file, folder, C# project, or solution to inspect.
3. Default mode: `audit`.
4. Default scope: the current repository.

Before beginning, read relevant `CLAUDE.md` files and respect project-specific instructions unless they contradict the non-negotiable requirements above. Reuse the unit-test framework already configured in the test project; the examples below use **xUnit**, as in the original skill.

## Identify the test project and application layout

1. Determine the application/repository project name from the solution and existing `.csproj` files. Call this `<ProjectName>`; it is a placeholder, not a literal project name.
2. Find the corresponding **existing** test project named exactly `<ProjectName>Test` (usually `<ProjectName>Test.csproj`). This is the **only** permitted destination for unit tests.
3. When the repository contains multiple application projects, identify the correct source-project name for each tested file. Use that name as the first folder beneath `<ProjectName>Test`.
4. Distinguish a monolith from a multi-project/microservice solution by inspecting actual solution/project structure. **Do not infer microservices solely from folder names**, and do not assume a different test project is needed for each microservice.
5. If `<ProjectName>Test` is missing, or multiple test projects match ambiguously, **report for manual review and do not write tests**. Do not create a test project automatically.
6. If a test project exists but lacks FakeItEasy, the required unit-test framework, a necessary project reference, or other dependencies, report the setup issue; do not install packages or modify production projects.

### Generic file naming convention

For a production source file named `<DomainName>Controller.cs`, its unit-test file must be `<DomainName>ControllerTest.cs`.

For a production source file named `<DomainName>Service.cs`, its unit-test file must be `<DomainName>ServiceTest.cs`.

In general:

`<SourceFileNameWithoutExtension>Test.cs`

Use the same convention for handlers, validators, processors, and other testable classes. Keep tests for different methods declared in one production class in **one corresponding test file**, unless the existing test project has a clear, consistent and compatible convention. Use a test class named `<SourceClassName>Test`.

### Folder structure — monolithic application

Example application:

```text
<ProjectName>/                         # Solution / repository
├── ProjectApp/                       # ASP.NET Core application project
│   ├── Controllers/
│   │   └── <DomainName>Controller.cs
│   └── Services/
│       └── <DomainName>Service.cs
└── <ProjectName>Test/                # The ONE designated test project
    ├── <ProjectName>Test.csproj
    └── ProjectApp/                   # Mirrors the source project name
        ├── Controllers/
        │   └── <DomainName>ControllerTest.cs
        └── Services/
            └── <DomainName>ServiceTest.cs
```

### Folder structure — microservice or multi-project application

Example solution with two microservice projects; **both belong to the same designated test project**:

```text
<ProjectName>/
├── ProjectAppA/                      # Microservice A (source project)
│   ├── Controllers/
│   │   └── <DomainName>Controller.cs
│   └── Services/
│       └── <DomainName>Service.cs
├── ProjectAppB/                      # Microservice B (source project)
│   ├── Controllers/
│   │   └── <OtherDomainName>Controller.cs
│   └── Services/
│       └── <OtherDomainName>Service.cs
└── <ProjectName>Test/               # The ONE designated test project
    ├── <ProjectName>Test.csproj
    ├── ProjectAppA/
    │   ├── Controllers/
    │   │   └── <DomainName>ControllerTest.cs
    │   └── Services/
    │       └── <DomainName>ServiceTest.cs
    └── ProjectAppB/
        ├── Controllers/
        │   └── <OtherDomainName>ControllerTest.cs
        └── Services/
            └── <OtherDomainName>ServiceTest.cs
```

**Path-mapping rule:** Given source path `<SourceProject>/<RelativeFolders>/<SourceFile>.cs`, place its tests at `<ProjectName>Test/<SourceProject>/<RelativeFolders>/<SourceFile>Test.cs`. Do not append `Test` to the `<SourceProject>` **folder** name. Only the **test project** and the **test source file** use the `Test` suffix.

Use names derived from the actual repository, never hard-coded names from another application. The angle-bracket values above are documentation placeholders and must be replaced with real names when files are created.

## What to inspect

Inspect `.cs` application source files in the requested scope, excluding:

- `bin/`, `obj/`, `.git/`, `node_modules/`, and `Logs/`
- Generated `*.g.cs`, `*.designer.cs`, and files with an `<auto-generated>` header
- `Program.cs`
- Files inside `Common/`, `Database/`, `Interfaces/`, `Helpers/`, and `Repository/` folders (retained from the original skill's default exclusions)
- Existing test projects and test files, **as production targets**; still read them to assess existing test coverage

Inspect eligible concrete methods, including asynchronous methods, controllers, services, and other application logic. Exclude declarations with no implementation (such as interface members). Prefer testing private/internal implementation details through public or otherwise intended entry points rather than adding reflection-based tests or changing production accessibility.

The folder exclusions are defaults for this skill; if a user explicitly requests testing one of those excluded folders, inspect it without modifying production code. Never modify generated source files.

## Definition of sufficient unit-test coverage

For each eligible method:

1. Provide **at least one meaningful positive scenario** and **one meaningful negative, edge, or failure scenario**, if the method has such a scenario.
2. Exercise all reachable executable lines and meaningful branches in the method **through the tests**, to the extent possible without changing production code.
3. Assert observable outcomes: return values, state changes, errors, response data/status, or relevant dependency interactions. Tests must not merely invoke methods without assertions.
4. Use a unit-test isolation boundary: fake external dependencies using FakeItEasy, and invoke the real system under test.
5. Avoid duplicate tests if adequate tests already exist. Add only missing scenarios, or improve inaccurate tests.
6. If 100% line coverage cannot be verified because coverage tooling is unavailable, **do not claim measured 100% coverage**. Explain what was exercised and mark quantitative coverage as unverified.
7. If a truly meaningful negative scenario cannot be produced without altering application logic, report the limitation for manual review rather than inventing a brittle or artificial test.

### Positive scenarios

Examples include:

- A valid input produces the intended output without an unexpected exception.
- A controller returns the appropriate successful 2xx response, where its contract calls for one.
- A collection-returning method produces expected records when records exist.
- A service writes/updates data correctly and reports success.

### Negative, edge, or failure scenarios

Examples include:

- Invalid input, missing records, null/empty input, or business-rule validation failure.
- A controller returns an appropriate 4xx or 5xx response **when the method's contract actually specifies that behavior**.
- A collection-returning method handles an empty result correctly.
- A dependency throws an exception; the system under test throws, translates, or handles it as intended.

**Important distinction:** An empty collection can be a *negative data scenario* even when an ASP.NET Core endpoint correctly returns `200 OK`. Do not assert a 4xx/5xx status merely because a test has `_Negative_` in its name. Match the application's actual contract and expected behavior. Exceptions are not required in every negative test.

## FakeItEasy-only testing rules

- Create dependency fakes with `A.Fake<T>()`.
- **Mandatory default syntax for every configurable dependency call:** write a direct, strongly typed `A.CallTo(() => _dependencyMock.Method(...))` expression. Use `.Returns(...)`, `.Throws(...)`, or an async-compatible equivalent supported by the installed FakeItEasy version.
- **Mandatory default syntax for interaction assertions:** reuse the same explicit `A.CallTo(() => _dependencyMock.Method(...))` expression and append `.MustHaveHappenedOnceExactly()`, `.MustNotHaveHappened()`, or another relevant call-count assertion.
- **Never** use `Mock<T>`, `Mock.Of<T>`, `Moq`, `NSubstitute`, `Substitute.For`, or hand-written fake/mock classes.
- Do not fake the class or method being tested. Instantiate the real system under test and inject FakeItEasy fakes for its dependencies.
- Use real lightweight data objects when possible. The `Mock` suffix below identifies *test data or fake dependencies*, not necessarily a dynamically mocked object.
- If a dependency cannot be faked safely using the rules below, flag it for manual review; do not substitute a different mocking library or generate complicated workaround code.

### Simple, direct FakeItEasy calls — mandatory readability standard

**The preferred pattern must appear directly in each test's Arrange section:**

```csharp
// Arrange
List<DomainDto> domainDtosMock = new() { new DomainDto() };
A.CallTo(() => _domainServiceMock.GetAllAsync()).Returns(domainDtosMock);

// Act: call the REAL method being tested, not a fake.
var result = await _domainController.GetAllAsync();

// Assert: verify observable output first.
Assert.NotNull(result);
A.CallTo(() => _domainServiceMock.GetAllAsync()).MustHaveHappenedOnceExactly();
```

Apply these rules **to every new or updated test**:

1. **One clear Arrange → Act → Assert flow.** In Arrange, create only the minimum necessary mock data and configure fakes with explicit `A.CallTo(() => ...).Returns(...)` (or `.Throws(...)`) calls. In Act, invoke the real method under test. In Assert, check the actual returned values/behavior first; verify fake calls only when useful.
2. **Do not hide `A.CallTo(...)` inside helper methods.** Avoid wrappers such as `CallToSendAsync()`, `ConfigureResponse(...)`, `SetupMock(...)`, or factory methods whose purpose is to build or obscure FakeItEasy call specifications. Each test should show the dependency calls being configured without navigating to another method.
3. **Do not use indirect or string-based matching** such as `A.CallTo(fake).Where(call => call.Method.Name == "SomeMethod").WithReturnType<...>()`. Prefer compiler-checked lambda expressions. Do not use reflection to identify calls.
4. **Avoid unnecessary complexity:** do not use `.Invokes(...)`, `.ReturnsLazily(...)`, `call.GetArgument<T>(...)`, captured-request fields, custom `FakeItEasy.Configuration.*` return types, synchronous blocking (`.Result` or `.GetAwaiter().GetResult()`), or multi-step configuration chains when a direct `.Returns(...)` / `.Throws(...)` can express the scenario. If an unavoidable advanced setup would violate the required simple style, report the test as a **manual-review item** rather than generating it.
5. **Only mock dependencies and calls that need control.** Ordinary calls to the real method under test, xUnit assertions, C# constructors, and test-data initialization are **not** wrapped in `A.CallTo`. A fake method that needs no configuration does not require a redundant `A.CallTo` setup.
6. **Use simple argument matching when needed:** `A.CallTo(() => _domainServiceMock.GetByIdAsync(A<Guid>._)).Returns(domainDtoMock);`. Prefer exact arguments when that better explains the test, and avoid elaborate predicates unless essential to validate the contract.
7. **Use minimal helpers.** A small helper for repetitive *test-data creation* is permitted when it makes tests shorter and clearer; helper methods that encapsulate fake-call arrangements or assertions are not permitted by default. Avoid introducing extra private state when local variables suffice.
8. **Readability takes priority over forced coverage.** Do not write hard-to-understand test scaffolding just to cover a line. When meaningful behavior cannot be isolated with the required direct FakeItEasy syntax, document the limitation for manual review; never modify production code automatically.

#### Special case: `HttpClient` / `HttpMessageHandler.SendAsync`

`HttpMessageHandler.SendAsync` is `protected`, so an external test ordinarily **cannot** express it as `A.CallTo(() => _httpMessageHandlerMock.SendAsync(...))`. `HttpClient.SendAsync` itself is not a normally interceptable virtual call. Do not force the preferred syntax where C# access rules or FakeItEasy's limitations make it impossible.

- **First** check whether the existing production code already injects an accessible interface/virtual abstraction for the HTTP operation (for example, an existing client interface). If so, fake that dependency directly with `A.Fake<T>()` and `A.CallTo(() => ...).Returns(...)`.
- **Otherwise**, **do not** generate a helper like `CallToSendAsync()` with `A.CallTo(_httpMessageHandlerMock).Where(...).WithReturnType<...>()`, a custom `HttpMessageHandler` subclass, or another framework as a workaround.
- Mark the HTTP-dependent scenario as **manual review** and explain that testing the protected `SendAsync` call cannot meet the requested direct-call style with the current production seam. Recommend an optional, separately approved design change to expose an injectable abstraction; **never change production code in this skill**.

This exception is deliberate: it is better to report a small testing limitation than to generate technically valid but hard-to-read unit tests.

### Variable and instance naming

Use the suffix `Mock` for fake dependencies and test data configured to simulate another component's output; maintain idiomatic C# casing:

```csharp
private readonly IDomainService _domainServiceMock = A.Fake<IDomainService>();

List<DomainDto> domainDtosMock = new();

// Real class under test: no "Mock" suffix.
private readonly DomainController _domainController;
```

Do not append `Mock` to the real system under test or its actual returned result (for example, `result`, `response`, or `domainDtos`).

## Test implementation conventions

Use the existing test framework's features (the following illustrates xUnit):

- Prefer `[Fact]` for one scenario, and `[Theory]` for several input variants.
- Name tests: `<MethodName>_<Positive|Negative>_<Scenario>_<ExpectedResult>`.
- Examples: `GetAllAsync_Positive_RecordsExist_ShouldReturnItems` and `GetAllAsync_Negative_NoRecordsExist_ShouldReturnEmptyCollection`.
- Arrange in this order: **test data → `A.CallTo(...)` configuration → actual method call → assertions**.
- Where helpful, assert FakeItEasy dependency calls **after** invoking the system under test, using the same direct `A.CallTo(() => _dependencyMock.Method(...))` form.
- Keep mock arrangements inside each test; do not create wrapper helpers for call matching, fake setup, or response capture.
- Use the simplest possible code that actually checks the intended behavior. Report scenarios requiring complex non-lambda FakeItEasy interception for manual review.
- Use `Assert.ThrowsAsync<TException>` when the method should propagate a particular exception; otherwise assert its intended error result.
- Verify the real returned payload and not only whether an exception was thrown.
- Replace all documentation placeholders with actual types, names, and method signatures found in the repository.

### Generic FakeItEasy + xUnit example

This example is **illustrative**: `DomainController`, `IDomainService`, and `DomainDto` stand in for the repository's actual types. Adapt constructors, interfaces, return types, and status/payload assertions to the actual production code; do not copy non-existent types into a project.

```csharp
public sealed class DomainControllerTest
{
    private readonly IDomainService _domainServiceMock = A.Fake<IDomainService>();
    private readonly DomainController _domainController;

    public DomainControllerTest()
    {
        _domainController = new DomainController(_domainServiceMock);
    }

    [Fact]
    public async Task GetAllAsync_Positive_RecordsExist_ShouldReturnItems()
    {
        // Arrange: test data and FakeItEasy behavior.
        List<DomainDto> domainDtosMock = new() { new DomainDto() };
        A.CallTo(() => _domainServiceMock.GetAllAsync()).Returns(domainDtosMock);

        // Act: invoke the real controller.
        var result = await _domainController.GetAllAsync();

        // Assert: adapt to the actual result contract.
        var response = Assert.IsType<OkObjectResult>(result.Result);
        var domainDtos = Assert.IsAssignableFrom<IEnumerable<DomainDto>>(response.Value);
        Assert.Single(domainDtos);
    }

    [Fact]
    public async Task GetAllAsync_Negative_NoRecordsExist_ShouldReturnEmptyCollection()
    {
        // Arrange.
        List<DomainDto> domainDtosMock = new();
        A.CallTo(() => _domainServiceMock.GetAllAsync()).Returns(domainDtosMock);

        // Act.
        var result = await _domainController.GetAllAsync();

        // Assert: 200 OK may be correct for an empty collection.
        var response = Assert.IsType<OkObjectResult>(result.Result);
        var domainDtos = Assert.IsAssignableFrom<IEnumerable<DomainDto>>(response.Value);
        Assert.Empty(domainDtos);
    }
}
```

The example assumes an action returning `ActionResult<IEnumerable<DomainDto>>`, a service method with a compatible async collection result, a parameterless `DomainDto` constructor, and a controller that responds with `Ok(...)`. **Inspect real source code instead of assuming those contracts apply.**

## Safety rules

1. In `fix` mode, create or edit files **only inside `<ProjectName>Test`**. This includes the monolith/microservice folders within that test project.
2. Never change production `.cs` files, production `.csproj` files, application configuration, existing business logic, or other projects.
3. Do not create additional test projects or test files outside the designated test project.
4. Do not install, upgrade, or download dependencies. If the correct framework/FakeItEasy/project reference is missing, report it for manual setup.
5. Do not remove unrelated tests, weaken assertions, or silence failing tests merely to obtain a green build.
6. Do not introduce indirect FakeItEasy interception, hidden call-setup helpers, or complicated HTTP handler fakes to force coverage. Report such cases for manual review.
7. Do not amend production logic that is difficult or impossible to test; report it for manual review.
8. Do not automatically commit or push changes.

## Audit mode procedure

1. Make **no changes** to any files.
2. Resolve the intended test project and map each eligible source file to its expected test path.
3. Scan source files and existing corresponding test files in scope.
4. Report gaps in positive/negative scenarios, missing assertions, test isolation, uncovered code paths, **and test-readability violations** (indirect FakeItEasy call matching, hidden fake configuration, unnecessary `.Invokes(...)` / `.ReturnsLazily(...)`, or HTTP interception workarounds).
5. For each finding, show where applicable:
   - production file path and method name
   - line number(s) and a concise relevant code excerpt
   - current test file/path and its existing behavior (or that the test is missing)
   - recommended test file, test case, or correction
   - reason and whether the item is safe to address automatically or needs manual review
6. Group findings into **safe automatic fixes** and **manual-review items**.
7. Summarize number of production files scanned, methods reviewed, findings, safe fixes, and manual-review items.

Do not fabricate line numbers, code excerpts, test outcomes, or measured coverage. If line numbers are unavailable, report the method name and path instead.

## Fix mode procedure

1. Resolve the intended test project and scan the requested scope.
2. Confirm the designated `<ProjectName>Test` project and required test dependencies already exist.
3. Add or improve tests **only** under that project, following the source-project folder mapping and file naming rules.
4. Ensure eligible methods receive meaningful positive and negative/edge cases; include additional tests to exercise remaining reachable branches **only when they can be written clearly under the direct-call rules**. Otherwise list the gaps for manual review.
5. Do not change manual-review items or application logic.
6. Review the resulting Git diff; confirm that every changed file belongs to `<ProjectName>Test`.
7. Check that test names, data, actual method signatures, fake call configurations, and assertions agree with the source implementation. Reject unnecessary wrapper helpers, `.Where(call => call.Method.Name == ...)` interception, and overcomplicated test arrangements.
8. Run `git diff --check` if the repository uses Git.
9. Follow applicable build/test instructions in `CLAUDE.md`. Otherwise use the narrowest relevant commands available, such as:

   ```bash
   dotnet build <ProjectName>Test/<ProjectName>Test.csproj
   dotnet test <ProjectName>Test/<ProjectName>Test.csproj --no-build
   ```

10. If compatible coverage tooling is **already installed/configured**, run it and report measured results. Otherwise identify line/branch coverage as unverified; do not install new packages.
11. Report all failures and blockers without silently altering production code or test scope.
12. Do not commit changes.

## Final report

After either mode, report:

- Mode and scope inspected
- Designated test project and detected application layout (monolith or multi-project/microservice)
- Number of production files scanned and methods reviewed
- Files created/changed (or explicitly none for audit)
- Methods for which new positive/negative cases were added, and remaining coverage gaps
- Safe automatic findings and manual-review items
- Build/test commands attempted and their actual results; coverage results **only if measured**
- Any relevant remaining warnings/errors
- A concise Git diff summary, when applicable

**Final check:** Every new or edited test must be inside the single `<ProjectName>Test` project, use FakeItEasy for dependency fakes, mirror the source project's structure, and prefer direct `A.CallTo(() => _dependencyMock.Method(...))` expressions visible inside each test. Any scenario that cannot meet these constraints must be disclosed as a manual-review gap, not disguised behind complex helper methods.
