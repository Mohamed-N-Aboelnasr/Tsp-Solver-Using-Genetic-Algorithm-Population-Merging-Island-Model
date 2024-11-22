FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app
COPY *.csv .

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Master.csproj", "Master/"]
RUN dotnet restore "Master/Master.csproj"
COPY . "Master/"
WORKDIR "/src/Master"
RUN dotnet build "Master.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Master.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Master.dll"]
