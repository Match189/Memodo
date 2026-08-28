# 待办备忘同步服务器
# 构建：docker build -t todolist-server .
# 运行：docker run -d -p 8080:8080 -e TODOLIST_TOKEN=换成你的长随机串 -v "$PWD/data:/app/data" todolist-server
FROM dart:3.7 AS build
WORKDIR /app
COPY pubspec.* ./
RUN dart pub get
COPY . .
RUN dart pub get --offline && dart compile exe bin/server.dart -o /app/server

FROM scratch
COPY --from=build /app/server /app/server
EXPOSE 8080
ENV TODOLIST_TOKEN=change-me
ENTRYPOINT ["/app/server", "--data-dir", "/app/data"]
