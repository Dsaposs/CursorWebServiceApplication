import * as signalR from '@microsoft/signalr';

export type HubEventHandler = (payload: unknown) => void;

export function useSessionHub() {
  const config = useRuntimeConfig();
  const base = config.public.apiBaseUrl as string;
  const { token } = useApi();

  const connection = ref<signalR.HubConnection | null>(null);
  const isConnected = ref(false);
  const connectionError = ref<string | null>(null);

  function buildConnection() {
    return new signalR.HubConnectionBuilder()
      .withUrl(`${base}/hubs/session`, {
        accessTokenFactory: () => token.value ?? '',
        transport: signalR.HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();
  }

  async function connect(sessionId: string, role: 'dm' | 'player', participantToken?: string) {
    if (connection.value) return;

    const hub = buildConnection();
    connection.value = hub;

    hub.onreconnected(() => { isConnected.value = true; });
    hub.onclose(() => { isConnected.value = false; });

    try {
      await hub.start();
      isConnected.value = true;

      if (role === 'dm') {
        await hub.invoke('JoinSessionAsDm', sessionId);
      } else {
        await hub.invoke('JoinSessionAsPlayer', participantToken ?? '');
      }
    } catch (e) {
      connectionError.value = String(e);
      isConnected.value = false;
    }
  }

  async function disconnect() {
    if (connection.value) {
      await connection.value.stop();
      connection.value = null;
      isConnected.value = false;
    }
  }

  function on(event: string, handler: HubEventHandler) {
    connection.value?.on(event, handler);
  }

  function off(event: string, handler: HubEventHandler) {
    connection.value?.off(event, handler);
  }

  return { connect, disconnect, on, off, isConnected, connectionError };
}
