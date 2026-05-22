<script setup lang="ts">
import { IonApp } from '@ionic/vue';
</script>

<template>
  <IonApp>
    <NuxtPage />
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
