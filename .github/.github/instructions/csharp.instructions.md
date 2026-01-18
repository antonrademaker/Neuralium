# C# / .NET Instructions

## Language & runtime
- C# (.NET 10)
- Nullable reference types enabled
- Implicit usings allowed

## Style
- Prefer records for immutable data.
- Prefer explicit interfaces for services.
- Avoid static state and singletons.
- Use `IOptions<T>` for configuration.

## Async
- All I/O async.
- No `.Result`, `.Wait()`, or blocking calls.

## Errors & logging
- Throw meaningful exceptions.
- Log with structured fields.
- Never swallow exceptions silently.
- Use OTel

## Tests
- xUnit
- Arrange / Act / Assert
- Deterministic, no time dependence or use ITimeProvider.
