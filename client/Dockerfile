# ---------- Stage 1: build the Angular app with Node ----------
FROM node:24-alpine AS build
WORKDIR /app

# Install packages first (cached until package.json / package-lock.json change)
COPY package.json package-lock.json ./
RUN npm ci

# Copy the source and build the production bundle into dist/
COPY . .
RUN npx ng build

# ---------- Stage 2: serve the built files with nginx ----------
FROM nginx:stable-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/Electric-Management-App/browser /usr/share/nginx/html
EXPOSE 80
