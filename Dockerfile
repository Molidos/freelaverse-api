# =========================
# BUILD
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia solução e csproj para cache de restore
COPY Freelaverse.sln ./
COPY Freelaverse.API/Freelaverse.API.csproj Freelaverse.API/
COPY Freelaverse.Data/Freelaverse.Data.csproj Freelaverse.Data/
COPY Freelaverse.Domain/Freelaverse.Domain.csproj Freelaverse.Domain/
COPY Freelaverse.Services/Freelaverse.Services.csproj Freelaverse.Services/

RUN dotnet restore Freelaverse.sln

# Copia o restante do código e publica a API
COPY . .
RUN dotnet publish Freelaverse.API/Freelaverse.API.csproj -c Release -o /app/publish

# =========================
# RUNTIME
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Porta padrão usada no compose (8080)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FreelaverseApi.dll"]