import { ActorType } from '../../domain/enums/actor-type.enum';

export interface CurrentUser {
  tenantId: string;
  userId: string;
  customerId?: string;
  actorType: ActorType;
  roles: string[];
  permissions: string[];
  traceId?: string;
}
