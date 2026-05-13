# Giai đoạn 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# 1. Chỉ copy file project để restore trước (tận dụng cache của Docker)
COPY *.csproj ./
RUN dotnet restore

# 2. Copy toàn bộ code còn lại
COPY . ./

# ⭐ BƯỚC QUAN TRỌNG: Xóa sạch đống bin/obj từ máy local (Windows) 
# Để ép Docker tự sinh lại file .dgspec.json sạch (Linux path) khi build.
RUN rm -rf obj/ bin/

# 3. Publish ứng dụng
# --no-restore giúp bỏ qua bước kiểm tra lại vì mình đã restore ở trên rồi
RUN dotnet publish -c Release -o out --no-restore

# Giai đoạn 2: Chạy
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# ⭐ Cài thư viện GSSAPI ở đây (Dành cho SQL Server authentication)
# Thêm lệnh dọn dẹp apt để giảm dung lượng image
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

# Copy kết quả build từ giai đoạn 1
COPY --from=build /app/out .

# Cấu hình Port
EXPOSE 5057
ENV ASPNETCORE_URLS=http://+:5057

# Chạy app
ENTRYPOINT ["dotnet", "VibeCity_API.dll"]