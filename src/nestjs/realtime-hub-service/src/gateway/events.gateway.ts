import {
  WebSocketGateway,
  WebSocketServer,
  OnGatewayConnection,
  OnGatewayDisconnect,
  SubscribeMessage,
  MessageBody,
  ConnectedSocket,
} from '@nestjs/websockets';
import { Server } from 'socket.io';
import { Logger, UseGuards } from '@nestjs/common';
import { WsJwtGuard, AuthenticatedSocket } from '../common/guards/ws-jwt.guard';
import { RealtimePayloadDto } from './dto/realtime-payload.dto';
import { OfflineBufferService, BufferedMessage } from '../messaging/offline-buffer.service';

@WebSocketGateway({
  cors: {
    origin: '*',
  },
})
export class EventsGateway implements OnGatewayConnection, OnGatewayDisconnect {
  @WebSocketServer()
  server: Server;

  private readonly logger = new Logger(EventsGateway.name);
  private readonly pendingAcks: Map<string, NodeJS.Timeout> = new Map();

  constructor(
    private readonly wsJwtGuard: WsJwtGuard,
    private readonly offlineBuffer: OfflineBufferService,
  ) {}

  async handleConnection(client: AuthenticatedSocket) {
    try {
      this.wsJwtGuard.validateSocket(client);

      const tenantId = client.data.tenantId;
      const userId = client.data.userId;

      // ── Multi-Tenant Room Isolation ──────────────────────────────────────
      const tenantRoom = `tenant:${tenantId}`;
      const userRoom = `user:${tenantId}:${userId}`;

      await client.join(tenantRoom);
      await client.join(userRoom);

      this.logger.log(
        `Socket Client connected: ID ${client.id} | Tenant: ${tenantId} | User: ${userId} | Joined Rooms: [${tenantRoom}, ${userRoom}]`,
      );

      client.emit('connected', {
        status: 'SUCCESS',
        message: 'Connected to Aurora Realtime Hub',
        tenantId,
        userId,
      });

      // ── TASK-005: Flush Offline Buffered Messages on Connect ─────────────
      const bufferedMsgs = await this.offlineBuffer.flush(tenantId, userId);
      if (bufferedMsgs.length > 0) {
        this.logger.log(`Flushing ${bufferedMsgs.length} offline message(s) for User ${userId}`);
        for (const msg of bufferedMsgs) {
          client.emit(msg.event, {
            event: msg.event,
            tenantId: msg.tenantId,
            timestamp: msg.timestamp,
            msgId: msg.msgId,
            data: msg.payload,
            isReplayed: true,
          });
        }
      }
    } catch (err) {
      this.logger.error(`Handshake rejected for socket ${client.id}: ${err.message}`);
      client.emit('error', { message: err.message });
      client.disconnect();
    }
  }

  handleDisconnect(client: AuthenticatedSocket) {
    this.logger.log(`Socket Client disconnected: ID ${client.id} | Tenant: ${client.data?.tenantId}`);
  }

  @SubscribeMessage('join_shipment')
  async handleJoinShipment(
    @ConnectedSocket() client: AuthenticatedSocket,
    @MessageBody() data: { shipmentId: string },
  ) {
    const tenantId = client.data?.tenantId || 'a0000000-0000-0000-0000-000000000001';
    if (!data || !data.shipmentId) {
      return { status: 'ERROR', message: 'shipmentId is required' };
    }

    const shipmentRoom = `shipment:${tenantId}:${data.shipmentId}`;
    await client.join(shipmentRoom);

    this.logger.log(`Client ${client.id} joined shipment room: ${shipmentRoom}`);
    return { status: 'SUCCESS', room: shipmentRoom };
  }

  @SubscribeMessage('leave_shipment')
  async handleLeaveShipment(
    @ConnectedSocket() client: AuthenticatedSocket,
    @MessageBody() data: { shipmentId: string },
  ) {
    const tenantId = client.data?.tenantId || 'a0000000-0000-0000-0000-000000000001';
    if (!data || !data.shipmentId) {
      return { status: 'ERROR', message: 'shipmentId is required' };
    }

    const shipmentRoom = `shipment:${tenantId}:${data.shipmentId}`;
    await client.leave(shipmentRoom);

    this.logger.log(`Client ${client.id} left shipment room: ${shipmentRoom}`);
    return { status: 'SUCCESS', room: shipmentRoom };
  }

  // ── Heartbeat / Keepalive ───────────────────────────────────────────────

  @SubscribeMessage('ping')
  handlePing(@ConnectedSocket() client: AuthenticatedSocket) {
    return { event: 'pong', timestamp: Date.now() };
  }

  // ── TASK-005: Client ACK Handler ─────────────────────────────────────────

  @SubscribeMessage('ack')
  handleAck(
    @ConnectedSocket() client: AuthenticatedSocket,
    @MessageBody() data: { msgId: string },
  ) {
    if (data && data.msgId && this.pendingAcks.has(data.msgId)) {
      clearTimeout(this.pendingAcks.get(data.msgId)!);
      this.pendingAcks.delete(data.msgId);
      this.logger.log(`[ACK Received] Client ${client.id} acknowledged msgId '${data.msgId}'`);
    }
    return { status: 'ACK_RECEIVED', msgId: data?.msgId };
  }

  // ── Helper Broadcast Methods ─────────────────────────────────────────────

  sendToTenant<T>(tenantId: string, event: string, data: T) {
    const room = `tenant:${tenantId}`;
    const payload: RealtimePayloadDto<T> = {
      event,
      tenantId,
      timestamp: Date.now(),
      data,
    };
    this.server.to(room).emit(event, payload);
    this.logger.log(`[Broadcast -> Room ${room}] Event: ${event}`);
  }

  async sendToUser<T>(tenantId: string, userId: string, event: string, data: T) {
    const room = `user:${tenantId}:${userId}`;
    const msgId = `msg_${Date.now()}_${Math.random().toString(36).substring(2, 7)}`;
    const payload: RealtimePayloadDto<T> & { msgId: string } = {
      event,
      tenantId,
      timestamp: Date.now(),
      msgId,
      data,
    };

    // Emit event with msgId
    this.server.to(room).emit(event, payload);
    this.logger.log(`[Broadcast -> Room ${room}] Event: ${event} | msgId: ${msgId}`);

    // TASK-005: 5-second ACK timeout check
    const timeout = setTimeout(async () => {
      if (this.pendingAcks.has(msgId)) {
        this.pendingAcks.delete(msgId);
        this.logger.warn(`[ACK Timeout] No ACK received for msgId '${msgId}'. Buffering for User ${userId}...`);
        await this.offlineBuffer.bufferMessage({
          msgId,
          tenantId,
          userId,
          event,
          payload: data,
          timestamp: Date.now(),
        });
      }
    }, 5000);

    this.pendingAcks.set(msgId, timeout);
  }

  sendToShipment<T>(tenantId: string, shipmentId: string, event: string, data: T) {
    const room = `shipment:${tenantId}:${shipmentId}`;
    const payload: RealtimePayloadDto<T> = {
      event,
      tenantId,
      timestamp: Date.now(),
      data,
    };
    this.server.to(room).emit(event, payload);
    this.logger.log(`[Broadcast -> Room ${room}] Event: ${event}`);
  }
}

