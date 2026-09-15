FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TrainCoach.slnx ./
COPY src/TrainCoach.Domain/TrainCoach.Domain.csproj src/TrainCoach.Domain/
COPY src/TrainCoach.Application/TrainCoach.Application.csproj src/TrainCoach.Application/
COPY src/TrainCoach.Infrastructure/TrainCoach.Infrastructure.csproj src/TrainCoach.Infrastructure/
COPY src/TrainCoach.Integrations/TrainCoach.Integrations.csproj src/TrainCoach.Integrations/
COPY src/TrainCoach.Api/TrainCoach.Api.csproj src/TrainCoach.Api/
COPY tests/TrainCoach.Domain.Tests/TrainCoach.Domain.Tests.csproj tests/TrainCoach.Domain.Tests/
COPY tests/TrainCoach.Application.Tests/TrainCoach.Application.Tests.csproj tests/TrainCoach.Application.Tests/
COPY tests/TrainCoach.Api.IntegrationTests/TrainCoach.Api.IntegrationTests.csproj tests/TrainCoach.Api.IntegrationTests/
RUN dotnet restore TrainCoach.slnx

COPY . .
RUN dotnet publish src/TrainCoach.Api/TrainCoach.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TrainCoach.Api.dll"]
