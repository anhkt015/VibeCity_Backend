# Giai đoạn 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# 1. Copy file project và code vào trước
COPY . ./

# 2. XÓA SẠCH file rác từ máy local (Windows) TRƯỚC KHI RESTORE
# Việc xóa ở đây đảm bảo không còn dấu vết ổ D:\ hay path của Windows
RUN rm -rf obj/ bin/

# 3. Chạy restore NGAY TRONG Docker để sinh ra file obj/ chuẩn của Linux
RUN dotnet restore

# 4. Publish ứng dụng
# Bây giờ có thể dùng --no-restore vì file assets đã được tạo ở bước trên
RUN dotnet publish -c Release -o out --no-restore

# Giai đoạn 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/out .
EXPOSE 5057
ENV ASPNETCORE_URLS=http://+:5057
ENTRYPOINT ["dotnet", "VibeCity_API.dll"]