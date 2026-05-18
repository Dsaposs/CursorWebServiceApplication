export function usePlayerTokens() {
  function sessionKey(joinCode: string | string[]) {
    return `ttrpg_player_${Array.isArray(joinCode) ? joinCode[0] : joinCode}`;
  }

  function gameKey(gameId: string) {
    return `ttrpg_player_${gameId}`;
  }

  function getSessionPlayerToken(joinCode: string | string[]) {
    if (!import.meta.client) return null;
    return localStorage.getItem(sessionKey(joinCode));
  }

  function getGamePlayerToken(gameId: string) {
    if (!import.meta.client) return null;
    return localStorage.getItem(gameKey(gameId));
  }

  function setSessionPlayerToken(joinCode: string | string[], token: string) {
    if (import.meta.client) {
      localStorage.setItem(sessionKey(joinCode), token);
    }
  }

  function setGamePlayerToken(gameId: string, token: string) {
    if (import.meta.client) {
      localStorage.setItem(gameKey(gameId), token);
    }
  }

  function setPlayerTokenForSessionAndGame(joinCode: string | string[], gameId: string, token: string) {
    setSessionPlayerToken(joinCode, token);
    setGamePlayerToken(gameId, token);
  }

  return {
    getSessionPlayerToken,
    getGamePlayerToken,
    setSessionPlayerToken,
    setGamePlayerToken,
    setPlayerTokenForSessionAndGame,
  };
}
