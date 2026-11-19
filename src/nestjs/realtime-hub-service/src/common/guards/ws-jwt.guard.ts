import { CanActivate, ExecutionContext, Injectable, Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { WsException } from '@nestjs/websockets';
import { Socket } from 'socket.io';
import * as jwt from 'jsonwebtoken';

export interface AuthenticatedSocket extends Socket {
  data: {
    tenantId: string;
    userId: string;
    email?: string;
    customerId?: string;
    roles?: string[];
    shipmentIds?: string[];
  };
}

@Injectable()
export class WsJwtGuard implements CanActivate {
  private readonly logger = new Logger(WsJwtGuard.name);

  constructor(private readonly configService: ConfigService) {}

  canActivate(context: ExecutionContext): boolean {
    const client: AuthenticatedSocket = context.switchToWs().getClient();
    return this.validateSocket(client);
  }

  validateSocket(client: AuthenticatedSocket): boolean {
    const token =
      client.handshake?.auth?.token ||
      client.handshake?.headers?.authorization?.split(' ')[1] ||
      client.handshake?.query?.token;

    const jwtSecret = this.configService.get<string>('auth.jwtSecret');
    const jwtPublicKey = this.configService.get<string>('auth.jwtPublicKey');

    if (!token) {
      throw new WsException('Unauthorized socket connection: Missing token');
    }
    if (!jwtSecret && !jwtPublicKey) {
      throw new WsException('Socket authentication is not configured');
    }

    try {
      const decoded = jwt.verify(String(token).replace('Bearer ', ''), (jwtPublicKey || jwtSecret)!, {
        algorithms: jwtPublicKey ? ['RS256'] : ['HS256'],
      }) as jwt.JwtPayload;
      const tenantId = decoded.tenantId || decoded.tenant_id;
      const userId = decoded.userId || decoded.sub;
      if (typeof tenantId !== 'string' || typeof userId !== 'string' || !tenantId || !userId) {
        throw new Error('Missing tenant or user identity');
      }
      client.data = {
        tenantId,
        userId,
        email: decoded.email || '',
        customerId: typeof (decoded.customer_id || decoded['custom:customer_id']) === 'string'
          ? decoded.customer_id || decoded['custom:customer_id'] : undefined,
        roles: (Array.isArray(decoded.roles) ? decoded.roles
          : Array.isArray(decoded['cognito:groups']) ? decoded['cognito:groups']
          : [decoded.role]).filter((role): role is string => typeof role === 'string'),
        shipmentIds: Array.isArray(decoded.shipment_ids)
          ? decoded.shipment_ids.filter((id): id is string => typeof id === 'string') : [],
      };
      return true;
    } catch (err) {
      this.logger.error(`Invalid JWT token on socket ${client.id}: ${err.message}`);
      throw new WsException('Unauthorized socket connection: Invalid JWT token');
    }
  }
}
