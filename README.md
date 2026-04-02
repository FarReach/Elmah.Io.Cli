# Elmah.Io.Cli

A CLI to execute common tasks against [elmah.io](https://elmah.io).

Documentation: [CLI overview](https://docs.elmah.io/cli-overview/)

## FarReach Team Installation

This fork publishes to the FarReach GitHub Packages NuGet feed. Team members must authenticate before installing.

### Prerequisites

You need a GitHub Personal Access Token (PAT) with `read:packages` scope.

1. Go to [GitHub Settings → Developer settings → Personal access tokens → Tokens (classic)](https://github.com/settings/tokens/new)
2. Select the `read:packages` scope
3. Generate and copy the token

### Add the NuGet source

```bash
dotnet nuget add source \
  --username <your-github-username> \
  --password <your-PAT> \
  --store-password-in-clear-text \
  --name farreach-github \
  "https://nuget.pkg.github.com/FarReach/index.json"
```

### Install the tool

```bash
dotnet tool install -g FarReach.Elmah.Io.Cli --source farreach-github
```

To update to the latest version:

```bash
dotnet tool update -g FarReach.Elmah.Io.Cli --source farreach-github
```

### Verify installation

```bash
elmahio --help
```

### Authenticate with elmah.io

```bash
elmahio login --apiKey <your-elmahio-api-key>
```

Your API key is stored locally and used automatically by subsequent commands. Alternatively, pass `--apiKey` to any command explicitly.