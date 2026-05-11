# Bước 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy file csproj và restore
COPY *.csproj ./
RUN dotnet restore

# Copy toàn bộ code và publish
COPY . ./
RUN dotnet publish -c Release -o out
RUN apt-get update && apt-get install -y libgssapi-krb5-2

# Bước 2: Run
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .
# THÊM DÒNG NÀY ĐỂ RENDER BIẾT CỔNG NÀO
EXPOSE 5057
ENV ASPNETCORE_URLS=http://+:5057

# Lưu ý: Chữ VibeCity_API.dll phải khớp với tên project của ông
ENTRYPOINT ["dotnet", "VibeCity_API.dll"]