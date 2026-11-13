# Giai đoạn 1: Base runtime để chạy app
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Giai đoạn 2: Dùng SDK để build mã nguồn
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy các project cần thiết cho API
COPY ["src/Connext.Shared/Connext.Shared.csproj", "src/Connext.Shared/"]
COPY ["src/Connext.Data/Connext.Data.csproj", "src/Connext.Data/"]
COPY ["src/Connext.Api/Connext.Api.csproj", "src/Connext.Api/"]

# Restore trực tiếp cho API
RUN dotnet restore "src/Connext.Api/Connext.Api.csproj"

# Copy toàn bộ mã nguồn và build
COPY . .
WORKDIR "/src/src/Connext.Api"
RUN dotnet build "Connext.Api.csproj" -c Release -o /app/build

# Giai đoạn 3: Publish ứng dụng
FROM build AS publish
RUN dotnet publish "Connext.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Giai đoạn 4: Ghép vào image chạy cuối cùng
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Connext.Api.dll"]