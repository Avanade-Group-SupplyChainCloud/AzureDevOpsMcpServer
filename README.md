# Azure DevOps MCP Server

This is a Model Context Protocol (MCP) server for Azure DevOps, built with .NET 10 and ASP.NET Core.

## Copilot Studio compatibility (important)

Copilot Studio (Power Platform) is fragile with nested object schemas and can reshape/drop nested objects (often surfacing as “ADTL” / schema issues).

In this repo, MCP tools are intentionally designed to avoid nested object parameters:

- Prefer flat parameters (`string`, `int`, `bool`, `double`).
- If a complex structure is needed, accept it as a single JSON string parameter named `...Json` (for example: `updatesJson`, `daysOffJson`, `workingDaysJson`) and parse server-side.
- Prefer flat outputs; if an API returns a deep graph, return a minimal flattened object or a serialized JSON string.

## Features

This repo contains three MCP server “agents”, each exposing its own set of tools:

### Manager Agent Tools (39 total)

#### Core
- `list_projects`: List all projects in the Azure DevOps organization. Supports filtering by state and name.
- `list_teams`: List all teams in the Azure DevOps organization.

#### Work Items & Queries
- `get_work_item`: Get a work item by ID with optional field expansion.
- `get_work_items_batch`: Get multiple work items by IDs in a single batch request.
- `get_work_items_by_ids`: Get multiple work items by their IDs in a single request.
- `create_work_item`: Create a new work item (Bug, Task, User Story, etc.) in a project.
- `update_work_item`: Update fields on an existing work item.
- `list_work_item_comments`: Get comments on a work item.
- `add_work_item_comment`: Add a comment to a work item.
- `list_work_item_revisions`: Get revision history of a work item.
- `add_child_work_items`: Create child work items under a parent work item.
- `link_work_items`: Link two work items together with a specified link type.
- `run_wiql_query`: Execute a WIQL (Work Item Query Language) query and return matching work items.
- `get_work_item_types`: Get all work item types available in a project.
- `get_work_item_fields`: Get all fields available for work items.
- `get_work_item_history`: Get the revision history of a work item.
- `get_executive_summary`: Get an executive summary for a work item, including children recursively.
- `get_saved_queries`: Get saved queries (My Queries or Shared Queries) in a project.
- `run_saved_query`: Execute a saved query by its ID and return the results.
- `get_query`: Get a saved work item query by ID or path.
- `get_query_results`: Execute a saved query and get results.

#### Iterations & Capacity
- `list_iterations`: List all iterations (sprints) for a team.
- `get_current_iteration`: Get the current iteration (sprint) for a team.
- `get_iteration_capacities`: Get an iteration's capacity for all teams.
- `get_team_capacity`: Get the capacity settings for a team in a specific iteration.
- `update_team_member_capacity`: Update capacity for a team member in an iteration.
- `get_team_days_off`: Get the team days off for a specific iteration.
- `set_team_days_off`: Set days off for the team in a specific iteration.
- `get_team_settings`: Get the team settings including backlog iteration, default iteration, and working days.
- `update_team_settings`: Update team settings like default iteration, backlog iteration, and working days.

#### Search
- `search_code`: Search Azure DevOps Repositories for code matching the search text.
- `search_wiki`: Search Azure DevOps Wiki for pages matching the search text.
- `search_work_items`: Search for work items by text across title, description, and other fields.
- `search_workitem`: Search Azure DevOps Work Items matching the search text.

#### Wiki
- `list_wikis`: List all wikis in a project or organization.
- `get_wiki`: Get details of a specific wiki by identifier.
- `list_wiki_pages`: List pages in a wiki with optional pagination.
- `get_wiki_page`: Get wiki page metadata (without content).
- `get_wiki_page_content`: Get wiki page content as markdown.
- `create_or_update_wiki_page`: Create a new wiki page or update an existing one.

### Infra Agent Tools (35 total)

