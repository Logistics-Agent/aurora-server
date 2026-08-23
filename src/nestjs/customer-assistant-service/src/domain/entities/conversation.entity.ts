import { ActorType } from '../enums/actor-type.enum';

export interface Conversation {
  id: string;
  tenantId: string;
  userId: string;
  actorType: ActorType;
  preferredLanguage: string;
  status: 'ACTIVE' | 'ARCHIVED';
  summary?: string;
  version: number;
  createdAt: Date;
  updatedAt: Date;
  lastActivityAt: Date;
}
