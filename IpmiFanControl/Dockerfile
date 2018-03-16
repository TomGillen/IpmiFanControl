FROM microsoft/dotnet:2.0-runtime AS base
RUN apt-get update && apt-get install -y ipmitool
WORKDIR /app

FROM microsoft/dotnet:2.0-sdk AS build
WORKDIR /src
COPY IpmiFanControl.sln ./
COPY IpmiFanControl/IpmiFanControl.csproj IpmiFanControl/
RUN dotnet restore -nowarn:msb3202,nu1503 -p:RestoreUseSkipNonexistentTargets=false
COPY . .
WORKDIR /src/IpmiFanControl
RUN dotnet build -c Release -o /app

FROM build AS publish
RUN dotnet publish -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "IpmiFanControl.dll"]
