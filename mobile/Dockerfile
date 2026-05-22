FROM node:22-alpine AS build
WORKDIR /app

# Production mode prevents @nuxt/devtools from loading during build,
# which avoids missing peer-dependency errors (e.g. unstorage).
ENV NODE_ENV=production

COPY src/package*.json ./
# Use npm install (not ci) because no lockfile exists until the project is bootstrapped locally.
# Once you run `npm install` in mobile/src/ and commit the generated package-lock.json,
# change this back to `npm ci` for reproducible, faster installs.
RUN npm install

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
