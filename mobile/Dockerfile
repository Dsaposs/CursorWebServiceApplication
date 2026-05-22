FROM node:22-alpine AS build
WORKDIR /app

COPY src/package*.json ./
RUN npm ci

COPY src/ .
RUN npm run build

FROM node:22-alpine AS runtime
WORKDIR /app

ENV NODE_ENV=production
ENV HOST=0.0.0.0
ENV PORT=3001

EXPOSE 3001

COPY --from=build /app/.output ./.output

CMD ["node", ".output/server/index.mjs"]
