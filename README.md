# Azure DevOps MCP Server

This is a Model Context Protocol (MCP) server for Azure DevOps, built with .NET 10 and ASP.NET Core.

## Features

This server exposes the following tools to MCP clients:

### Core Tools
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
