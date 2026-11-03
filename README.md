# AgentTown

A virtual town of 20–100 LLM-driven agents built with .NET 10, Aspire, NATS JetStream, and Ollama.

## How to run

```bash
# 1. Install Ollama on your host and pull the models
ollama pull qwen2.5:7b-instruct
ollama pull nomic-embed-text

# 2. Open the repo in the dev container (VS Code → "Reopen in Container")

# 3. Start the simulation
dotnet run --project src/AgentTown.AppHost
```

Open the Aspire dashboard link printed in the console.

## Project structure

See [PROJECT_PLAN.md](PROJECT_PLAN.md) for the full architecture, work split, and day-by-day plan.
