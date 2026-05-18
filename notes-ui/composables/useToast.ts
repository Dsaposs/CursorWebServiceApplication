export interface Toast {
  id: string;
  message: string;
  type: 'success' | 'error' | 'info' | 'warn';
}

const toastState = () => useState<Toast[]>('toasts', () => []);

export function useToast() {
  const list = toastState();

  function add(message: string, type: Toast['type'] = 'info', duration = 4000): string {
    const id = Math.random().toString(36).slice(2);
    list.value = [...list.value, { id, message, type }];
    if (duration > 0) {
      setTimeout(() => remove(id), duration);
    }
    return id;
  }

  function remove(id: string) {
    list.value = list.value.filter(t => t.id !== id);
  }

  return {
    toasts: list,
    success: (msg: string, dur?: number) => add(msg, 'success', dur),
    error: (msg: string, dur?: number) => add(msg, 'error', dur),
    info: (msg: string, dur?: number) => add(msg, 'info', dur),
    warn: (msg: string, dur?: number) => add(msg, 'warn', dur),
    remove,
  };
}
