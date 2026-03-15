# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution and project files first to maximize restore layer caching
COPY CloudM.sln ./
COPY CloudM.API/CloudM.API.csproj ./CloudM.API/
COPY CloudM.Application/CloudM.Application.csproj ./CloudM.Application/
COPY CloudM.Domain/CloudM.Domain.csproj ./CloudM.Domain/
COPY CloudM.Infrastructure/CloudM.Infrastructure.csproj ./CloudM.Infrastructure/

# Restore packages
RUN dotnet restore ./CloudM.API/CloudM.API.csproj

# Copy the full source tree
COPY CloudM.API ./CloudM.API
COPY CloudM.Application ./CloudM.Application
COPY CloudM.Domain ./CloudM.Domain
COPY CloudM.Infrastructure ./CloudM.Infrastructure

# Publish project
RUN dotnet publish ./CloudM.API/CloudM.API.csproj -c Release -o /out

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out ./
EXPOSE 10000
ENTRYPOINT ["dotnet", "CloudM.API.dll"]
