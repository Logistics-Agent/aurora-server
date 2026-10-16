import { Injectable, ForbiddenException, BadRequestException } from '@nestjs/common';
import { IAssistantTool, ToolExecutionContext, ToolResult } from './tool.interface';
import { AssistantIntent } from '../../domain/enums/assistant-intent.enum';
import { ActorType } from '../../domain/enums/actor-type.enum';
import { ReadModelStore } from '../../read-model/read-model.store';

@Injectable()
export class ShipmentLookupTool implements IAssistantTool {
  readonly name = 'ShipmentLookupTool';
  readonly description = 'Looks up shipment statuses, locations, and routes scoped to authenticated actor role.';
  readonly supportedIntents = [AssistantIntent.SHIPMENT_QUERY];
  readonly allowedActors = [ActorType.CUSTOMER, ActorType.STAFF, ActorType.ADMIN, ActorType.SYSTEM];

  constructor(private readonly readModel: ReadModelStore) {}

  async execute(context: ToolExecutionContext, params?: any): Promise<ToolResult> {
    const { actorType, customerId, tenantId } = context.currentUser;

    if (!this.allowedActors.includes(actorType)) {
      throw new ForbiddenException(`Actor ${actorType} is not authorized to execute ${this.name}`);
    }
    if (actorType !== ActorType.CUSTOMER && !context.currentUser.permissions.includes('shipments:read')) {
      throw new ForbiddenException('Shipment read permission is required');
    }

    const specificShipmentId = params?.shipmentId;
    const specificShipmentNo = params?.shipmentNo;
    if (actorType === ActorType.CUSTOMER && !customerId) {
      throw new ForbiddenException('Customer identity is required for shipment lookup');
    }

    // 1. Specific Shipment Lookup
    if (specificShipmentId || specificShipmentNo) {
      const shipment = specificShipmentId
        ? await this.readModel.getShipment(specificShipmentId, tenantId)
        : await this.readModel.getShipmentByNumber(specificShipmentNo, tenantId);
      if (shipment) {
        // Enforce customer boundary: Customer can only view their own shipment
        if (actorType === ActorType.CUSTOMER && shipment.customerId.toLowerCase() !== customerId!.toLowerCase()) {
          return {
            toolName: this.name,
            success: false,
            data: null,
            summary: `Không tìm thấy hoặc quý khách không có quyền truy cập lô hàng ${specificShipmentId || specificShipmentNo}.`,
          };
        }

        return {
          toolName: this.name,
          success: true,
          data: shipment,
          summary: `Lô hàng ${shipment.shipmentId} (${shipment.originPort} -> ${shipment.destinationPort}) hiện có trạng thái ${shipment.status}${shipment.currentLocation ? `. Vị trí hiện tại: ${shipment.currentLocation}` : ''}.`,
        };
      }
      return {
        toolName: this.name, success: false, data: null,
        summary: `Không tìm thấy lô hàng ${specificShipmentId || specificShipmentNo}.`,
      };
    }

    // 2. Customer Scoped List
    if (actorType === ActorType.CUSTOMER) {
      const shipments = await this.readModel.getShipmentsByCustomer(customerId!, tenantId);
      if (shipments.length === 0) {
        return {
          toolName: this.name,
          success: true,
          data: [],
          summary: `Hiện tại không tìm thấy lô hàng nào đang hoạt động cho tài khoản quý khách.`,
        };
      }

      const primary = shipments[0];
      return {
        toolName: this.name,
        success: true,
        data: shipments,
        summary: `Hiển thị ${shipments.length} lô hàng gần nhất. Lô ${primary.shipmentId} (${primary.originPort} -> ${primary.destinationPort}) trạng thái: ${primary.status}.`,
      };
    }

    // 3. Staff / Admin Tenant Scope List
    const targetCustId = params?.targetCustomerId || customerId;
    if (!targetCustId) throw new BadRequestException('customerId is required for shipment list');
    const shipments = await this.readModel.getShipmentsByCustomer(targetCustId, tenantId);
    if (shipments.length === 0) {
      return {
        toolName: this.name,
        success: true,
        data: [],
        summary: `Không tìm thấy lô hàng nào cho khách hàng ${targetCustId} trong hệ thống.`,
      };
    }

    const primary = shipments[0];
    return {
      toolName: this.name,
      success: true,
      data: shipments,
      summary: `[Staff View] Khách hàng ${targetCustId} có ${shipments.length} lô hàng. Lô ${primary.shipmentId} (${primary.originPort} -> ${primary.destinationPort}) trạng thái: ${primary.status}.`,
    };
  }
}
