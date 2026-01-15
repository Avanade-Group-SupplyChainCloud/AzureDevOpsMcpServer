# Copilot custom instructions (AzureDevOpsMcpServer)

These instructions apply when generating or modifying code in this repo.

## Critical: Copilot Studio compatibility (no nested objects)

Copilot Studio (Power Platform) is fragile with nested object schemas and will often reshape, drop, or corrupt nested objects/arrays-of-objects (commonly surfacing as “ADTL” / schema issues).

**Rule:** When designing tool inputs/outputs (MCP tools, connector contracts, DTOs meant for Copilot Studio), avoid nested objects.

### Do

- Prefer **flat parameters** (`string`, `int`, `bool`, `double`) for tool method signatures.
- If a complex structure is needed, accept it as a **single JSON string parameter** named `...Json` (e.g., `updatesJson`, `daysOffJson`, `workingDaysJson`) and parse it server-side.
- Prefer **flat outputs** or return a **JSON string** for complex results.
- Keep JSON examples simple and stable (flat fields; arrays of primitives are OK; arrays of objects should be avoided unless unavoidable and then passed as a string).

### Don’t

- Don’t add parameters like `object`, `Dictionary<string, object>`, nested DTOs, or any schema that produces nested objects in the generated tool contract.
- Don’t return raw Azure DevOps SDK types directly when they contain deep/nested graphs; instead serialize a minimal, flattened response (or stringify JSON).

### Patterns to follow in this repo

- Prefer the existing “`...Json` string” pattern used by tools like `update_work_item`.
- When you need a list of updates, keep the MCP signature flat and pass the list via a JSON string:
  - ✅ `UpdateWorkItem(int id, string updatesJson)`
  - ❌ `UpdateWorkItem(int id, List<FieldUpdate> updates)`

### If you must carry nested data

- Encapsulate it as a string (`json`, `yaml`, or base64) and document the expected format.
- Provide validation and clear error messages when parsing fails.

## General repo conventions

- Keep changes minimal and aligned with existing patterns.
- Prefer `System.Text.Json` for JSON parsing/serialization.
- When changing tool contracts, update `README.md` examples accordingly.
