# TTRPG Table — Mobile App

Ionic/Capacitor wrapper over a Nuxt 3 (SSR disabled) frontend. Shares the same backend API as the web UI.

## Tech stack

| Layer | Technology |
|-------|-----------|
| UI framework | Nuxt 3 + `@ionic/vue` |
| Native bridge | Capacitor 6 |
| Styling | Tailwind CSS |
| Real-time | SignalR (`@microsoft/signalr`) |
| Notifications | `@capacitor/push-notifications` (FCM / APNs) |

## Prerequisites

- Node.js 22 (nvm recommended)
- Xcode (iOS) or Android Studio (Android)
- Cocoapods (`sudo gem install cocoapods`) for iOS

## Getting started

```bash
cd mobile/src
npm install
```

> **First-time bootstrap:** After running `npm install`, commit the generated `package-lock.json`.
> Once it exists, edit `mobile/Dockerfile` and change `npm install` back to `npm ci`
> for faster, reproducible Docker builds.

### Web (development)

```bash
NUXT_API_BASE_URL=http://localhost:5294 npm run dev
# → http://localhost:3001
```

### iOS

```bash
npm run ios
# generates, syncs, and opens Xcode workspace
```

### Android

```bash
npm run android
# generates, syncs, and opens Android Studio
```

## Environment variables

| Variable | Default | Description |
|----------|---------|-------------|
| `NUXT_API_BASE_URL` | `http://localhost:5294` | Backend API URL |

## Core pages

| Route | Description |
|-------|-------------|
| `/login` | Login with email + password |
| `/home` | Games list with live-session badges |
| `/join` | Join a session by code + display name |
| `/session/[code]` | Player action view with SignalR live updates |

## Adding native platforms

```bash
# First-time iOS
npx cap add ios
npx cap sync ios

# First-time Android
npx cap add android
npx cap sync android
```

## Push notifications

FCM (Android) and APNs (iOS) are enabled via `@capacitor/push-notifications`. Configure server keys in the API and Capacitor's native project settings.
