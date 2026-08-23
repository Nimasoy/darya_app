FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
# Custom nginx config to handle SPA routing if necessary, but default serves index.html perfectly for this MVP
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
