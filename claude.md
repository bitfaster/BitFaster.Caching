BitFaster.Caching provides high performance, thread-safe in-memory caching primitives.

# Development Ethos

- This library is performance first, code is carefully optimized at the expense of readability or maintainability.
- Meta programming based on generics and structs enable the .NET JIT to elide unused code, devirtualize methods and perform other optimizations. Do not change structs into classes unless explicitly asked.
- API compatibility is important, unless explicitly asked, assume only additive changes can be made to the public API surface.

# Coding Conventions and Style

- We follow the standard [Microsoft C# Coding Conventions](learn.microsoft.com) and use the built-in `dotnet format` tool to enforce style.
- **Action Hook:** After generating or modifying any code, you must run `dotnet format` to ensure consistency.

# Unit Test Guidelines

- Each class should have one file containing the unit tests for that class. For example, Foo.cs should have an associated FooTests.cs. Do not introduce additional test files. 
- Each test should test only one unit of work. The name of the test must have a clear association with the assert.
- Name tests using the pattern `UnitOfWorkStateUnderTestExpectedBehavior` (typically `MethodName_..._...`, but no underscores in test name), so the name reads like a clear statement of what must be true.
- Define the “unit of work” as the in-memory use case starting at a public method and ending in one of: return/exception, system state change, or call to a third party (via mocks).
- Include both the condition and the outcome in the name: the relevant input or state being exercised and the expected behavior, so readers don’t need to open the test to understand it.
- Prefer readable, declarative names over short or numbered names; longer is fine if it improves intent and future maintenance.
- Name test variables to match intent (for example, `emptyString`, `badData`, `nonInitializedPerson`) and keep assertions and messages aligned with the requirement being tested.
