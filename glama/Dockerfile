# Local reproduction of the image Glama generates for this listing (see glama/README.md).
# Glama does not take this file: its admin form generates an equivalent Dockerfile (debian:trixie-slim +
# Node.js + mcp-proxy + uv/Python, `git clone` of this repo into /app, the form's build steps, then
# CMD ["mcp-proxy","--",<CMD arguments>]). Here the repo is COPYed instead of cloned and CMD runs the stdio
# server directly, so `docker run -i` speaks MCP over stdio exactly like the process mcp-proxy spawns.
#
#   docker build -f glama/Dockerfile -t godot-mcp-glama .     # from the repository root
#   docker run -i --rm godot-mcp-glama                        # MCP over stdio
FROM debian:trixie-slim
ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates curl git \
    && curl -fsSL https://deb.nodesource.com/setup_24.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && npm install -g mcp-proxy@6.4.3 \
    && apt-get clean && rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*
WORKDIR /app
COPY . .
# Glama "Build steps": ["bash glama/install.sh"]
RUN bash glama/install.sh
# Glama "CMD arguments": ["bash","glama/entrypoint.sh"] (Glama prepends "mcp-proxy","--")
CMD ["bash", "glama/entrypoint.sh"]
