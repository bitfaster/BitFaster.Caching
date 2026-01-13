BitFaster.Caching provides high performance, thread-safe in-memory caching primitives.

# Development Ethos

- This library is performance first, code is carefully optimized at the expense of readability or maintainability.
- Meta programming based on generics and structs enable the .NET JIT to elide unused code, devirtualize methods and perform other optimizations. Do not change structs into classes unless explicitly asked.
- API compatibility is important, unless explicitly asked, assume only additive changes can be made to the public API surface.

# Coding Conventions and Style

- We follow the standard [Microsoft C# Coding Conventions](learn.microsoft.com) and use the built-in `dotnet format` tool to enforce style.
- **Action Hook:** After generating or modifying any code, you must run `dotnet format` to ensure consistency.
