# syntax=docker/dockerfile:1

# The Node stage runs on the build machine and produces static files for every target platform.
FROM --platform=$BUILDPLATFORM node:24-alpine AS build
WORKDIR /src

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.28-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/invoiceflow-web/browser /usr/share/nginx/html
