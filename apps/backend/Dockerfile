# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files for restore
COPY SentientArchitect.slnx ./
COPY SentientArchitect.API/SentientArchitect.API.csproj SentientArchitect.API/
COPY SentientArchitect.Application/SentientArchitect.Application.csproj SentientArchitect.Application/
COPY SentientArchitect.Data/SentientArchitect.Data.csproj SentientArchitect.Data/
COPY SentientArchitect.Data.Postgres/SentientArchitect.Data.Postgres.csproj SentientArchitect.Data.Postgres/
COPY SentientArchitect.Domain/SentientArchitect.Domain.csproj SentientArchitect.Domain/
COPY SentientArchitect.Infrastructure/SentientArchitect.Infrastructure.csproj SentientArchitect.Infrastructure/

RUN dotnet restore SentientArchitect.API/SentientArchitect.API.csproj

# Copy the rest of the source (excludes tests — not needed at runtime)
COPY SentientArchitect.API/ SentientArchitect.API/
COPY SentientArchitect.Application/ SentientArchitect.Application/
COPY SentientArchitect.Data/ SentientArchitect.Data/
COPY SentientArchitect.Data.Postgres/ SentientArchitect.Data.Postgres/
COPY SentientArchitect.Domain/ SentientArchitect.Domain/
COPY SentientArchitect.Infrastructure/ SentientArchitect.Infrastructure/

RUN dotnet publish SentientArchitect.API/SentientArchitect.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends wget \
    && rm -rf /var/lib/apt/lists/*

ARG PORT=8080
EXPOSE ${PORT}

COPY --from=build /app/publish .

ENV PORT=${PORT}
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

ENTRYPOINT ["dotnet", "SentientArchitect.API.dll"]
