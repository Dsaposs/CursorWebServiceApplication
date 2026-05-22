<script setup lang="ts">
import { IonApp, IonRouterOutlet } from '@ionic/vue';

// Capacitor safe-area CSS vars are injected into the root element automatically.
// We import the Ionic core CSS bundle at app level so all pages inherit it.
</script>

<template>
  <IonApp>
    <IonRouterOutlet />
  </IonApp>
</template>

<style>
/* Safe area insets forwarded as Tailwind-friendly CSS vars */
:root {
  --ion-safe-area-top: env(safe-area-inset-top);
  --ion-safe-area-bottom: env(safe-area-inset-bottom);
  --ion-safe-area-left: env(safe-area-inset-left);
  --ion-safe-area-right: env(safe-area-inset-right);
}

html, body {
  @apply bg-surface text-white;
  overscroll-behavior-y: none;
}
</style>
