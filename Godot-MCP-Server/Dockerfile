# See https://docs.microsoft.com/aspnet/core/host-and-deploy/docker/building-net-docker-images
# for more information about using .NET with Docker.

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy main project file
COPY ["com.IvanMurzak.Godot.MCP.Server.csproj", "."]

# Restore dependencies
RUN dotnet restore "com.IvanMurzak.Godot.MCP.Server.csproj"

# Copy the rest of the source code
COPY . .

# Publish
RUN dotnet publish "com.IvanMurzak.Godot.MCP.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# MCP server metadata
LABEL io.modelcontextprotocol.server.name="io.github.IvanMurzak/Godot-MCP"

# Expose the default plugin port and the HTTP client port so external scanners
# (like Smithery) and platform port mappings can reach the server.
EXPOSE 8080

ENTRYPOINT ["dotnet", "godot-mcp-server.dll"]
