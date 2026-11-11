# ---- Build stage: compile the Angular app ----
FROM node:22-alpine AS build
WORKDIR /app

# Install dependencies first: this layer is cached until package*.json changes.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npx ng build --configuration production

# ---- Runtime stage: only static files + nginx (no Node, no source code) ----
FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/timetrack-web/browser /usr/share/nginx/html
EXPOSE 80
