
# Etap 1: budowanie React i TypeScript
FROM node:24-alpine AS build

WORKDIR /app

# Zależności npm.
COPY package.json package-lock.json ./

RUN npm ci

# Kod frontendu.
COPY . .

# Kompilacja TypeScript i budowanie Vite.
RUN npm run build

# Etap 2: serwer statycznych plików
FROM nginx:stable-alpine AS final

# Konfiguracja nginx.
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Gotowa aplikacja React.
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
