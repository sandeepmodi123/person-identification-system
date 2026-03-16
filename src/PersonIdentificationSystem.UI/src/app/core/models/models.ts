// Core models for the Person Identification System UI

export interface Person {
  id: string;
  name: string;
  description?: string;
  riskLevel: 'Low' | 'Medium' | 'High' | 'Critical';
  isActive: boolean;
  dateAdded: string;
  photos: PersonPhoto[];
}

export interface PersonPhoto {
  id: string;
  personId: string;
  photoUrl: string;
  qualityScore?: number;
  isPrimary: boolean;
  uploadDate: string;
  originalFilename?: string;
}

export interface RTSPStream {
  id: string;
  cameraName: string;
  cameraLocation?: string;
  rtspUrl: string;
  frameIntervalSeconds: number;
  isActive: boolean;
  status: 'Online' | 'Offline' | 'Error' | 'Unknown';
  lastChecked?: string;
}

export interface Detection {
  id: string;
  streamId: string;
  cameraName: string;
  personId?: string;
  personName?: string;
  riskLevel?: string;
  confidenceScore: number;
  detectionTimestamp: string;
  frameImageUrl?: string;
  isVerified: boolean;
  verificationStatus?: 'TruePositive' | 'FalsePositive';
  emailSent: boolean;
}

export interface NotificationLog {
  id: string;
  detectionId?: string;
  recipientEmail: string;
  sentTimestamp: string;
  status: 'Pending' | 'Sent' | 'Failed';
  errorMessage?: string;
}

export interface NotificationSettings {
  id: string;
  recipientEmails: string[];
  minimumConfidenceThreshold: number;
  notifyOnRiskLevels: string[];
  rateLimitMinutes: number;
  isEnabled: boolean;
  smtpHost?: string;
  smtpPort?: number;
  fromEmail?: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