#### Builds
- `get_build`: Get detailed information about a specific build.
- `get_build_logs`: Get the log entries for a build.
- `get_build_log_content`: Get the content of a specific build log.
- `get_build_timeline`: Get the timeline (stages, jobs, tasks) of a build.
- `get_build_changes`: Get the source changes associated with a build.
- `get_build_work_items`: Get work items associated with a build.
- `cancel_build`: Cancel a running build.
- `get_build_artifacts`: Get the artifacts published by a build.
- `get_pipeline_definition`: Get detailed information about a pipeline/build definition.
- `get_pipeline_runs`: Get recent runs of a specific pipeline.
- `queue_build_with_parameters`: Queue a new build with custom parameters and variables.

#### Pipelines
- `list_builds`: List builds in a project, optionally filtered by pipeline definition.
- `list_pipelines`: List all pipeline/build definitions in a project.
- `get_run`: Get details of a specific pipeline run.
- `list_runs`: List runs for a specific pipeline.
- `run_pipeline`: Queue a new build/pipeline run for a definition.
- `get_build_status`: Get the status of a specific build.
- `get_build_definition_revisions`: Get revision history of a build definition.
- `update_build_stage`: Update a build stage (cancel, retry, or run). Useful for stage-level control in YAML pipelines.

#### Git & Pull Requests
- `get_repo_by_name_or_id`: Get repository details by name or ID.
- `list_branches`: List all branches in a Git repository.
- `list_my_branches`: List branches created by the current user.
- `get_branch`: Get details of a specific branch including commit info.
- `get_file_content`: Get the content of a file from a repository at a specific branch or commit.
- `list_pull_requests`: List pull requests in a repository with filtering options.
- `get_pull_request_commits`: Get the commits associated with a pull request.
- `get_pull_request_reviewers`: Get the reviewers of a pull request.
- `update_pull_request`: Update a pull request (title, description, status, etc.).
- `update_pull_request_reviewers`: Add or remove reviewers from a pull request.
- `add_pull_request_reviewer`: Add a reviewer to a pull request.
- `get_pull_request_threads`: Get all comment threads on a pull request.
- `list_pull_request_thread_comments`: Get comments in a specific thread.
- `create_pull_request_thread`: Create a new comment thread on a pull request.
- `update_pull_request_thread`: Update an existing comment thread on a pull request.
- `reply_to_comment`: Reply to a comment in a pull request thread.

### QA Agent Tools (33 total)

#### Test Plans, Suites & Points
- `list_test_plans`: List test plans in a project.
- `get_test_plan`: Get a test plan by ID.
- `create_test_plan`: Create a new test plan in a project.
- `update_test_plan`: Update an existing test plan.
- `delete_test_plan`: Delete a test plan.
- `list_test_suites`: List test suites under a test plan.
- `get_test_suite`: Get a test suite by ID.
- `create_static_test_suite`: Create a static test suite under a plan.
- `create_requirement_based_suite`: Create a requirement-based suite under a plan for a given work item.
- `create_query_based_suite`: Create a query-based test suite under a plan.
- `update_test_suite`: Update an existing test suite.
- `delete_test_suite`: Delete a test suite from a plan.
- `list_test_points`: List test points for a plan and suite.
- `update_test_points`: Update test points (e.g., assign tester, reset outcome).
- `add_test_cases_to_suite`: Add existing test cases to a test suite.
- `list_test_cases_in_suite`: List test cases in a suite.
- `remove_test_cases_from_suite`: Remove test cases from a suite.

#### Test Runs & Results
- `list_test_runs`: List test runs in a project.
- `get_test_run`: Get a test run by ID.
- `create_test_run`: Create a new test run.
- `update_test_run`: Update test run properties (state, comment, name, etc.).
- `delete_test_run`: Delete a test run.
- `list_test_run_results`: List test results for a run.
- `get_test_result`: Get a specific test result by ID.
- `add_test_results`: Add test results to a test run.
- `update_test_results`: Update existing test results.
- `get_test_run_statistics`: Get statistics for a test run.

#### Attachments
- `list_test_run_attachments`: List attachments for a test run.
- `create_test_run_attachment`: Create an attachment for a test run.
- `create_test_result_attachment`: Create an attachment for a test result.

#### Test Case Work Items
- `create_test_case`: Create a new Test Case work item.
- `update_test_case`: Update fields on a Test Case work item.
- `get_test_case`: Get a Test Case work item by ID.

