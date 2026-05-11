# Giai đoạn 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY *.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o out

# Giai đoạn 2: Chạy (Chèn thư viện vào đây mới đúng)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# ⭐ Cài thư viện GSSAPI ở đây (Dành cho môi trường chạy)
RUN apt-get update && apt-get install -y libgssapi-krb5-2

COPY --from=build /app/out .
EXPOSE 5057
ENV ASPNETCORE_URLS=http://+:5057
ENTRYPOINT ["dotnet", "VibeCity_API.dll"]