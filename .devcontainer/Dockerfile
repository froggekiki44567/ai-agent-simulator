FROM mcr.microsoft.com/devcontainers/dotnet:10.0-noble
# Remove passwordless sudo, so the AI agent can't become root
RUN rm -f /etc/sudoers.d/vscode \
 && mkdir -p /home/vscode/.claude /home/vscode/.codex \
 && chown -R vscode:vscode /home/vscode/.claude /home/vscode/.codex
ENV DEVCONTAINER=true
USER vscode
WORKDIR /workspace
