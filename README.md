# Azure DevOps MCP Server

This is a Model Context Protocol (MCP) server for Azure DevOps, built with .NET 10 and ASP.NET Core.

## Features

This server exposes the following tools to MCP clients:

### Core Tools (Server)
- `list_projects`: List all projects in the organization.
- `get_project`: Get details of a specific project.
- `list_teams`: List all teams in a project.

### Repository Tools
- `list_repos`: List all repositories in a project.
- `get_repo`: Get details of a specific repository.
- `list_branches`: List branches in a repository.
- `get_file_content`: Get the content of a file in a repository.
- `list_pull_requests`: List pull requests in a repository.

### Work Item Tools
- `get_work_item`: Get a work item by ID.
- `search_work_items`: Search for work items using WIQL.
- `create_work_item`: Create a new work item.
- `update_work_item`: Update an existing work item.

### Pipeline Tools
- `list_pipelines`: List pipelines in a project.
- `run_pipeline`: Trigger a pipeline run.
- `get_build`: Get details of a build.

### Wiki Tools
- `list_wikis`: List wikis in a project.
- `get_wiki_page`: Get the content of a wiki page.

### QA Agent Tools (33 total)
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
- `list_test_run_attachments`: List attachments for a test run.
- `create_test_run_attachment`: Create an attachment for a test run.
- `create_test_result_attachment`: Create an attachment for a test result.
- `create_test_case`: Create a new Test Case work item.
- `update_test_case`: Update fields on a Test Case work item.
- `get_test_case`: Get a Test Case work item by ID.

## Prerequisites

- .NET 10 SDK
- An Azure DevOps organization and project.
- A Personal Access Token (PAT) with appropriate permissions (Code, Work Items, Build, etc.).

## Configuration

1.  Open `appsettings.json`.
2.  Update the `AzureDevOps` section with your organization URL and PAT:

```json
"AzureDevOps": {
  "OrgUrl": "https://dev.azure.com/your-organization",
  "Pat": "your-personal-access-token"
}
```

## Running the Server

1.  Navigate to the project directory:
    ```bash
    cd AzureDevOpsMcpServer
    ```

2.  Run the application:
    ```bash
    dotnet run
    ```

The server will start listening on `http://localhost:5115` (or the port configured in `launchSettings.json`).

## MCP Configuration

To use this server with an MCP client (like Claude Desktop or VS Code), configure the client to connect to the SSE endpoint.

**SSE Endpoint:** `http://localhost:5115/sse`

Example configuration for Claude Desktop (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "azure-devops": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "path/to/AzureDevOpsMcpServer/AzureDevOpsMcpServer.csproj"
      ]
    }
  }
}
```
*Note: For the SSE transport, you typically run the server separately and point the client to the URL, or use the stdio transport if supported. This implementation uses HTTP/SSE.*
