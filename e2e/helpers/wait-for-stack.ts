interface WaitForStackOptions {
  apiUrl: string;
  uiUrl: string;
  mobileUrl?: string;
  timeoutMs: number;
}

async function waitForOk(url: string, label: string, deadline: number) {
  let lastError = '';

  while (Date.now() < deadline) {
    try {
      const response = await fetch(url, { redirect: 'follow' });
      if (response.ok) {
        return;
      }
      lastError = `${label} returned ${response.status}`;
    } catch (error) {
      lastError = error instanceof Error ? error.message : String(error);
    }

    await new Promise(resolve => setTimeout(resolve, 2000));
  }

  throw new Error(`Timed out waiting for ${label} at ${url}. Last error: ${lastError}`);
}

export async function waitForStack(options: WaitForStackOptions) {
  const deadline = Date.now() + options.timeoutMs;
  await waitForOk(`${options.apiUrl}/health`, 'API health', deadline);
  await waitForOk(options.uiUrl, 'UI home', deadline);
  if (options.mobileUrl) {
    await waitForOk(options.mobileUrl, 'Mobile home', deadline);
  }
}
