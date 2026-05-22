interface ServiceVersionInfo {
  service: string;
  tech: string;
  version: string;
  apiVersion: string;
}

interface VersionResponse {
  services: ServiceVersionInfo[];
  supportedApiVersions: string[];
  deprecatedApiVersions: string[];
  timestamp: string;
}

export function useVersion() {
  const config = useRuntimeConfig();
  const { api } = useApi();

  const appVersion = config.public.appVersion as string;

  async function fetchServiceVersions(): Promise<VersionResponse> {
    return api<VersionResponse>('/api/version');
  }

  return { appVersion, fetchServiceVersions };
}
