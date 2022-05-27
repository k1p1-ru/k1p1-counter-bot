FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["k1p1-counter-bot/K1P1.RowCounterBot/K1P1.RowCounterBot.csproj", "K1P1.RowCounterBot/"]
RUN dotnet restore "K1P1.RowCounterBot/K1P1.RowCounterBot.csproj"
COPY . .
WORKDIR "/src/K1P1.RowCounterBot"
RUN dotnet build "K1P1.RowCounterBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "K1P1.RowCounterBot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "K1P1.RowCounterBot.dll"]
