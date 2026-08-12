# Defect Detection Patterns

> Each pattern is a searchable rule the gap agent (`zvec-gap-detection-expert`) and the
> pattern scanner (`scan_patterns.py`) apply to every diff. Patterns are expressed as
> regex / code-structure rules with severity and fix instructions.

## P1 — Must Fix Before Merge

### P1-01: Null Assignment Before Recovery
- **pattern:** `_nativeCollection = null` (or any field set to null/invalid state) followed by a method call that can throw
- **check:** If a field is set to null/invalid state before a subsequent operation that can throw, and there is no try/catch restoring the previous value, flag as P1.
- **fix:** Prepare new value first, then atomic swap. Never null-then-recover.

### P1-02: Dummy Test
- **pattern:** `Assert.True(true)` | `Assert.True(false)` | `Assert.Pass()`
- **fix:** Replace with real assertion testing actual behavior.

### P1-03: Swallowed Exception in Critical Path
- **pattern:** `catch { }` | `catch (Exception) { }` in non-test code
- **exception:** Allowed if a comment explains why (e.g., `// Dispose never throws per spec`)
- **fix:** Add logging or explicit justification comment.

### P1-04: Sync-Over-Async in Query Path
- **pattern:** `.Result` | `.Wait()` | `.GetAwaiter().GetResult()` in `src/` (not `tests/`)
- **fix:** Use `await` or configure with `.ConfigureAwait(false)`.

## P2 — Track and Fix Soon

### P2-01: Missing Type Dispatch
- **pattern:** `switch` / `match` expression that handles some types but has an `object` fallback
- **check:** Are common BCL types (`Guid`, `DateTime`, `decimal`) handled explicitly?
- **fix:** Add explicit cases for `Guid` and `DateTime` at minimum.

### P2-02: Unannotated Reflection
- **pattern:** `Type.GetProperties` | `Activator.CreateInstance` | `PropertyInfo.SetValue` NOT preceded by `[RequiresUnreferencedCode]` on the calling method
- **fix:** Add `[RequiresUnreferencedCode]` + `[RequiresDynamicCode]` annotations, or use the source generator path.

### P2-03: Array Allocation in Vector Path
- **pattern:** `new float[` | `.ToArray()` in files containing `Search` or `Query` or `Vector`
- **fix:** Use `ReadOnlyMemory<float>` pin path. `ArrayPool<T>` if allocation is unavoidable.

### P2-04: Missing Test for New Public Method
- **pattern:** New public method in `src/` with no corresponding `[Fact]` / `[Theory]` in `tests/`
- **check:** Compare method names in diff against test method names.
- **fix:** Write failing test before implementation (TDD).

### P2-05: Hardcoded String in Non-Constant File
- **pattern:** String literal in `src/` files that are NOT `*Constants.cs`, `*ErrorMessages.cs`, `*Messages.cs`
- **exception:** XML doc comments, test assertions, log messages.
- **fix:** Move to `ZVecErrorMessages` or `ZVecConstants`.

### P2-06: Lock Without Timeout/Recovery
- **pattern:** `lock (` or `Monitor.Enter` without corresponding try/catch for state recovery
- **check:** Does the lock protect a state transition that could leave state inconsistent on exception?
- **fix:** Use try/catch inside lock to restore previous state on failure.

### P2-07: HttpClient Without Factory
- **pattern:** `new HttpClient(` in `src/` files
- **check:** HttpClient should be injected via `IHttpClientFactory` to avoid socket exhaustion
- **fix:** Use `services.AddHttpClient()` and inject `IHttpClientFactory` or `HttpClient` via DI

## P3 — Polish

### P3-01: Missing XML Doc
- **pattern:** Public/internal member without `/// <summary>`
- **fix:** Add XML documentation.

### P3-02: Overly Defensive Copy
- **pattern:** `.ToArray()` or `new List<` where source is already the target type
- **fix:** Remove redundant copy; use the source directly.

### P3-03: Magic Boolean Parameter
- **pattern:** Method with `bool` parameter that is not an event handler or obvious flag
- **fix:** Replace with enum for clarity.

## Adding New Patterns

When a new defect class is observed:

1. Add the pattern under the appropriate severity section (P1/P2/P3).
2. Include `pattern`, `check` (if non-trivial), `fix`, and any `exception`.
3. Update `scan_patterns.py` to encode the regex so the scanner can detect it automatically.
4. Add a regression test in the next commit that would have caught the original defect.