## Prerequisites

- .NET 10 SDK
- An Azure DevOps organization and project.
- A Personal Access Token (PAT) with appropriate permissions (Code, Work Items, Build, etc.).

## Configuration

Each agent has its own `appsettings.json`.

1.  Open one (or all) of:
  - `ManagerAgent/appsettings.json`
  - `InfraAgent/appsettings.json`
  - `QAAgent/appsettings.json`
2.  Update the `AzureDevOps` section with your organization URL and PAT:

```json
"AzureDevOps": {
  "OrgUrl": "https://dev.azure.com/your-organization",
  "Pat": "your-personal-access-token"
}
```

### API key (optional but recommended)

If you set `ApiKey` in an agent's `appsettings.json`, the server requires an `x-api-key` header on all requests.

## Running the Server

Run whichever agent you want to expose:

- Manager agent: `dotnet run --project ManagerAgent/AzureDevOpsMcp.Manager.csproj`
- Infra agent: `dotnet run --project InfraAgent/AzureDevOpsMcp.Infra.csproj`
- QA agent: `dotnet run --project QAAgent/AzureDevOpsMcp.QA.csproj`

Each agent listens on the port configured in its `Properties/launchSettings.json` (current defaults):

- Manager agent: `http://localhost:5155`
- Infra agent: `http://localhost:5122`
- QA agent: `http://localhost:5180`

### Usage Examples

- create_work_item
  - Purpose: Create a new work item with one or more fields.
  - Signature: `create_work_item(workItemType, fieldsJson)`
  - Example:

    ```json
    {
      "tool": "create_work_item",
      "args": {
        "workItemType": "Task",
        "fieldsJson": "{\"System.Title\":\"(Hours) Example\",\"System.AreaPath\":\"Supply Chain Cloud\",\"System.IterationPath\":\"Supply Chain Cloud\\\\Sprint 82\",\"Microsoft.VSTS.Common.Activity\":\"Development\",\"Microsoft.VSTS.Scheduling.OriginalEstimate\":8,\"Microsoft.VSTS.Scheduling.RemainingWork\":8}"
      }
    }
    ```

  - Notes:
    - `fieldsJson` is required and must be a JSON object string.
    - Field names are Azure DevOps reference names (e.g., `System.AreaPath`).
    - Nested JSON objects are preserved as raw JSON strings to keep schemas Copilot Studio-friendly.

- update_work_item
  - Purpose: Update one or more fields on a work item by ID.
  - Signature: `update_work_item(id, updatesJson)`
  - Example:

    ```json
    {
      "tool": "update_work_item",
      "args": {
        "id": 12345,
        "updatesJson": "[{\"field\":\"System.Title\",\"value\":\"Update API contract\"},{\"field\":\"Microsoft.VSTS.Scheduling.OriginalEstimate\",\"value\":8}]"
      }
    }
    ```

  - Notes:
    - `updatesJson` is required and must be a JSON array string.
    - Field names are Azure DevOps reference names (e.g., `System.AreaPath`).
    - This `...Json` pattern is used to keep tool schemas flat and Copilot Studio-friendly.

## MCP Configuration

To use one of these servers with an MCP client (like Claude Desktop or VS Code), configure the client to start the desired agent.

### VS Code (MCP over HTTP)

This repo includes a working example at `.vscode/mcp.json` (Manager agent defaults to `http://localhost:5155`).

```jsonc
{
  "servers": {
    "my-mcp": {
      "type": "http",
      "url": "http://localhost:5155",
      "headers": {
        "x-api-key": "<your ApiKey>"
      }
    }
  }
}
```

Example configuration for Claude Desktop (`claude_desktop_config.json`) to run the Manager agent:

```json
{
  "mcpServers": {
    "azure-devops": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "path/to/AzureDevOpsMcpServer/ManagerAgent/AzureDevOpsMcp.Manager.csproj"
      ]
    }
  }
}
```
*Note: For the SSE transport, you typically run the server separately and point the client to the URL, or use the stdio transport if supported. This implementation uses HTTP/SSE.*
