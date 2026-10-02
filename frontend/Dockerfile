FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json .npmrc ./
RUN npm install

COPY . .
ARG VITE_API_BASE_URL=http://localhost:5158
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ARG VITE_MAP_TILE_URL=
ENV VITE_MAP_TILE_URL=$VITE_MAP_TILE_URL
ARG VITE_MAP_TILE_ATTRIBUTION=
ENV VITE_MAP_TILE_ATTRIBUTION=$VITE_MAP_TILE_ATTRIBUTION
ARG VITE_MAP_TILES_DARK=
ENV VITE_MAP_TILES_DARK=$VITE_MAP_TILES_DARK
RUN npm run build

FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
