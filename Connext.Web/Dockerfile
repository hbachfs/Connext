# Sử dụng Nginx Alpine siêu nhẹ để phục vụ web tĩnh
FROM nginx:alpine

# Xóa trang mặc định của Nginx
RUN rm -rf /usr/share/nginx/html/*

# Sao chép toàn bộ mã nguồn HTML/CSS/JS vào thư mục web root của Nginx
COPY . /usr/share/nginx/html

# Sao chép cấu hình Reverse Proxy vào Nginx
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Mở cổng 80 cho web
EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]