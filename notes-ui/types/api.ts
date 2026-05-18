export interface AuthResponse {
  token: string;
  expiresAt: string;
}

export interface NoteResponse {
  id: string;
  title?: string | null;
  content: string;
  createdAt: string;
  updatedAt: string;
}

export interface AdminUserReportResponse {
  userId: string;
  userName: string;
  email?: string | null;
  hasPasswordHash: boolean;
  notesCreatedCount: number;
  notesDeletedCount: number;
}
